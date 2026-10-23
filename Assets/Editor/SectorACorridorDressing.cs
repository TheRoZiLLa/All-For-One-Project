using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dresses the Sector A greybox as a lit sci-fi corridor.
/// Floor, wall, ceiling, debris, and ambush colliders stay in place.
/// </summary>
public static class SectorACorridorDressing
{
    const string RootName = "SectorA_VisualDressing";
    const string MatDir = "Assets/Materials/SectorA";

    static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
    {
        { 'A', "01110,10001,10001,11111,10001,10001,10001" },
        { 'B', "11110,10001,10001,11110,10001,10001,11110" },
        { 'C', "01111,10000,10000,10000,10000,10000,01111" },
        { 'D', "11110,10001,10001,10001,10001,10001,11110" },
        { 'E', "11111,10000,10000,11110,10000,10000,11111" },
        { 'H', "10001,10001,10001,11111,10001,10001,10001" },
        { 'I', "11111,00100,00100,00100,00100,00100,11111" },
        { 'L', "10000,10000,10000,10000,10000,10000,11111" },
        { 'N', "10001,11001,10101,10011,10001,10001,10001" },
        { 'O', "01110,10001,10001,10001,10001,10001,01110" },
        { 'P', "11110,10001,10001,11110,10000,10000,10000" },
        { 'R', "11110,10001,10001,11110,10100,10010,10001" },
        { 'S', "01111,10000,10000,01110,00001,00001,11110" },
        { 'T', "11111,00100,00100,00100,00100,00100,00100" },
        { 'U', "10001,10001,10001,10001,10001,10001,01110" },
        { 'V', "10001,10001,10001,10001,10001,01010,00100" },
        { 'Y', "10001,10001,01010,00100,00100,00100,00100" },
        { '-', "00000,00000,00000,11111,00000,00000,00000" },
        { ' ', "00000,00000,00000,00000,00000,00000,00000" }
    };

