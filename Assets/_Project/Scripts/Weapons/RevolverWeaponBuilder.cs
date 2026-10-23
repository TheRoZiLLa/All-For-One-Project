using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Recorded.Weapons
{
    /// <summary>
    /// Builds and attaches the Sci-Fi Revolver model (REV.fbx) with the friend's custom
    /// painted Red, Green, and Cyan emissive textures.
    /// Replaces the primitive cube mockup and configures viewmodel offsets and muzzle attachments.
    /// </summary>
    public static class RevolverWeaponBuilder
    {
        public const string FbxPath = "Assets/Assets/sci-fi-revolver/source/REV.fbx";
        public const string BodyMatPath = "Assets/Assets/sci-fi-revolver/textures/1.mat";
        public const string BarrelMatPath = "Assets/Assets/sci-fi-revolver/textures/2.mat";

        public static GameObject BuildRevolver(Transform parent, out Transform muzzlePoint, out Light muzzleLight)
        {
            // 1. Remove old primitive cube mockup if present
            Transform oldMockup = parent.Find("WeaponMockup");
            if (oldMockup != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(oldMockup.gameObject);
                else
                    Object.Destroy(oldMockup.gameObject);
#else
                Object.Destroy(oldMockup.gameObject);
#endif
            }

            // 2. Remove existing RevolverModel if already present to avoid duplicates
            Transform existingModel = parent.Find("RevolverModel");
            if (existingModel != null)
            {
                existingModel.name = "RevolverModel_Old";
                existingModel.gameObject.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(existingModel.gameObject);
                else
                    Object.Destroy(existingModel.gameObject);
#else
                Object.Destroy(existingModel.gameObject);
#endif
            }

            // 3. Load FBX asset & Materials
            GameObject fbxAsset = null;
            Material bodyMat = null;
            Material barrelMat = null;

#if UNITY_EDITOR
            fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            bodyMat = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
            barrelMat = AssetDatabase.LoadAssetAtPath<Material>(BarrelMatPath);
#endif

            GameObject modelInstance = null;
            if (fbxAsset != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, parent);
                    PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                else
                {
                    modelInstance = Object.Instantiate(fbxAsset, parent);
                }
#else
                modelInstance = Object.Instantiate(fbxAsset, parent);
#endif
            }

            // Fallback if FBX cannot be loaded: build primitive mockup
            if (modelInstance == null)
            {
                Debug.LogWarning("[RevolverWeaponBuilder] REV.fbx not found, falling back to primitive mockup.");
                return SpectrumGunMockupBuilder.BuildMockup(parent, out muzzlePoint, out muzzleLight);
            }

            modelInstance.name = "RevolverModel";

            // Remove colliders from weapon viewmodel parts so they never block raycasts
            Collider[] colliders = modelInstance.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(colliders[i]);
                else
                    Object.Destroy(colliders[i]);
#else
                Object.Destroy(colliders[i]);
#endif
            }

            // Assign materials explicitly to ensure emissive Red/Green/Cyan textures are bound
            MeshRenderer[] renderers = modelInstance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var rend in renderers)
            {
                string rName = rend.gameObject.name.ToLower();
                if (rName.Contains("body"))
                {
                    if (bodyMat != null) rend.sharedMaterial = bodyMat;
                }
                else if (rName.Contains("barrel"))
                {
                    if (barrelMat != null) rend.sharedMaterial = barrelMat;
                }
                rend.enabled = true;
                rend.gameObject.SetActive(true);
            }

            // Model orientation: FBX forward along X -> rotate by (0, -90, 0) around Y so barrel points forward (+Z) away from player
            // Handgun dimensions: 3.88cm in raw FBX (imported at 0.01m/unit) -> scale by 8.0f = ~0.31m (31cm length)
            modelInstance.transform.localScale = new Vector3(8.0f, 8.0f, 8.0f);
            modelInstance.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            modelInstance.transform.localPosition = Vector3.zero;

            // 4. MuzzlePoint Transform & Flash Light
            Transform existingMuzzle = parent.Find("MuzzlePoint");
            if (existingMuzzle != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(existingMuzzle.gameObject);
                else
                    Object.Destroy(existingMuzzle.gameObject);
#else
                Object.Destroy(existingMuzzle.gameObject);
#endif
            }

            GameObject muzzleObj = new GameObject("MuzzlePoint");
            muzzleObj.transform.SetParent(parent, false);
            // Barrel tip forward along +Z at rot -90: Z ~ 0.09m, Y ~ 0.01m
            muzzleObj.transform.localPosition = new Vector3(0f, 0.01f, 0.09f);
            muzzleObj.transform.localRotation = Quaternion.identity;
            muzzlePoint = muzzleObj.transform;

            // 5. Dynamic Chamber Glow Light inside revolver cylinder
            Transform existingChamber = parent.Find("ChamberGlow");
            if (existingChamber == null)
            {
                GameObject chamberObj = new GameObject("ChamberGlow");
                chamberObj.transform.SetParent(parent, false);
                chamberObj.transform.localPosition = new Vector3(0f, 0.035f, -0.02f);
                Light chamberLight = chamberObj.AddComponent<Light>();
                chamberLight.type = LightType.Point;
                chamberLight.range = 0.5f;
                chamberLight.intensity = 1.5f;
                chamberLight.color = new Color(1f, 0.2f, 0.2f);
                chamberLight.enabled = true;
            }

            muzzleLight = muzzleObj.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 3.5f;
            muzzleLight.color = new Color(0.2f, 0.88f, 1f);
            muzzleLight.intensity = 3.5f;
            muzzleLight.enabled = false;

            return modelInstance;
        }
    }
}
