using System;
using System.Collections.Generic;
using System.Text;
using MoreMountains.InventoryEngine;
using MoreMountains.TopDownEngine;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class MythosWorldStateWitness : EditorWindow
{
    private const string MenuPath = "Mythos/Validation/World State Witness";
    private const string LoadingSceneName = "Loading";

    // Snapshot only stores value data. It deliberately retains no Unity objects or live collections.
    private static WorldSnapshot _baseline;
    private static string _lastReport =
        "Enter Play Mode, reach a stable gameplay scene, then capture a baseline.";

    private Vector2 _scroll;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        GetWindow<MythosWorldStateWitness>("World State Witness");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            _baseline = null;
            _lastReport = "Play Mode ended. The runtime baseline was cleared.";
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("World State Witness", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Read-only runtime observation. This window never loads scenes, enters Play Mode, " +
            "or changes gameplay state.",
            MessageType.Info);

        string activeScene = SceneManager.GetActiveScene().name;
        EditorGUILayout.LabelField("Mode", EditorApplication.isPlaying ? "Play Mode" : "Edit Mode");
        EditorGUILayout.LabelField("Active scene", string.IsNullOrEmpty(activeScene) ? "<none>" : activeScene);
        EditorGUILayout.LabelField(
            "Baseline",
            _baseline == null
                ? "Not captured"
                : $"{_baseline.SceneName} (frame {_baseline.Frame})");

        bool stablePlayMode = EditorApplication.isPlaying &&
                              !EditorApplication.isCompiling &&
                              !string.Equals(activeScene, LoadingSceneName, StringComparison.Ordinal);

        using (new EditorGUI.DisabledScope(!stablePlayMode))
        {
            if (GUILayout.Button("Capture Baseline", GUILayout.Height(28f)))
            {
                CaptureBaseline();
            }
        }

        using (new EditorGUI.DisabledScope(!stablePlayMode || _baseline == null))
        {
            if (GUILayout.Button("Compare Current State", GUILayout.Height(28f)))
            {
                CompareCurrentState();
            }
        }

        if (EditorApplication.isPlaying &&
            string.Equals(activeScene, LoadingSceneName, StringComparison.Ordinal))
        {
            EditorGUILayout.HelpBox(
                "Loading is an intermediate scene. Wait for the destination before observing state.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Latest report", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_lastReport, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static void CaptureBaseline()
    {
        if (!TryBeginObservation("capture a baseline", out string rejection))
        {
            Publish(rejection);
            return;
        }

        WorldSnapshot snapshot = Observe();
        _baseline = snapshot;

        List<Result> results = new List<Result>();
        EvaluateCurrentSnapshot(snapshot, "BASELINE", results);
        AddInventoryEvidence(snapshot.MainInventory, "Main inventory evidence", results);
        AddWeaponEvidence(snapshot, results);

        Publish(BuildReport(
            "BASELINE CAPTURED",
            snapshot.SceneName,
            snapshot.SceneName,
            results,
            snapshot));
    }

    private static void CompareCurrentState()
    {
        if (_baseline == null)
        {
            Publish("[WARNING] No baseline exists. Capture Baseline first.");
            return;
        }

        if (!TryBeginObservation("compare current state", out string rejection))
        {
            Publish(rejection);
            return;
        }

        WorldSnapshot current = Observe();
        List<Result> results = new List<Result>();

        EvaluateCurrentSnapshot(current, "CURRENT", results);
        CompareSeer(_baseline, current, results);
        CompareInventory("Main inventory", _baseline.MainInventory, current.MainInventory, results);
        CompareInventory("Weapon inventory", _baseline.WeaponInventory, current.WeaponInventory, results);
        CompareEquippedWeapon(_baseline, current, results);
        CompareGameState(_baseline, current, results);
        AddInventoryEvidence(current.MainInventory, "Main inventory evidence", results);
        AddWeaponEvidence(current, results);

        Publish(BuildReport(
            "WORLD STATE COMPARISON",
            _baseline.SceneName,
            current.SceneName,
            results,
            current));
    }

    private static bool TryBeginObservation(string action, out string rejection)
    {
        if (!EditorApplication.isPlaying)
        {
            rejection = $"[WARNING] Enter Play Mode before attempting to {action}.";
            return false;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, LoadingSceneName, StringComparison.Ordinal))
        {
            rejection =
                "[WARNING] Loading is an intermediate scene, not a valid observation destination. " +
                "Wait for the gameplay scene to become active.";
            return false;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            rejection = "[WARNING] The active scene has no observable identity.";
            return false;
        }

        rejection = null;
        return true;
    }

    private static WorldSnapshot Observe()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        PlayerPersistOnce[] persistenceMarkers = FindAll<PlayerPersistOnce>();

        Character seer = null;
        PlayerPersistOnce marker = null;
        int validSeerCount = 0;
        foreach (PlayerPersistOnce candidate in persistenceMarkers)
        {
            Character character = candidate == null ? null : candidate.GetComponent<Character>();
            if (character == null)
            {
                continue;
            }

            validSeerCount++;
            if (seer == null)
            {
                marker = candidate;
                seer = character;
            }
        }

        int placementCount = marker == null
            ? 0
            : marker.GetComponents<PersistentPlayerScenePlacement>().Length;

        GameManager[] gameManagers = FindAll<GameManager>();
        Character registeredPersistentCharacter = gameManagers.Length == 1
            ? gameManagers[0].PersistentCharacter
            : null;

        LevelManager[] allLevelManagers = FindAll<LevelManager>();
        List<LevelManager> sceneLevelManagers = new List<LevelManager>();
        foreach (LevelManager candidate in allLevelManagers)
        {
            if (candidate != null &&
                candidate.gameObject.scene.handle == activeScene.handle &&
                candidate.isActiveAndEnabled)
            {
                sceneLevelManagers.Add(candidate);
            }
        }

        bool levelPlayersObserved = false;
        int levelPlayerCount = 0;
        int seerRegistrationCount = 0;
        if (sceneLevelManagers.Count == 1 && sceneLevelManagers[0].Players != null)
        {
            levelPlayersObserved = true;
            levelPlayerCount = sceneLevelManagers[0].Players.Count;
            foreach (Character player in sceneLevelManagers[0].Players)
            {
                if (player == seer)
                {
                    seerRegistrationCount++;
                }
            }
        }

        CharacterInventory characterInventory = seer == null
            ? null
            : seer.GetComponent<CharacterInventory>();
        InventoryReading mainInventory = ReadInventory(
            characterInventory == null ? null : characterInventory.MainInventory,
            seer);
        InventoryReading weaponInventory = ReadInventory(
            characterInventory == null ? null : characterInventory.WeaponInventory,
            seer);

        CharacterHandleWeapon[] weaponHandlers = seer == null
            ? Array.Empty<CharacterHandleWeapon>()
            : seer.GetComponents<CharacterHandleWeapon>();
        Weapon equippedWeapon = weaponHandlers.Length == 1 ? weaponHandlers[0].CurrentWeapon : null;
        string equippedWeaponIdentity = ReadWeaponIdentity(equippedWeapon, out bool equippedIdentityReliable);

        GameState[] gameStates = FindAll<GameState>();
        GameState gameState = gameStates.Length == 1 ? gameStates[0] : null;

        CameraReading camera = ReadCamera(seer);

        return new WorldSnapshot(
            activeScene.name,
            Time.frameCount,
            persistenceMarkers.Length,
            validSeerCount,
            ReadEntityId(marker),
            ReadEntityId(seer),
            seer == null ? string.Empty : seer.name,
            seer == null ? string.Empty : seer.PlayerID,
            placementCount,
            gameManagers.Length,
            ReadEntityId(registeredPersistentCharacter),
            sceneLevelManagers.Count,
            levelPlayersObserved,
            levelPlayerCount,
            seerRegistrationCount,
            characterInventory != null,
            mainInventory,
            weaponInventory,
            weaponHandlers.Length,
            equippedWeapon != null,
            equippedWeaponIdentity,
            equippedIdentityReliable,
            gameStates.Length,
            gameState != null && GameState.Instance == gameState,
            ReadEntityId(gameState),
            gameState == null ? 0 : gameState.TotalShards,
            gameState != null && gameState.WindWardBossDefeated,
            camera);
    }

    private static InventoryReading ReadInventory(Inventory inventory, Character seer)
    {
        if (inventory == null)
        {
            return InventoryReading.Missing;
        }

        bool contentObserved = inventory.Content != null;
        bool reliableIdentities = true;
        bool validQuantities = true;
        int filledSlots = 0;
        long totalQuantity = 0;
        StringBuilder signature = new StringBuilder();
        StringBuilder summary = new StringBuilder();
        Dictionary<string, long> quantitiesByIdentity = new Dictionary<string, long>();
        Dictionary<string, string> labelsByIdentity = new Dictionary<string, string>();

        if (contentObserved)
        {
            for (int i = 0; i < inventory.Content.Length; i++)
            {
                InventoryItem item = inventory.Content[i];
                if (InventoryItem.IsNull(item))
                {
                    continue;
                }

                filledSlots++;
                totalQuantity += item.Quantity;
                validQuantities &= item.Quantity > 0;
                reliableIdentities &= !string.IsNullOrWhiteSpace(item.ItemID);

                string itemId = item.ItemID ?? string.Empty;
                string itemName = item.ItemName ?? string.Empty;
                string identityKey = BuildIdentityKey(itemId, itemName);
                quantitiesByIdentity.TryGetValue(identityKey, out long existingQuantity);
                quantitiesByIdentity[identityKey] = existingQuantity + item.Quantity;
                labelsByIdentity[identityKey] = string.IsNullOrWhiteSpace(itemId) ? itemName : itemId;
            }
        }

        List<string> identityKeys = new List<string>(quantitiesByIdentity.Keys);
        identityKeys.Sort(StringComparer.Ordinal);
        foreach (string identityKey in identityKeys)
        {
            signature.Append(identityKey).Append(quantitiesByIdentity[identityKey]).Append(';');
            if (summary.Length > 0)
            {
                summary.Append(", ");
            }
            summary.Append(labelsByIdentity[identityKey])
                .Append(" x").Append(quantitiesByIdentity[identityKey]);
        }

        if (summary.Length == 0)
        {
            summary.Append(contentObserved ? "<empty>" : "<content unavailable>");
        }

        return new InventoryReading(
            true,
            ReadEntityId(inventory),
            inventory.name,
            inventory.PlayerID,
            inventory.GetComponent<PersistentInventoryObject>() != null,
            inventory.Owner == (seer == null ? null : seer.gameObject),
            contentObserved,
            reliableIdentities,
            validQuantities,
            filledSlots,
            totalQuantity,
            signature.ToString(),
            summary.ToString());
    }

    private static void AppendToken(StringBuilder builder, string value)
    {
        builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    private static string BuildIdentityKey(string itemId, string itemName)
    {
        StringBuilder key = new StringBuilder();
        AppendToken(key, itemId);
        AppendToken(key, itemName);
        return key.ToString();
    }

    private static string ReadWeaponIdentity(Weapon weapon, out bool reliable)
    {
        if (weapon == null)
        {
            reliable = false;
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(weapon.WeaponID))
        {
            reliable = true;
            return weapon.WeaponID;
        }

        reliable = false;
        if (!string.IsNullOrWhiteSpace(weapon.WeaponName))
        {
            return weapon.WeaponName;
        }

        return weapon.name ?? string.Empty;
    }

    private static CameraReading ReadCamera(Character seer)
    {
        CinemachineCameraController[] controllers = FindAll<CinemachineCameraController>();
        int activePlayerControllers = 0;
        int controllersTargetingSeer = 0;
        int controllersFollowingSeer = 0;
        foreach (CinemachineCameraController controller in controllers)
        {
            if (controller == null || !controller.isActiveAndEnabled || !controller.FollowsAPlayer)
            {
                continue;
            }

            activePlayerControllers++;
            if (controller.TargetCharacter == seer)
            {
                controllersTargetingSeer++;
                if (controller.FollowsPlayer)
                {
                    controllersFollowingSeer++;
                }
            }
        }

        CinemachineCamera[] cameras = FindAll<CinemachineCamera>();
        int activeCameras = 0;
        int camerasFollowingSeerTarget = 0;
        Transform expectedTarget = seer == null || seer.CameraTarget == null
            ? null
            : seer.CameraTarget.transform;
        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            activeCameras++;
            if (expectedTarget != null && camera.Follow == expectedTarget)
            {
                camerasFollowingSeerTarget++;
            }
        }

        return new CameraReading(
            activePlayerControllers,
            controllersTargetingSeer,
            controllersFollowingSeer,
            expectedTarget != null,
            activeCameras,
            camerasFollowingSeerTarget);
    }

    private static T[] FindAll<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include);
    }

    private static string ReadEntityId(UnityEngine.Object target)
    {
        return target == null ? string.Empty : target.GetEntityId().ToString();
    }

    private static void EvaluateCurrentSnapshot(
        WorldSnapshot snapshot,
        string scope,
        List<Result> results)
    {
        Add(results, Status.Pass, $"{scope} scene identity",
            $"Stable gameplay scene '{snapshot.SceneName}' observed at frame {snapshot.Frame}.");

        if (snapshot.PersistenceMarkerCount != 1 || snapshot.ValidSeerCount != 1)
        {
            Add(results, Status.Fail, $"{scope} authoritative Seer",
                $"Expected exactly one PlayerPersistOnce with Character; observed " +
                $"{snapshot.PersistenceMarkerCount} marker(s), {snapshot.ValidSeerCount} valid Character(s).");
        }
        else
        {
            Add(results, Status.Pass, $"{scope} authoritative Seer",
                $"{snapshot.SeerName} / {snapshot.SeerPlayerId} " +
                $"(Character instance {snapshot.SeerInstanceId}).");
        }

        Add(results,
            snapshot.PlacementCount == 1 ? Status.Pass : Status.Fail,
            $"{scope} persistent placement ownership",
            snapshot.PlacementCount == 1
                ? "The authoritative Seer has exactly one PersistentPlayerScenePlacement."
                : $"Expected one PersistentPlayerScenePlacement on the Seer; observed {snapshot.PlacementCount}.");

        if (snapshot.GameManagerCount == 0)
        {
            Add(results, Status.Warning, $"{scope} GameManager registration",
                "No active GameManager was available; PersistentCharacter could not be inspected.");
        }
        else if (snapshot.GameManagerCount != 1)
        {
            Add(results, Status.Fail, $"{scope} GameManager registration",
                $"Expected one GameManager; observed {snapshot.GameManagerCount}.");
        }
        else if (snapshot.RegisteredPersistentCharacterId != snapshot.SeerInstanceId ||
                 string.IsNullOrEmpty(snapshot.SeerInstanceId))
        {
            Add(results, Status.Fail, $"{scope} GameManager registration",
                "GameManager.PersistentCharacter is not the authoritative Seer.");
        }
        else
        {
            Add(results, Status.Pass, $"{scope} GameManager registration",
                "GameManager.PersistentCharacter is the authoritative Seer.");
        }

        if (snapshot.SceneLevelManagerCount != 1)
        {
            Add(results, Status.Fail, $"{scope} LevelManager registration",
                $"Expected one active LevelManager owned by '{snapshot.SceneName}'; " +
                $"observed {snapshot.SceneLevelManagerCount}.");
        }
        else if (!snapshot.LevelPlayersObserved)
        {
            Add(results, Status.Warning, $"{scope} LevelManager registration",
                "LevelManager.Players is not initialized yet; wait a frame and observe again.");
        }
        else if (snapshot.SeerRegistrationCount != 1 || snapshot.LevelPlayerCount != 1)
        {
            Add(results, Status.Fail, $"{scope} LevelManager registration",
                $"Players contains {snapshot.LevelPlayerCount} entry/entries and the authoritative " +
                $"Seer appears {snapshot.SeerRegistrationCount} time(s).");
        }
        else
        {
            Add(results, Status.Pass, $"{scope} LevelManager registration",
                "LevelManager.Players contains exactly the authoritative Seer.");
        }

        if (!snapshot.HasCharacterInventory)
        {
            Add(results, Status.Fail, $"{scope} character inventory binding",
                "The authoritative Seer has no CharacterInventory component.");
        }
        else
        {
            Add(results, Status.Pass, $"{scope} character inventory binding",
                "CharacterInventory is present on the authoritative Seer.");
        }

        EvaluateInventory(snapshot.MainInventory, $"{scope} main inventory", results);
        EvaluateInventory(snapshot.WeaponInventory, $"{scope} weapon inventory", results);
        EvaluateEquippedWeapon(snapshot, scope, results);
        EvaluateGameState(snapshot, scope, results);
        EvaluateCamera(snapshot, scope, results);
    }

    private static void EvaluateInventory(
        InventoryReading inventory,
        string label,
        List<Result> results)
    {
        if (!inventory.Present)
        {
            Add(results, Status.Fail, label,
                "The configured runtime inventory reference is missing.");
            return;
        }

        if (!inventory.HasPersistenceMarker)
        {
            Add(results, Status.Fail, label,
                $"'{inventory.Name}' has no PersistentInventoryObject marker.");
            return;
        }

        if (!inventory.OwnerIsSeer)
        {
            Add(results, Status.Fail, label,
                $"'{inventory.Name}' is not owned by the authoritative Seer.");
            return;
        }

        if (!inventory.ContentObserved)
        {
            Add(results, Status.Warning, label,
                $"'{inventory.Name}' exists, but Inventory.Content is unavailable.");
            return;
        }

        if (!inventory.ValidQuantities)
        {
            Add(results, Status.Fail, label,
                $"'{inventory.Name}' contains a populated item with a non-positive quantity.");
            return;
        }

        if (!inventory.ReliableIdentities)
        {
            Add(results, Status.Warning, label,
                $"'{inventory.Name}' content is readable, but at least one populated item has no ItemID. " +
                $"Observed: {inventory.Summary}");
            return;
        }

        Add(results, Status.Pass, label,
            $"'{inventory.Name}' ({inventory.PlayerId}, instance {inventory.InstanceId}): {inventory.Summary}");
    }

    private static void EvaluateEquippedWeapon(
        WorldSnapshot snapshot,
        string scope,
        List<Result> results)
    {
        if (snapshot.WeaponHandlerCount != 1)
        {
            Add(results, Status.Fail, $"{scope} equipped weapon",
                $"Expected one CharacterHandleWeapon; observed {snapshot.WeaponHandlerCount}.");
            return;
        }

        if (!snapshot.HasEquippedWeapon)
        {
            Add(results, Status.Warning, $"{scope} equipped weapon",
                "No CurrentWeapon is equipped; equipped-weapon continuity is not evidenced.");
            return;
        }

        if (!snapshot.EquippedIdentityReliable)
        {
            Add(results, Status.Warning, $"{scope} equipped weapon",
                $"CurrentWeapon is present but has no WeaponID (fallback '{snapshot.EquippedWeaponIdentity}').");
            return;
        }

        Add(results, Status.Pass, $"{scope} equipped weapon",
            $"CurrentWeapon.WeaponID is '{snapshot.EquippedWeaponIdentity}'.");
    }

    private static void EvaluateGameState(
        WorldSnapshot snapshot,
        string scope,
        List<Result> results)
    {
        if (snapshot.GameStateCount == 0)
        {
            Add(results, Status.Warning, $"{scope} GameState facts",
                "No GameState is active; TotalShards and WindWardBossDefeated are unobserved.");
            return;
        }

        if (snapshot.GameStateCount != 1 || !snapshot.GameStateInstanceMatches)
        {
            Add(results, Status.Fail, $"{scope} GameState facts",
                $"Expected exactly one GameState matching GameState.Instance; observed {snapshot.GameStateCount}.");
            return;
        }

        Add(results, Status.Pass, $"{scope} GameState facts",
            $"TotalShards={snapshot.TotalShards}, " +
            $"WindWardBossDefeated={snapshot.WindWardBossDefeated} " +
            $"(instance {snapshot.GameStateInstanceId}).");
    }

    private static void EvaluateCamera(
        WorldSnapshot snapshot,
        string scope,
        List<Result> results)
    {
        CameraReading camera = snapshot.Camera;
        if (camera.ActivePlayerControllerCount == 0)
        {
            Add(results, Status.Warning, $"{scope} camera target",
                "No active CinemachineCameraController configured to follow a player was observable.");
            return;
        }

        if (!camera.SeerCameraTargetExists)
        {
            Add(results, Status.Fail, $"{scope} camera target",
                "The authoritative Seer's Character.CameraTarget is missing.");
            return;
        }

        if (camera.ControllersTargetingSeer == 0 || camera.ControllersFollowingSeer == 0)
        {
            Add(results, Status.Fail, $"{scope} camera target",
                $"Active player controllers={camera.ActivePlayerControllerCount}, " +
                $"targeting Seer={camera.ControllersTargetingSeer}, " +
                $"following Seer={camera.ControllersFollowingSeer}.");
            return;
        }

        if (camera.ActiveCameraCount == 0)
        {
            Add(results, Status.Warning, $"{scope} camera target",
                "Controller targeting is correct, but no active CinemachineCamera Follow target was observable.");
            return;
        }

        if (camera.CamerasFollowingSeerTarget == 0)
        {
            Add(results, Status.Fail, $"{scope} camera target",
                "A player camera controller targets the Seer, but no active CinemachineCamera follows " +
                "Character.CameraTarget.");
            return;
        }

        Add(results, Status.Pass, $"{scope} camera target",
            "The TDE controller targets and follows the authoritative Seer, and an active " +
            "CinemachineCamera follows Character.CameraTarget.");
    }

    private static void CompareSeer(
        WorldSnapshot baseline,
        WorldSnapshot current,
        List<Result> results)
    {
        bool observable = baseline.ValidSeerCount == 1 &&
                          baseline.PersistenceMarkerCount == 1 &&
                          current.ValidSeerCount == 1 &&
                          current.PersistenceMarkerCount == 1;
        if (!observable)
        {
            Add(results, Status.Fail, "Persistent Seer continuity",
                "A unique authoritative Seer was not observable at both endpoints.");
            return;
        }

        bool same = baseline.PersistenceMarkerInstanceId == current.PersistenceMarkerInstanceId &&
                    baseline.SeerInstanceId == current.SeerInstanceId &&
                    string.Equals(baseline.SeerName, current.SeerName, StringComparison.Ordinal) &&
                    string.Equals(baseline.SeerPlayerId, current.SeerPlayerId, StringComparison.Ordinal);
        Add(results,
            same ? Status.Pass : Status.Fail,
            "Persistent Seer continuity",
            same
                ? $"The same Seer instance survived {baseline.SceneName} -> {current.SceneName}."
                : "The authoritative Seer's component instance, Character instance, name, or PlayerID changed.");
    }

    private static void CompareInventory(
        string label,
        InventoryReading baseline,
        InventoryReading current,
        List<Result> results)
    {
        if (!baseline.Present || !current.Present)
        {
            Add(results, Status.Fail, $"{label} continuity",
                "The inventory was not observable at both endpoints.");
            return;
        }

        if (!baseline.ContentObserved || !current.ContentObserved)
        {
            Add(results, Status.Warning, $"{label} continuity",
                "Inventory.Content was unavailable at one or both endpoints.");
            return;
        }

        bool sameObject = baseline.InstanceId == current.InstanceId;
        bool sameBinding = string.Equals(baseline.Name, current.Name, StringComparison.Ordinal) &&
                           string.Equals(baseline.PlayerId, current.PlayerId, StringComparison.Ordinal);
        bool sameContent = string.Equals(baseline.Signature, current.Signature, StringComparison.Ordinal);

        if (!sameObject || !sameBinding || !sameContent)
        {
            Add(results, Status.Fail, $"{label} continuity",
                $"Persistent object same={sameObject}, binding same={sameBinding}, content same={sameContent}. " +
                $"Baseline: {baseline.Summary}. Current: {current.Summary}.");
            return;
        }

        if (!baseline.ReliableIdentities || !current.ReliableIdentities)
        {
            Add(results, Status.Warning, $"{label} continuity",
                "Values match, but one or more populated items lack a stable ItemID.");
            return;
        }

        Add(results, Status.Pass, $"{label} continuity",
            $"The same '{current.Name}' object retained identical ItemID/quantity values: {current.Summary}.");
    }

    private static void CompareEquippedWeapon(
        WorldSnapshot baseline,
        WorldSnapshot current,
        List<Result> results)
    {
        if (!baseline.HasEquippedWeapon || !current.HasEquippedWeapon)
        {
            if (baseline.HasEquippedWeapon != current.HasEquippedWeapon)
            {
                Add(results, Status.Fail, "Equipped weapon continuity",
                    "An equipped CurrentWeapon existed at only one endpoint.");
            }
            else
            {
                Add(results, Status.Warning, "Equipped weapon continuity",
                    "No weapon was equipped at either endpoint; this invariant remains untested.");
            }
            return;
        }

        bool same = string.Equals(
            baseline.EquippedWeaponIdentity,
            current.EquippedWeaponIdentity,
            StringComparison.Ordinal);
        if (!same)
        {
            Add(results, Status.Fail, "Equipped weapon continuity",
                $"Weapon changed from '{baseline.EquippedWeaponIdentity}' to " +
                $"'{current.EquippedWeaponIdentity}'.");
            return;
        }

        if (!baseline.EquippedIdentityReliable || !current.EquippedIdentityReliable)
        {
            Add(results, Status.Warning, "Equipped weapon continuity",
                $"Fallback weapon identity '{current.EquippedWeaponIdentity}' matches, but WeaponID was unavailable.");
            return;
        }

        Add(results, Status.Pass, "Equipped weapon continuity",
            $"CurrentWeapon.WeaponID remained '{current.EquippedWeaponIdentity}'.");
    }

    private static void CompareGameState(
        WorldSnapshot baseline,
        WorldSnapshot current,
        List<Result> results)
    {
        if (baseline.GameStateCount == 0 && current.GameStateCount == 0)
        {
            Add(results, Status.Warning, "GameState continuity",
                "GameState was unavailable at both endpoints.");
            return;
        }

        if (baseline.GameStateCount != 1 || current.GameStateCount != 1)
        {
            Add(results, Status.Fail, "GameState continuity",
                "Exactly one GameState was not observable at both endpoints.");
            return;
        }

        bool sameInstance = baseline.GameStateInstanceId == current.GameStateInstanceId;
        bool sameFacts = baseline.TotalShards == current.TotalShards &&
                         baseline.WindWardBossDefeated == current.WindWardBossDefeated;
        Add(results,
            sameInstance && sameFacts ? Status.Pass : Status.Fail,
            "GameState continuity",
            sameInstance && sameFacts
                ? $"The same GameState retained TotalShards={current.TotalShards} and " +
                  $"WindWardBossDefeated={current.WindWardBossDefeated}."
                : $"Instance same={sameInstance}, facts same={sameFacts}. Baseline " +
                  $"({baseline.TotalShards}, {baseline.WindWardBossDefeated}); current " +
                  $"({current.TotalShards}, {current.WindWardBossDefeated}).");
    }

    private static void AddInventoryEvidence(
        InventoryReading inventory,
        string label,
        List<Result> results)
    {
        if (!inventory.Present || !inventory.ContentObserved)
        {
            return;
        }

        Add(results,
            inventory.TotalQuantity > 0 ? Status.Pass : Status.Warning,
            label,
            inventory.TotalQuantity > 0
                ? $"Observed {inventory.FilledSlots} populated slot(s), total quantity {inventory.TotalQuantity}."
                : "Inventory is empty; use a nonzero quantity for meaningful continuity evidence.");
    }

    private static void AddWeaponEvidence(WorldSnapshot snapshot, List<Result> results)
    {
        bool weaponInventoryPopulated = snapshot.WeaponInventory.Present &&
                                        snapshot.WeaponInventory.ContentObserved &&
                                        snapshot.WeaponInventory.TotalQuantity > 0;
        bool equipped = snapshot.HasEquippedWeapon && snapshot.EquippedIdentityReliable;
        Add(results,
            weaponInventoryPopulated && equipped ? Status.Pass : Status.Warning,
            "Weapon test evidence",
            weaponInventoryPopulated && equipped
                ? $"Weapon inventory is populated and '{snapshot.EquippedWeaponIdentity}' is equipped."
                : "Use a populated weapon inventory and an equipped weapon for meaningful continuity evidence.");
    }

    private static string BuildReport(
        string title,
        string source,
        string destination,
        List<Result> results,
        WorldSnapshot snapshot)
    {
        int pass = 0;
        int warning = 0;
        int fail = 0;
        foreach (Result result in results)
        {
            switch (result.Status)
            {
                case Status.Pass: pass++; break;
                case Status.Warning: warning++; break;
                case Status.Fail: fail++; break;
            }
        }

        string outcome = fail > 0
            ? "FAILURES OBSERVED"
            : warning > 0
                ? "PARTIALLY OBSERVED - warnings prevent complete verification"
                : "OBSERVED CONTINUITY PASSED";

        StringBuilder report = new StringBuilder();
        report.AppendLine($"=== MYTHOS {title} ===");
        report.AppendLine($"Source: {source}");
        report.AppendLine($"Destination: {destination}");
        report.AppendLine($"Frame: {snapshot.Frame}");
        report.AppendLine($"Result: {outcome} ({pass} PASS, {warning} WARNING, {fail} FAIL)");
        report.AppendLine();

        foreach (Result result in results)
        {
            report.Append('[').Append(result.Status.ToString().ToUpperInvariant()).Append("] ")
                .Append(result.Invariant).Append(": ").AppendLine(result.Detail);
        }

        report.AppendLine();
        report.Append("Witness is observational only; it does not prove unobserved intermediate behavior.");
        return report.ToString();
    }

    private static void Add(List<Result> results, Status status, string invariant, string detail)
    {
        results.Add(new Result(status, invariant, detail));
    }

    private static void Publish(string report)
    {
        _lastReport = report;
        Debug.Log(report);

        MythosWorldStateWitness window = GetWindow<MythosWorldStateWitness>("World State Witness");
        window.Repaint();
    }

    private enum Status
    {
        Pass,
        Warning,
        Fail
    }

    private readonly struct Result
    {
        public Result(Status status, string invariant, string detail)
        {
            Status = status;
            Invariant = invariant;
            Detail = detail;
        }

        public Status Status { get; }
        public string Invariant { get; }
        public string Detail { get; }
    }

    private sealed class InventoryReading
    {
        public static readonly InventoryReading Missing = new InventoryReading(
            false, string.Empty, string.Empty, string.Empty, false, false, false, false, false,
            0, 0, string.Empty, "<missing>");

        public InventoryReading(
            bool present,
            string instanceId,
            string name,
            string playerId,
            bool hasPersistenceMarker,
            bool ownerIsSeer,
            bool contentObserved,
            bool reliableIdentities,
            bool validQuantities,
            int filledSlots,
            long totalQuantity,
            string signature,
            string summary)
        {
            Present = present;
            InstanceId = instanceId;
            Name = name;
            PlayerId = playerId;
            HasPersistenceMarker = hasPersistenceMarker;
            OwnerIsSeer = ownerIsSeer;
            ContentObserved = contentObserved;
            ReliableIdentities = reliableIdentities;
            ValidQuantities = validQuantities;
            FilledSlots = filledSlots;
            TotalQuantity = totalQuantity;
            Signature = signature;
            Summary = summary;
        }

        public bool Present { get; }
        public string InstanceId { get; }
        public string Name { get; }
        public string PlayerId { get; }
        public bool HasPersistenceMarker { get; }
        public bool OwnerIsSeer { get; }
        public bool ContentObserved { get; }
        public bool ReliableIdentities { get; }
        public bool ValidQuantities { get; }
        public int FilledSlots { get; }
        public long TotalQuantity { get; }
        public string Signature { get; }
        public string Summary { get; }
    }

    private sealed class CameraReading
    {
        public CameraReading(
            int activePlayerControllerCount,
            int controllersTargetingSeer,
            int controllersFollowingSeer,
            bool seerCameraTargetExists,
            int activeCameraCount,
            int camerasFollowingSeerTarget)
        {
            ActivePlayerControllerCount = activePlayerControllerCount;
            ControllersTargetingSeer = controllersTargetingSeer;
            ControllersFollowingSeer = controllersFollowingSeer;
            SeerCameraTargetExists = seerCameraTargetExists;
            ActiveCameraCount = activeCameraCount;
            CamerasFollowingSeerTarget = camerasFollowingSeerTarget;
        }

        public int ActivePlayerControllerCount { get; }
        public int ControllersTargetingSeer { get; }
        public int ControllersFollowingSeer { get; }
        public bool SeerCameraTargetExists { get; }
        public int ActiveCameraCount { get; }
        public int CamerasFollowingSeerTarget { get; }
    }

    private sealed class WorldSnapshot
    {
        public WorldSnapshot(
            string sceneName,
            int frame,
            int persistenceMarkerCount,
            int validSeerCount,
            string persistenceMarkerInstanceId,
            string seerInstanceId,
            string seerName,
            string seerPlayerId,
            int placementCount,
            int gameManagerCount,
            string registeredPersistentCharacterId,
            int sceneLevelManagerCount,
            bool levelPlayersObserved,
            int levelPlayerCount,
            int seerRegistrationCount,
            bool hasCharacterInventory,
            InventoryReading mainInventory,
            InventoryReading weaponInventory,
            int weaponHandlerCount,
            bool hasEquippedWeapon,
            string equippedWeaponIdentity,
            bool equippedIdentityReliable,
            int gameStateCount,
            bool gameStateInstanceMatches,
            string gameStateInstanceId,
            int totalShards,
            bool windWardBossDefeated,
            CameraReading camera)
        {
            SceneName = sceneName;
            Frame = frame;
            PersistenceMarkerCount = persistenceMarkerCount;
            ValidSeerCount = validSeerCount;
            PersistenceMarkerInstanceId = persistenceMarkerInstanceId;
            SeerInstanceId = seerInstanceId;
            SeerName = seerName;
            SeerPlayerId = seerPlayerId;
            PlacementCount = placementCount;
            GameManagerCount = gameManagerCount;
            RegisteredPersistentCharacterId = registeredPersistentCharacterId;
            SceneLevelManagerCount = sceneLevelManagerCount;
            LevelPlayersObserved = levelPlayersObserved;
            LevelPlayerCount = levelPlayerCount;
            SeerRegistrationCount = seerRegistrationCount;
            HasCharacterInventory = hasCharacterInventory;
            MainInventory = mainInventory;
            WeaponInventory = weaponInventory;
            WeaponHandlerCount = weaponHandlerCount;
            HasEquippedWeapon = hasEquippedWeapon;
            EquippedWeaponIdentity = equippedWeaponIdentity;
            EquippedIdentityReliable = equippedIdentityReliable;
            GameStateCount = gameStateCount;
            GameStateInstanceMatches = gameStateInstanceMatches;
            GameStateInstanceId = gameStateInstanceId;
            TotalShards = totalShards;
            WindWardBossDefeated = windWardBossDefeated;
            Camera = camera;
        }

        public string SceneName { get; }
        public int Frame { get; }
        public int PersistenceMarkerCount { get; }
        public int ValidSeerCount { get; }
        public string PersistenceMarkerInstanceId { get; }
        public string SeerInstanceId { get; }
        public string SeerName { get; }
        public string SeerPlayerId { get; }
        public int PlacementCount { get; }
        public int GameManagerCount { get; }
        public string RegisteredPersistentCharacterId { get; }
        public int SceneLevelManagerCount { get; }
        public bool LevelPlayersObserved { get; }
        public int LevelPlayerCount { get; }
        public int SeerRegistrationCount { get; }
        public bool HasCharacterInventory { get; }
        public InventoryReading MainInventory { get; }
        public InventoryReading WeaponInventory { get; }
        public int WeaponHandlerCount { get; }
        public bool HasEquippedWeapon { get; }
        public string EquippedWeaponIdentity { get; }
        public bool EquippedIdentityReliable { get; }
        public int GameStateCount { get; }
        public bool GameStateInstanceMatches { get; }
        public string GameStateInstanceId { get; }
        public int TotalShards { get; }
        public bool WindWardBossDefeated { get; }
        public CameraReading Camera { get; }
    }
}
