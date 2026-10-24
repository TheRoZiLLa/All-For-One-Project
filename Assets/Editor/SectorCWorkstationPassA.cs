using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repeatable, scoped Stage A layout pass for the existing Sector C workstation ring.
/// Menu: Tools/PROLOGUE/Run Sector C Workstation Pass A
/// </summary>
[InitializeOnLoad]
public static class SectorCWorkstationPassA
{
    private const string ScenePath = "Assets/Scenes/PROLOGUE.unity";
    private const string BackupFileName = "PROLOGUE_SectorC_WorkstationPassA_Backup.json";
    private const string ParkedParentName = "[Parked_SectorC]";
    private const string WorkstationsParentName = "Workstations";
    private const string LegacyParentName = "SectorC_Workstations";
    private const float RingRadius = 9.5f;
    private const float ChairOffset = 1.2f;
    private const float ChairFootprintRadius = 0.5f;
    private const float RoomCenterZ = 59f;
    private const float RoomHalfExtent = 20f;
    private const float MinimumCoreClearance = 2f;
    private const string TriggerFileName = "SectorC_PassA.trigger";

    static SectorCWorkstationPassA()
    {
        EditorApplication.delayCall += RunIfTriggered;
    }

    private static void RunIfTriggered()
    {
        string triggerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", TriggerFileName);
        if (!File.Exists(triggerPath))
            return;
        File.Delete(triggerPath);
        try
        {
            RunPassA();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    [Serializable]
    private class TransformRecord
    {
        public string name;
        public string originalParent;
        public float[] worldPosition;
        public float[] worldRotation;
        public float[] localScale;
        public bool activeSelf;
        public int siblingIndex;
    }

    [Serializable]
    private class BackupFile
    {
        public string scene;
        public string note;
        public TransformRecord[] objects;
    }

    [MenuItem("Tools/PROLOGUE/Run Sector C Workstation Pass A")]
    public static void RunPassA()
    {
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform workstations = FindByName(scene, WorkstationsParentName);
            Transform legacyGroup = FindByName(scene, LegacyParentName);
            Transform core = FindByName(scene, "SectorC_CentralCore");
            Transform floor = FindByName(scene, "SectorC_Floor");
            Require(workstations != null, "Missing root: Workstations");
            Require(legacyGroup != null, "Missing group: SectorC_Workstations");
            Require(core != null, "Missing object: SectorC_CentralCore");
            Require(floor != null, "Missing object: SectorC_Floor");

            List<Transform> desks = workstations.Cast<Transform>()
                .Where(t => Regex.IsMatch(t.name, @"^Desk_SectorC_Workstation_R[12]_\d{2}$"))
                .OrderBy(t => t.name, StringComparer.Ordinal)
                .ToList();
            List<Transform> proxies = legacyGroup.Cast<Transform>()
                .Where(t => Regex.IsMatch(t.name, @"^SectorC_Workstation_R[123]_\d{2}$"))
                .OrderBy(t => t.name, StringComparer.Ordinal)
                .ToList();

            Require(desks.Count == 19, "Expected exactly 19 workstation roots under Workstations; found " + desks.Count);
            Require(proxies.Count == 36, "Expected exactly 36 legacy proxies under SectorC_Workstations; found " + proxies.Count);

            List<Transform> chairs = new List<Transform>();
            int tableCount = 0;
            float tableRadius = 0f;
            foreach (Transform desk in desks)
            {
                Transform chair = desk.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Chair");
                Require(chair != null, "No Office Chair prefab root under " + desk.name);
                chairs.Add(chair);
                HashSet<GameObject> tableRoots = new HashSet<GameObject>();
                foreach (Transform child in desk.GetComponentsInChildren<Transform>(true))
                {
                    string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject).Replace('\\', '/');
                    if (prefabPath.EndsWith("/Tables/Metal/Table Metal Variant.prefab", StringComparison.Ordinal))
                    {
                        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(child.gameObject);
                        if (prefabRoot != null)
                            tableRoots.Add(prefabRoot);
                    }
                }
                Require(tableRoots.Count == 1, "Expected one Table Metal Variant prefab under " + desk.name + "; found " + tableRoots.Count);
                tableCount += tableRoots.Count;
                tableRadius = Mathf.Max(tableRadius, HorizontalBoundsRadius(tableRoots.First().transform, desk.position));
            }
            Require(tableCount == 19, "Expected exactly 19 Table Metal Variant prefabs under workstation roots; found " + tableCount);

            ValidateLegacyObjects(proxies);
            ValidateClearance(scene, desks, core, floor, tableRadius);
            SaveOriginalBackupIfMissing(scene, proxies);

            Transform parked = FindByName(scene, ParkedParentName);
            if (parked == null)
            {
                GameObject parkedObject = new GameObject(ParkedParentName);
                Undo.RegisterCreatedObjectUndo(parkedObject, "Create Sector C parked group");
                parked = parkedObject.transform;
                parked.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                parked.localScale = Vector3.one;
            }

            // Park only the 36 disabled legacy proxies. Preserve their world transforms and active state.
            foreach (Transform proxy in proxies)
            {
                Undo.RecordObject(proxy, "Park legacy Sector C workstation proxy");
                proxy.SetParent(parked, true);
            }
            for (int i = 0; i < proxies.Count; i++)
                proxies[i].SetSiblingIndex(i);

            Vector3 center = new Vector3(0f, 0f, RoomCenterZ);
            for (int i = 0; i < desks.Count; i++)
            {
                Transform desk = desks[i];
                Transform chair = chairs[i];
                float angle = 2f * Mathf.PI * i / desks.Count;
                Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 position = center + outward * RingRadius;
                position.y = desk.position.y;

                Undo.RecordObject(desk, "Arrange Sector C workstation ring");
                desk.SetPositionAndRotation(position, Quaternion.LookRotation(center - position, Vector3.up));

                Vector3 chairPosition = position + outward * ChairOffset;
                chairPosition.y = chair.position.y;
                Undo.RecordObject(chair, "Place workstation chair outside ring");
                chair.SetPositionAndRotation(chairPosition, Quaternion.LookRotation(-outward, Vector3.up));
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Unity could not save PROLOGUE.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Sector C Stage A complete: 19 workstations at radius 9.5 m; chairs offset outward 1.2 m; 36 disabled legacy proxies grouped under [Parked_SectorC]. Original proxy state backup is at " + Path.Combine(Directory.GetParent(Application.dataPath).FullName, BackupFileName));
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("Sector C Stage A stopped without reporting success: " + ex);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
                return;
            }
            throw;
        }
    }

