using UnityEngine;
using UnityEngine.SceneManagement;
using MoreMountains.TopDownEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class PlayerPersistOnce : MonoBehaviour
{
    private static PlayerPersistOnce _instance;
    private Character _character;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _character = GetComponent<Character>();

        if (_character == null)
        {
            Debug.LogError(
                "PlayerPersistOnce must be attached to the same GameObject as a Character.",
                this
            );
            return;
        }

        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // If the scene's GameManager has already initialized, register immediately.
        // Start() repeats this after all scene Awake() calls have completed.
        if (GameManager.HasInstance)
        {
            RegisterPersistentCharacter(GameManager.Current);
        }
    }

    private void Start()
    {
        if (_instance == this && _character != null)
        {
            RegisterPersistentCharacter(GameManager.Instance);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_instance != this || _character == null)
        {
            return;
        }

        // sceneLoaded is raised after the destination scene's Awake() calls and
        // before its Start() calls. Register here so LevelManager sees the survivor
        // when it builds Players in its initialization coroutine.
        RegisterPersistentCharacter(GameManager.Instance);
    }

    private void RegisterPersistentCharacter(GameManager gameManager)
    {
        if (gameManager.PersistentCharacter != _character)
        {
            gameManager.SetPersistentCharacter(_character);
        }
    }

    private void OnDestroy()
    {
        if (_instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (
            GameManager.HasInstance &&
            GameManager.Current.PersistentCharacter == _character
        )
        {
            GameManager.Current.SetPersistentCharacter(null);
        }

        _instance = null;
    }
}