    [MenuItem("Tools/Prologue/Dress Sector A Corridor")]
    public static void Dress()
    {
        try
        {
            DressInternal();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    static void DressInternal()
    {
        var corridor = GameObject.Find("SectorA_UncannyCorridor");
        if (corridor == null)
        {
            Debug.LogError("SectorA_UncannyCorridor was not found.");
            return;
        }

        EnsureFolder(MatDir);
        Material panel = MakeLit("M_WhitePanel", new Color(0.78f, 0.81f, 0.84f), Color.black, 0.62f, 0.84f);
        panel.SetTexture("_BaseMap", SaveTex("T_WallPanel.png", PanelTex(new Color(0.82f, 0.84f, 0.87f), new Color(0.62f, 0.66f, 0.7f))));
        Material floor = MakeLit("M_WhiteFloor", new Color(0.84f, 0.86f, 0.88f), Color.black, 0.45f, 0.78f);
        floor.SetTexture("_BaseMap", SaveTex("T_FloorPanel.png", PanelTex(new Color(0.9f, 0.91f, 0.93f), new Color(0.7f, 0.73f, 0.76f))));
        floor.SetTextureScale("_BaseMap", new Vector2(2f, 8f));
        panel.SetTextureScale("_BaseMap", new Vector2(2f, 10f));

        Material led = MakeLit("M_LedStrip", new Color(0.55f, 0.85f, 1f), new Color(0.45f, 1.5f, 2.4f), 0.1f, 0.9f);
        Material ceilingLight = MakeLit("M_CeilingLight", Color.white, new Color(1.4f, 1.65f, 1.9f), 0f, 0.7f);
        Material cable = MakeLit("M_CableBlack", new Color(0.02f, 0.02f, 0.025f), Color.black, 0.25f, 0.45f);
        Material pipe = MakeLit("M_PipeMetal", new Color(0.18f, 0.2f, 0.22f), Color.black, 0.88f, 0.72f);
        Material cardboard = MakeLit("M_Cardboard", new Color(0.72f, 0.55f, 0.36f), new Color(0.08f, 0.05f, 0.02f), 0.02f, 0.18f);
        Material crate = MakeLit("M_SupplyCrate", new Color(0.45f, 0.48f, 0.52f), Color.black, 0.75f, 0.55f);
        Material cyan = MakeLit("M_GlitchCyan", new Color(0.05f, 0.35f, 0.4f), new Color(0f, 2.4f, 4f), 0.2f, 0.7f);
        Material red = MakeLit("M_GlitchRed", new Color(0.35f, 0.04f, 0.06f), new Color(3.2f, 0.15f, 0.25f), 0.2f, 0.65f);
        Material ink = MakeUnlit("M_WallInk", SaveTex("T_SectorTitle.png", Raster("SECTOR A - THE UNCANNY CORRIDOR")));
        Material labs = MakeUnlit("M_VesperInk", SaveTex("T_VesperLabs.png", Raster("VESPER LABS")));
        Material hologram = HologramMaterial();

        Transform existing = corridor.transform.Find(RootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        PaintShell(corridor.transform, floor, panel);
        HideVisualKeepCollider(GameObject.Find("Destructible_Wall_Ambush"));
        var debrisRoot = GameObject.Find("Puzzle_Red_Debris");
        if (debrisRoot != null)
        {
            foreach (var renderer in debrisRoot.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        var dressing = new GameObject(RootName);
        dressing.transform.SetParent(corridor.transform, false);
        Transform root = dressing.transform;

        BuildDebris(root, cardboard, crate, pipe);
        BuildLighting(root, led, ceilingLight, cable);
        BuildLeftWall(root, cable, pipe);
        BuildSignage(root, ink, labs);
        BuildGlitch(root, cyan, red, pipe);
        BuildDrone(root, pipe, cyan, red);
        BuildEcho(root, hologram);

        var fogColor = new Color(0.045f, 0.07f, 0.11f);
        if (!RenderSettings.fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.01f;
        }
        RenderSettings.fogColor = fogColor;

        EditorSceneManager.MarkSceneDirty(corridor.scene);
        EditorSceneManager.SaveScene(corridor.scene);
        Debug.Log("Sector A corridor dressing saved.");
    }

    static void PaintShell(Transform corridor, Material floor, Material panel)
    {
        Assign(corridor, "SectorA_Floor_South", floor);
        Assign(corridor, "SectorA_Floor_North", floor);
        Assign(corridor, "SectorA_Wall_West", panel);
        Assign(corridor, "SectorA_Wall_East_South", panel);
        Assign(corridor, "SectorA_Wall_East_North", panel);
        Assign(corridor, "SectorA_Ceiling", panel);
    }

    static void BuildDebris(Transform root, Material cardboard, Material crate, Material pipeMat)
    {
        var pile = new GameObject("EntranceClutter").transform;
        pile.SetParent(root, false);

        Box(pile, "Cardboard_A", new Vector3(-0.55f, 0.48f, 4.62f), new Vector3(0.72f, 0.42f, 0.55f), 14f, cardboard);
        Box(pile, "Cardboard_B", new Vector3(0.2f, 0.42f, 4.78f), new Vector3(0.86f, 0.38f, 0.58f), -10f, cardboard);
        Box(pile, "Cardboard_C", new Vector3(0.28f, 0.82f, 4.72f), new Vector3(0.48f, 0.36f, 0.42f), 8f, cardboard);
        Box(pile, "Cardboard_D", new Vector3(-0.15f, 0.78f, 4.95f), new Vector3(0.5f, 0.28f, 0.4f), -18f, cardboard);
        Box(pile, "Crate_A", new Vector3(0.72f, 0.4f, 5.15f), new Vector3(0.58f, 0.42f, 0.5f), 20f, crate);
        Box(pile, "Crate_B", new Vector3(-0.85f, 0.36f, 5.05f), new Vector3(0.52f, 0.38f, 0.48f), -6f, crate);
        Box(pile, "Crate_C", new Vector3(-0.35f, 0.72f, 5.18f), new Vector3(0.46f, 0.32f, 0.4f), 16f, crate);

        Pipe(pile, "Pipe_Large", new Vector3(-0.95f, 0.32f, 4.48f), new Vector3(0.15f, 0.38f, 5.05f), 0.16f, pipeMat);
        Pipe(pile, "Pipe_Elbow", new Vector3(0.15f, 0.28f, 5.05f), new Vector3(0.85f, 0.42f, 5.35f), 0.11f, pipeMat);
        Pipe(pile, "Pipe_Small", new Vector3(-0.4f, 0.22f, 4.55f), new Vector3(0.45f, 0.24f, 4.95f), 0.07f, pipeMat);
        Point(pile, "DoorwayFill", new Vector3(0f, 1.3f, 3.6f), new Color(0.85f, 0.9f, 1f), 4f, 7f);
    }

    static void BuildLighting(Transform root, Material led, Material fixture, Material cable)
    {
        var lights = new GameObject("CorridorLighting").transform;
        lights.SetParent(root, false);

        Cube(lights, "Led_WestCeiling", new Vector3(-1.9f, 3.4f, 20f), new Vector3(0.05f, 0.035f, 29.2f), Quaternion.identity, led);
        Cube(lights, "Led_EastCeiling", new Vector3(1.9f, 3.4f, 20f), new Vector3(0.05f, 0.035f, 29.2f), Quaternion.identity, led);
        Cube(lights, "Led_WestFloor", new Vector3(-1.9f, 0.28f, 20f), new Vector3(0.04f, 0.025f, 29.2f), Quaternion.identity, led);
        Cube(lights, "Led_EastFloor", new Vector3(1.9f, 0.28f, 20f), new Vector3(0.04f, 0.025f, 29.2f), Quaternion.identity, led);

        for (int i = 0; i < 9; i++)
        {
            float z = 7.2f + i * 3.15f;
            Cube(lights, "RecessFrame_" + i, new Vector3(0f, 3.5f, z), new Vector3(0.72f, 0.02f, 1.7f), Quaternion.identity, cable);
            Cube(lights, "RecessLight_" + i, new Vector3(0f, 3.49f, z), new Vector3(0.55f, 0.015f, 1.45f), Quaternion.identity, fixture);
            if (i % 2 == 0)
            {
                var go = new GameObject("CeilingPoint_" + i);
                go.transform.SetParent(lights, false);
                go.transform.position = new Vector3(0f, 3.3f, z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.72f, 0.88f, 1f);
                light.intensity = 2.4f;
                light.range = 6.5f;
                light.shadows = LightShadows.None;
            }
        }
    }

    static void BuildLeftWall(Transform root, Material cable, Material pipeMat)
    {
        var wall = new GameObject("LeftWallConduits").transform;
        wall.SetParent(root, false);
        float x = -1.9f;
        Pipe(wall, "Conduit_Main", new Vector3(x, 2.55f, 8f), new Vector3(x, 2.55f, 33.5f), 0.09f, pipeMat);
        Pipe(wall, "Cable_A", new Vector3(x - 0.02f, 2.78f, 7f), new Vector3(x - 0.02f, 2.72f, 34f), 0.035f, cable);
        Pipe(wall, "Cable_B", new Vector3(x, 2.92f, 6.5f), new Vector3(x, 2.96f, 33f), 0.03f, cable);
        Pipe(wall, "Cable_C", new Vector3(x + 0.03f, 3.08f, 9f), new Vector3(x + 0.01f, 3.02f, 32f), 0.028f, cable);
        Pipe(wall, "Cable_D", new Vector3(x, 3.2f, 8.5f), new Vector3(x - 0.02f, 3.16f, 31f), 0.022f, cable);
        Pipe(wall, "Cable_Sag", new Vector3(x + 0.08f, 2.7f, 16f), new Vector3(x + 0.16f, 2.35f, 18.5f), 0.03f, cable);
        Pipe(wall, "Cable_SagReturn", new Vector3(x + 0.16f, 2.35f, 18.5f), new Vector3(x + 0.04f, 2.75f, 21f), 0.03f, cable);
    }

    static void BuildSignage(Transform root, Material title, Material labs)
    {
        var signs = new GameObject("Signage").transform;
        signs.SetParent(root, false);
        // Quad faces +X. Negative X scale un-mirrors the label for a viewer looking at the west wall.
        // Thin cubes, not quads: a Unity quad faces +Z and disappears when turned onto the wall.
        Cube(signs, "Sign_SectorA", new Vector3(-1.88f, 2.05f, 14.2f), new Vector3(0.03f, 0.55f, 8.2f), Quaternion.identity, title);
        Cube(signs, "Sign_VesperLabs", new Vector3(-1.88f, 1.25f, 8.2f), new Vector3(0.03f, 0.48f, 3.1f), Quaternion.identity, labs);
    }

    static void BuildGlitch(Transform root, Material cyan, Material red, Material metal)
    {
        var glitch = new GameObject("GlitchBreach").transform;
        glitch.SetParent(root, false);
        for (int i = 0; i < 42; i++)
        {
            float n1 = Hash(i * 3 + 1);
            float n2 = Hash(i * 3 + 2);
            float n3 = Hash(i * 3 + 3);
            float size = Mathf.Lerp(0.08f, 0.32f, n1);
            var pos = new Vector3(
                Mathf.Lerp(1.85f, 0.55f, n2),
                Mathf.Lerp(1.05f, 2.55f, n3),
                Mathf.Lerp(19.15f, 20.85f, Hash(i * 5 + 4)));
            Material mat = i % 5 == 0 ? metal : (i % 2 == 0 ? cyan : red);
            var cube = Cube(glitch, "Voxel_" + i, pos, Vector3.one * size, Quaternion.Euler(0f, n1 * 40f, n3 * 25f), mat);
            cube.transform.localScale = new Vector3(size * Mathf.Lerp(0.6f, 1.3f, n1), size, size * Mathf.Lerp(0.7f, 1.4f, n3));
        }

        Point(glitch, "GlitchCyanLight", new Vector3(1.35f, 2.05f, 20f), new Color(0.2f, 0.85f, 1f), 5.5f, 4.5f);
        Point(glitch, "GlitchRedLight", new Vector3(1.55f, 1.55f, 20.35f), new Color(1f, 0.18f, 0.22f), 3.8f, 3.5f);
    }

    static void BuildDrone(Transform root, Material metal, Material cyan, Material red)
    {
        var drone = new GameObject("Security_Drone").transform;
        drone.SetParent(root, false);
        drone.position = new Vector3(0.85f, 2.35f, 18.4f);
        drone.rotation = Quaternion.LookRotation(new Vector3(1f, -0.15f, 0.85f));
        Cube(drone, "Body", drone.position, new Vector3(0.55f, 0.12f, 0.32f), drone.rotation, metal);
        Cylinder(drone, "RotorL", drone.position + drone.right * -0.18f + Vector3.up * 0.05f, new Vector3(0.16f, 0.008f, 0.16f), Quaternion.identity, metal);
        Cylinder(drone, "RotorR", drone.position + drone.right * 0.18f + Vector3.up * 0.05f, new Vector3(0.16f, 0.008f, 0.16f), Quaternion.identity, metal);
        Cube(drone, "Lens", drone.position + drone.forward * 0.12f, new Vector3(0.06f, 0.04f, 0.04f), drone.rotation, red);
        Point(drone, "DroneLight", drone.position + Vector3.down * 0.08f, new Color(0.5f, 0.85f, 1f), 1.4f, 2.2f);
    }

    static void BuildEcho(Transform root, Material hologram)
    {
        var echo = new GameObject("FrozenEcho_Gate").transform;
        echo.SetParent(root, false);
        echo.position = new Vector3(0.15f, 0.22f, 33.1f);
        echo.rotation = Quaternion.LookRotation(Vector3.back);

        Part(echo, "Pelvis", new Vector3(0f, 0.95f, 0f), new Vector3(0.32f, 0.16f, 0.16f), hologram);
        Part(echo, "Chest", new Vector3(0f, 1.28f, 0f), new Vector3(0.38f, 0.4f, 0.16f), hologram);
        Part(echo, "Head", new Vector3(0f, 1.7f, 0f), new Vector3(0.18f, 0.22f, 0.18f), hologram);
        Part(echo, "ArmL", new Vector3(-0.26f, 1.25f, 0f), new Vector3(0.08f, 0.55f, 0.08f), hologram);
        Part(echo, "ArmR", new Vector3(0.26f, 1.25f, 0f), new Vector3(0.08f, 0.55f, 0.08f), hologram);
        Part(echo, "LegL", new Vector3(-0.1f, 0.48f, 0f), new Vector3(0.09f, 0.7f, 0.09f), hologram);
        Part(echo, "LegR", new Vector3(0.1f, 0.48f, 0f), new Vector3(0.09f, 0.7f, 0.09f), hologram);
    }

    static void Part(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = Cube(parent, name, parent.position, scale, parent.rotation, mat);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void Box(Transform parent, string name, Vector3 pos, Vector3 scale, float yaw, Material mat)
    {
        Cube(parent, name, pos, scale, Quaternion.Euler(0f, yaw, 0f), mat);
    }

    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        Strip(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Quad(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = scale;
        Strip(go);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
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

    static void Pipe(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material mat)
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.05f) return;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        Cylinder(parent, name, (a + b) * 0.5f, new Vector3(radius * 2f, length * 0.5f, radius * 2f), rot, mat);
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

    static Material HologramMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SectorC/M_Hologram.mat");
        if (existing != null) return existing;
        var shader = Shader.Find("Prologue/HologramEcho");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        string path = MatDir + "/M_Hologram.mat";
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void Assign(Transform parent, string childName, Material mat)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;
        var renderer = child.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
    }

    static void HideVisualKeepCollider(GameObject go)
    {
        if (go == null) return;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
    }

    static void Strip(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    static float Hash(int index)
    {
        float value = Mathf.Sin(index * 12.9898f) * 43758.55f;
        return value - Mathf.Floor(value);
    }

    static Material MakeLit(string name, Color baseColor, Color emission, float metallic, float smoothness)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0.01f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
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
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 2000;
        mat.SetTextureScale("_BaseMap", Vector2.one);
        mat.SetTextureOffset("_BaseMap", Vector2.zero);
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
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D PanelTex(Color fill, Color line)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool groove = x % 64 == 0 || y % 64 == 0 || x % 64 == 1 || y % 64 == 1;
                pixels[y * size + x] = groove ? line : fill;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D Raster(string text)
    {
        const int scale = 8;
        const int gap = 6;
        int gw = 5 * scale;
        int gh = 7 * scale;
        int width = text.Length * (gw + gap) + gap;
        int height = gh + gap * 2;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        var plate = new Color(0.1f, 0.12f, 0.14f, 1f);
        var ink = new Color(0.9f, 0.95f, 1f, 1f);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = plate;

        for (int i = 0; i < text.Length; i++)
        {
            if (!Glyphs.TryGetValue(text[i], out string pattern)) continue;
            string[] rows = pattern.Split(',');
            int originX = gap + i * (gw + gap);
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    if (rows[row][col] != '1') continue;
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
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
    }
}
