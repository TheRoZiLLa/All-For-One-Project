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
    /// Ensures that the friend's Player controller, the camera, Sci-Fi Revolver viewmodel,
    /// 6-chamber Cylinder HUD, crosshair, and RGB physics/puzzle test targets are properly configured.
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

        private void Start()
        {
            LogGunDiagnostics();
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
                {
                    Camera cam = Camera.main;
                    Transform existingGun = cam != null ? cam.transform.Find("SpectrumGun") : null;
                    if (existingGun != null)
                    {
                        RevolverWeaponBuilder.BuildRevolver(existingGun, out Transform mp, out Light ml);
                        RaycastWeapon rw = existingGun.GetComponent<RaycastWeapon>();
                        if (rw != null) rw.SetMuzzlePoint(mp);
                        WeaponImpactFeedback fb = existingGun.GetComponent<WeaponImpactFeedback>();
                        if (fb != null) fb.SetMuzzleLight(ml);
                        LogGunDiagnostics();
                    }
                }
            }
        }

        public void LogGunDiagnostics()
        {
            Camera cam = Camera.main;
            Debug.Log($"<color=#FFFF00>[GunDiagnostics] Camera.main: {cam?.name}, pos: {cam?.transform.position}, fov: {cam?.fieldOfView}, near: {cam?.nearClipPlane}, far: {cam?.farClipPlane}, cullingMask: {cam?.cullingMask}</color>");

            SpectrumGunController gun = FindFirstObjectByType<SpectrumGunController>();
            if (gun == null)
            {
                Debug.LogError("<color=#FF0000>[GunDiagnostics] SpectrumGunController NOT FOUND in scene!</color>");
                return;
            }

            RaycastWeapon rw = gun.GetComponent<RaycastWeapon>();
            Transform mp = rw != null ? rw.MuzzlePoint : gun.transform.Find("MuzzlePoint");
            Debug.Log($"<color=#FFFF00>[GunDiagnostics] Gun: {gun.gameObject.name}, activeInHierarchy: {gun.gameObject.activeInHierarchy}, parent: {gun.transform.parent?.name}, localPos: {gun.transform.localPosition}, localRot: {gun.transform.localRotation.eulerAngles}, muzzleLocalPos: {mp?.localPosition}, muzzleWorldPos: {mp?.position}</color>");
        }

        [ContextMenu("Setup Test Environment")]
        public void SetupTestEnvironment()
        {
            SetupLightingAndFloor();
            Camera cam = SetupPlayerAndCamera();
            SetupCylinderAmmoHUD();
            SetupSpectrumGun(cam);
            EnsureCrosshairPresentation();
            SetupTestTargets();
        }

        private static void EnsureCrosshairPresentation()
        {
            CrosshairUI crosshair = FindFirstObjectByType<CrosshairUI>();
            if (crosshair != null && crosshair.GetComponent<CrosshairSettingsUI>() == null)
            {
                crosshair.gameObject.AddComponent<CrosshairSettingsUI>();
            }
        }

        private void SetupLightingAndFloor()
        {
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
                floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            }

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
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                var existingController = FindFirstObjectByType<CharacterController>();
                if (existingController != null)
                {
                    player = existingController.gameObject;
                }
            }

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
                    Camera oldCam = Camera.main;
                    if (oldCam != null && oldCam.GetComponentInParent<CharacterController>() == null)
                    {
                        DestroyImmediate(oldCam.gameObject);
                    }

                    player = Instantiate(prefab, new Vector3(0f, 0f, -3f), Quaternion.identity);
                    player.name = "Player";
                }
            }

            Camera cam = null;
            if (player != null)
            {
                cam = player.GetComponentInChildren<Camera>();
            }

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

            Transform existingGun = cam.transform.Find("SpectrumGun");
            if (existingGun != null)
            {
                RaycastWeapon rw = existingGun.GetComponent<RaycastWeapon>();
                if (rw != null && rw.PlayerCamera == null) rw.SetCamera(cam);

                Transform oldMockup = existingGun.Find("WeaponMockup");
                Transform revModel = existingGun.Find("RevolverModel");
                MeshRenderer[] renderers = existingGun.GetComponentsInChildren<MeshRenderer>(true);
                if (oldMockup != null || revModel == null || renderers.Length == 0 || revModel.localScale.x < 1.0f)
                {
                    RevolverWeaponBuilder.BuildRevolver(existingGun, out Transform muzzlePoint, out Light muzzleLight);
                    if (rw != null) rw.SetMuzzlePoint(muzzlePoint);
                    WeaponImpactFeedback fb = existingGun.GetComponent<WeaponImpactFeedback>();
                    if (fb != null) fb.SetMuzzleLight(muzzleLight);
                }
                else
                {
                    if (revModel.localRotation.eulerAngles.y < 200f || revModel.localRotation.eulerAngles.y > 300f)
                    {
                        revModel.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    }

                    MeshRenderer barrelRend = null;
                    foreach (var r in revModel.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.gameObject.name.ToLower().Contains("barrel")) { barrelRend = r; break; }
                    }

                    Transform mp = existingGun.Find("MuzzlePoint");
                    if (mp != null)
                    {
                        mp.localPosition = RevolverWeaponBuilder.CalculateBarrelTipLocalPosition(existingGun, barrelRend);
                        if (rw != null) rw.SetMuzzlePoint(mp);
                    }
                    Transform cg = existingGun.Find("ChamberGlow");
                    if (cg != null) cg.localPosition = RevolverWeaponBuilder.DefaultChamberGlowLocalPosition;
                }

                existingGun.localPosition = RevolverWeaponBuilder.DefaultViewmodelLocalPosition;
                existingGun.localRotation = Quaternion.Euler(RevolverWeaponBuilder.DefaultViewmodelLocalEuler);
                return;
            }

            SpectrumGunController existingController = FindFirstObjectByType<SpectrumGunController>();
            if (existingController != null)
            {
                existingController.transform.SetParent(cam.transform, false);
                existingController.transform.localPosition = RevolverWeaponBuilder.DefaultViewmodelLocalPosition;
                existingController.transform.localRotation = Quaternion.Euler(RevolverWeaponBuilder.DefaultViewmodelLocalEuler);
                RaycastWeapon existingRaycast = existingController.GetComponent<RaycastWeapon>();
                if (existingRaycast != null) existingRaycast.SetCamera(cam);

                Transform oldMockup = existingController.transform.Find("WeaponMockup");
                Transform revModel = existingController.transform.Find("RevolverModel");
                MeshRenderer[] renderers = existingController.GetComponentsInChildren<MeshRenderer>(true);
                if (oldMockup != null || revModel == null || renderers.Length == 0 || revModel.localScale.x < 1.0f)
                {
                    RevolverWeaponBuilder.BuildRevolver(existingController.transform, out Transform muzzlePoint, out Light muzzleLight);
                    if (existingRaycast != null) existingRaycast.SetMuzzlePoint(muzzlePoint);
                    WeaponImpactFeedback fb = existingController.GetComponent<WeaponImpactFeedback>();
                    if (fb != null) fb.SetMuzzleLight(muzzleLight);
                }
                else
                {
                    if (revModel.localRotation.eulerAngles.y < 200f || revModel.localRotation.eulerAngles.y > 300f)
                    {
                        revModel.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    }

                    MeshRenderer barrelRend = null;
                    foreach (var r in revModel.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.gameObject.name.ToLower().Contains("barrel")) { barrelRend = r; break; }
                    }

                    Transform mp = existingController.transform.Find("MuzzlePoint");
                    if (mp != null)
                    {
                        mp.localPosition = RevolverWeaponBuilder.CalculateBarrelTipLocalPosition(existingController.transform, barrelRend);
                        if (existingRaycast != null) existingRaycast.SetMuzzlePoint(mp);
                    }
                    Transform cg = existingController.transform.Find("ChamberGlow");
                    if (cg != null) cg.localPosition = RevolverWeaponBuilder.DefaultChamberGlowLocalPosition;
                }
                return;
            }

            // Viewmodel root attached under Camera
            GameObject gunRoot = new GameObject("SpectrumGun");
            gunRoot.transform.SetParent(cam.transform, false);
            gunRoot.transform.localPosition = RevolverWeaponBuilder.DefaultViewmodelLocalPosition;
            gunRoot.transform.localRotation = Quaternion.Euler(RevolverWeaponBuilder.DefaultViewmodelLocalEuler);

            RaycastWeapon raycastWeapon = gunRoot.AddComponent<RaycastWeapon>();
            raycastWeapon.SetCamera(cam);

            WeaponImpactFeedback feedback = gunRoot.AddComponent<WeaponImpactFeedback>();
            SpectrumGunController controller = gunRoot.AddComponent<SpectrumGunController>();

            RevolverWeaponBuilder.BuildRevolver(gunRoot.transform, out Transform newMuzzlePoint, out Light newMuzzleLight);

            raycastWeapon.SetMuzzlePoint(newMuzzlePoint);
            feedback.SetMuzzleLight(newMuzzleLight);

            if (FindFirstObjectByType<CrosshairUI>() == null)
            {
                GameObject crosshairObj = new GameObject("CrosshairUI_Manager");
                crosshairObj.transform.SetParent(gunRoot.transform, false);
                crosshairObj.AddComponent<CrosshairUI>();
                crosshairObj.AddComponent<CrosshairSettingsUI>();
            }

            controller.SetColorMode(SpectrumColorMode.Red);
        }

        private void SetupTestTargets()
        {
            Transform targetsContainer = GameObject.Find("TestTargets_Group")?.transform;
            if (targetsContainer != null)
            {
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

                    // Ensure Rigidbody & SpectrumSkillResponder are attached so RED Impulse, GREEN Bind, and CYAN Stasis work on scene targets!
                    Rigidbody rb = child.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = child.gameObject.AddComponent<Rigidbody>();
                        rb.mass = 1.5f;
                        rb.linearDamping = 0.5f;
                        rb.angularDamping = 0.5f;
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                    }

                    SpectrumSkillResponder responder = child.GetComponent<SpectrumSkillResponder>();
                    if (responder == null)
                    {
                        responder = child.gameObject.AddComponent<SpectrumSkillResponder>();
                    }
                    responder.EnsureInitialized();

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
                targetObj.transform.position = new Vector3(xOffsets[i], 1.1f, dist);
                targetObj.transform.localScale = new Vector3(1f, 1.8f, 1f);

                Material mat = new Material(litShader)
                {
                    name = $"Mat_{targetNames[i]}",
                    color = neutralGrey
                };
                targetObj.GetComponent<Renderer>().sharedMaterial = mat;

                Rigidbody rb = targetObj.AddComponent<Rigidbody>();
                rb.mass = 1.5f;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                RGBColorTarget colorTarget = targetObj.AddComponent<RGBColorTarget>();
                SpectrumSkillResponder responder = targetObj.AddComponent<SpectrumSkillResponder>();
                responder.EnsureInitialized();
                colorTarget.ResetColors();
            }
        }

        private void SetupCylinderAmmoHUD()
        {
            var existingHud = FindFirstObjectByType<Weapons.Ammo.CylinderAmmoHUD>();
            if (existingHud != null)
            {
                existingHud.BuildHUDHierarchy();
                return;
            }

            GameObject hudManagerObj = new GameObject("CylinderAmmoHUD_System");
            hudManagerObj.AddComponent<Weapons.Ammo.CylinderAmmoController>();
            hudManagerObj.AddComponent<Weapons.Ammo.CylinderRotationController>();
            var hud = hudManagerObj.AddComponent<Weapons.Ammo.CylinderAmmoHUD>();
            hudManagerObj.AddComponent<Weapons.Ammo.AmmoTestInput>();
            hud.BuildHUDHierarchy();
        }
    }
}
