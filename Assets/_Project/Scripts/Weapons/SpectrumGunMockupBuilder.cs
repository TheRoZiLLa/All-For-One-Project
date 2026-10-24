using UnityEngine;

namespace Recorded.Weapons
{
    /// <summary>
    /// Utility to generate the Realistic Sci-Fi Graybox Prototype / Mockup for the Spectrum Gun.
    /// Built entirely from Unity Primitives (Cubes, Cylinders) with industrial aesthetic.
    /// Strictly separated from shooting logic so it can be swapped for a final 3D model later.
    /// </summary>
    public static class SpectrumGunMockupBuilder
    {
        public static GameObject BuildMockup(Transform parent, out Transform muzzlePoint, out Light muzzleLight)
        {
            return BuildMockup(parent, out muzzlePoint, out muzzleLight, out _);
        }

        public static GameObject BuildMockup(Transform parent, out Transform muzzlePoint, out Light muzzleLight, out Renderer rgbCoreRenderer)
        {
            GameObject mockupRoot = new GameObject("WeaponMockup");
            mockupRoot.transform.SetParent(parent, false);

            // Shaders & Materials (Industrial Gunmetal, Composite, Emissive RGB Core)
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            Material gunmetalMat = new Material(litShader)
            {
                name = "Mat_Gunmetal",
                color = new Color(0.18f, 0.20f, 0.22f) // Dark metallic slate
            };
            if (gunmetalMat.HasProperty("_Metallic")) gunmetalMat.SetFloat("_Metallic", 0.85f);
            if (gunmetalMat.HasProperty("_Smoothness")) gunmetalMat.SetFloat("_Smoothness", 0.65f);

            Material compositeMat = new Material(litShader)
            {
                name = "Mat_Composite",
                color = new Color(0.12f, 0.13f, 0.14f) // Matte dark composite
            };
            if (compositeMat.HasProperty("_Metallic")) compositeMat.SetFloat("_Metallic", 0.2f);
            if (compositeMat.HasProperty("_Smoothness")) compositeMat.SetFloat("_Smoothness", 0.3f);

            Material rgbCoreMat = new Material(litShader)
            {
                name = "Mat_RGBCore_Emissive",
                color = new Color(1f, 0.25f, 0.25f, 0.9f) // Default Red / Impulse
            };
            if (rgbCoreMat.HasProperty("_EmissionColor"))
            {
                rgbCoreMat.EnableKeyword("_EMISSION");
                rgbCoreMat.SetColor("_EmissionColor", new Color(1.5f, 0.3f, 0.3f));
            }

            // 1. Receiver Body
            CreatePart(PrimitiveType.Cube, "Part_ReceiverBody", mockupRoot.transform,
                new Vector3(0, 0, 0),
                Quaternion.identity,
                new Vector3(0.08f, 0.11f, 0.32f),
                gunmetalMat);

            // 2. Upper Heat Shroud
            CreatePart(PrimitiveType.Cube, "Part_UpperShroud", mockupRoot.transform,
                new Vector3(0, 0.05f, 0.05f),
                Quaternion.identity,
                new Vector3(0.065f, 0.035f, 0.26f),
                compositeMat);

            // 3. Barrel Assembly
            CreatePart(PrimitiveType.Cylinder, "Part_BarrelAssembly", mockupRoot.transform,
                new Vector3(0, 0.035f, 0.25f),
                Quaternion.Euler(90f, 0, 0),
                new Vector3(0.038f, 0.10f, 0.038f),
                gunmetalMat);

            // 4. Muzzle Brake / Tip
            CreatePart(PrimitiveType.Cube, "Part_MuzzleBrake", mockupRoot.transform,
                new Vector3(0, 0.035f, 0.36f),
                Quaternion.identity,
                new Vector3(0.055f, 0.055f, 0.04f),
                compositeMat);

            // 5. RGB Core Chamber (Energy Indicator)
            GameObject coreObj = CreatePart(PrimitiveType.Cube, "Part_RGBCoreChamber", mockupRoot.transform,
                new Vector3(0, 0.005f, 0.02f),
                Quaternion.identity,
                new Vector3(0.086f, 0.06f, 0.10f),
                rgbCoreMat);
            rgbCoreRenderer = coreObj.GetComponent<Renderer>();

            // 6. Grip Handle
            CreatePart(PrimitiveType.Cube, "Part_GripHandle", mockupRoot.transform,
                new Vector3(0, -0.09f, -0.06f),
                Quaternion.Euler(-22f, 0, 0),
                new Vector3(0.045f, 0.13f, 0.055f),
                compositeMat);

            // 7. Battery / Rear Module
            CreatePart(PrimitiveType.Cube, "Part_BatteryModule", mockupRoot.transform,
                new Vector3(0, -0.035f, -0.14f),
                Quaternion.identity,
                new Vector3(0.075f, 0.075f, 0.08f),
                gunmetalMat);

            // 8. MuzzlePoint Transform & Flash Light
            GameObject muzzleObj = new GameObject("MuzzlePoint");
            muzzleObj.transform.SetParent(mockupRoot.transform, false);
            muzzleObj.transform.localPosition = new Vector3(0, 0.035f, 0.40f);
            muzzlePoint = muzzleObj.transform;

            muzzleLight = muzzleObj.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 3f;
            muzzleLight.color = new Color(0.4f, 0.85f, 1f);
            muzzleLight.intensity = 3.5f;
            muzzleLight.enabled = false;

            return mockupRoot;
        }

        private static GameObject CreatePart(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Quaternion localRot, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = localRot;
            part.transform.localScale = localScale;

            // Remove colliders from weapon viewmodel parts so they don't block Raycasts!
            Collider col = part.GetComponent<Collider>();
            if (col != null)
            {
                Object.Destroy(col);
            }

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null && material != null)
            {
                rend.material = material;
            }

            return part;
        }
    }
}
