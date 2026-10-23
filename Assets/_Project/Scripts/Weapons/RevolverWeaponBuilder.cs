using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Recorded.Weapons
{
    /// <summary>
    /// Builds and attaches the Sci-Fi Revolver model (REV.fbx) with the friend's custom
    /// painted Red, Green, and Cyan emissive textures.
    /// Configures accurate first-person viewmodel offsets and exact barrel-tip MuzzlePoint placement.
    /// </summary>
    public static class RevolverWeaponBuilder
    {
        public const string FbxPath = "Assets/Assets/sci-fi-revolver/source/REV.fbx";
        public const string BodyMatPath = "Assets/Assets/sci-fi-revolver/textures/1.mat";
        public const string BarrelMatPath = "Assets/Assets/sci-fi-revolver/textures/2.mat";

        // Natural First-Person Viewmodel Placement (close to right hand, aiming toward screen center)
        public static readonly Vector3 DefaultViewmodelLocalPosition = new Vector3(0.14f, -0.11f, 0.25f);
        public static readonly Vector3 DefaultViewmodelLocalEuler = new Vector3(-1.2f, -3.5f, 0f);

        // Exact front tip of the upper barrel bore on REV.fbx (at scale 8, rot Y=-90)
        public static readonly Vector3 DefaultMuzzleLocalPosition = new Vector3(0.01f, 0.045f, 0.065f);
        public static readonly Vector3 DefaultChamberGlowLocalPosition = new Vector3(0.01f, 0.035f, -0.02f);

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
            MeshRenderer barrelRenderer = null;
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
                    barrelRenderer = rend;
                    if (barrelMat != null) rend.sharedMaterial = barrelMat;
                }
                rend.enabled = true;
                rend.gameObject.SetActive(true);
            }

            // Model orientation: FBX forward along X -> rotate by (0, -90, 0) around Y so barrel points forward (+Z) away from player
            modelInstance.transform.localScale = new Vector3(8.0f, 8.0f, 8.0f);
            modelInstance.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            modelInstance.transform.localPosition = Vector3.zero;

            // 4. MuzzlePoint Transform & Flash Light at exact barrel bore tip
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
            muzzleObj.transform.localPosition = CalculateBarrelTipLocalPosition(parent, barrelRenderer);
            muzzleObj.transform.localRotation = Quaternion.identity;
            muzzlePoint = muzzleObj.transform;

            // 5. Dynamic Chamber Glow Light inside revolver cylinder
            Transform existingChamber = parent.Find("ChamberGlow");
            if (existingChamber == null)
            {
                GameObject chamberObj = new GameObject("ChamberGlow");
                chamberObj.transform.SetParent(parent, false);
                chamberObj.transform.localPosition = DefaultChamberGlowLocalPosition;
                Light chamberLight = chamberObj.AddComponent<Light>();
                chamberLight.type = LightType.Point;
                chamberLight.range = 0.5f;
                chamberLight.intensity = 1.5f;
                chamberLight.color = new Color(1f, 0.2f, 0.2f);
                chamberLight.enabled = true;
            }
            else
            {
                existingChamber.localPosition = DefaultChamberGlowLocalPosition;
            }

            muzzleLight = muzzleObj.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 3.5f;
            muzzleLight.color = new Color(0.2f, 0.88f, 1f);
            muzzleLight.intensity = 3.5f;
            muzzleLight.enabled = false;

            return modelInstance;
        }

        public static Vector3 CalculateBarrelTipLocalPosition(Transform gunRoot, MeshRenderer barrelRenderer)
        {
            if (barrelRenderer != null && gunRoot != null)
            {
                MeshFilter mf = barrelRenderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds meshBounds = mf.sharedMesh.bounds;
                    // Sample the 8 corners of the Barrel mesh in gunRoot local space
                    Vector3 min = meshBounds.min;
                    Vector3 max = meshBounds.max;
                    Vector3 localMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    Vector3 localMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                    for (int ix = 0; ix < 2; ix++)
                    {
                        for (int iy = 0; iy < 2; iy++)
                        {
                            for (int iz = 0; iz < 2; iz++)
                            {
                                Vector3 corner = new Vector3(
                                    ix == 0 ? min.x : max.x,
                                    iy == 0 ? min.y : max.y,
                                    iz == 0 ? min.z : max.z);
                                Vector3 worldPt = barrelRenderer.transform.TransformPoint(corner);
                                Vector3 rootPt = gunRoot.InverseTransformPoint(worldPt);
                                localMin = Vector3.Min(localMin, rootPt);
                                localMax = Vector3.Max(localMax, rootPt);
                            }
                        }
                    }

                    // Upper bore center at the front-most +Z face of the barrel
                    float centerX = (localMin.x + localMax.x) * 0.5f;
                    float boreY = Mathf.Lerp(localMin.y, localMax.y, 0.62f);
                    float frontZ = localMax.z + 0.015f;
                    return new Vector3(centerX, boreY, frontZ);
                }
            }

            return DefaultMuzzleLocalPosition;
        }
    }
}
