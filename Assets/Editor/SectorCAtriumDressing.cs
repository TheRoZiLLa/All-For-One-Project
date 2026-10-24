using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Replaces Sector C greybox visuals with a sci-fi atrium dressing.
/// Room shell colliders and world positions stay put.
/// </summary>
public static class SectorCAtriumDressing
{
    const string RootName = "SectorC_VisualDressing";
    const string MatDir = "Assets/Materials/SectorC";
    const float FloorY = 0.22f;
    const float CoreX = 0f;
    const float CoreZ = 59f;

    const string TablePath = "Assets/ScifiOfficeLite/Prefabs/Tables/Metal/Table Metal Variant.prefab";
    const string ChairPath = "Assets/ScifiOfficeLite/Prefabs/Chairs and Stools/Office Chair.prefab";
    const string TvPath = "Assets/ScifiOfficeLite/Prefabs/TVs/Tv 32 Inch.prefab";
    const string PcPath = "Assets/ScifiOfficeLite/Prefabs/Tech Accessories/PC 2.prefab";
    const string RackPath = "Assets/ScifiOfficeLite/Prefabs/Tech Accessories/Server Rack.prefab";
    const string ShelfPath = "Assets/ScifiOfficeLite/Prefabs/Shelf with Crates.prefab";
    const string DoorPath = "Assets/ScifiOfficeLite/Prefabs/Doors/Broken Door.prefab";
    const string LightPath = "Assets/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light Bright.prefab";

    [MenuItem("Tools/Prologue/Dress Sector C Atrium")]
    public static void Dress()
    {
        try
        {
            DressInternal();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    static void DressInternal()
    {
        var atrium = GameObject.Find("SectorC_GrandAtrium");
        if (atrium == null)
        {
            Debug.LogError("SectorC_GrandAtrium was not found in the open scene.");
            return;
        }

        EnsureFolder(MatDir);
        Material metal = MakeLit("M_DarkMetal", new Color(0.12f, 0.13f, 0.15f), Color.black, 0.85f, 0.55f, false);
        Material concrete = MakeLit("M_Concrete", new Color(0.18f, 0.19f, 0.2f), Color.black, 0.05f, 0.18f, false);
        Material floor = MakeLit("M_FloorGrate", new Color(0.1f, 0.11f, 0.13f), Color.black, 0.6f, 0.35f, false);
        floor.SetTexture("_BaseMap", SaveTex("T_FloorGrate.png", MakeFloorTex()));
        floor.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
        Material cyanEmit = MakeLit("M_CoreCyan", new Color(0.05f, 0.2f, 0.25f), new Color(0f, 2.2f, 4f), 0.4f, 0.8f, false);
        Material redEmit = MakeLit("M_AlertRed", new Color(0.25f, 0.02f, 0.02f), new Color(3.5f, 0.15f, 0.1f), 0.2f, 0.6f, false);
        Material screenBlue = MakeLit("M_ScreenBlue", new Color(0.02f, 0.15f, 0.2f), new Color(0f, 1.6f, 2.8f), 0.1f, 0.9f, false);
        Material hazard = MakeLit("M_Hazard", Color.white, Color.black, 0.1f, 0.25f, false);
        hazard.SetTexture("_BaseMap", SaveTex("T_Hazard.png", MakeHazardTex()));
        hazard.SetTextureScale("_BaseMap", new Vector2(2f, 1f));
        Material cable = MakeLit("M_Cable", new Color(0.03f, 0.03f, 0.035f), Color.black, 0.3f, 0.4f, false);
        Shader hologramShader = Shader.Find("Prologue/HologramEcho");
        if (hologramShader == null)
            hologramShader = Shader.Find("Universal Render Pipeline/Lit");
        string hologramPath = MatDir + "/M_Hologram.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(hologramPath) != null)
            AssetDatabase.DeleteAsset(hologramPath);
        Material hologram = new Material(hologramShader);
        if (hologramShader.name.Contains("Lit"))
        {
            hologram.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f, 0.45f));
            hologram.EnableKeyword("_EMISSION");
            hologram.SetColor("_EmissionColor", new Color(0f, 2f, 3f));
        }
        AssetDatabase.CreateAsset(hologram, hologramPath);

        Transform old = atrium.transform.Find(RootName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);

        var dressing = new GameObject(RootName);
        dressing.transform.SetParent(atrium.transform, false);
        Transform root = dressing.transform;

        PaintShell(atrium.transform, floor, concrete, metal);
        HideMarkers(atrium.transform);

        var pulseTargets = new List<Renderer>();
        BuildCore(root, metal, cyanEmit, redEmit, cable, pulseTargets);
        List<Vector3> desks = BuildDesks(root, atrium.transform, metal, screenBlue, redEmit);
        BuildCables(root, desks, cable);
        BuildEchoes(atrium.transform, desks, hologram);
        BuildCatwalk(root, atrium.transform, metal, cable, redEmit);
        BuildShellDressing(root, metal, cable, concrete, screenBlue, redEmit, hazard);
        BuildLights(root, cyanEmit, redEmit, pulseTargets);

        var pulseGo = new GameObject("CorePulse");
        pulseGo.transform.SetParent(root, false);
        var pulse = pulseGo.AddComponent<CoreEnergyPulse>();
        pulse.targets = pulseTargets.ToArray();
        Transform lights = root.Find("Lights");
        if (lights != null)
        {
            Transform coreLight = lights.Find("CorePoint");
            Transform redLight = lights.Find("CoreRedPoint");
            if (coreLight != null) pulse.coreLight = coreLight.GetComponent<Light>();
            if (redLight != null) pulse.redLight = redLight.GetComponent<Light>();
        }

        ApplyMood();
        EditorSceneManager.MarkSceneDirty(atrium.scene);
        EditorSceneManager.SaveScene(atrium.scene);
        Debug.Log("Sector C atrium dressing saved. Desks: " + desks.Count);
    }

