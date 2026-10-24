using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds a stormy mountain-exterior blockout around the existing prologue greybox.
/// Interior colliders and dressing are left in place.
/// </summary>
public static class ExteriorMountainBase
{
    const string RootName = "Exterior_Mountain_Base";
    const string MatDir = "Assets/Materials/Exterior";

    static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
    {
        { 'A', "01110,10001,10001,11111,10001,10001,10001" },
        { 'E', "11111,10000,10000,11110,10000,10000,11111" },
        { 'R', "11110,10001,10001,11110,10100,10010,10001" },
        { '2', "11111,00001,00001,01110,10000,10000,11111" },
        { ' ', "00000,00000,00000,00000,00000,00000,00000" }
    };

    [MenuItem("Tools/Prologue/Build Exterior Mountain Base")]
    public static void Build()
    {
        try
        {
            BuildInternal();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    static void BuildInternal()
    {
        EnsureFolder(MatDir);
        Material rock = MakeLit("M_CliffRock", new Color(0.34f, 0.36f, 0.38f), 0.02f, 0.12f);
        rock.SetTexture("_BaseMap", SaveTex("T_Rock.png", RockTex()));
        rock.SetTextureScale("_BaseMap", new Vector2(0.12f, 0.12f));
        Material rockDark = MakeLit("M_Chasm", new Color(0.03f, 0.035f, 0.04f), 0f, 0.05f);
        Material metal = MakeLit("M_ExtMetal", new Color(0.34f, 0.36f, 0.38f), 0.75f, 0.42f);
        Material metalDark = MakeLit("M_Truss", new Color(0.28f, 0.3f, 0.32f), 0.6f, 0.3f);
        Material hatch = MakeLit("M_Hatch", new Color(0.7f, 0.72f, 0.7f), 0.4f, 0.35f);
        Material dome = MakeDome("M_DomeCyan");
        Material warm = MakeLit("M_WarmLamp", new Color(1f, 0.85f, 0.55f), 0.1f, 0.5f);
        warm.EnableKeyword("_EMISSION");
        warm.SetColor("_EmissionColor", new Color(2.2f, 1.3f, 0.4f));
        Material crate = MakeLit("M_Cargo", new Color(0.28f, 0.24f, 0.18f), 0.2f, 0.25f);
        Material tree = MakeLit("M_Pine", new Color(0.05f, 0.1f, 0.07f), 0f, 0.15f);
        Material sign = MakeUnlit("M_Area2", SaveTex("T_Area2.png", Raster("AREA 2")));
        sign.SetTextureScale("_BaseMap", Vector2.one);
        sign.SetTextureOffset("_BaseMap", Vector2.zero);

        var previous = GameObject.Find(RootName);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        var rootGo = new GameObject(RootName);
        Transform root = rootGo.transform;

        BuildCliffs(root, rock, rockDark);
        BuildPlateauDressing(root, metal, hatch, crate, warm);
        BuildBridge(root, metal, metalDark);
        BuildAtriumShell(root, metal, metalDark, dome);
        BuildArea2(root, rock, rockDark, metal, sign, warm);
        BuildTrees(root, tree, metalDark);
        BuildRain(root);
        ApplyMood();
        var grade = GameObject.Find("Exterior_Grade");
        if (grade != null)
            grade.transform.SetParent(root, true);

        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        EditorSceneManager.SaveScene(rootGo.scene);
        Debug.Log("Exterior mountain base saved.");
    }

    static void BuildCliffs(Transform root, Material rock, Material dark)
    {
        var cliffs = Group(root, "Cliffs");
        // Left cave wall. Inner face is near x=-40 and it stops south of the atrium, so the gorge stays open.
        Cube(cliffs, "Cliff_West", new Vector3(-58f, 36f, 82f), new Vector3(36f, 140f, 78f), rock);
        Cube(cliffs, "Cliff_West_Peak", new Vector3(-72f, 92f, 96f), new Vector3(26f, 42f, 34f), Quaternion.Euler(0f, 14f, 7f), rock);
        // Right wall behind the drop-zone ledge. Inner face is near x=60.
        Cube(cliffs, "Cliff_East", new Vector3(80f, 30f, 52f), new Vector3(40f, 120f, 110f), rock);
        Cube(cliffs, "Cliff_East_Peak", new Vector3(96f, 78f, 78f), new Vector3(22f, 36f, 28f), Quaternion.Euler(6f, -12f, -5f), rock);
        // Rock roof over the dome, and the cave wall behind it.
        Cube(cliffs, "Cave_Overhang", new Vector3(-8f, 48f, 90f), new Vector3(62f, 16f, 46f), rock);
        Cube(cliffs, "Cave_Back", new Vector3(-6f, 28f, 122f), new Vector3(78f, 100f, 34f), rock);
        // Shelf the atrium sits on. The chasm under the bridge stays open.
        Cube(cliffs, "Cave_Floor", new Vector3(-8f, -8f, 74f), new Vector3(68f, 16f, 64f), rock);
        // South-east plateau. Top is y = 0.
        Cube(cliffs, "Plateau_DropZone", new Vector3(10f, -4f, -6f), new Vector3(48f, 8f, 36f), rock);
        Cube(cliffs, "Plateau_Nose", new Vector3(18f, -14f, 12f), new Vector3(26f, 20f, 12f), rock);
        Cube(cliffs, "Chasm_Floor", new Vector3(4f, -82f, 28f), new Vector3(100f, 8f, 70f), dark);
        Cube(cliffs, "Foreground_Left", new Vector3(-24f, 2f, -18f), new Vector3(16f, 22f, 14f), Quaternion.Euler(0f, 16f, 0f), rock);
    }

    static void BuildPlateauDressing(Transform root, Material metal, Material hatchMat, Material crate, Material warm)
    {
        var zone = Group(root, "DropZone_Exterior");
        // Open hatch leaves over the existing 2 m ceiling hole at (0, 6.1, 2.5).
        Cube(zone, "Hatch_Left", new Vector3(-0.85f, 7.15f, 2.5f), new Vector3(1.15f, 0.08f, 2.1f), Quaternion.Euler(0f, 0f, 58f), hatchMat);
        Cube(zone, "Hatch_Right", new Vector3(0.85f, 7.15f, 2.5f), new Vector3(1.15f, 0.08f, 2.1f), Quaternion.Euler(0f, 0f, -58f), hatchMat);
        Point(zone, "Hatch_Light", new Vector3(0f, 5.2f, 2.5f), new Color(1f, 0.93f, 0.8f), 14f, 12f);

        Cube(zone, "Cargo_A", new Vector3(6.2f, 0.45f, 1.2f), new Vector3(1.1f, 0.7f, 0.8f), Quaternion.Euler(0f, 12f, 0f), crate);
        Cube(zone, "Cargo_B", new Vector3(7.1f, 0.35f, 2.4f), new Vector3(0.8f, 0.55f, 0.7f), Quaternion.Euler(0f, -8f, 0f), crate);
        Cube(zone, "Cargo_C", new Vector3(5.4f, 1.05f, 1.5f), new Vector3(0.7f, 0.5f, 0.6f), Quaternion.identity, crate);
        Cube(zone, "Cargo_D", new Vector3(8.4f, 0.4f, -1.5f), new Vector3(1.2f, 0.6f, 0.9f), Quaternion.Euler(0f, 20f, 0f), crate);

        var crane = Group(zone, "Crane");
        Cylinder(crane, "Mast", new Vector3(22f, 11f, -1f), new Vector3(0.55f, 11f, 0.55f), Quaternion.identity, metal);
        Cube(crane, "Arm", new Vector3(13f, 21.4f, 0.4f), new Vector3(18f, 0.35f, 0.35f), metal);
        Cube(crane, "Cab", new Vector3(22f, 19.6f, -1f), new Vector3(1.4f, 1.1f, 1.4f), warm);
        Cylinder(crane, "Cable", new Vector3(5.5f, 14f, 1.2f), new Vector3(0.05f, 7f, 0.05f), Quaternion.identity, metal);
    }

    static void BuildBridge(Transform root, Material metal, Material truss)
    {
        var bridge = Group(root, "Chasm_Bridge");
        // Shell sits outside the 4 m interior corridor. Ends stay open.
        Cube(bridge, "Wall_West", new Vector3(-2.85f, 2.05f, 22f), new Vector3(0.35f, 4.3f, 34f), metal);
        Cube(bridge, "Wall_East", new Vector3(2.85f, 2.05f, 22f), new Vector3(0.35f, 4.3f, 34f), metal);
        Cube(bridge, "Roof", new Vector3(0f, 4.35f, 22f), new Vector3(6f, 0.28f, 34f), metal);
        Cube(bridge, "Underside", new Vector3(0f, -0.35f, 22f), new Vector3(5.4f, 0.28f, 34f), metal);

        for (int i = 0; i < 8; i++)
        {
            float z = 8f + i * 4.2f;
            Cylinder(bridge, "Pier_W_" + i, new Vector3(-2.3f, -16f, z), new Vector3(0.28f, 16f, 0.28f), Quaternion.identity, truss);
            Cylinder(bridge, "Pier_E_" + i, new Vector3(2.3f, -16f, z), new Vector3(0.28f, 16f, 0.28f), Quaternion.identity, truss);
            Cube(bridge, "Cross_" + i, new Vector3(0f, -4f, z), new Vector3(5f, 0.16f, 0.16f), truss);
            Cube(bridge, "CrossLow_" + i, new Vector3(0f, -12f, z), new Vector3(5f, 0.14f, 0.14f), truss);
            float next = z + 4.2f;
            if (i < 7)
            {
                Brace(bridge, "DiagA_" + i, new Vector3(-2.3f, -2f, z), new Vector3(2.3f, -14f, next), truss);
                Brace(bridge, "DiagB_" + i, new Vector3(2.3f, -2f, z), new Vector3(-2.3f, -14f, next), truss);
            }
        }
    }

    static void BuildAtriumShell(Transform root, Material metal, Material truss, Material domeMat)
    {
        var shell = Group(root, "Atrium_Exterior");
        Vector3 center = new Vector3(0f, 9f, 64f);
        const float radius = 30f;
        const int sides = 8;
        float chord = 2f * radius * Mathf.Tan(Mathf.PI / sides);

        for (int i = 0; i < sides; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI * 2f / sides;
            var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            // Leave the south face open so the bridge can enter the atrium.
            if (dir.z < -0.4f)
                continue;
            Cube(shell, "Octagon_" + i, center + dir * radius, new Vector3(chord * 1.04f, 18f, 1.6f), Quaternion.LookRotation(dir), metal);
        }

        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "Glass_Dome";
        dome.transform.SetParent(shell, true);
        dome.transform.position = new Vector3(0f, 24f, 64f);
        dome.transform.localScale = new Vector3(26f, 14f, 26f);
        Strip(dome);
        dome.GetComponent<Renderer>().sharedMaterial = domeMat;

        Point(shell, "Dome_CoreLight", new Vector3(0f, 20f, 64f), new Color(0.25f, 0.75f, 1f), 40f, 55f);

        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            if (dir.z < -0.35f)
                continue;
            Vector3 basePos = new Vector3(0f, 3f, 64f) + dir * 32.5f;
            Cylinder(shell, "ExteriorPipe_" + i, basePos + Vector3.up * 6f, new Vector3(0.7f, 7f, 0.7f), Quaternion.identity, truss);
            Cube(shell, "Scaffold_" + i, basePos + new Vector3(0f, 8f, 0f), new Vector3(0.25f, 14f, 2.4f), Quaternion.LookRotation(dir), truss);
        }

        Cube(shell, "PipeRun_South", new Vector3(0f, 6f, 46f), new Vector3(1.1f, 1.1f, 16f), metal);
    }

    static void BuildArea2(Transform root, Material rock, Material dark, Material metal, Material sign, Material warm)
    {
        var area = Group(root, "Area2_Tunnel");
        // Southeast of the atrium, in front of the east wall, so the hero view sees the mouth.
        Vector3 mouth = new Vector3(26f, 4.2f, 40f);
        Vector3 outDir = new Vector3(-0.72f, 0f, -0.69f).normalized;
        var aim = new GameObject("MouthAim");
        aim.transform.SetParent(area, false);
        aim.transform.SetPositionAndRotation(mouth, Quaternion.LookRotation(outDir));
        Vector3 right = aim.transform.right;
        Vector3 into = -outDir;
        Quaternion frame = aim.transform.rotation;

        Cube(area, "Tunnel_Pad", mouth + into * 3f + Vector3.up * -2.3f, new Vector3(14f, 1.1f, 16f), frame, rock);
        Cube(area, "Cave_Lintel", mouth + Vector3.up * 3.7f, new Vector3(12f, 1.5f, 2.4f), frame, rock);
        Cube(area, "Cave_Left", mouth + right * -5.3f + into * 2.2f + Vector3.up * 1.2f, new Vector3(1.8f, 6.4f, 8f), frame, rock);
        Cube(area, "Cave_Right", mouth + right * 5.3f + into * 2.2f + Vector3.up * 1.2f, new Vector3(1.8f, 6.4f, 8f), frame, rock);
        Cube(area, "Tunnel_Back", mouth + into * 8f + Vector3.up * 1.3f, new Vector3(8.5f, 6.2f, 9f), frame, dark);
        Cube(area, "Tunnel_Roof", mouth + into * 5f + Vector3.up * 4.5f, new Vector3(8.5f, 1.1f, 8f), frame, rock);

        Cube(area, "Rail_Left", mouth + right * -0.7f + into * 4f + Vector3.up * -1.7f, new Vector3(0.1f, 0.06f, 12f), frame, metal);
        Cube(area, "Rail_Right", mouth + right * 0.7f + into * 4f + Vector3.up * -1.7f, new Vector3(0.1f, 0.06f, 12f), frame, metal);
        for (int i = 0; i < 7; i++)
            Cube(area, "Sleeper_" + i, mouth + into * (i * 1.7f) + Vector3.up * -1.8f, new Vector3(2.2f, 0.06f, 0.32f), frame, metal);

        // Same thin-cube orientation that reads correctly on the corridor wall. +X faces the camera.
        Cube(area, "Sign_Area2", mouth + Vector3.up * 5.15f + outDir * 0.55f, new Vector3(0.08f, 1.25f, 4.2f), Quaternion.FromToRotation(Vector3.right, outDir), sign);

        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("TunnelSpot_" + i);
            go.transform.SetParent(area, false);
            go.transform.position = mouth + right * (-1.7f + i * 1.7f) + Vector3.up * 2.5f + into * 0.4f;
            go.transform.rotation = Quaternion.LookRotation(outDir);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.72f, 0.35f);
            light.intensity = 16f;
            light.range = 28f;
            light.spotAngle = 56f;
            light.shadows = LightShadows.None;
            Cube(area, "TunnelLamp_" + i, go.transform.position, new Vector3(0.5f, 0.28f, 0.7f), frame, warm);
        }
    }

    static void BuildTrees(Transform root, Material pine, Material trunk)
    {
        var trees = Group(root, "Trees");
        Vector3[] spots =
        {
            new Vector3(-12f, 0f, -8f),
            new Vector3(2f, 0f, -14f),
            new Vector3(20f, 0f, -10f),
            new Vector3(24f, 0f, 2f),
            new Vector3(-58f, 106f, 78f),
            new Vector3(80f, 90f, 48f)
        };
        for (int i = 0; i < spots.Length; i++)
        {
            float h = 4f + (i % 3);
            Cylinder(trees, "Trunk_" + i, spots[i] + Vector3.up * (h * 0.35f), new Vector3(0.35f, h * 0.35f, 0.35f), Quaternion.identity, trunk);
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown_" + i;
            crown.transform.SetParent(trees, true);
            crown.transform.position = spots[i] + Vector3.up * (h * 0.85f);
            crown.transform.localScale = new Vector3(2.2f, h, 2.2f);
            Strip(crown);
            crown.GetComponent<Renderer>().sharedMaterial = pine;
        }
    }

    static void BuildRain(Transform root)
    {
        var go = new GameObject("Heavy_Rain");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(0f, 48f, 40f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 6f;
        main.startLifetime = 1.35f;
        main.startSpeed = 0f;
        main.startSize = 0.045f;
        main.startColor = new Color(0.82f, 0.88f, 0.95f, 0.18f);
        main.maxParticles = 7000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        var emission = ps.emission;
        emission.rateOverTime = 1400f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(110f, 1f, 140f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 1.5f;
        velocity.y = -55f;
        velocity.z = 0.4f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 4f;
        renderer.velocityScale = 0.15f;
        renderer.material = RainMaterial();

        ps.Simulate(1.2f, true, true);
        ps.Play(true);
    }

    static void ApplyMood()
    {
        var sky = Shader.Find("Skybox/Procedural");
        if (sky != null)
        {
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_StormSky.mat");
            if (skyMat == null)
            {
                skyMat = new Material(sky);
                AssetDatabase.CreateAsset(skyMat, MatDir + "/M_StormSky.mat");
            }
            skyMat.shader = sky;
            skyMat.SetColor("_SkyTint", new Color(0.025f, 0.035f, 0.05f));
            skyMat.SetColor("_GroundColor", new Color(0.008f, 0.01f, 0.014f));
            skyMat.SetFloat("_Exposure", 0.07f);
            skyMat.SetFloat("_AtmosphereThickness", 0.22f);
            skyMat.SetFloat("_SunSize", 0f);
            RenderSettings.skybox = skyMat;
            EditorUtility.SetDirty(skyMat);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.028f, 0.034f, 0.046f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.065f, 0.085f);
        RenderSettings.fogDensity = 0.013f;

        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional)
                continue;
            light.color = new Color(0.5f, 0.6f, 0.78f);
            light.intensity = 0.28f;
            light.transform.rotation = Quaternion.Euler(24f, 150f, 0f);
            break;
        }

        const string profilePath = MatDir + "/ExteriorVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        Bloom bloom = profile.TryGet(out Bloom existingBloom) ? existingBloom : profile.Add<Bloom>();
        bloom.active = true;
        bloom.intensity.Override(1.15f);
        bloom.threshold.Override(0.85f);
        bloom.scatter.Override(0.7f);

        ColorAdjustments grade = profile.TryGet(out ColorAdjustments existingGrade) ? existingGrade : profile.Add<ColorAdjustments>();
        grade.active = true;
        grade.postExposure.Override(-0.15f);
        grade.contrast.Override(28f);
        grade.colorFilter.Override(new Color(0.72f, 0.82f, 1f));
        grade.saturation.Override(-12f);
        EditorUtility.SetDirty(profile);

        var volumeGo = GameObject.Find("Exterior_Grade");
        if (volumeGo == null)
            volumeGo = new GameObject("Exterior_Grade");
        var volume = volumeGo.GetComponent<Volume>();
        if (volume == null)
            volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;
        volume.sharedProfile = profile;
    }

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        Cube(parent, name, pos, scale, Quaternion.identity, mat);
    }

    static void Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        Strip(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        Strip(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void Brace(Transform parent, string name, Vector3 a, Vector3 b, Material mat)
    {
        Vector3 delta = b - a;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        go.transform.localScale = new Vector3(0.14f, delta.magnitude * 0.5f, 0.14f);
        Strip(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void Point(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
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
    }

    static void Strip(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
    }

    static Material MakeLit(string name, Color color, float metallic, float smoothness)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeDome(string name)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", new Color(0.15f, 0.55f, 0.85f, 0.45f));
        mat.SetFloat("_Metallic", 0.15f);
        mat.SetFloat("_Smoothness", 0.85f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0f, 3.2f, 5f));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeUnlit(string name, Texture2D tex)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material RainMaterial()
    {
        string path = MatDir + "/M_Rain.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f, 0.55f));
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
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

    static Texture2D RockTex()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                float crack = (x + y * 3) % 37 == 0 ? 0.45f : 1f;
                float shade = Mathf.Lerp(0.35f, 0.85f, n) * crack;
                pixels[y * size + x] = new Color(shade, shade, shade * 1.02f, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D Raster(string text)
    {
        const int scale = 10;
        const int gap = 8;
        int gw = 5 * scale;
        int gh = 7 * scale;
        int width = text.Length * (gw + gap) + gap;
        int height = gh + gap * 2;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        var plate = new Color(0.08f, 0.08f, 0.09f, 1f);
        var ink = new Color(0.95f, 0.9f, 0.75f, 1f);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = plate;
        for (int i = 0; i < text.Length; i++)
        {
            if (!Glyphs.TryGetValue(text[i], out string pattern))
                continue;
            string[] rows = pattern.Split(',');
            int originX = gap + i * (gw + gap);
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    if (rows[row][col] != '1')
                        continue;
                    for (int dy = 0; dy < scale; dy++)
                    {
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int x = originX + col * scale + dx;
                            int y = gap + (6 - row) * scale + dy;
                            pixels[y * width + x] = ink;
                        }
                    }
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }
}
