#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Recorded.Enemies.Editor
{
    [InitializeOnLoad]
    internal static class InstallSurveyorOnce
    {
        private const string Key = "RECORDED.Surveyor.InstallOnce.20261025";
        private const string ScenePath = "Assets/_Project/Scenes/Dev GameTest.unity";
        private const string ScriptPath = "Assets/_Project/Scripts/Enemies/Editor/InstallSurveyorOnce.cs";

        static InstallSurveyorOnce()
        {
            if (SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, true);
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            try
            {
                if (SceneManager.GetActiveScene().path == ScenePath)
                    SurveyorEnemySceneSetup.BuildInDevGameTest();
            }
            finally
            {
                AssetDatabase.DeleteAsset(ScriptPath);
            }
        }
    }
}
#endif
