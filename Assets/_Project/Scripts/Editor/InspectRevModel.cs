using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Recorded.Testing.Editor;

namespace Recorded.EditorTools
{
    [InitializeOnLoad]
    public static class RevolverSceneSetupOnLoad
    {
        private const string ScenePath = "Assets/_Project/Scenes/Dev GameTest.unity";

        static RevolverSceneSetupOnLoad()
        {
            EditorApplication.update += CheckAndExecute;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update += CheckAndExecute;
            }
        }

        private static void CheckAndExecute()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= CheckAndExecute;
            ExecuteSetup();
        }

        private static void ExecuteSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isLoaded && (activeScene.name == "Dev GameTest" || activeScene.path == ScenePath || activeScene.path.Contains("Dev GameTest")))
            {
                RevolverSetupUtility.CreateOrUpdateRevolverPrefab();
                RevolverSetupUtility.SetupRevolverInActiveScene(true);
                RGBColorSystemVerificationTests.RunAllTests();
            }
        }
    }
}