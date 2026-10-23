using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Recorded.Weapons;
using Recorded.ColorSystem;

namespace Recorded.EditorTools
{
    public static class RevolverSetupUtility
    {
        public const string FbxPath = "Assets/Assets/sci-fi-revolver/source/REV.fbx";
        public const string BodyMatPath = "Assets/Assets/sci-fi-revolver/textures/1.mat";
        public const string BarrelMatPath = "Assets/Assets/sci-fi-revolver/textures/2.mat";
        public const string PrefabDir = "Assets/_Project/Prefabs/Weapons";
        public const string PrefabPath = "Assets/_Project/Prefabs/Weapons/SpectrumGun_Revolver.prefab";

        [MenuItem("RECORDED/Setup Revolver Weapon Model", false, 2)]
        public static void SetupRevolverMenu()
        {
            SetupRevolverInActiveScene();
        }

        public static GameObject CreateOrUpdateRevolverPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Weapons");
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[RevolverSetup] FBX asset not found at {FbxPath}");
                return null;
            }

            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
            Material barrelMat = AssetDatabase.LoadAssetAtPath<Material>(BarrelMatPath);

            // Create temporary instance in scene to assemble
            GameObject root = new GameObject("SpectrumGun");

            // Attach weapon logic scripts
            RaycastWeapon raycastWeapon = root.AddComponent<RaycastWeapon>();
            WeaponImpactFeedback feedback = root.AddComponent<WeaponImpactFeedback>();
            SpectrumGunController controller = root.AddComponent<SpectrumGunController>();

            // Instantiate FBX model as child and unpack completely so mesh renderers are directly serialized
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, root.transform);
            PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelInstance.name = "RevolverModel";

            // Remove colliders from model so they don't block player raycasts
            foreach (var col in modelInstance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }

            // Assign materials explicitly
            foreach (var renderer in modelInstance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.name.ToLower().Contains("body"))
                {
                    if (bodyMat != null) renderer.sharedMaterial = bodyMat;
                }
                else if (renderer.gameObject.name.ToLower().Contains("barrel"))
                {
                    if (barrelMat != null) renderer.sharedMaterial = barrelMat;
                }
            }

            // Viewmodel positioning and scaling
            // Model orientation: rotate by (0, -90, 0) around Y so barrel points forward (+Z) away from player
            // Handgun dimensions: 3.88cm in raw FBX (imported at 0.01m/unit) -> scale by 8.0f = ~0.31m (31cm length)
            modelInstance.transform.localScale = new Vector3(8.0f, 8.0f, 8.0f);
            modelInstance.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            modelInstance.transform.localPosition = Vector3.zero;

            // Muzzle Point & Light setup
            GameObject muzzleObj = new GameObject("MuzzlePoint");
            muzzleObj.transform.SetParent(root.transform, false);
            // Barrel tip forward along +Z at rot -90: Z ~ 0.09m, Y ~ 0.01m
            muzzleObj.transform.localPosition = new Vector3(0f, 0.01f, 0.09f);
            muzzleObj.transform.localRotation = Quaternion.identity;

            Light muzzleLight = muzzleObj.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 3.5f;
            muzzleLight.color = new Color(0.2f, 0.88f, 1f); // Cyan default
            muzzleLight.intensity = 3.5f;
            muzzleLight.enabled = false;

            // Dynamic Chamber Glow Indicator inside revolver cylinder
            GameObject chamberLightObj = new GameObject("ChamberGlow");
            chamberLightObj.transform.SetParent(root.transform, false);
            chamberLightObj.transform.localPosition = new Vector3(0f, 0.035f, -0.02f);
            Light chamberLight = chamberLightObj.AddComponent<Light>();
            chamberLight.type = LightType.Point;
            chamberLight.range = 0.5f;
            chamberLight.intensity = 1.5f;
            chamberLight.color = new Color(1f, 0.2f, 0.2f);
            chamberLight.enabled = true;

            // Wire up components
            raycastWeapon.SetMuzzlePoint(muzzleObj.transform);
            feedback.SetMuzzleLight(muzzleLight);

            // Save as Prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FF99><b>[RevolverSetup]</b> Created/Updated Revolver weapon prefab at {PrefabPath}</color>");
            return prefab;
        }

        public static void SetupRevolverInActiveScene(bool force = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activeScene = EditorSceneManager.GetActiveScene();

            // 1. Ensure Prefab exists
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                prefab = CreateOrUpdateRevolverPrefab();
            }

            // 2. Find or Spawn Player & Camera
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                var existingCc = Object.FindFirstObjectByType<CharacterController>();
                if (existingCc != null) player = existingCc.gameObject;
            }

            if (player == null)
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab");
                if (playerPrefab != null)
                {
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

            Camera playerCam = player != null ? player.GetComponentInChildren<Camera>(true) : Camera.main;
            if (playerCam == null)
            {
                Debug.LogError("[RevolverSetup] Could not find or spawn Player Camera in scene.");
                return;
            }

            // If already present with RevolverModel of correct scale and not forcing recreation, skip
            if (!force)
            {
                SpectrumGunController existing = playerCam.GetComponentInChildren<SpectrumGunController>(true);
                if (existing != null)
                {
                    Transform revModel = existing.transform.Find("RevolverModel");
                    if (revModel != null && revModel.localScale.x >= 1.0f && existing.GetComponentsInChildren<MeshRenderer>(true).Length > 0)
                    {
                        return;
                    }
                }
            }

            // 3. Remove existing SpectrumGun or WeaponMockup
            SpectrumGunController[] existingControllers = Object.FindObjectsByType<SpectrumGunController>(FindObjectsSortMode.None);
            foreach (var ctrl in existingControllers)
            {
                Undo.DestroyObjectImmediate(ctrl.gameObject);
            }

            // Also check for any orphaned WeaponMockup objects
            GameObject oldMockup = GameObject.Find("WeaponMockup");
            if (oldMockup != null)
            {
                Undo.DestroyObjectImmediate(oldMockup);
            }

            // 4. Instantiate Revolver Prefab under Camera and unpack completely
            GameObject gunInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeScene);
            PrefabUtility.UnpackPrefabInstance(gunInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            gunInstance.name = "SpectrumGun";
            gunInstance.transform.SetParent(playerCam.transform, false);
            // Position gun nicely in first-person view: slightly down and to the right
            gunInstance.transform.localPosition = new Vector3(0.20f, -0.16f, 0.45f);
            gunInstance.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(gunInstance, "Instantiate Revolver SpectrumGun");

            // 5. Wire raycast weapon camera
            RaycastWeapon raycastWeapon = gunInstance.GetComponent<RaycastWeapon>();
            if (raycastWeapon != null)
            {
                raycastWeapon.SetCamera(playerCam);
            }

            // 6. Ensure Crosshair UI is present
            if (Object.FindFirstObjectByType<UI.CrosshairUI>() == null)
            {
                GameObject crosshairObj = new GameObject("CrosshairUI_Manager");
                crosshairObj.transform.SetParent(gunInstance.transform, false);
                crosshairObj.AddComponent<UI.CrosshairUI>();
                Undo.RegisterCreatedObjectUndo(crosshairObj, "Create CrosshairUI_Manager");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=#00FF99><b>[RevolverSetup]</b> Successfully installed Sci-Fi Revolver with Red/Green/Cyan emissive colors into Dev GameTest scene!</color>");
        }
    }
}