    [MenuItem("Tools/PROLOGUE/Restore Pre-Stage A Legacy Proxies")]
    public static void RestoreLegacyProxies()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, BackupFileName);
        Require(File.Exists(path), "Stage A backup file not found: " + path);
        BackupFile backup = JsonUtility.FromJson<BackupFile>(File.ReadAllText(path));
        Require(backup != null && backup.objects != null && backup.objects.Length == 36,
            "Backup does not contain exactly 36 proxy records");
        Transform originalParent = FindByName(scene, LegacyParentName);
        Require(originalParent != null, "Original parent not found: " + LegacyParentName);

        foreach (TransformRecord record in backup.objects)
        {
            Transform proxy = FindByName(scene, record.name);
            Require(proxy != null, "Cannot restore missing proxy: " + record.name);
            Undo.RecordObject(proxy, "Restore legacy Sector C workstation proxy");
            proxy.SetParent(originalParent, true);
            proxy.SetPositionAndRotation(
                new Vector3(record.worldPosition[0], record.worldPosition[1], record.worldPosition[2]),
                new Quaternion(record.worldRotation[0], record.worldRotation[1], record.worldRotation[2], record.worldRotation[3]));
            proxy.localScale = new Vector3(record.localScale[0], record.localScale[1], record.localScale[2]);
            proxy.gameObject.SetActive(record.activeSelf);
            proxy.SetSiblingIndex(record.siblingIndex);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Unity could not save PROLOGUE.unity during restore");
        Debug.Log("Restored all 36 legacy proxies from " + path);
    }

    private static void ValidateLegacyObjects(List<Transform> proxies)
    {
        foreach (Transform proxy in proxies)
        {
            MeshRenderer renderer = proxy.GetComponent<MeshRenderer>();
            BoxCollider collider = proxy.GetComponent<BoxCollider>();
            Require(renderer != null && !renderer.enabled, proxy.name + " is not a disabled MeshRenderer proxy");
            Require(collider != null && !collider.enabled, proxy.name + " is not a disabled BoxCollider proxy");
            Require(PrefabUtility.GetPrefabInstanceStatus(proxy.gameObject) == PrefabInstanceStatus.NotAPrefab,
                proxy.name + " unexpectedly became a prefab instance");
        }
    }

    private static void ValidateClearance(Scene scene, List<Transform> desks, Transform core, Transform floor, float tableRadius)
    {
        Bounds floorBounds = BoundsFor(floor);
        float coreRadius = HorizontalRadius(core);
        float coreGap = RingRadius - coreRadius - tableRadius;
        float wallGap = Math.Min(floorBounds.extents.x, floorBounds.extents.z) - RingRadius - ChairOffset - ChairFootprintRadius;
        Require(coreGap >= MinimumCoreClearance,
            "Core-to-table clearance would be " + coreGap.ToString("F2") + " m, below 2 m");
        Require(wallGap >= 2f,
            "Outermost chair-to-wall clearance would be " + wallGap.ToString("F2") + " m, below 2 m");

        HashSet<Transform> movedRoots = new HashSet<Transform>(desks);
        List<Renderer> fixedRenderers = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
            .Where(r => r.enabled && r.gameObject.activeInHierarchy)
            .Where(r => !movedRoots.Any(root => r.transform == root || r.transform.IsChildOf(root)))
            .Where(r => !r.transform.name.StartsWith("SectorC_Workstation_R", StringComparison.Ordinal))
            .Where(r => r.transform.name != "SectorC_Floor")
            .Where(r => r.bounds.center.y < 2.5f)
            .ToList();

        float minFixedGap = float.PositiveInfinity;
        string nearest = "none";
        foreach (Transform desk in desks)
        {
            float angle = 2f * Mathf.PI * desks.IndexOf(desk) / desks.Count;
            Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 tablePoint = new Vector3(outward.x * RingRadius, desk.position.y, RoomCenterZ + outward.z * RingRadius);
            Vector3 chairPoint = tablePoint + outward * ChairOffset;
            foreach (Renderer renderer in fixedRenderers)
            {
                Bounds b = renderer.bounds;
                float tableDistance = HorizontalPointToBoundsDistance(tablePoint, b) - tableRadius;
                float chairDistance = HorizontalPointToBoundsDistance(chairPoint, b) - ChairFootprintRadius;
                float gap = Math.Min(tableDistance, chairDistance);
                if (gap < minFixedGap)
                {
                    minFixedGap = gap;
                    nearest = renderer.name;
                }
            }
        }
        Require(minFixedGap >= 0f,
            "Planned table/chair ring overlaps fixed object '" + nearest + "' by " + (-minFixedGap).ToString("F2") + " m");

        Debug.Log("Stage A clearance preflight passed: wall margin " + wallGap.ToString("F2") + " m; conservative core gap " + coreGap.ToString("F2") + " m; nearest fixed renderer clearance " + (float.IsInfinity(minFixedGap) ? "n/a" : minFixedGap.ToString("F2") + " m to " + nearest));
    }

    private static float HorizontalPointToBoundsDistance(Vector3 point, Bounds bounds)
    {
        float dx = Math.Max(Math.Max(bounds.min.x - point.x, 0f), point.x - bounds.max.x);
        float dz = Math.Max(Math.Max(bounds.min.z - point.z, 0f), point.z - bounds.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float HorizontalRadius(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        if (renderers.Length == 0)
            return 4f; // serialized SectorC_CentralCore scale is 8 m across
        Vector3 center = root.position;
        float radius = 0f;
        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;
            float dx = Mathf.Abs(b.center.x - center.x) + b.extents.x;
            float dz = Mathf.Abs(b.center.z - center.z) + b.extents.z;
            radius = Mathf.Max(radius, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return radius;
    }

    private static float HorizontalBoundsRadius(Transform root, Vector3 origin)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        Require(renderers.Length > 0, "No enabled renderer found under table prefab " + root.name);
        float radius = 0f;
        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;
            foreach (float x in new[] { b.min.x, b.max.x })
            foreach (float z in new[] { b.min.z, b.max.z })
            {
                float dx = x - origin.x;
                float dz = z - origin.z;
                radius = Mathf.Max(radius, Mathf.Sqrt(dx * dx + dz * dz));
            }
        }
        return radius;
    }

    private static Bounds BoundsFor(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        if (renderers.Length == 0)
            return new Bounds(root.position, new Vector3(40f, 0.2f, 40f)); // serialized floor scale
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1))
            bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static void SaveOriginalBackupIfMissing(Scene scene, List<Transform> proxies)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(projectRoot, BackupFileName);
        if (File.Exists(path))
        {
            Debug.Log("Existing Stage A backup kept unchanged: " + path);
            return;
        }

        TransformRecord[] records = proxies.Select(t => new TransformRecord
        {
            name = t.name,
            originalParent = t.parent == null ? "" : t.parent.name,
            worldPosition = new[] { t.position.x, t.position.y, t.position.z },
            worldRotation = new[] { t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w },
            localScale = new[] { t.localScale.x, t.localScale.y, t.localScale.z },
            activeSelf = t.gameObject.activeSelf,
            siblingIndex = t.GetSiblingIndex()
        }).ToArray();
        BackupFile backup = new BackupFile
        {
            scene = ScenePath,
            note = "Original world transform and activeSelf state for the 36 disabled SectorC_Workstation proxy GameObjects before Stage A. Renderer and collider components were already disabled.",
            objects = records
        };
        File.WriteAllText(path, JsonUtility.ToJson(backup, true));
        Debug.Log("Wrote original Stage A proxy backup: " + path);
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().Select(go => go.transform).FirstOrDefault(t => t.name == name);
    }

    private static Transform FindByName(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
