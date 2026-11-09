#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot, explicitly invoked Editor utility for the approved Phase 2B
/// Museum <-> CITYSCAPE integration. It never runs automatically.
/// </summary>
internal static class MythosPhase2BIntegrator
{
    private const string MenuPath =
        "Mythos/Phase 2B/Apply Museum-CITYSCAPE Integration";

    private const string CityscapeScenePath =
        "Assets/_Project/Scenes/CITYSCAPE/CITYSCAPE.unity";
    private const string MuseumScenePath =
        "Assets/_Project/Scenes/MuseumHub_WindWard/MuseumHub_WindWard.unity";
    private const string SeerPrefabPath =
        "Assets/_Project/Prefabs/MinimalCharacter 1.prefab";
    private const string Minimal3DCameraPrefabPath =
        "Assets/_ThirdParty/TopDownEngine/Demos/Minimal3D/Prefabs/Camera/Minimal3DCameras --------------------------------------------------------.prefab";

    private const string ExistingRecoveryFolder =
        "_Recovery/Phase2B_2026-11-01_preimplementation";
    private const string ApplyRecoveryFolder =
        "_Recovery/Phase2B_2026-11-01_before_apply";
    private const string ReportRelativePath =
        "Library/Phase2BIntegrationReport.txt";
    private const string RecoveryManifestName = "SHA256SUMS.txt";

    private const string CityContainerName = "Phase2B_MuseumCityscape";
    private const string CityPortalName = "MuseumEntrance_ToMuseum";
    private const string CitySpawnObjectName = "Spawn_FromMuseum";
    private const string MuseumContainerName = "Phase2B_CityscapeConnection";
    private const string MuseumPortalName = "MuseumExit_ToCityscape";
    private const string MuseumSpawnObjectName = "Spawn_FromCityscape";

    private const string FromMuseumSpawnName = "FromMuseum";
    private const string FromCityscapeSpawnName = "FromCityscape";

