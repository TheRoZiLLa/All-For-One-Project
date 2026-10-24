#if UNITY_EDITOR
using Recorded.ColorSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Recorded.Enemies.Editor
{
    public static class SurveyorEnemySceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Dev GameTest.unity";
        private const string MaterialFolder = "Assets/_Project/Materials/Enemies";

        [MenuItem("RECORDED/Enemies/Build Surveyor Prototype in Dev GameTest")]
        public static void BuildInDevGameTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Enemy_Surveyor") != null)
            {
                Selection.activeGameObject = GameObject.Find("Enemy_Surveyor");
                return;
            }

            EnsureFolder("Assets/_Project/Materials", "Enemies");
            Material bodyMaterial = GetOrCreateMaterial("M_Surveyor.mat", "Universal Render Pipeline/Lit", new Color(0.16f, 0.30f, 0.38f));
            if (bodyMaterial == null) bodyMaterial = GetOrCreateMaterial("M_Surveyor.mat", "Standard", new Color(0.16f, 0.30f, 0.38f));
            Material lineMaterial = GetOrCreateMaterial("M_SurveyorTelegraph.mat", "Sprites/Default", new Color(0.1f, 0.95f, 1f));

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(enemy, "Create Surveyor Enemy Prototype");
            enemy.name = "Enemy_Surveyor";
            enemy.transform.position = new Vector3(5f, 1f, 10f);
            enemy.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            Rigidbody body = enemy.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            enemy.AddComponent<RGBColorTarget>();
            enemy.AddComponent<SpectrumSkillResponder>();
            enemy.AddComponent<ColorMixPhysicsReactor>();
            enemy.AddComponent<EnemyColorReactor>();

            GameObject signal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(signal, "Create Surveyor Telegraph");
            signal.name = "TelegraphBeam";
            signal.transform.SetParent(enemy.transform, false);
            signal.transform.localScale = new Vector3(0.055f, 0.045f, 1f);
            Object.DestroyImmediate(signal.GetComponent<Collider>());
            signal.GetComponent<Renderer>().sharedMaterial = lineMaterial;
            signal.GetComponent<Renderer>().enabled = false;

            SurveyorEnemyAI ai = enemy.AddComponent<SurveyorEnemyAI>();
            ai.SetTargets(FindPlayer(), FindHologram());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = enemy;
            Debug.Log("[RECORDED Enemies] Added one Surveyor prototype to Dev GameTest; no weapon, recorder, or non-enemy systems were edited.");
        }

        private static Transform FindPlayer()
        {
            Camera camera = Camera.main;
            if (camera != null) return camera.transform;
            GameObject player = GameObject.Find("Player");
            return player != null ? player.transform : null;
        }

        private static Transform FindHologram()
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (candidate.name.IndexOf("Hologram", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            return null;
        }

        private static Material GetOrCreateMaterial(string name, string shaderName, Color color)
        {
            string path = MaterialFolder + "/" + name;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) return null;
            material = new Material(shader) { name = name.Replace(".mat", ""), color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                string root = parent.Substring(0, parent.LastIndexOf('/'));
                string leaf = parent.Substring(parent.LastIndexOf('/') + 1);
                EnsureFolder(root, leaf);
                AssetDatabase.CreateFolder(root, leaf);
            }
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
