using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Recorded.Testing;

namespace Recorded.EditorTools
{
    [InitializeOnLoad]
    public static class DevGameTestSetupEditor
    {
        private const string ScenePath = "Assets/_Project/Scenes/Dev GameTest.unity";
        private const string PlayerPrefabPath = "Assets/Recorded/Prefabs/Player.prefab";

        static DevGameTestSetupEditor()
        {
            EditorApplication.delayCall += AutoSetupIfDevGameTestActive;
        }

        private static void AutoSetupIfDevGameTestActive()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == ScenePath)
            {
                SetupScene(false);
            }
        }

        [MenuItem("RECORDED/Spawn Player in Scene (Dev GameTest)", false, 0)]
        public static void SpawnPlayerMenu()
        {
            SetupScene(true);
        }

        [MenuItem("RECORDED/Setup Dev GameTest Scene", false, 1)]
        public static void SetupSceneMenu()
        {
            SetupScene(true);
        }

        public static void SetupScene(bool showDialogIfOtherScene = true)
        {
            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                if (showDialogIfOtherScene)
                {
                    bool open = EditorUtility.DisplayDialog(
                        "Open Dev GameTest Scene?",
                        "Do you want to open or create Assets/_Project/Scenes/Dev GameTest.unity to perform setup?",
                        "Yes", "No");

                    if (!open) return;
                }
                else
                {
                    return;
                }

                if (System.IO.File.Exists(ScenePath))
                {
                    activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
                else
                {
                    activeScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(activeScene, ScenePath);
                }
            }

            // 1. Check for friend's Player.prefab and instantiate into the scene
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                var existingCc = Object.FindFirstObjectByType<CharacterController>();
                if (existingCc != null) player = existingCc.gameObject;
            }

            if (player == null)
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (playerPrefab != null)
                {
                    // Remove old standalone camera
                    Camera oldCam = Camera.main;
                    if (oldCam != null && oldCam.GetComponentInParent<CharacterController>() == null)
                    {
                        Undo.DestroyObjectImmediate(oldCam.gameObject);
                    }

                    player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, activeScene);
                    player.transform.position = new Vector3(0f, 0.05f, -3f);
                    player.transform.rotation = Quaternion.identity;
                    player.name = "Player";
                    Undo.RegisterCreatedObjectUndo(player, "Spawn Player Prefab");
                }
            }

            // 2. Find or create bootstrap
            DevGameTestBootstrap bootstrap = Object.FindFirstObjectByType<DevGameTestBootstrap>();
            if (bootstrap == null)
            {
                GameObject bootObj = new GameObject("DevGameTest_Bootstrap");
                bootstrap = bootObj.AddComponent<DevGameTestBootstrap>();
                Undo.RegisterCreatedObjectUndo(bootObj, "Create DevGameTest_Bootstrap");
            }

            // 3. Run full environment & gun setup
            bootstrap.SetupTestEnvironment();

            // 4. Update target materials in Edit Mode so they appear Neutral Grey immediately
            GameObject targetsGroup = GameObject.Find("TestTargets_Group");
            if (targetsGroup != null)
            {
                foreach (Transform child in targetsGroup.transform)
                {
                    Renderer rend = child.GetComponent<Renderer>();
                    if (rend != null && rend.sharedMaterial != null)
                    {
                        rend.sharedMaterial.color = ColorSystem.RGBColorTarget.ColorNeutralGrey;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=#00FF99><b>[RECORDED]</b> Player and RGB targets successfully configured in Dev GameTest scene!</color>");

            // Run verification suite for the 11 test cases
            Recorded.Testing.Editor.RGBColorSystemVerificationTests.RunAllTests();
        }
    }

    [UnityEditor.CustomEditor(typeof(DevGameTestBootstrap))]
    public class DevGameTestBootstrapEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
            if (GUILayout.Button("🎮 Spawn Player into Scene", GUILayout.Height(38)))
            {
                DevGameTestSetupEditor.SetupScene(true);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Click the button above to place Player.prefab into this scene, or select RECORDED > Spawn Player in Scene (Dev GameTest) from the top menu.", MessageType.Info);
        }
    }
}
