using UnityEngine;
using Recorded.Weapons;
using Recorded.UI;
using Recorded.ColorSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Recorded.Testing
{
    /// <summary>
    /// Runtime and Editor test harness for Dev GameTest scene.
    /// Ensures that the friend's Player controller, the camera, mock Spectrum Gun viewmodel,
    /// crosshair, and test target dummies are properly configured and ready for immediate testing.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DevGameTestBootstrap : MonoBehaviour
    {
        [Header("Auto-Setup Settings")]
        [SerializeField] private bool autoBuildIfMissing = true;

        [Header("Player & Viewmodel Settings")]
        [Tooltip("The friend's Player prefab (Assets/Recorded/Prefabs/Player.prefab).")]
        [SerializeField] private GameObject playerPrefab;

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
            Camera cam = SetupPlayerAndCamera();
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

        private Camera SetupPlayerAndCamera()
        {
            // 1. Look for existing Player in the scene
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                var existingController = FindFirstObjectByType<CharacterController>();
                if (existingController != null)
                {
                    player = existingController.gameObject;
                }
            }

            // 2. If no Player in scene, spawn the friend's Player.prefab
            if (player == null)
            {
                GameObject prefab = playerPrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab");
                    playerPrefab = prefab;
                }
#endif
                if (prefab != null)
                {
                    // Clean up any old standalone Main Camera that was not part of a player
                    Camera oldCam = Camera.main;
                    if (oldCam != null && oldCam.GetComponentInParent<CharacterController>() == null)
                    {
                        DestroyImmediate(oldCam.gameObject);
                    }

                    player = Instantiate(prefab, new Vector3(0f, 0f, -3f), Quaternion.identity);
                    player.name = "Player";
                }
            }

            // 3. Resolve Camera on Player
            Camera cam = null;
            if (player != null)
            {
                cam = player.GetComponentInChildren<Camera>();
            }

            // 4. Fallback if Player prefab could not be loaded
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    GameObject camObj = new GameObject("Main Camera");
                    cam = camObj.AddComponent<Camera>();
                    camObj.AddComponent<AudioListener>();
                    camObj.tag = "MainCamera";
                }
                cam.transform.position = new Vector3(0, 1.6f, -3f);
                cam.transform.rotation = Quaternion.identity;
            }

            return cam;
        }

        private void SetupSpectrumGun(Camera cam)
        {
            if (cam == null) return;

            // Check if gun is already assembled under this camera
            Transform existingGun = cam.transform.Find("SpectrumGun");
            if (existingGun != null) return;

            // Check if gun exists elsewhere in the scene and reparent it to player camera
            SpectrumGunController existingController = FindFirstObjectByType<SpectrumGunController>();
            if (existingController != null)
            {
                existingController.transform.SetParent(cam.transform, false);
                existingController.transform.localPosition = new Vector3(0.24f, -0.22f, 0.45f);
                existingController.transform.localRotation = Quaternion.identity;
                RaycastWeapon existingRaycast = existingController.GetComponent<RaycastWeapon>();
                if (existingRaycast != null) existingRaycast.SetCamera(cam);
                return;
            }

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
            if (targetsContainer != null)
            {
                // Ensure all existing targets under TestTargets_Group have RGBColorTarget and are Neutral Grey
                foreach (Transform child in targetsContainer)
                {
                    var oldTestTarget = child.GetComponent<TestTarget>();
                    if (oldTestTarget != null)
                    {
                        DestroyImmediate(oldTestTarget);
                    }

                    RGBColorTarget colorTarget = child.GetComponent<RGBColorTarget>();
                    if (colorTarget == null)
                    {
                        colorTarget = child.gameObject.AddComponent<RGBColorTarget>();
                    }

                    // Reset to None state and Neutral Grey visuals
                    colorTarget.ResetColors();
                }
                return;
            }

            GameObject groupObj = new GameObject("TestTargets_Group");
            targetsContainer = groupObj.transform;

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            Color neutralGrey = RGBColorTarget.ColorNeutralGrey; // #7F7F7F
            string[] targetNames = new string[] { "Target_RGB_8m", "Target_RGB_16m", "Target_RGB_26m" };
            float[] xOffsets = new float[] { 0f, -2.5f, 2.5f };

            for (int i = 0; i < 3; i++)
            {
                float dist = targetDistances[i];
                GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                targetObj.name = targetNames[i];
                targetObj.transform.SetParent(targetsContainer, false);
                targetObj.transform.position = new Vector3(xOffsets[i], 1.4f, dist);
                targetObj.transform.localScale = new Vector3(1f, 1.8f, 1f);

                Material mat = new Material(litShader)
                {
                    name = $"Mat_{targetNames[i]}",
                    color = neutralGrey
                };
                targetObj.GetComponent<Renderer>().material = mat;

                RGBColorTarget colorTarget = targetObj.AddComponent<RGBColorTarget>();
                colorTarget.ResetColors();
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
