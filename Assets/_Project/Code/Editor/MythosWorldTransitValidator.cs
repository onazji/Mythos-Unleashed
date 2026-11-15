using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class MythosWorldTransitValidator
{
    private const string MenuPath = "Mythos/Validation/Audit World Transit";

    private static readonly string[] GameplaySceneNames =
    {
        "MuseumHub_WindWard",
        "CITYSCAPE",
        "WindWard"
    };

    [MenuItem(MenuPath)]
    private static void AuditWorldTransit()
    {
        List<Finding> findings = new List<Finding>();
        Dictionary<string, SceneSnapshot> snapshots =
            new Dictionary<string, SceneSnapshot>(StringComparer.Ordinal);
        SceneSetup[] setupBefore = EditorSceneManager.GetSceneManagerSetup();
        Dictionary<Scene, bool> dirtyBefore = CaptureDirtyState();

        try
        {
            Dictionary<string, List<string>> enabledScenes = GetEnabledBuildScenes();

            foreach (string sceneName in GameplaySceneNames)
            {
                if (!enabledScenes.TryGetValue(sceneName, out List<string> paths))
                {
                    Add(findings, Severity.Fail, "STATIC",
                        $"Registered gameplay scene '{sceneName}' is not enabled in Build Settings.");
                    continue;
                }

                if (paths.Count != 1)
                {
                    Add(findings, Severity.Fail, "STATIC",
                        $"Enabled Build Settings contain {paths.Count} scenes named '{sceneName}'; portal names are ambiguous.");
                    continue;
                }

                InspectScene(paths[0], findings, snapshots);
            }

            ValidatePortalLinks(findings, snapshots, enabledScenes);
            ValidateCrossScenePersistence(findings, snapshots);
            Add(findings, Severity.Warning, "RUNTIME ONLY",
                "Static inspection cannot prove player persistence order, async loading/fades, controller clearance, or a complete round trip. Playtest each route in both directions.");
        }
        catch (Exception exception)
        {
            Add(findings, Severity.Fail, "STATIC",
                $"Audit stopped on an unexpected exception: {exception.GetType().Name}: {exception.Message}");
        }

        bool setupUnchanged = SceneSetupMatches(setupBefore, EditorSceneManager.GetSceneManagerSetup());
        bool dirtyUnchanged = DirtyStateMatches(dirtyBefore);
        Add(findings, setupUnchanged && dirtyUnchanged ? Severity.Pass : Severity.Fail, "READ ONLY",
            setupUnchanged && dirtyUnchanged
                ? "Editor scene setup and pre-existing dirty flags were unchanged; no scene or project settings were saved."
                : "Editor scene setup or a pre-existing dirty flag changed during inspection.");

        Emit(findings);
    }

    [MenuItem(MenuPath, true)]
    private static bool CanAuditWorldTransit()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating;
    }

    private static void InspectScene(
        string path,
        List<Finding> findings,
        Dictionary<string, SceneSnapshot> snapshots)
    {
        Scene scene = FindLoadedScene(path);
        bool closePreview = false;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenPreviewScene(path);
                closePreview = true;
            }

            SceneSnapshot snapshot = CaptureScene(scene, findings);
            snapshots.Add(snapshot.Name, snapshot);
            Add(findings, Severity.Pass, "STATIC",
                $"{snapshot.Name}: inspected {snapshot.Portals.Count} portal(s), {snapshot.SpawnPoints.Count} SpawnPoint(s), and {snapshot.LevelManagerCount} LevelManager(s)." +
                (closePreview ? " Preview scene closed without saving." : " Used the currently loaded scene, including unsaved changes."));
        }
        finally
        {
            if (closePreview && scene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }

    private static SceneSnapshot CaptureScene(Scene scene, List<Finding> findings)
    {
        SceneSnapshot snapshot = new SceneSnapshot
        {
            Name = scene.name
        };

        ScenePortal[] portals = GetSceneComponents<ScenePortal>(scene);
        SpawnPoint[] spawnPoints = GetSceneComponents<SpawnPoint>(scene);
        SpawnResolver[] resolvers = GetSceneComponents<SpawnResolver>(scene);
        LevelManager[] levelManagers = GetSceneComponents<LevelManager>(scene);

        snapshot.LevelManagerCount = levelManagers.Length;
        snapshot.PlayerPersistCount = GetSceneComponents<PlayerPersistOnce>(scene).Length;
        snapshot.PlayerPlacementCount = GetSceneComponents<PersistentPlayerScenePlacement>(scene).Length;
        PersistentInventoryObject[] inventoryPersistence =
            GetSceneComponents<PersistentInventoryObject>(scene);
        snapshot.InventoryPersistenceCount = inventoryPersistence.Length;
        snapshot.GameStateCount = GetSceneComponents<GameState>(scene).Length;
        snapshot.SceneTransitCount = GetSceneComponents<SceneTransit>(scene).Length;
        snapshot.SceneLoaderCount = GetSceneComponents<Mythos.Unleashed.Runtime.SceneFlow.SceneLoader>(scene).Length;

        foreach (SpawnPoint spawnPoint in spawnPoints)
        {
            snapshot.SpawnPoints.Add(new SpawnSnapshot
            {
                Name = spawnPoint.SpawnName ?? string.Empty,
                ObjectPath = GetObjectPath(spawnPoint.transform),
                Active = spawnPoint.gameObject.activeInHierarchy && spawnPoint.enabled,
                Position = spawnPoint.transform.position
            });
        }

        foreach (ScenePortal portal in portals)
        {
            SerializedObject serializedPortal = new SerializedObject(portal);
            SerializedProperty targetScene = serializedPortal.FindProperty("targetScene");
            SerializedProperty targetSpawn = serializedPortal.FindProperty("targetSpawnPointName");
            PortalSnapshot portalSnapshot = new PortalSnapshot
            {
                ObjectPath = GetObjectPath(portal.transform),
                Active = portal.gameObject.activeInHierarchy && portal.enabled,
                TargetScene = targetScene == null ? string.Empty : targetScene.stringValue,
                TargetSpawn = targetSpawn == null ? string.Empty : targetSpawn.stringValue
            };

            Collider[] colliders = portal.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (collider.enabled && collider.gameObject.activeInHierarchy && collider.isTrigger)
                {
                    portalSnapshot.TriggerBounds.Add(collider.bounds);
                }
            }

            if (portalSnapshot.Active && portalSnapshot.TriggerBounds.Count == 0)
            {
                Add(findings, Severity.Fail, "STATIC",
                    $"{scene.name}/{portalSnapshot.ObjectPath}: active ScenePortal has no active trigger Collider.");
            }

            snapshot.Portals.Add(portalSnapshot);
        }

        ValidateSpawnPoints(scene.name, snapshot.SpawnPoints, findings);
        ValidateInventoryPersistence(scene.name, inventoryPersistence, findings);
        ValidateSceneOwnership(scene.name, snapshot, levelManagers, resolvers, findings);

        if (levelManagers.Length == 1)
        {
            LevelManager manager = levelManagers[0];
            snapshot.HasValidInitialSpawn =
                manager.InitialSpawnPoint != null &&
                manager.InitialSpawnPoint.gameObject.scene == scene;
            snapshot.InitialSpawnPosition = snapshot.HasValidInitialSpawn
                ? manager.InitialSpawnPoint.transform.position
                : Vector3.zero;
            snapshot.HasPersistentPlacementPrefab = manager.PlayerPrefabs != null &&
                manager.PlayerPrefabs.Any(character =>
                    character != null &&
                    character.GetComponent<PlayerPersistOnce>() != null &&
                    character.GetComponent<PersistentPlayerScenePlacement>() != null);

            if (!snapshot.HasValidInitialSpawn)
            {
                Add(findings, Severity.Fail, "STATIC",
                    $"{scene.name}: LevelManager has no valid scene-owned InitialSpawnPoint.");
            }

            if (manager.SceneCharacters != null && manager.SceneCharacters.Count > 0)
            {
                Add(findings, Severity.Warning, "ARCHITECTURE",
                    $"{scene.name}: LevelManager has {manager.SceneCharacters.Count} SceneCharacter(s); these override PlayerPrefabs and can compete with persistent-player registration.");
            }
        }

        foreach (SpawnResolver resolver in resolvers)
        {
            SerializedProperty fallback = new SerializedObject(resolver).FindProperty("fallbackSpawnName");
            snapshot.ResolverFallbackNames.Add(fallback == null ? "Default" : fallback.stringValue);
        }

        if (portals.Length == 0)
        {
            Add(findings, Severity.Warning, "STATIC",
                $"{scene.name}: no ScenePortal component was found; outbound travel cannot be verified in this scene.");
        }

        return snapshot;
    }

    private static void ValidatePortalLinks(
        List<Finding> findings,
        Dictionary<string, SceneSnapshot> snapshots,
        Dictionary<string, List<string>> enabledScenes)
    {
        foreach (SceneSnapshot source in snapshots.Values.OrderBy(snapshot => snapshot.Name))
        {
            foreach (PortalSnapshot portal in source.Portals.OrderBy(portal => portal.ObjectPath))
            {
                string label = $"{source.Name}/{portal.ObjectPath}";
                if (string.IsNullOrWhiteSpace(portal.TargetScene))
                {
                    Add(findings, Severity.Fail, "STATIC", $"{label}: destination scene is empty.");
                    continue;
                }

                if (!enabledScenes.TryGetValue(portal.TargetScene, out List<string> targetPaths) || targetPaths.Count != 1)
                {
                    Add(findings, Severity.Fail, "STATIC",
                        $"{label}: destination '{portal.TargetScene}' does not resolve to exactly one enabled Build Settings scene.");
                    continue;
                }

                Add(findings, Severity.Pass, "STATIC",
                    $"{label}: destination '{portal.TargetScene}' is enabled in Build Settings.");

                if (!snapshots.TryGetValue(portal.TargetScene, out SceneSnapshot destination))
                {
                    Add(findings, Severity.Warning, "STATIC",
                        $"{label}: destination is enabled but outside this validator's registered gameplay scenes; SpawnPoint contract was not inspected.");
                    continue;
                }

                List<SpawnSnapshot> matches = destination.SpawnPoints
                    .Where(spawn => spawn.Active && string.Equals(spawn.Name, portal.TargetSpawn, StringComparison.Ordinal))
                    .ToList();

                if (!string.IsNullOrEmpty(portal.TargetSpawn) && matches.Count == 1)
                {
                    Add(findings, Severity.Pass, "STATIC",
                        $"{label}: requested SpawnPoint '{portal.TargetSpawn}' exists once and is active in {destination.Name}.");
                    ValidateArrivalAgainstTriggers(findings, label, destination, matches[0].Position, portal.TargetSpawn);
                    continue;
                }

                if (matches.Count > 1)
                {
                    Add(findings, Severity.Fail, "STATIC",
                        $"{label}: requested SpawnPoint '{portal.TargetSpawn}' is ambiguous in {destination.Name}.");
                    continue;
                }

                string fallback = DescribeFallback(destination);
                if (fallback != null)
                {
                    Add(findings, Severity.Warning, "KNOWN FALLBACK",
                        $"{label}: requested SpawnPoint '{DisplayName(portal.TargetSpawn)}' is not active in {destination.Name}; {fallback}");
                    Vector3 fallbackPosition = GetFallbackPosition(destination);
                    ValidateArrivalAgainstTriggers(findings, label, destination, fallbackPosition, "fallback arrival");
                }
                else
                {
                    Add(findings, Severity.Fail, "STATIC",
                        $"{label}: requested SpawnPoint '{DisplayName(portal.TargetSpawn)}' is not active in {destination.Name}, and no configured fallback could be confirmed.");
                }
            }
        }
    }

    private static void ValidateArrivalAgainstTriggers(
        List<Finding> findings,
        string sourceLabel,
        SceneSnapshot destination,
        Vector3 arrivalPosition,
        string arrivalName)
    {
        foreach (PortalSnapshot destinationPortal in destination.Portals)
        {
            if (!destinationPortal.Active)
            {
                continue;
            }

            foreach (Bounds triggerBounds in destinationPortal.TriggerBounds)
            {
                if (triggerBounds.Contains(arrivalPosition))
                {
                    Add(findings, Severity.Warning, "STATIC",
                        $"{sourceLabel}: {destination.Name} {arrivalName} lies inside the trigger bounds on '{destinationPortal.ObjectPath}' and may immediately retrigger travel.");
                    return;
                }
            }
        }

        Add(findings, Severity.Pass, "STATIC",
            $"{sourceLabel}: {destination.Name} {arrivalName} is outside all active portal trigger bounds.");
    }

    private static void ValidateSpawnPoints(
        string sceneName,
        List<SpawnSnapshot> spawnPoints,
        List<Finding> findings)
    {
        foreach (SpawnSnapshot spawn in spawnPoints.Where(spawn => string.IsNullOrWhiteSpace(spawn.Name)))
        {
            Add(findings, Severity.Warning, "STATIC",
                $"{sceneName}/{spawn.ObjectPath}: SpawnPoint has an empty name and cannot satisfy a named portal request.");
        }

        foreach (IGrouping<string, SpawnSnapshot> group in spawnPoints
                     .Where(spawn => !string.IsNullOrWhiteSpace(spawn.Name))
                     .GroupBy(spawn => spawn.Name, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            int activeCount = group.Count(spawn => spawn.Active);
            Add(findings, activeCount > 1 ? Severity.Fail : Severity.Warning, "STATIC",
                $"{sceneName}: SpawnPoint name '{group.Key}' occurs {group.Count()} times ({activeCount} active); named arrival must remain unambiguous.");
        }
    }

    private static void ValidateSceneOwnership(
        string sceneName,
        SceneSnapshot snapshot,
        LevelManager[] levelManagers,
        SpawnResolver[] resolvers,
        List<Finding> findings)
    {
        if (levelManagers.Length != 1)
        {
            Add(findings, Severity.Fail, "ARCHITECTURE",
                $"{sceneName}: expected one TopDown Engine LevelManager, found {levelManagers.Length}.");
        }

        ReportSceneOwned(findings, sceneName, "PlayerPersistOnce", snapshot.PlayerPersistCount, true);
        ReportSceneOwned(findings, sceneName, "PersistentPlayerScenePlacement", snapshot.PlayerPlacementCount, true);
        if (snapshot.InventoryPersistenceCount > 0)
        {
            Add(findings, Severity.Warning, "ARCHITECTURE",
                $"{sceneName}: contains {snapshot.InventoryPersistenceCount} scene-owned PersistentInventoryObject component(s); their ownership is scene-local even though the objects survive scene loads.");
        }
        ReportSceneOwned(findings, sceneName, "GameState", snapshot.GameStateCount, true);
        ReportSceneOwned(findings, sceneName, "SceneTransit", snapshot.SceneTransitCount, true);
        ReportSceneOwned(findings, sceneName, "SceneLoader", snapshot.SceneLoaderCount, true);

        if (resolvers.Length > 0)
        {
            Add(findings, Severity.Warning, "ARCHITECTURE",
                $"{sceneName}: contains {resolvers.Length} legacy SpawnResolver component(s). It shares SceneTransit.PlacedThisScene with PersistentPlayerScenePlacement, so placement ownership/order requires runtime verification.");
        }
    }

    private static void ValidateInventoryPersistence(
        string sceneName,
        PersistentInventoryObject[] components,
        List<Finding> findings)
    {
        var entries = components.Select(component =>
        {
            SerializedProperty property = new SerializedObject(component).FindProperty("persistenceId");
            string id = property == null ? string.Empty : property.stringValue;
            return new
            {
                Id = string.IsNullOrWhiteSpace(id) ? component.gameObject.name : id,
                Path = GetObjectPath(component.transform)
            };
        });

        foreach (var duplicate in entries.GroupBy(entry => entry.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            Add(findings, Severity.Fail, "ARCHITECTURE",
                $"{sceneName}: PersistentInventoryObject ID '{duplicate.Key}' is duplicated at {string.Join(", ", duplicate.Select(entry => entry.Path))}.");
        }
    }

    private static void ValidateCrossScenePersistence(
        List<Finding> findings,
        Dictionary<string, SceneSnapshot> snapshots)
    {
        ValidateCrossSceneCount(findings, snapshots, "PlayerPersistOnce", snapshot => snapshot.PlayerPersistCount);
        ValidateCrossSceneCount(findings, snapshots, "PersistentPlayerScenePlacement", snapshot => snapshot.PlayerPlacementCount);
        ValidateCrossSceneCount(findings, snapshots, "GameState", snapshot => snapshot.GameStateCount);
        ValidateCrossSceneCount(findings, snapshots, "SceneTransit", snapshot => snapshot.SceneTransitCount);
        ValidateCrossSceneCount(findings, snapshots, "SceneLoader", snapshot => snapshot.SceneLoaderCount);
    }

    private static void ValidateCrossSceneCount(
        List<Finding> findings,
        Dictionary<string, SceneSnapshot> snapshots,
        string componentName,
        Func<SceneSnapshot, int> count)
    {
        string[] owners = snapshots.Values.Where(snapshot => count(snapshot) > 0)
            .Select(snapshot => snapshot.Name).OrderBy(name => name).ToArray();
        if (owners.Length > 1)
        {
            Add(findings, Severity.Fail, "ARCHITECTURE",
                $"Scene-owned {componentName} appears in multiple gameplay scenes ({string.Join(", ", owners)}); loading between them creates an obvious duplicate-persistence configuration.");
        }
    }

    private static void ReportSceneOwned(
        List<Finding> findings,
        string sceneName,
        string componentName,
        int count,
        bool persistent)
    {
        if (count == 0)
        {
            return;
        }

        Severity severity = count > 1 ? Severity.Fail : Severity.Warning;
        Add(findings, severity, "ARCHITECTURE",
            $"{sceneName}: contains {count} scene-owned {componentName} component(s)" +
            (persistent ? "; confirm ownership belongs to the bootstrap/player prefab rather than each gameplay scene." : "."));
    }

    private static string DescribeFallback(SceneSnapshot destination)
    {
        foreach (string fallbackName in destination.ResolverFallbackNames)
        {
            int matches = destination.SpawnPoints.Count(spawn =>
                spawn.Active && string.Equals(spawn.Name, fallbackName, StringComparison.Ordinal));
            if (matches == 1)
            {
                return $"legacy SpawnResolver will use its '{fallbackName}' SpawnPoint fallback. This is runtime order-dependent.";
            }
        }

        if (destination.HasValidInitialSpawn && destination.HasPersistentPlacementPrefab)
        {
            return "PersistentPlayerScenePlacement will use LevelManager.InitialSpawnPoint as its documented fallback.";
        }

        if (destination.HasValidInitialSpawn)
        {
            return "LevelManager.InitialSpawnPoint exists, but whether the arriving persistent player uses it is runtime-only.";
        }

        return null;
    }

    private static Vector3 GetFallbackPosition(SceneSnapshot destination)
    {
        foreach (string fallbackName in destination.ResolverFallbackNames)
        {
            SpawnSnapshot spawn = destination.SpawnPoints.FirstOrDefault(candidate =>
                candidate.Active && string.Equals(candidate.Name, fallbackName, StringComparison.Ordinal));
            if (spawn != null)
            {
                return spawn.Position;
            }
        }

        return destination.InitialSpawnPosition;
    }

    private static Dictionary<string, List<string>> GetEnabledBuildScenes()
    {
        Dictionary<string, List<string>> result =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes.Where(scene => scene.enabled))
        {
            string name = Path.GetFileNameWithoutExtension(scene.path);
            if (!result.TryGetValue(name, out List<string> paths))
            {
                paths = new List<string>();
                result.Add(name, paths);
            }

            paths.Add(scene.path);
        }

        return result;
    }

    private static Scene FindLoadedScene(string path)
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded && string.Equals(scene.path, path, StringComparison.Ordinal))
            {
                return scene;
            }
        }

        return default;
    }

    private static T[] GetSceneComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .Where(component => component != null && component.gameObject.scene == scene)
            .ToArray();
    }

    private static string GetObjectPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        while (transform != null)
        {
            names.Push(transform.name);
            transform = transform.parent;
        }

        return string.Join("/", names);
    }

    private static Dictionary<Scene, bool> CaptureDirtyState()
    {
        Dictionary<Scene, bool> state = new Dictionary<Scene, bool>();
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.IsValid())
            {
                state[scene] = scene.isDirty;
            }
        }

        return state;
    }

    private static bool DirtyStateMatches(Dictionary<Scene, bool> before)
    {
        Dictionary<Scene, bool> after = CaptureDirtyState();
        return before.Count == after.Count && before.All(pair =>
            after.TryGetValue(pair.Key, out bool dirty) && dirty == pair.Value);
    }

    private static bool SceneSetupMatches(SceneSetup[] before, SceneSetup[] after)
    {
        if (before.Length != after.Length)
        {
            return false;
        }

        for (int index = 0; index < before.Length; index++)
        {
            if (!string.Equals(before[index].path, after[index].path, StringComparison.Ordinal) ||
                before[index].isLoaded != after[index].isLoaded ||
                before[index].isActive != after[index].isActive)
            {
                return false;
            }
        }

        return true;
    }

    private static string DisplayName(string value)
    {
        return string.IsNullOrEmpty(value) ? "<empty>" : value;
    }

    private static void Add(List<Finding> findings, Severity severity, string category, string message)
    {
        findings.Add(new Finding(severity, category, message));
    }

    private static void Emit(List<Finding> findings)
    {
        int pass = findings.Count(finding => finding.Severity == Severity.Pass);
        int warning = findings.Count(finding => finding.Severity == Severity.Warning);
        int fail = findings.Count(finding => finding.Severity == Severity.Fail);

        Debug.Log($"[Mythos World Transit Audit] START — registered scenes: {string.Join(", ", GameplaySceneNames)}");
        foreach (Finding finding in findings)
        {
            string line = $"[{finding.Severity.ToString().ToUpperInvariant()}] [{finding.Category}] {finding.Message}";
            if (finding.Severity == Severity.Fail)
            {
                Debug.LogError(line);
            }
            else if (finding.Severity == Severity.Warning)
            {
                Debug.LogWarning(line);
            }
            else
            {
                Debug.Log(line);
            }
        }

        string summary = $"[Mythos World Transit Audit] COMPLETE — PASS {pass}, WARNING {warning}, FAIL {fail}.";
        if (fail > 0)
        {
            Debug.LogError(summary);
        }
        else if (warning > 0)
        {
            Debug.LogWarning(summary);
        }
        else
        {
            Debug.Log(summary);
        }
    }

    private enum Severity
    {
        Pass,
        Warning,
        Fail
    }

    private sealed class Finding
    {
        public Finding(Severity severity, string category, string message)
        {
            Severity = severity;
            Category = category;
            Message = message;
        }

        public Severity Severity { get; }
        public string Category { get; }
        public string Message { get; }
    }

    private sealed class SceneSnapshot
    {
        public string Name;
        public int LevelManagerCount;
        public int PlayerPersistCount;
        public int PlayerPlacementCount;
        public int InventoryPersistenceCount;
        public int GameStateCount;
        public int SceneTransitCount;
        public int SceneLoaderCount;
        public bool HasValidInitialSpawn;
        public bool HasPersistentPlacementPrefab;
        public Vector3 InitialSpawnPosition;
        public readonly List<PortalSnapshot> Portals = new List<PortalSnapshot>();
        public readonly List<SpawnSnapshot> SpawnPoints = new List<SpawnSnapshot>();
        public readonly List<string> ResolverFallbackNames = new List<string>();
    }

    private sealed class PortalSnapshot
    {
        public string ObjectPath;
        public string TargetScene;
        public string TargetSpawn;
        public bool Active;
        public readonly List<Bounds> TriggerBounds = new List<Bounds>();
    }

    private sealed class SpawnSnapshot
    {
        public string Name;
        public string ObjectPath;
        public bool Active;
        public Vector3 Position;
    }

}