    [MenuItem(MenuPath)]
    private static void ApplyFromMenu()
    {
        ApplyIntegration(showDialog: true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenuItem()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating;
    }

    /// <summary>
    /// Optional explicit entry point for a supported Unity -executeMethod call.
    /// It is not invoked automatically.
    /// </summary>
    public static void ApplyFromCommandLine()
    {
        ApplyIntegration(showDialog: false);
    }

    private static void ApplyIntegration(bool showDialog)
    {
        StringBuilder report = new StringBuilder();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        ApplyProgress progress = new ApplyProgress();

        Append(report, "PHASE 2B MUSEUM <-> CITYSCAPE INTEGRATION");
        Append(report, $"Started: {DateTime.Now:O}");

        try
        {
            RequireEditorReady();
            RequireAllLoadedScenesClean();
            RequireProjectFilesAndRecovery(report);
            RequireSerializedContracts();

            Scene cityscape = EditorSceneManager.OpenScene(
                CityscapeScenePath,
                OpenSceneMode.Single
            );
            Scene museum = EditorSceneManager.OpenScene(
                MuseumScenePath,
                OpenSceneMode.Additive
            );

            SeerPrefabReferences inspectionSeer = ResolveAuthoritativeSeerPrefab();
            CityscapePlan cityPlan = InspectCityscape(
                cityscape,
                inspectionSeer.Character,
                report
            );
            MuseumPlan museumPlan = InspectMuseum(
                museum,
                inspectionSeer.Character,
                report
            );

            CreateApplyRecovery(report);
            progress.RecoveryVerified = true;

            progress.ChangesStarted = true;
            SeerPrefabReferences cityApplySeer = ResolveAuthoritativeSeerPrefab();
            ApplyCityscape(
                cityPlan,
                cityApplySeer.Character,
                cityApplySeer.Controller,
                report
            );

            SeerPrefabReferences museumApplySeer = ResolveAuthoritativeSeerPrefab();
            ApplyMuseum(
                museumPlan,
                museumApplySeer.Character,
                museumApplySeer.Controller,
                report
            );

            // Scene mutation may invalidate cached UnityEngine.Object wrappers for
            // prefab sub-objects. Resolve from the authoritative asset path again,
            // then reassert both serialized LevelManager references before saving.
            SeerPrefabReferences validationSeer = ResolveAuthoritativeSeerPrefab();
            AssignAuthoritativeSeerPlayerPrefab(
                cityPlan.LevelManager,
                validationSeer.Character,
                "CITYSCAPE"
            );
            AssignAuthoritativeSeerPlayerPrefab(
                museumPlan.LevelManager,
                validationSeer.Character,
                "Museum"
            );

            ValidateCityscape(
                cityPlan.Scene,
                validationSeer.Character,
                validationSeer.Controller,
                report
            );
            ValidateMuseum(
                museumPlan.Scene,
                validationSeer.Character,
                validationSeer.Controller,
                report
            );

            SeerPrefabReferences citySaveSeer = ResolveAuthoritativeSeerPrefab();
            AssignAuthoritativeSeerPlayerPrefab(
                cityPlan.LevelManager,
                citySaveSeer.Character,
                "CITYSCAPE"
            );
            progress.CityscapeSaveAttempted = true;
            progress.CityscapeSaved = EditorSceneManager.SaveScene(cityPlan.Scene);
            Require(progress.CityscapeSaved, "Unity failed to save CITYSCAPE.");
            Append(report, "Saved CITYSCAPE successfully.");

            SeerPrefabReferences museumSaveSeer = ResolveAuthoritativeSeerPrefab();
            AssignAuthoritativeSeerPlayerPrefab(
                museumPlan.LevelManager,
                museumSaveSeer.Character,
                "Museum"
            );
            progress.MuseumSaveAttempted = true;
            progress.MuseumSaved = EditorSceneManager.SaveScene(museumPlan.Scene);
            Require(progress.MuseumSaved, "Unity failed to save MuseumHub_WindWard.");
            Append(report, "Saved MuseumHub_WindWard successfully.");

            progress.BuildSettingsAttempted = true;
            progress.BuildSettingsChanged = RegisterCityscapeBuildScene(report);
            progress.BuildSettingsVerified = true;

            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            Append(report, "Restored the creator's original open-scene setup.");
            Append(report, "SUCCESS: approved Phase 2B scene changes were saved.");

            WriteReport(report);
            Debug.Log("[Phase2B] Museum <-> CITYSCAPE integration completed.\n" + report);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Phase 2B Complete",
                    "Museum <-> CITYSCAPE integration was applied and validated. " +
                    "Do not enter Play Mode until you have reviewed the Console and " +
                    "Library/Phase2BIntegrationReport.txt.",
                    "OK"
                );
            }
        }
        catch (Exception exception)
        {
            Append(report, $"FAILED: {exception.Message}");
            Append(report, exception.ToString());

            if (!progress.ChangesStarted)
            {
                TryRestoreOriginalSceneSetup(originalSetup, report);
            }
            else
            {
                AppendPartialSaveAndRecoveryGuidance(progress, report);
            }

            WriteReport(report);
            Debug.LogError("[Phase2B] Integration stopped safely.\n" + report);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Phase 2B Stopped",
                    exception.Message +
                    "\n\nNo speculative fallback was attempted. Review the Console and " +
                    "Library/Phase2BIntegrationReport.txt.",
                    "OK"
                );
            }

            if (!showDialog)
            {
                throw;
            }
        }
    }

    private static CityscapePlan InspectCityscape(
        Scene scene,
        Character seerCharacter,
        StringBuilder report
    )
    {
        RequireScene(scene, "CITYSCAPE");
        Append(report, "Inspecting CITYSCAPE without modifying it.");

        LevelManager levelManager = RequireSingleComponent<LevelManager>(scene);
        RequireValidInitialSpawn(levelManager, scene);
        RequireNoSceneOwnedPersistence(scene);
        RequireNoCompetingPlacement(scene);
        RequireNoSceneCharacters(levelManager, "CITYSCAPE");
        RequireNoConflictingSpawnPoint(
            scene,
            FromMuseumSpawnName,
            CitySpawnObjectName
        );

        Character[] sceneCharacters = GetSceneComponents<Character>(scene);
        Require(
            sceneCharacters.Length == 0,
            "CITYSCAPE contains a scene-owned Character. The utility will not guess " +
            "which player should be removed."
        );

        GameObject ground = RequireSingleGameObject(scene, "Ground");
        Collider groundCollider = ground.GetComponent<Collider>();
        Require(
            groundCollider != null && groundCollider.enabled && !groundCollider.isTrigger,
            "CITYSCAPE Ground must have one enabled, non-trigger Collider."
        );

        GameObject cameraRig = RequireSinglePrefabRoot(
            scene,
            Minimal3DCameraPrefabPath
        );

        if (levelManager.PlayerPrefabs == null ||
            levelManager.PlayerPrefabs.Length != 1 ||
            levelManager.PlayerPrefabs[0] != seerCharacter)
        {
            Append(
                report,
                "CITYSCAPE LevelManager still references the inherited demo player; " +
                "it will be changed to the persistent Seer prefab."
            );
        }

        LogPresentationInventory(scene, "CITYSCAPE before changes", report);

        return new CityscapePlan
        {
            Scene = scene,
            LevelManager = levelManager,
            GroundCollider = groundCollider,
            InheritedCameraRig = cameraRig,
        };
    }

    private static MuseumPlan InspectMuseum(
        Scene scene,
        Character seerCharacter,
        StringBuilder report
    )
    {
        RequireScene(scene, "MuseumHub_WindWard");
        Append(report, "Inspecting MuseumHub_WindWard without modifying it.");

        LevelManager levelManager = RequireSingleComponent<LevelManager>(scene);
        RequireValidInitialSpawn(levelManager, scene);
        RequireNoCompetingPlacement(scene);
        RequireSeerPlayerPrefab(levelManager, seerCharacter, "Museum");
        RequireNoConflictingSpawnPoint(
            scene,
            FromCityscapeSpawnName,
            MuseumSpawnObjectName
        );

        GameObject mazePortalObject = RequireSingleGameObject(scene, "Portal_ToMaze");
        ScenePortal mazePortal = mazePortalObject.GetComponent<ScenePortal>();
        Require(mazePortal != null, "Portal_ToMaze has no ScenePortal component.");
        RequirePortalValues(mazePortal, "WindWard", "MazeSpawnpoint");

        GameObject doorway = RequireSingleGameObject(scene, "SM_Env_Door_Frame_01");
        Require(
            doorway.activeInHierarchy,
            "The verified Museum doorway SM_Env_Door_Frame_01 is inactive."
        );
        Require(
            doorway.GetComponentsInChildren<Renderer>(true).Length > 0,
            "The verified Museum doorway has no renderable geometry."
        );

        GameObject portalsParent = RequireSingleGameObject(scene, "Portals");
        LogPresentationInventory(scene, "Museum before changes", report);

        return new MuseumPlan
        {
            Scene = scene,
            LevelManager = levelManager,
            Doorway = doorway.transform,
            PortalsParent = portalsParent.transform,
            MazePortal = mazePortal,
        };
    }

    private static void ApplyCityscape(
        CityscapePlan plan,
        Character seerCharacter,
        CharacterController seerController,
        StringBuilder report
    )
    {
        LevelManager levelManager = plan.LevelManager;
        AssignAuthoritativeSeerPlayerPrefab(
            levelManager,
            seerCharacter,
            "CITYSCAPE"
        );
        Append(report, "CITYSCAPE LevelManager now references the approved Seer prefab.");

        if (plan.InheritedCameraRig.activeSelf)
        {
            plan.InheritedCameraRig.SetActive(false);
            EditorUtility.SetDirty(plan.InheritedCameraRig);
            Append(
                report,
                "Disabled the inherited Minimal3D camera/HUD presentation prefab. " +
                "Bootstrap remains authoritative for camera, HUD, AudioListener, and EventSystem."
            );
        }
        else
        {
            Append(report, "Inherited Minimal3D camera/HUD prefab was already disabled.");
        }

        Bounds groundBounds = plan.GroundCollider.bounds;
        Vector3 portalPosition = new Vector3(
            groundBounds.center.x,
            groundBounds.max.y,
            groundBounds.center.z
        );
        Vector3 spawnPosition = portalPosition + Vector3.forward * 4.5f;

        Require(
            ContainsXZ(groundBounds, portalPosition) && ContainsXZ(groundBounds, spawnPosition),
            "Computed CITYSCAPE portal or arrival point is outside the Ground bounds."
        );
        RequireSafePortalAndSpawnPlacement(
            plan.Scene,
            portalPosition,
            spawnPosition,
            seerController,
            "CITYSCAPE"
        );

        GameObject container = EnsureRoot(plan.Scene, CityContainerName);
        GameObject portal = EnsurePortal(
            container.transform,
            CityPortalName,
            portalPosition,
            Quaternion.LookRotation(Vector3.forward),
            "MuseumHub_WindWard",
            FromCityscapeSpawnName
        );
        EnsurePrototypeArch(portal.transform);

        EnsureSpawnPoint(
            container.transform,
            CitySpawnObjectName,
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward),
            FromMuseumSpawnName
        );

        EditorSceneManager.MarkSceneDirty(plan.Scene);
        Append(
            report,
            $"CITYSCAPE portal placed at {Format(portalPosition)}; FromMuseum arrival " +
            $"placed at {Format(spawnPosition)}."
        );
    }

    private static void ApplyMuseum(
        MuseumPlan plan,
        Character seerCharacter,
        CharacterController seerController,
        StringBuilder report
    )
    {
        // Reassert the already approved player reference without changing any other
        // LevelManager behavior.
        AssignAuthoritativeSeerPlayerPrefab(
            plan.LevelManager,
            seerCharacter,
            "Museum"
        );

        Vector3 floorReference = plan.LevelManager.InitialSpawnPoint.transform.position;
        Vector3 doorwayPosition = plan.Doorway.position;
        Vector3 portalPosition = new Vector3(
            doorwayPosition.x,
            floorReference.y,
            doorwayPosition.z
        );

        Vector3 inward = floorReference - portalPosition;
        inward.y = 0f;
        Require(
            inward.sqrMagnitude > 4f,
            "Museum doorway is too close to InitialSpawnPoint to derive a safe inward direction."
        );
        inward.Normalize();

        Vector3 spawnPosition = portalPosition + inward * 4.5f;
        Quaternion facing = Quaternion.LookRotation(inward, Vector3.up);
        RequireSafePortalAndSpawnPlacement(
            plan.Scene,
            portalPosition,
            spawnPosition,
            seerController,
            "Museum"
        );

        GameObject container = EnsureChild(
            plan.PortalsParent,
            MuseumContainerName
        );
        GameObject portal = EnsurePortal(
            container.transform,
            MuseumPortalName,
            portalPosition,
            facing,
            "CITYSCAPE",
            FromMuseumSpawnName
        );
        EnsurePrototypeArch(portal.transform);

        EnsureSpawnPoint(
            container.transform,
            MuseumSpawnObjectName,
            spawnPosition,
            facing,
            FromCityscapeSpawnName
        );

        // Regression guard: the existing Wind Ward portal must be untouched.
        RequirePortalValues(plan.MazePortal, "WindWard", "MazeSpawnpoint");

        EditorSceneManager.MarkSceneDirty(plan.Scene);
        Append(
            report,
            $"Museum CITYSCAPE exit placed at verified doorway {Format(portalPosition)}; " +
            $"FromCityscape arrival placed inward at {Format(spawnPosition)}."
        );
    }

    private static void ValidateCityscape(
        Scene scene,
        Character seerCharacter,
        CharacterController seerController,
        StringBuilder report
    )
    {
        LevelManager levelManager = RequireSingleComponent<LevelManager>(scene);
        RequireSeerPlayerPrefab(levelManager, seerCharacter, "CITYSCAPE");
        RequireValidInitialSpawn(levelManager, scene);
        RequireNoSceneOwnedPersistence(scene);
        RequireNoCompetingPlacement(scene);

        GameObject cameraRig = RequireSinglePrefabRoot(scene, Minimal3DCameraPrefabPath);
        Require(!cameraRig.activeSelf, "Inherited CITYSCAPE camera rig is still active.");

        ScenePortal portal = RequireSingleGameObject(scene, CityPortalName)
            .GetComponent<ScenePortal>();
        Require(portal != null, "CITYSCAPE portal lost its ScenePortal component.");
        RequirePortalValues(portal, "MuseumHub_WindWard", FromCityscapeSpawnName);
        RequireTrigger(portal.gameObject);

        SpawnPoint spawn = RequireSingleGameObject(scene, CitySpawnObjectName)
            .GetComponent<SpawnPoint>();
        Require(spawn != null, "CITYSCAPE FromMuseum object has no SpawnPoint.");
        Require(
            string.Equals(spawn.SpawnName, FromMuseumSpawnName, StringComparison.Ordinal),
            "CITYSCAPE SpawnPoint name is not exactly FromMuseum."
        );
        Require(
            Vector3.Distance(portal.transform.position, spawn.transform.position) >= 4f,
            "CITYSCAPE arrival point is too close to its return portal."
        );
        RequireSafePortalAndSpawnPlacement(
            scene,
            portal.transform.position,
            spawn.transform.position,
            seerController,
            "CITYSCAPE"
        );

        Require(
            CountActiveComponents<Camera>(scene) == 0 &&
            CountActiveComponents<AudioListener>(scene) == 0 &&
            CountActiveComponents<Canvas>(scene) == 0 &&
            CountActiveComponents<EventSystem>(scene) == 0,
            "CITYSCAPE still has an active inherited camera, AudioListener, Canvas, " +
            "or EventSystem after the verified camera-rig disable."
        );
        RequireNoMissingScripts(scene);
        Append(report, "CITYSCAPE static validation passed.");
        LogPresentationInventory(scene, "CITYSCAPE after changes", report);
    }

    private static void ValidateMuseum(
        Scene scene,
        Character seerCharacter,
        CharacterController seerController,
        StringBuilder report
    )
    {
        LevelManager levelManager = RequireSingleComponent<LevelManager>(scene);
        RequireSeerPlayerPrefab(levelManager, seerCharacter, "Museum");
        RequireValidInitialSpawn(levelManager, scene);
        RequireNoCompetingPlacement(scene);

        ScenePortal mazePortal = RequireSingleGameObject(scene, "Portal_ToMaze")
            .GetComponent<ScenePortal>();
        Require(mazePortal != null, "Portal_ToMaze lost its ScenePortal component.");
        RequirePortalValues(mazePortal, "WindWard", "MazeSpawnpoint");

        ScenePortal cityPortal = RequireSingleGameObject(scene, MuseumPortalName)
            .GetComponent<ScenePortal>();
        Require(cityPortal != null, "Museum CITYSCAPE portal has no ScenePortal.");
        RequirePortalValues(cityPortal, "CITYSCAPE", FromMuseumSpawnName);
        RequireTrigger(cityPortal.gameObject);

        SpawnPoint spawn = RequireSingleGameObject(scene, MuseumSpawnObjectName)
            .GetComponent<SpawnPoint>();
        Require(spawn != null, "Museum FromCityscape object has no SpawnPoint.");
        Require(
            string.Equals(spawn.SpawnName, FromCityscapeSpawnName, StringComparison.Ordinal),
            "Museum SpawnPoint name is not exactly FromCityscape."
        );
        Require(
            Vector3.Distance(cityPortal.transform.position, spawn.transform.position) >= 4f,
            "Museum return arrival point is too close to its CITYSCAPE portal."
        );
        RequireSafePortalAndSpawnPlacement(
            scene,
            cityPortal.transform.position,
            spawn.transform.position,
            seerController,
            "Museum"
        );

        RequireNoMissingScripts(scene);
        Append(report, "Museum static validation passed; Wind Ward portal is unchanged.");
    }

    private static GameObject EnsurePortal(
        Transform parent,
        string objectName,
        Vector3 position,
        Quaternion rotation,
        string targetScene,
        string targetSpawn
    )
    {
        GameObject portal = EnsureChild(parent, objectName);
        portal.transform.SetPositionAndRotation(position, rotation);
        portal.transform.localScale = Vector3.one;
        portal.layer = 0;

        BoxCollider trigger = portal.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = portal.AddComponent<BoxCollider>();
        }
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.25f, 0f);
        trigger.size = new Vector3(3f, 2.5f, 1.5f);

        ScenePortal scenePortal = portal.GetComponent<ScenePortal>();
        if (scenePortal == null)
        {
            scenePortal = portal.AddComponent<ScenePortal>();
        }

        SerializedObject serializedPortal = new SerializedObject(scenePortal);
        SetString(serializedPortal, "targetScene", targetScene);
        SetString(serializedPortal, "targetSpawnPointName", targetSpawn);
        SetBool(serializedPortal, "useAsyncLoader", true);
        SetFloat(serializedPortal, "fadeDuration", 0.8f);
        SetString(serializedPortal, "requiredTag", "Player");
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(portal);
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(scenePortal);
        return portal;
    }

    private static SpawnPoint EnsureSpawnPoint(
        Transform parent,
        string objectName,
        Vector3 position,
        Quaternion rotation,
        string spawnName
    )
    {
        GameObject spawnObject = EnsureChild(parent, objectName);
        spawnObject.transform.SetPositionAndRotation(position, rotation);
        spawnObject.transform.localScale = Vector3.one;

        SpawnPoint spawnPoint = spawnObject.GetComponent<SpawnPoint>();
        if (spawnPoint == null)
        {
            spawnPoint = spawnObject.AddComponent<SpawnPoint>();
        }

        SerializedObject serializedSpawn = new SerializedObject(spawnPoint);
        SetString(serializedSpawn, "spawnName", spawnName);
        serializedSpawn.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawnObject);
        EditorUtility.SetDirty(spawnPoint);
        return spawnPoint;
    }

    private static void EnsurePrototypeArch(Transform portal)
    {
        EnsureVisualCube(
            portal,
            "Prototype_LeftPillar",
            new Vector3(-1.35f, 1.5f, 0f),
            new Vector3(0.35f, 3f, 0.35f)
        );
        EnsureVisualCube(
            portal,
            "Prototype_RightPillar",
            new Vector3(1.35f, 1.5f, 0f),
            new Vector3(0.35f, 3f, 0.35f)
        );
        EnsureVisualCube(
            portal,
            "Prototype_Header",
            new Vector3(0f, 3f, 0f),
            new Vector3(3.05f, 0.35f, 0.35f)
        );

        GameObject lightObject = EnsureChild(portal, "Prototype_CyanLight");
        lightObject.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        lightObject.transform.localRotation = Quaternion.identity;
        lightObject.transform.localScale = Vector3.one;

        Light light = lightObject.GetComponent<Light>();
        if (light == null)
        {
            light = lightObject.AddComponent<Light>();
        }
        light.type = LightType.Point;
        light.color = new Color(0.1f, 0.8f, 1f, 1f);
        light.intensity = 2f;
        light.range = 6f;
        light.shadows = LightShadows.None;
        EditorUtility.SetDirty(light);
    }

    private static void EnsureVisualCube(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale
    )
    {
        GameObject visual = FindDirectChild(parent, objectName);
        if (visual == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = objectName;
            visual.transform.SetParent(parent, false);
        }

        Collider[] colliders = visual.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        Require(
            visual.GetComponent<MeshFilter>() != null &&
            visual.GetComponent<MeshRenderer>() != null,
            $"Prototype visual {objectName} is missing its mesh components."
        );

        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = localScale;
        EditorUtility.SetDirty(visual);
    }

    private static GameObject EnsureRoot(Scene scene, string objectName)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .Where(root => root.name == objectName)
            .ToArray();
        Require(matches.Length <= 1, $"Multiple root objects named {objectName}.");

        if (matches.Length == 1)
        {
            return matches[0];
        }

        GameObject created = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static GameObject EnsureChild(Transform parent, string objectName)
    {
        GameObject existing = FindDirectChild(parent, objectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject FindDirectChild(Transform parent, string objectName)
    {
        List<GameObject> matches = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
            {
                matches.Add(child.gameObject);
            }
        }

        Require(
            matches.Count <= 1,
            $"Multiple children named {objectName} under {parent.name}."
        );
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool RegisterCityscapeBuildScene(StringBuilder report)
    {
        List<EditorBuildSettingsScene> scenes =
            EditorBuildSettings.scenes.ToList();
        bool changed = false;
        int existingIndex = scenes.FindIndex(
            scene => string.Equals(
                scene.path,
                CityscapeScenePath,
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (existingIndex >= 0)
        {
            if (!scenes[existingIndex].enabled)
            {
                scenes[existingIndex] = new EditorBuildSettingsScene(
                    CityscapeScenePath,
                    true
                );
                EditorBuildSettings.scenes = scenes.ToArray();
                changed = true;
                Append(report, "Enabled the existing CITYSCAPE build-scene entry.");
            }
            else
            {
                Append(report, "CITYSCAPE was already enabled in build settings.");
            }
        }
        else
        {
            scenes.Add(new EditorBuildSettingsScene(CityscapeScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            changed = true;
            Append(
                report,
                "Appended CITYSCAPE as an enabled build scene; existing entries and " +
                "their order were preserved."
            );
        }

        Require(
            EditorBuildSettings.scenes.Any(
                scene => scene.enabled && string.Equals(
                    scene.path,
                    CityscapeScenePath,
                    StringComparison.OrdinalIgnoreCase
                )
            ),
            "CITYSCAPE build-scene registration did not persist."
        );
        return changed;
    }

    private static void RequireProjectFilesAndRecovery(StringBuilder report)
    {
        Require(File.Exists(CityscapeScenePath), $"Missing {CityscapeScenePath}");
        Require(File.Exists(MuseumScenePath), $"Missing {MuseumScenePath}");
        Require(File.Exists(SeerPrefabPath), $"Missing {SeerPrefabPath}");

        string projectRoot = ProjectRoot;
        string recoveryScene = Path.Combine(
            projectRoot,
            ExistingRecoveryFolder,
            "CITYSCAPE.unity"
        );
        string recoveryMeta = recoveryScene + ".meta";
        Require(
            File.Exists(recoveryScene) && File.Exists(recoveryMeta),
            "The required preimplementation CITYSCAPE recovery copy is incomplete."
        );
        RequireValidUnityYaml(recoveryScene, "--- !u!");
        RequireMatchingMetaGuids(
            recoveryMeta,
            Path.Combine(projectRoot, CityscapeScenePath)
                + ".meta",
            "preimplementation CITYSCAPE recovery"
        );
        Append(
            report,
            $"Verified existing recovery structure, asset GUID, and SHA-256 " +
            $"({ComputeSha256(recoveryScene)}): {ExistingRecoveryFolder}"
        );
    }

    private static void CreateApplyRecovery(StringBuilder report)
    {
        string destination = Path.Combine(ProjectRoot, ApplyRecoveryFolder);
        string manifestPath = Path.Combine(destination, RecoveryManifestName);
        bool destinationAlreadyExisted = Directory.Exists(destination);

        if (destinationAlreadyExisted)
        {
            Require(
                File.Exists(manifestPath),
                $"Existing apply-time recovery has no {RecoveryManifestName}; " +
                "its integrity cannot be authenticated. Preserve it and resolve " +
                "the recovery folder manually before applying Phase 2B."
            );
        }

        Directory.CreateDirectory(destination);

        string[] sourcePaths =
        {
            CityscapeScenePath,
            CityscapeScenePath + ".meta",
            MuseumScenePath,
            MuseumScenePath + ".meta",
            "ProjectSettings/EditorBuildSettings.asset",
        };
        string[] destinationNames =
        {
            "CITYSCAPE.unity",
            "CITYSCAPE.unity.meta",
            "MuseumHub_WindWard.unity",
            "MuseumHub_WindWard.unity.meta",
            "EditorBuildSettings.asset",
        };

        for (int i = 0; i < sourcePaths.Length; i++)
        {
            CopyOnce(sourcePaths[i], Path.Combine(destination, destinationNames[i]));
        }

        RequireValidUnityYaml(Path.Combine(destination, "CITYSCAPE.unity"), "--- !u!");
        RequireValidUnityYaml(
            Path.Combine(destination, "MuseumHub_WindWard.unity"),
            "--- !u!"
        );
        RequireValidUnityYaml(
            Path.Combine(destination, "EditorBuildSettings.asset"),
            "EditorBuildSettings:"
        );
        RequireMatchingMetaGuids(
            Path.Combine(destination, "CITYSCAPE.unity.meta"),
            Path.Combine(ProjectRoot, CityscapeScenePath) + ".meta",
            "apply-time CITYSCAPE recovery"
        );
        RequireMatchingMetaGuids(
            Path.Combine(destination, "MuseumHub_WindWard.unity.meta"),
            Path.Combine(ProjectRoot, MuseumScenePath) + ".meta",
            "apply-time Museum recovery"
        );

        string[] expectedManifest = destinationNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(
                name => $"{ComputeSha256(Path.Combine(destination, name))}  {name}"
            )
            .ToArray();

        if (!File.Exists(manifestPath))
        {
            File.WriteAllLines(manifestPath, expectedManifest, Encoding.UTF8);
        }

        string[] actualManifest = File.ReadAllLines(manifestPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        Require(
            actualManifest.SequenceEqual(expectedManifest, StringComparer.Ordinal),
            $"Apply-time recovery hash verification failed: {manifestPath}"
        );

        Append(
            report,
            $"Verified apply-time recovery structure, asset GUIDs, and SHA-256 " +
            $"manifest: {ApplyRecoveryFolder}/{RecoveryManifestName}"
        );
    }

    private static void CopyOnce(string sourceRelativePath, string destinationPath)
    {
        string sourcePath = Path.GetFullPath(
            Path.Combine(ProjectRoot, sourceRelativePath)
        );
        Require(File.Exists(sourcePath), $"Recovery source is missing: {sourceRelativePath}");

        if (File.Exists(destinationPath))
        {
            return;
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
        Require(
            new FileInfo(sourcePath).Length == new FileInfo(destinationPath).Length &&
            string.Equals(
                ComputeSha256(sourcePath),
                ComputeSha256(destinationPath),
                StringComparison.Ordinal
            ),
            $"Recovery copy verification failed: {destinationPath}"
        );
    }

    private static void RequireValidUnityYaml(string path, string requiredMarker)
    {
        Require(File.Exists(path), $"Recovery file is missing: {path}");
        Require(new FileInfo(path).Length > 0, $"Recovery file is empty: {path}");

        string[] firstLines = File.ReadLines(path).Take(8).ToArray();
        Require(
            firstLines.Length > 0 && firstLines[0].StartsWith("%YAML", StringComparison.Ordinal),
            $"Recovery file is not recognizable Unity YAML: {path}"
        );
        Require(
            File.ReadLines(path).Any(line => line.Contains(requiredMarker)),
            $"Recovery file is missing expected marker {requiredMarker}: {path}"
        );
    }

    private static void RequireMatchingMetaGuids(
        string recoveryMetaPath,
        string sourceMetaPath,
        string label
    )
    {
        string recoveryGuid = ReadMetaGuid(recoveryMetaPath);
        string sourceGuid = ReadMetaGuid(sourceMetaPath);
        Require(
            string.Equals(recoveryGuid, sourceGuid, StringComparison.Ordinal),
            $"The {label} .meta GUID does not match its source asset."
        );
    }

    private static string ReadMetaGuid(string path)
    {
        Require(File.Exists(path), $"Meta file is missing: {path}");
        string guidLine = File.ReadLines(path)
            .FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal));
        Require(!string.IsNullOrEmpty(guidLine), $"Meta file has no GUID: {path}");
        return guidLine.Substring("guid: ".Length).Trim();
    }

    private static string ComputeSha256(string path)
    {
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            return BitConverter.ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }

    private static void RequireSerializedContracts()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Require(
            typeof(ScenePortal).GetField("targetScene", flags) != null &&
            typeof(ScenePortal).GetField("targetSpawnPointName", flags) != null &&
            typeof(ScenePortal).GetField("useAsyncLoader", flags) != null &&
            typeof(ScenePortal).GetField("fadeDuration", flags) != null &&
            typeof(ScenePortal).GetField("requiredTag", flags) != null,
            "ScenePortal serialized contract changed; utility refused to guess fields."
        );
        Require(
            typeof(SpawnPoint).GetField("spawnName", flags) != null,
            "SpawnPoint serialized contract changed; utility refused to guess fields."
        );

        PropertyInfo persistentCharacter = typeof(GameManager).GetProperty(
            "PersistentCharacter",
            BindingFlags.Instance | BindingFlags.Public
        );
        Require(
            persistentCharacter != null &&
            persistentCharacter.CanRead &&
            persistentCharacter.CanWrite &&
            persistentCharacter.PropertyType == typeof(Character),
            "GameManager.PersistentCharacter contract changed; persistent Seer " +
            "registration can no longer be guaranteed."
        );

        PropertyInfo players = typeof(LevelManager).GetProperty(
            "Players",
            BindingFlags.Instance | BindingFlags.Public
        );
        Require(
            players != null &&
            players.CanRead &&
            players.PropertyType == typeof(List<Character>),
            "LevelManager.Players contract changed; persistent Seer registration " +
            "can no longer be guaranteed."
        );
        Require(
            typeof(LevelManager).GetMethod(
                "InstantiatePlayableCharacters",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) != null,
            "LevelManager player-instantiation contract changed; utility stopped " +
            "before modifying scenes."
        );
    }

    private static SeerPrefabReferences ResolveAuthoritativeSeerPrefab()
    {
        GameObject seerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            SeerPrefabPath
        );
        Require(seerPrefab != null, $"Missing Seer prefab: {SeerPrefabPath}");
        Require(
            PrefabUtility.IsPartOfPrefabAsset(seerPrefab) &&
            string.Equals(
                AssetDatabase.GetAssetPath(seerPrefab),
                SeerPrefabPath,
                StringComparison.Ordinal
            ),
            $"Resolved Seer root is not the authoritative prefab asset: {SeerPrefabPath}"
        );

        Character seerCharacter = seerPrefab.GetComponent<Character>();
        RequireAuthoritativeSeerCharacter(seerCharacter, "Resolved Seer");
        CharacterController seerController = RequireSeerPersistenceContract(
            seerPrefab,
            seerCharacter
        );
        Require(
            seerController != null &&
            PrefabUtility.IsPartOfPrefabAsset(seerController) &&
            string.Equals(
                AssetDatabase.GetAssetPath(seerController),
                SeerPrefabPath,
                StringComparison.Ordinal
            ),
            "Resolved Seer CharacterController is not a live component of the " +
            "authoritative prefab asset."
        );

        return new SeerPrefabReferences
        {
            Prefab = seerPrefab,
            Character = seerCharacter,
            Controller = seerController,
        };
    }

    private static void AssignAuthoritativeSeerPlayerPrefab(
        LevelManager manager,
        Character seerCharacter,
        string label
    )
    {
        RequireAuthoritativeSeerCharacter(seerCharacter, $"{label} assignment");
        manager.PlayerPrefabs = new[] { seerCharacter };
        EditorUtility.SetDirty(manager);
        RequireSeerPlayerPrefab(manager, seerCharacter, label);
    }

    private static void RequireAuthoritativeSeerCharacter(
        Character seerCharacter,
        string label
    )
    {
        Require(
            seerCharacter != null,
            $"{label} Character reference is missing or has been destroyed."
        );
        Require(
            PrefabUtility.IsPartOfPrefabAsset(seerCharacter) &&
            string.Equals(
                AssetDatabase.GetAssetPath(seerCharacter),
                SeerPrefabPath,
                StringComparison.Ordinal
            ),
            $"{label} Character is not a live component of the authoritative " +
            $"Seer prefab asset: {SeerPrefabPath}"
        );
    }

    private static CharacterController RequireSeerPersistenceContract(
        GameObject seerPrefab,
        Character seerCharacter
    )
    {
        PlayerPersistOnce[] persistenceComponents =
            seerPrefab.GetComponentsInChildren<PlayerPersistOnce>(true);
        Require(
            persistenceComponents.Length == 1 &&
            persistenceComponents[0].gameObject == seerCharacter.gameObject,
            "Seer prefab must have exactly one PlayerPersistOnce on the same " +
            "GameObject as Character."
        );

        PersistentPlayerScenePlacement[] placementComponents =
            seerPrefab.GetComponentsInChildren<PersistentPlayerScenePlacement>(true);
        Require(
            placementComponents.Length == 1 &&
            placementComponents[0].gameObject == seerCharacter.gameObject,
            "Seer prefab must have exactly one PersistentPlayerScenePlacement on " +
            "the same GameObject as Character."
        );
        Require(
            seerPrefab.GetComponentsInChildren<CharacterPersistence>(true).Length == 0,
            "Seer prefab has both project-owned and TopDown Engine character " +
            "persistence components; player registration would have two owners."
        );
        Require(
            seerCharacter.CharacterType == Character.CharacterTypes.Player,
            "Seer CharacterType must be Player for LevelManager registration."
        );
        Require(
            seerCharacter.gameObject.CompareTag("Player"),
            "Seer root must use the Player tag required by the scene portals."
        );

        CharacterController controller = seerCharacter.GetComponent<CharacterController>();
        Require(
            controller != null &&
            controller.enabled &&
            controller.radius > 0f &&
            controller.height >= controller.radius * 2f,
            "Seer must have a valid enabled CharacterController for arrival " +
            "clearance validation."
        );
        return controller;
    }

    private static void RequireNoSceneOwnedPersistence(Scene scene)
    {
        Require(
            GetSceneComponents<PlayerPersistOnce>(scene).Length == 0,
            $"{scene.name} contains a scene-owned PlayerPersistOnce."
        );
        Require(
            GetSceneComponents<PersistentInventoryObject>(scene).Length == 0,
            $"{scene.name} contains a scene-owned PersistentInventoryObject."
        );
    }

    private static void RequireNoCompetingPlacement(Scene scene)
    {
        MonoBehaviour[] behaviours = GetSceneComponents<MonoBehaviour>(scene);
        Require(
            behaviours.All(
                behaviour => behaviour == null ||
                             behaviour.GetType().Name != "SpawnResolver"
            ),
            $"{scene.name} contains SpawnResolver; competing placement is forbidden."
        );
    }

    private static void RequireNoSceneCharacters(LevelManager manager, string label)
    {
        Require(
            manager.SceneCharacters == null || manager.SceneCharacters.Count == 0,
            $"{label} LevelManager has scene characters that would override " +
            "PlayerPrefabs and persistent-player registration."
        );
    }

    private static void RequireNoConflictingSpawnPoint(
        Scene scene,
        string spawnName,
        string managedObjectName
    )
    {
        SpawnPoint[] matches = GetSceneComponents<SpawnPoint>(scene)
            .Where(
                spawn => spawn != null && string.Equals(
                    spawn.SpawnName,
                    spawnName,
                    StringComparison.Ordinal
                )
            )
            .ToArray();
        Require(
            matches.Length <= 1 &&
            matches.All(spawn => spawn.gameObject.name == managedObjectName),
            $"{scene.name} already contains a conflicting SpawnPoint named " +
            $"{spawnName}; named arrival would be ambiguous."
        );
    }

    private static void RequireSafePortalAndSpawnPlacement(
        Scene scene,
        Vector3 portalPosition,
        Vector3 spawnPosition,
        CharacterController playerController,
        string label
    )
    {
        Require(playerController != null, $"{label} player controller is missing.");
        RequireFinite(portalPosition, $"{label} portal position");
        RequireFinite(spawnPosition, $"{label} arrival position");

        Vector2 portalXZ = new Vector2(portalPosition.x, portalPosition.z);
        Vector2 spawnXZ = new Vector2(spawnPosition.x, spawnPosition.z);
        Require(
            Vector2.Distance(portalXZ, spawnXZ) >= 4f,
            $"{label} arrival must be at least 4 world units from its return portal."
        );

        Physics.SyncTransforms();
        RequireWalkableSupport(scene, portalPosition, playerController, $"{label} portal");
        RaycastHit arrivalSupport = RequireWalkableSupport(
            scene,
            spawnPosition,
            playerController,
            $"{label} arrival"
        );
        RequirePlayerClearance(
            scene,
            spawnPosition,
            playerController,
            arrivalSupport,
            label
        );
    }

    private static RaycastHit RequireWalkableSupport(
        Scene scene,
        Vector3 position,
        CharacterController playerController,
        string label
    )
    {
        float skinWidth = GetUsableSkinWidth(playerController);
        float controllerBottomOffset =
            playerController.center.y - playerController.height * 0.5f;
        float allowedDrop = Mathf.Max(0.5f, playerController.stepOffset + 0.2f);
        float minimumSupportY =
            position.y + controllerBottomOffset - allowedDrop;
        float maximumSupportY =
            position.y + controllerBottomOffset + skinWidth +
            Physics.defaultContactOffset;
        Vector3 origin = new Vector3(
            position.x,
            maximumSupportY + 0.5f,
            position.z
        );
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            origin.y - minimumSupportY,
            ~0,
            QueryTriggerInteraction.Ignore
        );
        RaycastHit[] walkableHits = hits
            .Where(
                hit => hit.collider != null &&
                       hit.collider.gameObject.scene.handle == scene.handle &&
                       hit.point.y >= minimumSupportY &&
                       hit.point.y <= maximumSupportY &&
                       Vector3.Angle(hit.normal, Vector3.up) <=
                           playerController.slopeLimit + 0.1f
            )
            .OrderByDescending(hit => hit.point.y)
            .ToArray();
        Require(
            walkableHits.Length > 0,
            $"{label} has no scene-owned walkable support between Y=" +
            $"{minimumSupportY:F3} and Y={maximumSupportY:F3}."
        );
        return walkableHits[0];
    }

    private static void RequirePlayerClearance(
        Scene scene,
        Vector3 spawnPosition,
        CharacterController playerController,
        RaycastHit support,
        string label
    )
    {
        float skinWidth = GetUsableSkinWidth(playerController);
        float radius = playerController.radius - skinWidth;
        float height = Mathf.Max(
            playerController.height - skinWidth * 2f,
            radius * 2f
        );
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 center = spawnPosition + playerController.center;
        Vector3 bottom = center - Vector3.up * halfSegment;
        Vector3 top = center + Vector3.up * halfSegment;

        Collider[] blockers = Physics.OverlapCapsule(
                bottom,
                top,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore
            )
            .Where(
                collider => collider != null &&
                            collider.gameObject.scene.handle == scene.handle
            )
            .ToArray();

        Require(
            blockers.Length == 0,
            $"{label} arrival's skin-inset standing volume is obstructed. " +
            $"Supporting collider: {GetHierarchyPath(support.collider.transform)}. " +
            $"Blocking collider(s): {string.Join(", ", blockers.Select(blocker => GetHierarchyPath(blocker.transform)))}. " +
            $"Controller radius={playerController.radius:F3}, " +
            $"height={playerController.height:F3}, skin={skinWidth:F3}."
        );
    }

    private static float GetUsableSkinWidth(CharacterController playerController)
    {
        float maximumSkin = Mathf.Min(
            playerController.radius - 0.001f,
            playerController.height * 0.5f - 0.001f
        );
        return Mathf.Clamp(playerController.skinWidth, 0f, maximumSkin);
    }

    private static void RequireFinite(Vector3 value, string label)
    {
        Require(
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z),
            $"{label} contains a non-finite coordinate."
        );
    }

    private static void RequireValidInitialSpawn(LevelManager manager, Scene scene)
    {
        Require(
            manager.InitialSpawnPoint != null &&
            manager.InitialSpawnPoint.gameObject.scene.handle == scene.handle,
            $"{scene.name} LevelManager has no valid scene-owned InitialSpawnPoint."
        );
    }

    private static void RequireSeerPlayerPrefab(
        LevelManager manager,
        Character seerCharacter,
        string label
    )
    {
        Require(
            manager.PlayerPrefabs != null &&
            manager.PlayerPrefabs.Length == 1,
            $"{label} LevelManager does not reference exactly one approved Seer prefab."
        );
        Character assignedCharacter = manager.PlayerPrefabs[0];
        RequireAuthoritativeSeerCharacter(
            assignedCharacter,
            $"{label} LevelManager.PlayerPrefabs[0]"
        );
        RequireAuthoritativeSeerCharacter(
            seerCharacter,
            $"{label} expected Seer"
        );
        Require(
            assignedCharacter == seerCharacter,
            $"{label} LevelManager references a different Character asset."
        );
        Require(
            manager.SceneCharacters == null || manager.SceneCharacters.Count == 0,
            $"{label} LevelManager has scene characters that would override PlayerPrefabs."
        );
    }

    private static void RequirePortalValues(
        ScenePortal portal,
        string expectedScene,
        string expectedSpawn
    )
    {
        SerializedObject serializedPortal = new SerializedObject(portal);
        string actualScene = RequireProperty(serializedPortal, "targetScene").stringValue;
        string actualSpawn = RequireProperty(
            serializedPortal,
            "targetSpawnPointName"
        ).stringValue;

        Require(
            string.Equals(actualScene, expectedScene, StringComparison.Ordinal) &&
            string.Equals(actualSpawn, expectedSpawn, StringComparison.Ordinal),
            $"Portal {portal.name} expected {expectedScene}/{expectedSpawn}, " +
            $"found {actualScene}/{actualSpawn}."
        );
    }

    private static void RequireTrigger(GameObject portal)
    {
        BoxCollider collider = portal.GetComponent<BoxCollider>();
        Require(
            collider != null && collider.enabled && collider.isTrigger,
            $"Portal {portal.name} has no enabled trigger BoxCollider."
        );
    }

    private static void RequireNoMissingScripts(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                Require(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject
                    ) == 0,
                    $"Missing script found on {scene.name}/{GetHierarchyPath(transform)}."
                );
            }
        }
    }

    private static void RequireEditorReady()
    {
        Require(
            !EditorApplication.isPlayingOrWillChangePlaymode,
            "Stop Play Mode before applying Phase 2B."
        );
        Require(!EditorApplication.isCompiling, "Unity is compiling; wait and try again.");
        Require(!EditorApplication.isUpdating, "Unity is importing; wait and try again.");
    }

    private static void RequireAllLoadedScenesClean()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Require(
                !scene.isDirty,
                $"Open scene {scene.name} has unsaved changes. Save or discard them " +
                "manually before invoking Phase 2B."
            );
        }
    }

    private static void RequireScene(Scene scene, string expectedName)
    {
        Require(scene.IsValid() && scene.isLoaded, $"Scene {expectedName} did not load.");
        Require(
            string.Equals(scene.name, expectedName, StringComparison.Ordinal),
            $"Expected scene {expectedName}, opened {scene.name}."
        );
    }

    private static T RequireSingleComponent<T>(Scene scene) where T : Component
    {
        T[] matches = GetSceneComponents<T>(scene);
        Require(
            matches.Length == 1,
            $"Expected one {typeof(T).Name} in {scene.name}, found {matches.Length}."
        );
        return matches[0];
    }

    private static GameObject RequireSingleGameObject(Scene scene, string objectName)
    {
        GameObject[] matches = GetSceneGameObjects(scene)
            .Where(gameObject => gameObject.name == objectName)
            .ToArray();
        Require(
            matches.Length == 1,
            $"Expected one object named {objectName} in {scene.name}, found {matches.Length}."
        );
        return matches[0];
    }

    private static GameObject RequireSinglePrefabRoot(Scene scene, string prefabPath)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .Where(
                root => string.Equals(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
                    prefabPath,
                    StringComparison.Ordinal
                )
            )
            .ToArray();
        Require(
            matches.Length == 1,
            $"Expected one root instance of {prefabPath} in {scene.name}, " +
            $"found {matches.Length}."
        );
        return matches[0];
    }

    private static T[] GetSceneComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static GameObject[] GetSceneGameObjects(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .ToArray();
    }

    private static int CountActiveComponents<T>(Scene scene) where T : Component
    {
        return GetSceneComponents<T>(scene)
            .Count(component => component != null && component.gameObject.activeInHierarchy);
    }

    private static void LogPresentationInventory(
        Scene scene,
        string label,
        StringBuilder report
    )
    {
        Append(
            report,
            $"{label}: active Cameras={CountActiveComponents<Camera>(scene)}, " +
            $"AudioListeners={CountActiveComponents<AudioListener>(scene)}, " +
            $"Canvases={CountActiveComponents<Canvas>(scene)}, " +
            $"EventSystems={CountActiveComponents<EventSystem>(scene)}, " +
            $"Characters={CountActiveComponents<Character>(scene)}, " +
            $"PersistentInventoryObjects={CountActiveComponents<PersistentInventoryObject>(scene)}."
        );
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Require(
            property != null,
            $"{serializedObject.targetObject.GetType().Name}.{propertyName} was not found."
        );
        return property;
    }

    private static void SetString(
        SerializedObject serializedObject,
        string propertyName,
        string value
    )
    {
        RequireProperty(serializedObject, propertyName).stringValue = value;
    }

    private static void SetBool(
        SerializedObject serializedObject,
        string propertyName,
        bool value
    )
    {
        RequireProperty(serializedObject, propertyName).boolValue = value;
    }

    private static void SetFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value
    )
    {
        RequireProperty(serializedObject, propertyName).floatValue = value;
    }

    private static bool ContainsXZ(Bounds bounds, Vector3 position)
    {
        return position.x >= bounds.min.x && position.x <= bounds.max.x &&
               position.z >= bounds.min.z && position.z <= bounds.max.z;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        List<string> names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static string Format(Vector3 vector)
    {
        return $"({vector.x:F2}, {vector.y:F2}, {vector.z:F2})";
    }

    private static void AppendPartialSaveAndRecoveryGuidance(
        ApplyProgress progress,
        StringBuilder report
    )
    {
        Append(report, "DURABLE-CHANGE STATUS:");
        Append(
            report,
            $"- CITYSCAPE save attempted={progress.CityscapeSaveAttempted}, " +
            $"confirmed saved={progress.CityscapeSaved}."
        );
        Append(
            report,
            $"- Museum save attempted={progress.MuseumSaveAttempted}, " +
            $"confirmed saved={progress.MuseumSaved}."
        );
        string buildSettingsStatus = !progress.BuildSettingsAttempted
            ? "not attempted"
            : progress.BuildSettingsVerified
                ? $"verified; changed={progress.BuildSettingsChanged}"
                : "attempted but not verified; it may have changed";
        Append(report, $"- Build settings update: {buildSettingsStatus}.");

        if (progress.CityscapeSaved != progress.MuseumSaved)
        {
            Append(
                report,
                "PARTIAL SAVE CONFIRMED: exactly one target scene was saved. " +
                "Do not save the other dirty scene while deciding whether to recover."
            );
        }
        else if (!progress.CityscapeSaved && !progress.MuseumSaved)
        {
            Append(
                report,
                "No target scene save was confirmed. In-memory scenes may still be " +
                "dirty; close or reopen them without saving to return to disk state."
            );
        }
        else
        {
            Append(report, "Both target scene saves were confirmed.");
        }

        if (progress.RecoveryVerified)
        {
            Append(
                report,
                "RECOVERY: first close affected scenes without saving. To undo any " +
                "confirmed or uncertain durable changes, copy the verified files from " +
                $"{ApplyRecoveryFolder} back to:"
            );
            Append(report, $"- CITYSCAPE.unity -> {CityscapeScenePath}");
            Append(report, $"- CITYSCAPE.unity.meta -> {CityscapeScenePath}.meta");
            Append(
                report,
                $"- MuseumHub_WindWard.unity -> {MuseumScenePath}"
            );
            Append(
                report,
                $"- MuseumHub_WindWard.unity.meta -> {MuseumScenePath}.meta"
            );
            Append(
                report,
                "- EditorBuildSettings.asset -> " +
                "ProjectSettings/EditorBuildSettings.asset (only if the build-settings " +
                "update was attempted)."
            );
            Append(
                report,
                $"Verify {ApplyRecoveryFolder}/{RecoveryManifestName} before copying."
            );
        }
        else
        {
            Append(
                report,
                "Apply-time recovery was not verified; do not rely on it for rollback."
            );
        }
    }

    private static void TryRestoreOriginalSceneSetup(
        SceneSetup[] originalSetup,
        StringBuilder report
    )
    {
        try
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            Append(report, "Restored the original open-scene setup after preflight failure.");
        }
        catch (Exception restoreException)
        {
            Append(report, $"Could not restore original scene setup: {restoreException.Message}");
        }
    }

    private static void WriteReport(StringBuilder report)
    {
        string fullPath = Path.Combine(ProjectRoot, ReportRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, report.ToString());
    }

    private static string ProjectRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static void Append(StringBuilder report, string message)
    {
        report.AppendLine(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CityscapePlan
    {
        public Scene Scene;
        public LevelManager LevelManager;
        public Collider GroundCollider;
        public GameObject InheritedCameraRig;
    }

    private sealed class MuseumPlan
    {
        public Scene Scene;
        public LevelManager LevelManager;
        public Transform Doorway;
        public Transform PortalsParent;
        public ScenePortal MazePortal;
    }

    private sealed class SeerPrefabReferences
    {
        public GameObject Prefab;
        public Character Character;
        public CharacterController Controller;
    }

    private sealed class ApplyProgress
    {
        public bool RecoveryVerified;
        public bool ChangesStarted;
        public bool CityscapeSaveAttempted;
        public bool CityscapeSaved;
        public bool MuseumSaveAttempted;
        public bool MuseumSaved;
        public bool BuildSettingsAttempted;
        public bool BuildSettingsVerified;
        public bool BuildSettingsChanged;
    }
}
#endif
