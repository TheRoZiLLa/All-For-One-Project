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
            SetupRevolverInActiveScene(true);
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
            MeshRenderer barrelRend = null;
            foreach (var renderer in modelInstance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.name.ToLower().Contains("body"))
                {
                    if (bodyMat != null) renderer.sharedMaterial = bodyMat;
                }
                else if (renderer.gameObject.name.ToLower().Contains("barrel"))
                {
                    barrelRend = renderer;
                    if (barrelMat != null) renderer.sharedMaterial = barrelMat;
                }
            }

            // Viewmodel positioning and scaling
            modelInstance.transform.localScale = new Vector3(8.0f, 8.0f, 8.0f);
            modelInstance.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            modelInstance.transform.localPosition = Vector3.zero;

            // Muzzle Point & Light setup at exact front barrel bore tip
            GameObject muzzleObj = new GameObject("MuzzlePoint");
            muzzleObj.transform.SetParent(root.transform, false);
            muzzleObj.transform.localPosition = RevolverWeaponBuilder.CalculateBarrelTipLocalPosition(root.transform, barrelRend);
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
            chamberLightObj.transform.localPosition = RevolverWeaponBuilder.DefaultChamberGlowLocalPosition;
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
            if (prefab == null || force)
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

            // If already present with RevolverModel of correct scale and not forcing recreation, update its placement & muzzle
            if (!force)
            {
                SpectrumGunController existing = playerCam.GetComponentInChildren<SpectrumGunController>(true);
                if (existing != null)
                {
                    Transform revModel = existing.transform.Find("RevolverModel");
                    if (revModel != null && revModel.localScale.x >= 1.0f && existing.GetComponentsInChildren<MeshRenderer>(true).Length > 0)
                    {
                        existing.transform.localPosition = RevolverWeaponBuilder.DefaultViewmodelLocalPosition;
                        existing.transform.localRotation = Quaternion.Euler(RevolverWeaponBuilder.DefaultViewmodelLocalEuler);
                        Transform mp = existing.transform.Find("MuzzlePoint");
                        if (mp != null)
                        {
                            MeshRenderer barrelRend = null;
                            foreach (var r in revModel.GetComponentsInChildren<MeshRenderer>(true))
                            {
                                if (r.gameObject.name.ToLower().Contains("barrel")) { barrelRend = r; break; }
                            }
                            mp.localPosition = RevolverWeaponBuilder.CalculateBarrelTipLocalPosition(existing.transform, barrelRend);
                            var rw = existing.GetComponent<RaycastWeapon>();
                            if (rw != null)
                            {
                                rw.SetCamera(playerCam);
                                rw.SetMuzzlePoint(mp);
                            }
                        }
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
            gunInstance.transform.localPosition = RevolverWeaponBuilder.DefaultViewmodelLocalPosition;
            gunInstance.transform.localRotation = Quaternion.Euler(RevolverWeaponBuilder.DefaultViewmodelLocalEuler);
            Undo.RegisterCreatedObjectUndo(gunInstance, "Instantiate Revolver SpectrumGun");

            // 5. Wire raycast weapon camera and muzzlePoint explicitly
            RaycastWeapon raycastWeapon = gunInstance.GetComponent<RaycastWeapon>();
            Transform muzzleTr = gunInstance.transform.Find("MuzzlePoint");
            if (raycastWeapon != null)
            {
                raycastWeapon.SetCamera(playerCam);
                if (muzzleTr != null) raycastWeapon.SetMuzzlePoint(muzzleTr);
            }

            WeaponImpactFeedback fb = gunInstance.GetComponent<WeaponImpactFeedback>();
            if (fb != null && muzzleTr != null)
            {
                Light ml = muzzleTr.GetComponent<Light>();
                if (ml != null) fb.SetMuzzleLight(ml);
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
