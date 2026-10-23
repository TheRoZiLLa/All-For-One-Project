using UnityEngine;
using UnityEditor;
using Recorded.Testing.Editor;

namespace Recorded.EditorTools
{
    /// <summary>
    /// Provides menu utilities to inspect or rebuild the Sci-Fi Revolver prefab and scene setup on demand.
    /// Does not auto-mutate or auto-save scenes during background reloads or Play Mode transitions.
    /// </summary>
    [InitializeOnLoad]
    public static class RevolverSceneSetupMenu
    {
        private const string VerifyKey = "RECORDED_VERIFY_CLEAN_V1";

        static RevolverSceneSetupMenu()
        {
            if (!SessionState.GetBool(VerifyKey, false))
            {
                SessionState.SetBool(VerifyKey, true);
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        RGBColorSystemVerificationTests.RunAllTests();
                    }
                };
            }
        }

        [MenuItem("RECORDED/Rebuild Sci-Fi Revolver Prefab & Scene Setup", false, 2)]
        public static void ExecuteManualSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            RevolverSetupUtility.CreateOrUpdateRevolverPrefab();
            RevolverSetupUtility.SetupRevolverInActiveScene(true);
        }
    }
}