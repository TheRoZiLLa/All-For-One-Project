using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Recorded.ColorSystem;

/// <summary>
/// Wires Spectrum components on Samir blockout puzzle props. Does not move or reparent greybox.
/// </summary>
public static class DirectorCorridorAtriumIntegration
{
    const string ScenePath = "Assets/Scenes/PROLOGUE.unity";
    const float GreenSearchRadius = 10f;
    const float GlitchCrateMass = 85f;

    static readonly string[] JordanDuplicateNames =
    {
        "Atrium_PowerPlug_BatteryCore",
        "Atrium_PowerPlug_Receptacle",
        "Atrium_YellowButton_SlingshotBlock",
        "Chasm_SteppingBlock",
        "GlitchWall_DestructibleProxy",
    };

    [MenuItem("Tools/Prologue/Wire Sector A Readability Colliders (Jordan)")]
    public static void WireReadabilityColliders() => WireReadabilityCollidersInternal();

    [MenuItem("Tools/Prologue/Wire Corridor RGB Tutorial (Jordan)")]
    public static void WireCorridorRgbTutorial() => WireCorridorRgbTutorialInternal();

    [MenuItem("Tools/Prologue/Integrate Director Corridor + Atrium (Jordan)")]
    public static void Integrate()
    {
        try
        {
            IntegrateInternal();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    static void IntegrateInternal()
    {
        if (!EditorSceneManager.GetActiveScene().path.Replace("\\", "/").EndsWith("PROLOGUE.unity"))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        var report = new StringBuilder("DirectorCorridorAtriumIntegration (wire-only)\n");

        foreach (var dup in JordanDuplicateNames)
        {
            var go = GameObject.Find(dup);
            if (go == null)
                continue;
            Object.DestroyImmediate(go);
            report.AppendLine("Removed Jordan duplicate: " + dup);
        }

        WirePaintable("GlitchWall_HeavyCrate", GlitchCrateMass, kinematic: false);
        WirePaintable("PowerPlug_BatteryCore", preserveMass: true, kinematic: false);
        WirePaintable("PowerPlug_Receptacle", preserveMass: true, kinematic: true);
        WirePaintable("Yellow_SlingshotBlock", preserveMass: true, kinematic: false);

        var bat = GameObject.Find("PowerPlug_BatteryCore");
        var rec = GameObject.Find("PowerPlug_Receptacle");
        if (bat != null && rec != null)
        {
            float dist = Vector3.Distance(bat.transform.position, rec.transform.position);
            report.AppendLine($"Battery↔Receptacle distance: {dist:F2} (greenSearchRadius={GreenSearchRadius})");
        }

        report.AppendLine("Chasm gap width per blockout v1: 4.0");
        report.AppendLine("Hooks unchanged: Chasm_DispenserSocket, Atrium_PowerPlug_DoorHook, Yellow_EmergencyButton / Yellow_FinalBridgeStub");

        Debug.Log(report.ToString());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void WirePaintable(string objectName, float massOverride = -1f, bool preserveMass = false, bool kinematic = false)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("Paintable not found (Samir blockout): " + objectName);
            return;
        }

        if (go.GetComponent<Collider>() == null)
            go.AddComponent<BoxCollider>();

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
            rb = go.AddComponent<Rigidbody>();

        if (massOverride > 0f)
            rb.mass = massOverride;
        else if (!preserveMass)
            rb.mass = 1.5f;

        rb.isKinematic = kinematic;
        rb.useGravity = !kinematic;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var colorTarget = go.GetComponent<RGBColorTarget>();
        if (colorTarget == null)
            colorTarget = go.AddComponent<RGBColorTarget>();

        var responder = go.GetComponent<SpectrumSkillResponder>();
        if (responder == null)
            responder = go.AddComponent<SpectrumSkillResponder>();
        responder.EnsureInitialized();

        var so = new SerializedObject(responder);
        var radiusProp = so.FindProperty("greenSearchRadius");
        if (radiusProp != null)
            radiusProp.floatValue = GreenSearchRadius;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (go.GetComponent<ColorMixPhysicsReactor>() == null)
            go.AddComponent<ColorMixPhysicsReactor>();

        colorTarget.ResetColors();
    }

    public static string WireCorridorRgbTutorialInternal()
    {
        if (!EditorSceneManager.GetActiveScene().path.Replace("\\", "/").EndsWith("PROLOGUE.unity"))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return "blocked: open PROLOGUE";
            EditorSceneManager.OpenScene(ScenePath);
        }

        var report = new StringBuilder();

        EnsureSolidBlock("GlitchWall_PathSeal");

        for (int i = 1; i <= 5; i++)
        {
            string name = $"SectorA_RedCrate_{i:D2}";
            if (WireTutorialProp(name, puzzleCritical: true))
                report.AppendLine(name + ": wired (dynamic, puzzle-critical)");
            else
                report.AppendLine(name + ": MISSING");
        }

        if (WireTutorialProp("Chasm_TetherAnchor_Near", puzzleCritical: true))
            report.AppendLine("Chasm_TetherAnchor_Near: wired (kinematic preserved)");
        else
            report.AppendLine("Chasm_TetherAnchor_Near: MISSING");

        if (WireTutorialProp("Chasm_TetherAnchor_Far", puzzleCritical: true))
            report.AppendLine("Chasm_TetherAnchor_Far: wired (kinematic preserved)");
        else
            report.AppendLine("Chasm_TetherAnchor_Far: MISSING");

        if (WireTutorialProp("SectorA_BluePendulum", puzzleCritical: true))
            report.AppendLine("SectorA_BluePendulum: wired (hinge preserved, no Animator)");
        else
            report.AppendLine("SectorA_BluePendulum: MISSING");

        string bridgeResult = VerifyKinematicGreenBridge();
        report.AppendLine("Green bridge check: " + bridgeResult);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(report.ToString());
        return report.ToString();
    }