    static void PaintShell(Transform atrium, Material floor, Material concrete, Material metal)
    {
        Assign(atrium, "SectorC_Floor", floor);
        Assign(atrium, "SectorC_Wall_South_West", concrete);
        Assign(atrium, "SectorC_Wall_South_East", concrete);
        Assign(atrium, "SectorC_Wall_South_Lintel", metal);
        Assign(atrium, "SectorC_Wall_North", concrete);
        Assign(atrium, "SectorC_Wall_West", concrete);
        Assign(atrium, "SectorC_Wall_East", concrete);
        var catwalk = atrium.Find("SectorC_Catwalk");
        if (catwalk == null) return;
        foreach (Transform child in catwalk)
            AssignRenderer(child.gameObject, metal);
    }

    static void HideMarkers(Transform atrium)
    {
        HideRenderer(atrium.Find("SectorC_CentralCore"));
        Transform stations = atrium.Find("SectorC_Workstations");
        if (stations != null)
        {
            foreach (Transform child in stations)
            {
                HideRenderer(child);
                Collider collider = child.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }
        }

        for (int i = 1; i <= 6; i++)
        {
            Transform echo = atrium.Find("Frozen_Echo_NPC_0" + i);
            if (echo == null) continue;
            HideRenderer(echo);
            Collider collider = echo.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Transform previous = echo.Find("HologramBody");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        }
    }

    static void BuildCore(Transform root, Material metal, Material cyan, Material red, Material cable, List<Renderer> pulse)
    {
        var core = new GameObject("CyberReactor").transform;
        core.SetParent(root, false);
        Vector3 center = new Vector3(CoreX, 7.5f, CoreZ);

        Renderer glass = Cylinder(core, "CoreGlass", center, new Vector3(5.4f, 7.1f, 5.4f), Quaternion.identity, cyan).GetComponent<Renderer>();
        pulse.Add(glass);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Cube(core, "Fin_" + i, center + dir * 4.15f, new Vector3(0.28f, 13.6f, 1.15f), Quaternion.LookRotation(dir), metal);
            if (i % 2 == 0)
            {
                Renderer strip = Cube(core, "DataStrip_" + i, center + dir * 4.05f + Vector3.up * 0.2f, new Vector3(0.08f, 11.5f, 0.18f), Quaternion.LookRotation(dir), i % 4 == 0 ? red : cyan).GetComponent<Renderer>();
                pulse.Add(strip);
            }
        }

