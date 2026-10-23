using UnityEngine;
using Recorded.Weapons;
using Recorded.UI;

namespace Recorded.Testing
{
    /// <summary>
    /// Runtime and Editor test harness for Dev GameTest scene.
    /// Ensures that the camera, mock Spectrum Gun viewmodel, crosshair, and test target dummies
    /// are properly configured and ready for immediate testing.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DevGameTestBootstrap : MonoBehaviour
    {
        [Header("Auto-Setup Settings")]
        [SerializeField] private bool autoBuildIfMissing = true;

        [Header("Target Spacing")]
        [SerializeField] private float[] targetDistances = new float[] { 8f, 16f, 26f };

        private void Awake()
        {
            if (autoBuildIfMissing)
            {
                SetupTestEnvironment();
            }
        }

        [ContextMenu("Setup Test Environment")]
        public void SetupTestEnvironment()
        {
            SetupLightingAndFloor();
            Camera cam = SetupTestCamera();
            SetupCylinderAmmoHUD();
            SetupSpectrumGun(cam);
            SetupTestTargets();
        }

        private void SetupLightingAndFloor()
        {
            // Floor
            if (GameObject.Find("TestFloor") == null)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "TestFloor";
                floor.transform.position = Vector3.zero;
                floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50x50 meters

                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");
                Material floorMat = new Material(litShader)
                {
                    name = "Mat_TestFloor",
                    color = new Color(0.15f, 0.16f, 0.18f)
                };
                floor.GetComponent<Renderer>().material = floorMat;
            }

            // Directional Light
            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.intensity = 1.5f;
                dirLight.color = new Color(1f, 0.98f, 0.95f);
                lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0);
            }
        }

        private Camera SetupTestCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
                camObj.tag = "MainCamera";
            }

            // Position at eye-level looking forward towards targets
            cam.transform.position = new Vector3(0, 1.6f, -3f);
            cam.transform.rotation = Quaternion.identity;

            return cam;
        }

        private void SetupSpectrumGun(Camera cam)
        {
            if (cam == null) return;

            Transform existingGun = cam.transform.Find("SpectrumGun");
            if (existingGun != null) return; // Already assembled

            // Viewmodel root attached under Camera
            GameObject gunRoot = new GameObject("SpectrumGun");
            gunRoot.transform.SetParent(cam.transform, false);
            // First-person viewmodel offset (slightly lower and to the right)
            gunRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.45f);
            gunRoot.transform.localRotation = Quaternion.identity;

            // Weapon Components
            RaycastWeapon raycastWeapon = gunRoot.AddComponent<RaycastWeapon>();
            raycastWeapon.SetCamera(cam);

            WeaponImpactFeedback feedback = gunRoot.AddComponent<WeaponImpactFeedback>();

            SpectrumGunController controller = gunRoot.AddComponent<SpectrumGunController>();

            // Build Mockup Model
            SpectrumGunMockupBuilder.BuildMockup(gunRoot.transform, out Transform muzzlePoint, out Light muzzleLight, out Renderer rgbCoreRenderer);

            raycastWeapon.SetMuzzlePoint(muzzlePoint);
            feedback.SetMuzzleLight(muzzleLight);
            controller.SetCoreRenderer(rgbCoreRenderer);

            // Crosshair UI
            if (FindFirstObjectByType<CrosshairUI>() == null)
            {
                GameObject crosshairObj = new GameObject("CrosshairUI_Manager");
                crosshairObj.transform.SetParent(gunRoot.transform, false);
                crosshairObj.AddComponent<CrosshairUI>();
            }

            // Apply initial Red mode
            controller.SetColorMode(SpectrumColorMode.Red);
        }

        private void SetupTestTargets()
        {
            Transform targetsContainer = GameObject.Find("TestTargets_Group")?.transform;
            if (targetsContainer != null) return; // Already present

            GameObject groupObj = new GameObject("TestTargets_Group");
            targetsContainer = groupObj.transform;

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // Strictly 3 RGB Targets: Red, Green, Blue
            string[] rgbNames = new string[] { "Target_Red_8m", "Target_Green_16m", "Target_Blue_26m" };
            Color[] rgbColors = new Color[]
            {
                SpectrumGunController.ColorRed,   // Red Dummy
                SpectrumGunController.ColorGreen, // Green Dummy
                SpectrumGunController.ColorBlue   // Blue Dummy
            };
            float[] xOffsets = new float[] { 0f, -2.5f, 2.5f };

            for (int i = 0; i < 3; i++)
            {
                float dist = targetDistances[i];
                GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                targetObj.name = rgbNames[i];
                targetObj.transform.SetParent(targetsContainer, false);
                targetObj.transform.position = new Vector3(xOffsets[i], 1.4f, dist);
                targetObj.transform.localScale = new Vector3(1f, 1.8f, 1f);

                Material mat = new Material(litShader)
                {
                    name = $"Mat_{rgbNames[i]}",
                    color = rgbColors[i]
                };
                targetObj.GetComponent<Renderer>().material = mat;

                targetObj.AddComponent<TestTarget>();
            }
        }

        private void SetupCylinderAmmoHUD()
        {
            if (FindFirstObjectByType<Weapons.Ammo.CylinderAmmoHUD>() != null) return;

            GameObject hudManagerObj = new GameObject("CylinderAmmoHUD_System");
            hudManagerObj.AddComponent<Weapons.Ammo.CylinderAmmoController>();
            hudManagerObj.AddComponent<Weapons.Ammo.CylinderRotationController>();
            hudManagerObj.AddComponent<Weapons.Ammo.CylinderAmmoHUD>();
            hudManagerObj.AddComponent<Weapons.Ammo.AmmoTestInput>();
        }
    }
}
