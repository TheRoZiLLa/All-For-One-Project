using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Recorded.Testing;

namespace Recorded.EditorTools
{
    public static class DevGameTestSetupEditor
    {
        private const string ScenePath = "Assets/_Project/Scenes/Dev GameTest.unity";

        [MenuItem("RECORDED/Setup Dev GameTest Scene", false, 1)]
        public static void SetupScene()
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
                bool open = EditorUtility.DisplayDialog(
                    "Open Dev GameTest Scene?",
                    "Do you want to open or create Assets/_Project/Scenes/Dev GameTest.unity to perform setup?",
                    "Yes", "No");

                if (!open) return;

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

            // Find or create bootstrap
            DevGameTestBootstrap bootstrap = Object.FindFirstObjectByType<DevGameTestBootstrap>();
            if (bootstrap == null)
            {
                GameObject bootObj = new GameObject("DevGameTest_Bootstrap");
                bootstrap = bootObj.AddComponent<DevGameTestBootstrap>();
            }

            bootstrap.SetupTestEnvironment();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=#00FF99><b>[RECORDED]</b> Dev GameTest Scene successfully setup and saved!</color>");
        }
    }
}