    static bool WireTutorialProp(string objectName, bool puzzleCritical)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
            return false;

        int layer = LayerMask.NameToLayer(ColorInteractable.LayerName);
        if (layer >= 0)
            go.layer = layer;

        if (go.GetComponent<Collider>() == null)
            go.AddComponent<BoxCollider>();

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var colorTarget = go.GetComponent<RGBColorTarget>();
        if (colorTarget == null)
            colorTarget = go.AddComponent<RGBColorTarget>();
        colorTarget.SetPuzzleCritical(puzzleCritical);

        var responder = go.GetComponent<SpectrumSkillResponder>();
        if (responder == null)
            responder = go.AddComponent<SpectrumSkillResponder>();
        responder.EnsureInitialized();

        var responderSo = new SerializedObject(responder);
        var radiusProp = responderSo.FindProperty("greenSearchRadius");
        if (radiusProp != null)
            radiusProp.floatValue = GreenSearchRadius;
        responderSo.ApplyModifiedPropertiesWithoutUndo();

        if (go.GetComponent<ColorMixPhysicsReactor>() == null)
            go.AddComponent<ColorMixPhysicsReactor>();

        var spawner = go.GetComponent<PuzzleItemSpawner>();
        if (spawner == null)
            spawner = go.AddComponent<PuzzleItemSpawner>();
        var spawnerSo = new SerializedObject(spawner);
        var criticalProp = spawnerSo.FindProperty("forcePuzzleCritical");
        if (criticalProp != null)
            criticalProp.boolValue = puzzleCritical;
        spawnerSo.ApplyModifiedPropertiesWithoutUndo();

        colorTarget.ResetColors();
        return true;
    }

    static string VerifyKinematicGreenBridge()
    {
        var nearGo = GameObject.Find("Chasm_TetherAnchor_Near");
        var farGo = GameObject.Find("Chasm_TetherAnchor_Far");
        if (nearGo == null || farGo == null)
            return "FAILED (anchor missing)";

        var nearRb = nearGo.GetComponent<Rigidbody>();
        var farRb = farGo.GetComponent<Rigidbody>();
        if (nearRb == null || farRb == null || !nearRb.isKinematic || !farRb.isKinematic)
            return "FAILED (anchors must stay kinematic for this check)";

        var near = nearGo.GetComponent<SpectrumSkillResponder>();
        var far = farGo.GetComponent<SpectrumSkillResponder>();
        if (near == null || far == null)
            return "FAILED (SpectrumSkillResponder missing)";

        float dist = Vector3.Distance(near.TetherPoint, far.TetherPoint);
        if (dist > GreenSearchRadius)
            return $"FAILED (distance {dist:F2} > radius {GreenSearchRadius})";

        near.DeactivateGreenBind();
        far.DeactivateGreenBind();

        near.ActivateGreenBind(30f, true);
        far.ActivateGreenBind(30f, true);

        if (near.IsTethered || far.IsTethered)
        {
            near.DeactivateGreenBind();
            far.DeactivateGreenBind();
            return "SPAWNED (kinematic pair)";
        }

        near.DeactivateGreenBind();
        far.DeactivateGreenBind();
        return "FAILED (TryCreateTether did not link kinematic pair)";
    }

    static void WireReadabilityCollidersInternal()
    {
        EnsureSolidBlock("GlitchWall_PathSeal");
        EnsureSolidBlock("Chasm_Rim_West");
        EnsureSolidBlock("Chasm_Rim_East");

        var abyss = GameObject.Find("Chasm_AbyssVisual");
        if (abyss != null)
        {
            foreach (var col in abyss.GetComponents<Collider>())
                Object.DestroyImmediate(col);
            StripColorComponents(abyss);
        }

        var tag = GameObject.Find("GlitchWall_PuzzleTag");
        if (tag != null)
        {
            foreach (var col in tag.GetComponents<Collider>())
                Object.DestroyImmediate(col);
            StripColorComponents(tag);
        }

        var kill = GameObject.Find("Chasm_KillVolume");
        if (kill != null)
        {
            var col = kill.GetComponent<Collider>() ?? kill.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.enabled = true;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void EnsureSolidBlock(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("Readability object missing: " + objectName);
            return;
        }

        var col = go.GetComponent<Collider>() ?? go.AddComponent<BoxCollider>();
        col.isTrigger = false;
        col.enabled = true;

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        StripColorComponents(go);
    }

    static void StripColorComponents(GameObject go)
    {
        var color = go.GetComponent<RGBColorTarget>();
        if (color != null)
            Object.DestroyImmediate(color);
        var responder = go.GetComponent<SpectrumSkillResponder>();
        if (responder != null)
            Object.DestroyImmediate(responder);
        var reactor = go.GetComponent<ColorMixPhysicsReactor>();
        if (reactor != null)
            Object.DestroyImmediate(reactor);
    }
}