        Cylinder(core, "Clamp_Base", new Vector3(CoreX, 0.55f, CoreZ), new Vector3(9.1f, 0.22f, 9.1f), Quaternion.identity, metal);
        Cylinder(core, "Clamp_Mid", new Vector3(CoreX, 7.2f, CoreZ), new Vector3(8.8f, 0.16f, 8.8f), Quaternion.identity, metal);
        Cylinder(core, "Clamp_Top", new Vector3(CoreX, 13.4f, CoreZ), new Vector3(8.8f, 0.18f, 8.8f), Quaternion.identity, metal);

        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90f + 20f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Cube(core, "ServerPanel_" + i, new Vector3(CoreX, 3.2f + (i % 2) * 4.5f, CoreZ) + dir * 4.35f, new Vector3(1.5f, 2.4f, 0.22f), Quaternion.LookRotation(dir), metal);
            Renderer lamp = Cube(core, "PanelLamp_" + i, new Vector3(CoreX, 3.8f + (i % 2) * 4.5f, CoreZ) + dir * 4.5f, new Vector3(1.1f, 0.18f, 0.05f), Quaternion.LookRotation(dir), i % 2 == 0 ? cyan : red).GetComponent<Renderer>();
            pulse.Add(lamp);
        }

        // Ducts from the core crown out toward the ceiling ring.
        string[] names = { "North", "East", "South", "West" };
        Vector3[] ends =
        {
            new Vector3(0f, 14.4f, 73f),
            new Vector3(14f, 14.4f, 59f),
            new Vector3(0f, 14.4f, 45f),
            new Vector3(-14f, 14.4f, 59f)
        };
        for (int i = 0; i < 4; i++)
        {
            Vector3 from = new Vector3(0f, 14.4f, CoreZ);
            Beam(core, "Vent_" + names[i], from, ends[i], 0.85f, 0.55f, metal);
            Beam(core, "CableTray_" + names[i], from + Vector3.down * 0.7f, ends[i] + Vector3.down * 0.7f, 0.35f, 0.12f, cable);
            // Hang a bundle from the duct midpoint down to the core shoulder.
            Vector3 mid = Vector3.Lerp(from, ends[i], 0.45f);
            Beam(core, "Hang_" + names[i], mid, new Vector3(Mathf.Sign(ends[i].x) * 2.2f, 8.5f, CoreZ + Mathf.Sign(ends[i].z - CoreZ) * 2.2f), 0.12f, 0.12f, cable);
        }
    }

    static List<Vector3> BuildDesks(Transform root, Transform atrium, Material metal, Material screenBlue, Material screenRed)
    {
        var parent = new GameObject("Workstations").transform;
        parent.SetParent(root, false);
        var desks = new List<Vector3>();
        Transform stations = atrium.Find("SectorC_Workstations");
        if (stations == null) return desks;

        int index = 0;
        int outerIndex = 0;
        foreach (Transform marker in stations)
        {
            bool inner = marker.name.Contains("_R1_");
            bool middle = marker.name.Contains("_R2_");
            bool outer = marker.name.Contains("_R3_");
            if (marker.name.EndsWith("_R2_07")) continue;
            if (!inner && !middle && !outer) continue;
            if (outer && outerIndex++ % 2 != 0) continue;

            Vector3 pos = marker.position;
            pos.y = FloorY;
            Vector3 outward = pos - new Vector3(CoreX, pos.y, CoreZ);
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
            outward.Normalize();
            Quaternion faceOut = Quaternion.LookRotation(outward, Vector3.up);

            if (outer)
            {
                GameObject rack = Spawn(RackPath, parent, pos - outward * 0.4f, faceOut, "ServerNode_" + marker.name);
                if (rack != null) Sit(rack, FloorY);
                continue;
            }

            var unit = new GameObject("Desk_" + marker.name).transform;
            unit.SetParent(parent, false);
            unit.SetPositionAndRotation(pos, faceOut);

            GameObject table = Spawn(TablePath, unit, pos, faceOut, "Table");
            if (table == null) continue;
            Sit(table, FloorY);
            Bounds tableBounds = BoundsOf(table);
            Vector3 top = new Vector3(tableBounds.center.x, tableBounds.max.y, tableBounds.center.z);

            bool error = index % 3 == 0;
            PlaceMonitor(unit, top - outward * 0.15f + Vector3.right * 0f, faceOut, outward, -0.42f, error ? screenRed : screenBlue, error, "MonitorL_" + index);
            PlaceMonitor(unit, top - outward * 0.15f, faceOut, outward, 0.42f, screenBlue, false, "MonitorR_" + index);

            Cube(unit, "Keyboard", top + outward * 0.22f + Vector3.up * 0.02f, new Vector3(0.55f, 0.03f, 0.2f), faceOut, metal);
            Cube(unit, "Keys", top + outward * 0.22f + Vector3.up * 0.045f, new Vector3(0.48f, 0.01f, 0.14f), faceOut, screenBlue);

            GameObject chair = Spawn(ChairPath, unit, pos + outward * 1.15f, Quaternion.LookRotation(-outward, Vector3.up), "Chair");
            if (chair != null) Sit(chair, FloorY);

            if (index % 2 == 0)
            {
                GameObject rack = Spawn(RackPath, unit, pos - outward * 1.35f + Vector3.Cross(outward, Vector3.up) * 1.1f, faceOut, "Rack");
                if (rack != null) Sit(rack, FloorY);
            }
            else
            {
                GameObject pc = Spawn(PcPath, unit, top + outward * 0.05f + Vector3.Cross(Vector3.up, outward) * 0.7f, faceOut, "PC");
                if (pc != null)
                    pc.transform.position = new Vector3(pc.transform.position.x, top.y + 0.15f, pc.transform.position.z);
            }

            desks.Add(tableBounds.center);
            index++;
        }

        return desks;
    }

    static void PlaceMonitor(Transform parent, Vector3 anchor, Quaternion faceOut, Vector3 outward, float side, Material screenMat, bool error, string name)
    {
        Vector3 sideDir = Vector3.Cross(Vector3.up, outward);
        Vector3 pos = anchor + sideDir * side;
        GameObject tv = Spawn(TvPath, parent, pos, faceOut, name);
        if (tv != null)
        {
            Bounds before = BoundsOf(tv);
            tv.transform.position += Vector3.up * (anchor.y - before.min.y);
        }

        var screen = Cube(parent, name + "_UI", pos + Vector3.up * 0.35f + outward * 0.12f, new Vector3(0.62f, 0.34f, 0.02f), faceOut, screenMat);
        if (error)
            AddErrorLabel(screen.transform, screen.transform.position + outward * 0.08f, faceOut, 0.18f);
    }

    static void AddErrorLabel(Transform parent, Vector3 position, Quaternion rotation, float scale)
    {
        var labelGo = new GameObject("ErrorLabel");
        labelGo.transform.SetParent(parent, true);
        labelGo.transform.SetPositionAndRotation(position, rotation);
        labelGo.transform.localScale = Vector3.one * scale;
        var text = labelGo.AddComponent<TextMesh>();
        text.text = "Error";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.25f;
        text.fontSize = 64;
        text.color = new Color(1f, 0.22f, 0.16f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null)
            text.font = font;
    }

    static void BuildCables(Transform root, List<Vector3> desks, Material cable)
    {
        var parent = new GameObject("FloorCables").transform;
        parent.SetParent(root, false);
        for (int i = 0; i < desks.Count; i++)
        {
            Vector3 desk = desks[i];
            desk.y = FloorY + 0.05f;
            Vector3 from = new Vector3(CoreX, FloorY + 0.08f, CoreZ);
            Vector3 dir = desk - from;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                from += dir.normalized * 4.4f;
            Vector3 mid = Vector3.Lerp(from, desk, 0.5f);
            mid += Vector3.Cross(dir.normalized, Vector3.up) * ((i % 2 == 0) ? 0.35f : -0.25f);
            mid.y = FloorY + 0.04f;
            Beam(parent, "CableA_" + i, from, mid, 0.07f, 0.045f, cable);
            Beam(parent, "CableB_" + i, mid, desk, 0.06f, 0.04f, cable);
            if (i % 3 == 0)
            {
                Vector3 offset = Vector3.Cross(dir.normalized, Vector3.up) * 0.22f;
                Beam(parent, "CableOverlap_" + i, from + offset, desk + offset, 0.04f, 0.03f, cable);
            }
        }
    }

    static void BuildEchoes(Transform atrium, List<Vector3> desks, Material hologram)
    {
        for (int i = 1; i <= 6; i++)
        {
            Transform echo = atrium.Find("Frozen_Echo_NPC_0" + i);
            if (echo == null) continue;
            Vector3 target = Nearest(echo.position, desks);
            Vector3 to = target - echo.position;
            to.y = 0f;
            float yaw = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to).eulerAngles.y : 0f;

            var body = new GameObject("HologramBody").transform;
            body.SetParent(echo, false);
            body.localPosition = new Vector3(0f, -0.85f, 0f);
            body.localRotation = Quaternion.Euler(24f, yaw, 0f);

            Part(body, "Pelvis", new Vector3(0f, 0.95f, 0f), new Vector3(0.34f, 0.16f, 0.18f), hologram);
            Part(body, "Chest", new Vector3(0f, 1.25f, 0.04f), new Vector3(0.4f, 0.42f, 0.18f), hologram);
            Part(body, "Head", new Vector3(0f, 1.68f, 0.08f), new Vector3(0.2f, 0.24f, 0.2f), hologram);
            Arm(body, -1f, hologram);
            Arm(body, 1f, hologram);
            Leg(body, -0.12f, hologram);
            Leg(body, 0.12f, hologram);

            for (int g = 0; g < 8; g++)
            {
                float a = g * 47f;
                Vector3 jitter = new Vector3(Mathf.Sin(a) * 0.28f, 1.1f + (g % 4) * 0.18f, Mathf.Cos(a) * 0.22f);
                var bit = Part(body, "Glitch_" + g, jitter, Vector3.one * (0.04f + (g % 3) * 0.02f), hologram);
                bit.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }

    static void Arm(Transform body, float side, Material hologram)
    {
        Part(body, side < 0 ? "UpperArmL" : "UpperArmR", new Vector3(side * 0.28f, 1.38f, 0.08f), new Vector3(0.08f, 0.32f, 0.08f), hologram);
        Part(body, side < 0 ? "ForeArmL" : "ForeArmR", new Vector3(side * 0.34f, 1.12f, 0.22f), new Vector3(0.07f, 0.28f, 0.07f), hologram);
        Part(body, side < 0 ? "HandL" : "HandR", new Vector3(side * 0.3f, 0.96f, 0.36f), new Vector3(0.08f, 0.06f, 0.1f), hologram);
    }

    static void Leg(Transform body, float x, Material hologram)
    {
        Part(body, "Thigh", new Vector3(x, 0.68f, 0.02f), new Vector3(0.1f, 0.38f, 0.1f), hologram);
        Part(body, "Shin", new Vector3(x, 0.28f, 0.04f), new Vector3(0.08f, 0.36f, 0.08f), hologram);
    }

    static Renderer Part(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var renderer = Cube(parent, name, parent.position, scale, parent.rotation, mat).GetComponent<Renderer>();
        renderer.transform.SetParent(parent, true);
        renderer.transform.localPosition = localPos;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = scale;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    static void BuildCatwalk(Transform root, Transform atrium, Material metal, Material cable, Material red)
    {
        var parent = new GameObject("CatwalkDressing").transform;
        parent.SetParent(root, false);
        Transform catwalk = atrium.Find("SectorC_Catwalk");
        if (catwalk == null) return;

        RailAlong(parent, new Vector3(-16f, 8.28f, 73f), new Vector3(16f, 8.28f, 73f), metal);
        RailAlong(parent, new Vector3(-16f, 8.28f, 75f), new Vector3(16f, 8.28f, 75f), metal);
        RailAlong(parent, new Vector3(-16f, 8.28f, 43f), new Vector3(16f, 8.28f, 43f), metal);
        RailAlong(parent, new Vector3(-16f, 8.28f, 45f), new Vector3(16f, 8.28f, 45f), metal);
        RailAlong(parent, new Vector3(-16f, 8.28f, 45f), new Vector3(-16f, 8.28f, 73f), metal);
        RailAlong(parent, new Vector3(-14f, 8.28f, 45f), new Vector3(-14f, 8.28f, 73f), metal);
        RailAlong(parent, new Vector3(16f, 8.28f, 45f), new Vector3(16f, 8.28f, 73f), metal);
        RailAlong(parent, new Vector3(14f, 8.28f, 45f), new Vector3(14f, 8.28f, 73f), metal);

        Beam(parent, "TrussN", new Vector3(-12f, 7.4f, 74f), new Vector3(12f, 7.4f, 74f), 0.12f, 0.35f, metal);
        Beam(parent, "TrussS", new Vector3(-12f, 7.4f, 44f), new Vector3(12f, 7.4f, 44f), 0.12f, 0.35f, metal);
        Beam(parent, "TrussW", new Vector3(-15f, 7.4f, 50f), new Vector3(-15f, 7.4f, 68f), 0.12f, 0.35f, metal);
        Beam(parent, "TrussE", new Vector3(15f, 7.4f, 50f), new Vector3(15f, 7.4f, 68f), 0.12f, 0.35f, metal);
        Beam(parent, "TrayN", new Vector3(-10f, 7.7f, 73.2f), new Vector3(10f, 7.7f, 73.2f), 0.45f, 0.08f, cable);
        Beam(parent, "TrayS", new Vector3(-10f, 7.7f, 44.8f), new Vector3(10f, 7.7f, 44.8f), 0.45f, 0.08f, cable);

        Stairs(parent, -17.4f, 50f, 1f, metal);
        Stairs(parent, 17.4f, 68f, -1f, metal);

        Vector3[] beacons =
        {
            new Vector3(-15.5f, 9.3f, 73.5f),
            new Vector3(15.5f, 9.3f, 73.5f),
            new Vector3(-15.5f, 9.3f, 44.5f),
            new Vector3(15.5f, 9.3f, 44.5f)
        };
        for (int i = 0; i < beacons.Length; i++)
        {
            Sphere(parent, "Beacon_" + i, beacons[i], 0.18f, red);
            var lightGo = new GameObject("BeaconLight_" + i);
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = beacons[i];
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.15f, 0.1f);
            light.range = 6f;
            light.intensity = 3.5f;
            light.shadows = LightShadows.None;
        }
    }

    static void RailAlong(Transform parent, Vector3 a, Vector3 b, Material metal)
    {
        Vector3 dir = b - a;
        float length = dir.magnitude;
        if (length < 0.1f) return;
        Vector3 step = dir / Mathf.Max(1f, Mathf.Round(length / 3.2f));
        int count = Mathf.RoundToInt(length / 3.2f);
        for (int i = 0; i <= count; i++)
        {
            Vector3 p = a + step * i;
            Cube(parent, "Post", p + Vector3.up * 0.55f, new Vector3(0.07f, 1.1f, 0.07f), Quaternion.identity, metal);
        }
        Vector3 mid = (a + b) * 0.5f;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        Cube(parent, "Handrail", mid + Vector3.up * 1.05f, new Vector3(0.06f, 0.06f, length), rot, metal);
        Cube(parent, "Midrail", mid + Vector3.up * 0.55f, new Vector3(0.04f, 0.04f, length), rot, metal);
    }

    static void Stairs(Transform parent, float x, float zStart, float direction, Material metal)
    {
        const int steps = 14;
        float rise = (8.15f - FloorY) / steps;
        float run = 0.62f;
        var stair = new GameObject(direction > 0f ? "Stair_West" : "Stair_East").transform;
        stair.SetParent(parent, false);
        for (int i = 0; i < steps; i++)
        {
            float y = FloorY + rise * (i + 0.5f);
            float z = zStart + direction * run * i;
            Cube(stair, "Step_" + i, new Vector3(x, y, z), new Vector3(1.35f, 0.1f, 0.5f), Quaternion.identity, metal);
            Cube(stair, "RailL_" + i, new Vector3(x - 0.62f, y + 0.85f, z), new Vector3(0.05f, 0.9f, 0.05f), Quaternion.identity, metal);
            Cube(stair, "RailR_" + i, new Vector3(x + 0.62f, y + 0.85f, z), new Vector3(0.05f, 0.9f, 0.05f), Quaternion.identity, metal);
        }
        Vector3 bottom = new Vector3(x, FloorY + 0.9f, zStart);
        Vector3 top = new Vector3(x, 8.15f + 0.7f, zStart + direction * run * (steps - 1));
        Beam(stair, "SlopeRailL", bottom + Vector3.left * 0.62f, top + Vector3.left * 0.62f, 0.05f, 0.05f, metal);
        Beam(stair, "SlopeRailR", bottom + Vector3.right * 0.62f, top + Vector3.right * 0.62f, 0.05f, 0.05f, metal);
    }

    static void BuildShellDressing(Transform root, Material metal, Material cable, Material concrete, Material screen, Material red, Material hazard)
    {
        var env = new GameObject("Environment").transform;
        env.SetParent(root, false);

        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI * 2f / 16f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * 5.7f, FloorY + 0.02f, CoreZ + Mathf.Sin(angle) * 5.7f);
            Quaternion rot = Quaternion.LookRotation(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            Cube(env, "Hazard_" + i, pos, new Vector3(0.9f, 0.02f, 0.28f), rot, hazard);
        }

        Pipe(env, new Vector3(0f, 13.2f, 50f), Vector3.right, 36f, 0.42f, metal);
        Pipe(env, new Vector3(0f, 12.4f, 66f), Vector3.right, 36f, 0.55f, metal);
        Pipe(env, new Vector3(-12f, 13.6f, CoreZ), Vector3.forward, 34f, 0.38f, cable);
        Pipe(env, new Vector3(12f, 13.0f, CoreZ), Vector3.forward, 34f, 0.5f, metal);
        Pipe(env, new Vector3(-19.4f, 11.5f, CoreZ), Vector3.forward, 32f, 0.7f, metal);
        Pipe(env, new Vector3(19.4f, 10.5f, CoreZ), Vector3.forward, 32f, 0.6f, concrete);

        for (int i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            Pipe(env, new Vector3(-19.6f, 4f + i, 48f), Vector3.up, 8f, 0.18f, cable);
            Pipe(env, new Vector3(19.6f, 4f + i, 70f), Vector3.up, 8f, 0.16f, cable);
        }

        Beam(env, "CeilingTrussA", new Vector3(-18f, 14.6f, 52f), new Vector3(18f, 14.6f, 52f), 0.18f, 0.4f, metal);
        Beam(env, "CeilingTrussB", new Vector3(-18f, 14.6f, 66f), new Vector3(18f, 14.6f, 66f), 0.18f, 0.4f, metal);
        Beam(env, "CeilingTrussC", new Vector3(-10f, 14.7f, 42f), new Vector3(-10f, 14.7f, 76f), 0.16f, 0.3f, metal);
        Beam(env, "CeilingTrussD", new Vector3(10f, 14.7f, 42f), new Vector3(10f, 14.7f, 76f), 0.16f, 0.3f, metal);

        PlaceDoor(env, new Vector3(-19.55f, FloorY, 62f), Quaternion.Euler(0f, 90f, 0f));
        PlaceDoor(env, new Vector3(19.55f, FloorY, 54f), Quaternion.Euler(0f, -90f, 0f));

        WallScreen(env, new Vector3(-19.7f, 3.2f, 70f), Quaternion.Euler(0f, 90f, 0f), screen, false);
        WallScreen(env, new Vector3(19.7f, 3.4f, 48f), Quaternion.Euler(0f, -90f, 0f), red, true);
        WallScreen(env, new Vector3(8f, 4.2f, 78.7f), Quaternion.Euler(0f, 180f, 0f), screen, false);
        Cube(env, "AlarmPanelW", new Vector3(-19.7f, 2.2f, 58f), new Vector3(0.08f, 0.4f, 0.28f), Quaternion.Euler(0f, 90f, 0f), red);
        Cube(env, "AlarmPanelE", new Vector3(19.7f, 2.2f, 60f), new Vector3(0.08f, 0.4f, 0.28f), Quaternion.Euler(0f, -90f, 0f), red);

        GameObject shelf = Spawn(ShelfPath, env, new Vector3(-16.5f, FloorY, 72f), Quaternion.Euler(0f, 20f, 0f), "UtilityShelf");
        if (shelf != null) Sit(shelf, FloorY);
        GameObject cart = Spawn(ShelfPath, env, new Vector3(16.2f, FloorY, 46.5f), Quaternion.Euler(0f, -30f, 0f), "UtilityCart");
        if (cart != null) Sit(cart, FloorY);

        for (int i = 0; i < 6; i++)
        {
            GameObject fixture = Spawn(LightPath, env, new Vector3(-12f + i * 5f, 14.85f, 59f + ((i % 2 == 0) ? 6f : -6f)), Quaternion.Euler(90f, 0f, 0f), "CeilingFixture_" + i);
            if (fixture != null)
                fixture.transform.localScale = Vector3.one * 2.2f;
        }
    }

    static void PlaceDoor(Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject door = Spawn(DoorPath, parent, pos, rot, "AccessDoor");
        if (door != null) Sit(door, FloorY);
    }

    static void WallScreen(Transform parent, Vector3 pos, Quaternion rot, Material mat, bool error)
    {
        GameObject tv = Spawn(TvPath, parent, pos, rot, error ? "WallMonitor_Error" : "WallMonitor");
        var panel = Cube(parent, error ? "WallUI_Error" : "WallUI", pos + rot * Vector3.forward * 0.2f, new Vector3(0.9f, 0.48f, 0.03f), rot, mat);
        if (error)
            AddErrorLabel(panel.transform, panel.transform.position + rot * Vector3.forward * 0.08f, rot, 0.22f);
    }

    static void BuildLights(Transform root, Material cyan, Material red, List<Renderer> pulse)
    {
        var lights = new GameObject("Lights").transform;
        lights.SetParent(root, false);

        Light core = Point(lights, "CorePoint", new Vector3(CoreX, 6.5f, CoreZ), new Color(0.35f, 0.75f, 1f), 12f, 16f);
        core.shadows = LightShadows.Soft;
        Point(lights, "CoreRedPoint", new Vector3(CoreX, 11f, CoreZ), new Color(1f, 0.2f, 0.15f), 4f, 10f);

        Vector3[] spots =
        {
            new Vector3(-8f, 8.0f, 68f),
            new Vector3(8f, 8.0f, 68f),
            new Vector3(-8f, 8.0f, 50f),
            new Vector3(8f, 8.0f, 50f),
            new Vector3(0f, 13.5f, 59f),
            new Vector3(-14f, 12.5f, 59f),
            new Vector3(14f, 12.5f, 59f)
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var go = new GameObject("DownSpot_" + i);
            go.transform.SetParent(lights, false);
            go.transform.position = spots[i];
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = i == 4 ? new Color(0.5f, 0.8f, 1f) : new Color(0.75f, 0.86f, 1f);
            light.intensity = i == 4 ? 10f : 6f;
            light.range = 18f;
            light.spotAngle = i == 4 ? 70f : 48f;
            light.shadows = LightShadows.Soft;
            Sphere(lights, "SpotHousing_" + i, spots[i], 0.16f, i == 4 ? cyan : red);
            if (i == 4)
            {
                Renderer housing = lights.Find("SpotHousing_" + i).GetComponent<Renderer>();
                pulse.Add(housing);
            }
        }
    }

    static void ApplyMood()
    {
        var sun = UnityEngine.Object.FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.color = new Color(0.62f, 0.74f, 0.92f);
            sun.intensity = 0.28f;
            sun.transform.rotation = Quaternion.Euler(58f, 20f, 0f);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.025f, 0.035f, 0.05f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.09f);
        RenderSettings.fogDensity = 0.012f;

        const string volumePath = MatDir + "/SectorC_VolumeProfile.asset";
        var existing = GameObject.Find("SectorC_PostVolume");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var volumeAsset = AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
        if (volumeAsset == null)
        {
            volumeAsset = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(volumeAsset, volumePath);
        }
        Bloom bloom = volumeAsset.TryGet(out Bloom existingBloom) ? existingBloom : volumeAsset.Add<Bloom>();
        bloom.active = true;
        bloom.intensity.Override(0.85f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.65f);
        Vignette vignette = volumeAsset.TryGet(out Vignette existingVignette) ? existingVignette : volumeAsset.Add<Vignette>();
        vignette.active = true;
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.4f);
        EditorUtility.SetDirty(volumeAsset);

        var volumeGo = new GameObject("SectorC_PostVolume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = volumeAsset;
    }

    static Light Point(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        StripCollider(go);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        return go;
    }

    static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        StripCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Sphere(Transform parent, string name, Vector3 pos, float radius, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        StripCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float width, float height, Material mat)
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.05f) return;
        var go = Cube(parent, name, (a + b) * 0.5f, new Vector3(width, height, length), Quaternion.LookRotation(delta.normalized, Vector3.up), mat);
        go.transform.localScale = new Vector3(width, height, length);
    }

    static void Pipe(Transform parent, Vector3 center, Vector3 axis, float length, float radius, Material mat)
    {
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
        Cylinder(parent, "Pipe", center, new Vector3(radius * 2f, length * 0.5f, radius * 2f), rot, mat);
    }

    static GameObject Spawn(string path, Transform parent, Vector3 pos, Quaternion rot, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("Missing prefab " + path);
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        return go;
    }

    static void Sit(GameObject go, float floorY)
    {
        Bounds bounds = BoundsOf(go);
        go.transform.position += Vector3.up * (floorY - bounds.min.y);
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Vector3 Nearest(Vector3 from, List<Vector3> points)
    {
        Vector3 best = from + Vector3.forward;
        float bestDist = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float dist = (points[i] - from).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = points[i];
            }
        }
        return best;
    }

    static void Assign(Transform atrium, string childName, Material mat)
    {
        Transform child = atrium.Find(childName);
        if (child != null) AssignRenderer(child.gameObject, mat);
    }

    static void AssignRenderer(GameObject go, Material mat)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
    }

    static void HideRenderer(Transform t)
    {
        if (t == null) return;
        var renderer = t.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
    }

    static void StripCollider(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    static Material MakeLit(string name, Color baseColor, Color emission, float metallic, float smoothness, bool transparent)
    {
        string path = MatDir + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0.01f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Texture2D SaveTex(string file, Texture2D tex)
    {
        string path = MatDir + "/" + file;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.sRGBTexture = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D MakeFloorTex()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool line = x % 32 == 0 || y % 32 == 0 || x % 32 == 1 || y % 32 == 1;
                bool bolt = (x % 32 == 4 && y % 32 == 4);
                Color color = line ? new Color(0.28f, 0.3f, 0.33f) : new Color(0.07f, 0.075f, 0.085f);
                if (bolt) color = new Color(0.45f, 0.4f, 0.2f);
                pixels[y * size + x] = color;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D MakeHazardTex()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool stripe = ((x + y) / 8) % 2 == 0;
                pixels[y * size + x] = stripe ? new Color(0.95f, 0.78f, 0.05f) : new Color(0.05f, 0.05f, 0.05f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }
}
