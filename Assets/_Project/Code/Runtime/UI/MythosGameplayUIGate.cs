using System;
using System.Collections;
using MoreMountains.InventoryEngine;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Mythos.Unleashed.Runtime.UI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class MythosGameplayUIGate : MonoBehaviour
    {
        private static readonly string[] NonGameplaySceneNames =
        {
            "BootStrap",
            "Bootstrap",
            "MainMenu",
            "Loading"
        };

        [SerializeField]
        private InventoryInputManager inventoryInputManager;

        [SerializeField]
        private EventSystem gameplayEventSystem;

        [SerializeField]
        private BaseInputModule gameplayInputModule;

        [SerializeField]
        private CanvasGroup weaponHudCanvasGroup;

        [SerializeField]
        private InventorySelectionMarker inventorySelectionMarker;

        private Coroutine _activationRoutine;
        private bool _gameplayUIEnabled;

        private void Awake()
        {
            ApplyGameplayUIState(false);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            ScheduleEvaluation(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            if (_activationRoutine != null)
            {
                StopCoroutine(_activationRoutine);
                _activationRoutine = null;
            }

            ApplyGameplayUIState(false);
        }

        private void LateUpdate()
        {
            if (_gameplayUIEnabled)
            {
                DisableCompetingEventSystems();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ScheduleEvaluation(scene);
        }

        private void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            if (!IsGameplaySceneReady(current))
            {
                ApplyGameplayUIState(false);
            }

            ScheduleEvaluation(current);
        }

        private void ScheduleEvaluation(Scene scene)
        {
            if (_activationRoutine != null)
            {
                StopCoroutine(_activationRoutine);
            }

            _activationRoutine = StartCoroutine(EvaluateAfterSceneInitialization(scene));
        }

        private IEnumerator EvaluateAfterSceneInitialization(Scene scene)
        {
            // sceneLoaded runs after Awake and before Start. Waiting one frame lets the
            // destination's native managers finish their normal initialization first.
            yield return null;

            Scene activeScene = SceneManager.GetActiveScene();
            bool sameActiveScene = scene.IsValid() &&
                                   activeScene.IsValid() &&
                                   scene.handle == activeScene.handle;

            ApplyGameplayUIState(sameActiveScene && IsGameplaySceneReady(activeScene));
            _activationRoutine = null;
        }

        private bool IsGameplaySceneReady(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || IsNonGameplayScene(scene.name))
            {
                return false;
            }

            int activeLevelManagers = 0;
            LevelManager[] levelManagers = FindObjectsByType<LevelManager>(
                FindObjectsInactive.Exclude
            );

            foreach (LevelManager levelManager in levelManagers)
            {
                if (levelManager != null &&
                    levelManager.isActiveAndEnabled &&
                    levelManager.gameObject.scene.handle == scene.handle)
                {
                    activeLevelManagers++;
                }
            }

            return activeLevelManagers == 1;
        }

        private static bool IsNonGameplayScene(string sceneName)
        {
            foreach (string nonGameplaySceneName in NonGameplaySceneNames)
            {
                if (string.Equals(
                        sceneName,
                        nonGameplaySceneName,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyGameplayUIState(bool shouldEnable)
        {
            if (!shouldEnable)
            {
                CloseInventoryIfOpen();
                SetGameplayPresentationActive(false);

                if (inventoryInputManager != null)
                {
                    inventoryInputManager.enabled = false;
                }

                SetGameplayEventSystemActive(false);
                _gameplayUIEnabled = false;
                return;
            }

            if (inventoryInputManager == null ||
                gameplayEventSystem == null ||
                gameplayInputModule == null ||
                weaponHudCanvasGroup == null ||
                inventorySelectionMarker == null)
            {
                Debug.LogError(
                    "[MythosGameplayUIGate] Gameplay UI references are incomplete.",
                    this
                );
                ApplyGameplayUIState(false);
                return;
            }

            DisableCompetingEventSystems();
            SetGameplayEventSystemActive(true);
            SetGameplayPresentationActive(true);
            inventoryInputManager.enabled = true;
            _gameplayUIEnabled = true;
        }

        private void SetGameplayPresentationActive(bool active)
        {
            if (weaponHudCanvasGroup != null)
            {
                weaponHudCanvasGroup.alpha = active ? 1f : 0f;
                weaponHudCanvasGroup.interactable = active;
                weaponHudCanvasGroup.blocksRaycasts = active;
            }

            if (inventorySelectionMarker != null)
            {
                inventorySelectionMarker.enabled = active;
            }
        }

        private void CloseInventoryIfOpen()
        {
            if (inventoryInputManager != null && inventoryInputManager.InventoryIsOpen)
            {
                // Preserve Inventory Engine close events and GameManager's native
                // pause/resume behavior before input and event routing are disabled.
                inventoryInputManager.CloseInventory();
            }
        }

        private void SetGameplayEventSystemActive(bool active)
        {
            if (gameplayEventSystem == null)
            {
                return;
            }

            GameObject eventSystemObject = gameplayEventSystem.gameObject;
            if (active && !eventSystemObject.activeSelf)
            {
                eventSystemObject.SetActive(true);
            }

            if (gameplayInputModule != null)
            {
                gameplayInputModule.enabled = active;
            }

            gameplayEventSystem.enabled = active;

            if (!active && eventSystemObject.activeSelf)
            {
                eventSystemObject.SetActive(false);
            }
        }

        private void DisableCompetingEventSystems()
        {
            EventSystem[] eventSystems = FindObjectsByType<EventSystem>(
                FindObjectsInactive.Exclude
            );

            foreach (EventSystem candidate in eventSystems)
            {
                if (candidate == null || candidate == gameplayEventSystem)
                {
                    continue;
                }

                foreach (BaseInputModule module in candidate.GetComponents<BaseInputModule>())
                {
                    module.enabled = false;
                }

                candidate.enabled = false;
            }
        }
    }
}
