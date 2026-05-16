using System.Collections;
using UnityEngine;
using MoreMountains.TopDownEngine;

public class RuntimeSpawnResolver : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private string playerTag = "Player";

    [Header("Spawn Settings")]
    [SerializeField] private string spawnPointName = "MazeSpawnPoint";
    [SerializeField] private float initialDelay = 0.5f;
    [SerializeField] private float retryDelay = 0.25f;
    [SerializeField] private int maxAttempts = 20;

    private void Start()
    {
        StartCoroutine(ResolveSpawn());
    }

    private IEnumerator ResolveSpawn()
    {
        // Give Dungeon Architect time to generate the level
        yield return new WaitForSeconds(initialDelay);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            SpawnPoint spawnPoint = FindSpawnPointByName(spawnPointName);

            if (player != null && spawnPoint != null)
            {
                MovePlayerToSpawn(player, spawnPoint);
                Debug.Log("[RuntimeSpawnResolver] Player moved to spawn: " + spawnPointName);
                yield break;
            }

            yield return new WaitForSeconds(retryDelay);
        }

        Debug.LogWarning("[RuntimeSpawnResolver] Failed to find Player or SpawnPoint: " + spawnPointName);
    }

    private SpawnPoint FindSpawnPointByName(string targetName)
    {
        SpawnPoint[] spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

        foreach (SpawnPoint sp in spawnPoints)
        {
            if (sp.name == targetName)
            {
                return sp;
            }
        }

        return null;
    }

    private void MovePlayerToSpawn(GameObject player, SpawnPoint spawnPoint)
    {
        CharacterController controller = player.GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        player.transform.position = spawnPoint.transform.position;
        player.transform.rotation = spawnPoint.transform.rotation;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}