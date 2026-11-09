using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using MoreMountains.TopDownEngine;

[DisallowMultipleComponent]
public class PersistentPlayerScenePlacement : MonoBehaviour
{
    private const string IntermediateLoadingSceneName = "Loading";

    private int _activeSceneHandle = -1;
    private int _lastPlacedSceneHandle = -1;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(PlacePlayerAfterSceneInitialization(scene));
    }

    private IEnumerator PlacePlayerAfterSceneInitialization(Scene loadedScene)
    {
        // Allow the scene's LevelManager and CheckPoints to initialize first.
        yield return null;

        // A newer scene may have become active while this coroutine was waiting.
        // Ignore that stale callback rather than placing into the wrong scene.
        if (
            !loadedScene.IsValid() ||
            !loadedScene.isLoaded ||
            SceneManager.GetActiveScene().handle != loadedScene.handle
        )
        {
            yield break;
        }

        // PlacedThisScene is normally reset by ScenePortal.SetNextSpawn(). Reset it
        // here as well when the active scene changes so non-portal loads cannot
        // inherit a successful-placement flag from the previous scene.
        if (_activeSceneHandle != loadedScene.handle)
        {
            _activeSceneHandle = loadedScene.handle;
            SceneTransit.PlacedThisScene = false;
        }

        // The intermediate loading scene must never consume or clear a pending
        // named arrival request. The destination scene will resolve it later.
        if (loadedScene.name == IntermediateLoadingSceneName)
        {
            yield break;
        }

        // Multiple callbacks for the same scene must not move the player twice.
        if (
            _lastPlacedSceneHandle == loadedScene.handle ||
            SceneTransit.PlacedThisScene
        )
        {
            yield break;
        }

        string requestedSpawnName = SceneTransit.NextSpawn;
        SpawnPoint requestedSpawnPoint = FindRequestedSpawnPoint(
            loadedScene,
            requestedSpawnName
        );

        Transform arrivalTransform = null;
        string arrivalDescription = null;

        if (requestedSpawnPoint != null)
        {
            arrivalTransform = requestedSpawnPoint.transform;
            arrivalDescription = $"spawn point {requestedSpawnPoint.SpawnName}";
        }
        else
        {
            LevelManager levelManager = FindLevelManagerInScene(loadedScene);

            if (levelManager == null)
            {
                Debug.LogWarning(
                    $"No LevelManager found in scene {loadedScene.name}.",
                    this
                );
                yield break;
            }

            CheckPoint targetCheckPoint = levelManager.InitialSpawnPoint;

            if (
                targetCheckPoint == null ||
                targetCheckPoint.gameObject.scene.handle != loadedScene.handle
            )
            {
                Debug.LogWarning(
                    $"LevelManager in scene {loadedScene.name} has no valid Initial Spawn Point.",
                    levelManager
                );
                yield break;
            }

            arrivalTransform = targetCheckPoint.transform;
            arrivalDescription = $"checkpoint {targetCheckPoint.name}";
        }

        transform.SetPositionAndRotation(
            arrivalTransform.position,
            arrivalTransform.rotation
        );

        _lastPlacedSceneHandle = loadedScene.handle;
        SceneTransit.MarkPlaced();

        Debug.Log(
            $"Persistent player placed at {arrivalDescription} " +
            $"in scene {loadedScene.name}.",
            this
        );
    }

    private static SpawnPoint FindRequestedSpawnPoint(
        Scene loadedScene,
        string requestedSpawnName
    )
    {
        if (string.IsNullOrEmpty(requestedSpawnName))
        {
            return null;
        }

        SpawnPoint[] spawnPoints = FindObjectsByType<SpawnPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (SpawnPoint spawnPoint in spawnPoints)
        {
            if (
                spawnPoint != null &&
                spawnPoint.gameObject.activeInHierarchy &&
                spawnPoint.gameObject.scene.handle == loadedScene.handle &&
                string.Equals(
                    spawnPoint.SpawnName,
                    requestedSpawnName,
                    System.StringComparison.Ordinal
                )
            )
            {
                return spawnPoint;
            }
        }

        return null;
    }

    private static LevelManager FindLevelManagerInScene(Scene loadedScene)
    {
        LevelManager[] levelManagers = FindObjectsByType<LevelManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (LevelManager levelManager in levelManagers)
        {
            if (
                levelManager != null &&
                levelManager.gameObject.scene.handle == loadedScene.handle
            )
            {
                return levelManager;
            }
        }

        return null;
    }
}
