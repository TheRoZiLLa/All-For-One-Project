using UnityEngine;
using UnityEditor;
using Recorded.Testing;
using Recorded.Testing.Editor;

namespace Recorded.EditorTools
{
    /// <summary>
    /// Provides menu utilities to inspect or rebuild the Sci-Fi Revolver prefab and scene setup on demand.
    /// </summary>
    [InitializeOnLoad]
    public static class RevolverSceneSetupMenu
    {
        private const string VerifyKey = "RECORDED_OVERHAUL_12OCLOCK_V1";

        static RevolverSceneSetupMenu()
        {
            if (!SessionState.GetBool(VerifyKey, false))
            {
                SessionState.SetBool(VerifyKey, true);
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                    {
                        RebuildCylinderHUDInActiveScene();
                        RGBColorSystemVerificationTests.RunAllTests();
                    }
                };
            }
        }

        [MenuItem("RECORDED/Rebuild Sci-Fi Revolver Prefab & Scene Setup", false, 2)]
        public static void ExecuteManualSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ExecuteOverhaulSceneRebuild();
            RGBColorSystemVerificationTests.RunAllTests();
        }

        public static void RebuildCylinderHUDInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

            var hud = Object.FindFirstObjectByType<Recorded.Weapons.Ammo.CylinderAmmoHUD>();
            if (hud != null)
            {
                for (int c = hud.transform.childCount - 1; c >= 0; c--)
                {
                    Object.DestroyImmediate(hud.transform.GetChild(c).gameObject);
                }
                hud.BuildHUDHierarchy();

                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
                }
            }
        }

        public static void ExecuteOverhaulSceneRebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            RevolverSetupUtility.CreateOrUpdateRevolverPrefab();
            RevolverSetupUtility.SetupRevolverInActiveScene(true);

            // Rebuild CylinderAmmoHUD in active scene so serialized canvas matches the 12 o'clock bracket UI
            RebuildCylinderHUDInActiveScene();

            // Ensure bootstrap updates targets with Rigidbody + SpectrumSkillResponder
            var bootstrap = Object.FindFirstObjectByType<DevGameTestBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.SetupTestEnvironment();
            }

            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
            }
        }
    }
}
