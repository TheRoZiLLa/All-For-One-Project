using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Recorded.Editor
{
    public static class MovementSetup
    {
        private const string Root = "Assets/Recorded";
        public const string PrefabPath = Root + "/Prefabs/Player.prefab";
        public const string ScenePath = Root + "/Scenes/MovementSandbox.unity";
        public const string CameraProfilePath = Root + "/Settings/PlayerCameraProfile.asset";

        [MenuItem("RECORDED/Create Movement Assets")]
        public static void CreateAssets()
        {
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Movement assets already exist. Existing assets were preserved.");
            const string inputPath = Root + "/Input/RecordedInput.inputactions";
            Directory.CreateDirectory(Root + "/Input");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");
            if (!File.Exists(inputPath))
                File.WriteAllText(inputPath, File.ReadAllText("Assets/InputSystem_Actions.inputactions")
                    .Replace("<Keyboard>/c\"", "<Keyboard>/leftCtrl\""));
            AssetDatabase.ImportAsset(inputPath);
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(inputPath);
            if (actions == null) throw new InvalidOperationException("Could not import RecordedInput.inputactions.");

            Scene previous = SceneManager.GetActiveScene();
            Scene sandbox = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(sandbox);
                var player = new GameObject("Player");
                player.AddComponent<CharacterController>();
                var input = player.AddComponent<PlayerInputReader>();
                var look = player.AddComponent<FirstPersonLook>();
                var motor = player.AddComponent<FirstPersonMotor>();
                var pivot = new GameObject("CameraPivot").transform;
                pivot.SetParent(player.transform, false);
                pivot.localPosition = new Vector3(0f, 1.65f, 0f);
                var cameraObject = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(pivot, false);
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.nearClipPlane = 0.03f;
                camera.fieldOfView = 75f;
                SetReference(input, "inputActions", actions);
                SetReference(look, "input", input);
                SetReference(look, "cameraPivot", pivot);
                SetReference(motor, "input", input);
                SetReference(motor, "cameraPivot", pivot);
                var capsule = player.GetComponent<CharacterController>();
                capsule.height = 1.8f;
                capsule.radius = 0.3f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.skinWidth = 0.03f;
                capsule.stepOffset = 0.3f;
                capsule.slopeLimit = 50f;
                capsule.minMoveDistance = 0f;
                ConfigureCameraEffects(player);
                var prefab = PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
                UnityEngine.Object.DestroyImmediate(player);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sandbox);
                instance.transform.position = new Vector3(0f, 0.05f, -6f);

                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                var floor = MakeMaterial("Floor", new Color(0.22f, 0.26f, 0.3f), shader);
                var obstacle = MakeMaterial("Obstacles", new Color(0.15f, 0.65f, 0.7f), shader);
                Box("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 24f), floor);
                Box("Wall", new Vector3(-5f, 1.5f, 0f), new Vector3(0.5f, 3f, 8f), obstacle);
                Box("Low ceiling - crouch", new Vector3(0f, 1.65f, 2f), new Vector3(3f, 0.7f, 4f), obstacle);
                var ramp = Box("Ramp - 25 degrees", new Vector3(5f, 0.65f, 0f), new Vector3(3f, 0.3f, 4f), obstacle);
                ramp.transform.rotation = Quaternion.Euler(-25f, 0f, 0f);
                for (int i = 0; i < 6; i++)
                    Box("Step " + (i + 1), new Vector3(5f, (i + 1) * 0.1f, 4f + i * 0.5f),
                        new Vector3(3f, (i + 1) * 0.2f, 0.5f), obstacle);
                Box("Ledge", new Vector3(-2f, 0.5f, 7f), new Vector3(2f, 1f, 3f), obstacle);
                var lightObject = new GameObject("Directional Light", typeof(Light));
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2f;
                light.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
                if (!EditorSceneManager.SaveScene(sandbox, ScenePath))
                    throw new InvalidOperationException("Could not save the movement sandbox.");
                AssetDatabase.SaveAssets();
                Debug.Log("RECORDED movement ready: " + PrefabPath + " and " + ScenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(sandbox, true);
            }
        }

        public static void ConfigureCameraEffects(GameObject player)
        {
            var look = new SerializedObject(player.GetComponent<FirstPersonLook>());
            look.FindProperty("enableBob").boolValue = true;
            look.FindProperty("enableTilt").boolValue = true;
            look.ApplyModifiedPropertiesWithoutUndo();
            var camera = player.GetComponentInChildren<Camera>();
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1;
            if (!AssetDatabase.IsValidFolder(Root + "/Settings")) AssetDatabase.CreateFolder(Root, "Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CameraProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, CameraProfilePath);
            }
            if (!profile.TryGet<MotionBlur>(out var blur))
            {
                blur = profile.Add<MotionBlur>(true);
                AssetDatabase.AddObjectToAsset(blur, profile);
            }
            blur.active = true;
            blur.mode.Override(MotionBlurMode.CameraOnly);
            blur.quality.Override(MotionBlurQuality.Medium);
            blur.intensity.Override(0.12f);
            blur.clamp.Override(0.025f);
            EditorUtility.SetDirty(blur);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            var effects = player.transform.Find("Camera Effects");
            if (effects == null)
            {
                effects = new GameObject("Camera Effects", typeof(Volume)).transform;
                effects.SetParent(player.transform, false);
            }
            var volume = effects.GetComponent<Volume>();
            if (volume == null) volume = effects.gameObject.AddComponent<Volume>();
            effects.gameObject.layer = 0;
            volume.enabled = true;
            volume.weight = 1f;
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            Debug.Assert(blur.IsActive() && data.renderPostProcessing && volume.sharedProfile == profile,
                "Player camera post-processing configuration is incomplete.");
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material MakeMaterial(string name, Color color, Shader shader)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetPositionAndRotation(position, Quaternion.identity);
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
    }
}
