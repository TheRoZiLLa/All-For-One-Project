using UnityEngine;
using UnityEngine.InputSystem;
using Recorded.ColorSystem;

namespace Recorded.Weapons
{
    public enum SpectrumColorMode
    {
        Red,    // Impulse (provisional)
        Green,  // Bind (provisional)
        Blue    // Stasis (provisional)
    }

    /// <summary>
    /// Master controller for Spectrum Gun prototype.
    /// Manages fire rate, input handling, real-time RGB mode switching (Keys 1, 2, 3 / Scroll Wheel),
    /// and synchronizes colors across Weapon Model Core, Crosshair, and VFX.
    /// </summary>
    public class SpectrumGunController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RaycastWeapon raycastWeapon;
        [SerializeField] private WeaponImpactFeedback impactFeedback;
        [SerializeField] private Renderer rgbCoreRenderer;
        [SerializeField] private UI.CrosshairUI crosshairUI;

        [Header("Ammunition System Integration")]
        [SerializeField] private Ammo.CylinderAmmoController ammoController;
        [SerializeField] private Ammo.CylinderRotationController rotationController;

        [Header("Fire Settings")]
        [Tooltip("Rounds per second.")]
        [SerializeField] private float fireRate = 5f;

        [Tooltip("If true, gun fires continuously while holding fire button. If false, semi-auto.")]
        [SerializeField] private bool isAutomatic = false;

        [Header("Input Configuration")]
        [Tooltip("Optional Input Action Reference for Attack. If not set, reads default mouse left-click.")]
        [SerializeField] private InputActionReference attackAction;

        [Header("RGB Spectrum Mode (Fallback)")]
        [SerializeField] private SpectrumColorMode currentMode = SpectrumColorMode.Red;

        // Color definitions for RGB Spectrum
        public static readonly Color ColorRed = new Color(1f, 0.18f, 0.18f, 1f);    // Impulse Red
        public static readonly Color ColorGreen = new Color(0.2f, 1f, 0.35f, 1f);   // Bind Green
        public static readonly Color ColorBlue = new Color(0.18f, 0.88f, 1f, 1f);  // Stasis Cyan / Light Blue

        private float _nextFireTime;
        private InputAction _resolvedAttackAction;

        public SpectrumColorMode CurrentMode => currentMode;
        public float FireRate { get => fireRate; set => fireRate = Mathf.Max(0.1f, value); }
        public bool IsAutomatic { get => isAutomatic; set => isAutomatic = value; }

        private void Awake()
        {
            if (raycastWeapon == null)
            {
                raycastWeapon = GetComponent<RaycastWeapon>();
            }

            if (impactFeedback == null)
            {
                impactFeedback = GetComponent<WeaponImpactFeedback>();
            }

            if (ammoController == null)
            {
                ammoController = FindFirstObjectByType<Ammo.CylinderAmmoController>();
            }

            if (rotationController == null)
            {
                rotationController = FindFirstObjectByType<Ammo.CylinderRotationController>();
            }

            ResolveCoreRenderer();
        }

        private void Start()
        {
            if (ammoController != null)
            {
                SyncWithActiveChamber();
            }
            else
            {
                ApplyColorMode(currentMode);
            }
        }

        private void OnEnable()
        {
            if (attackAction != null && attackAction.action != null)
            {
                _resolvedAttackAction = attackAction.action;
                _resolvedAttackAction.Enable();
            }

            if (ammoController != null)
            {
                ammoController.OnActiveChamberChanged += HandleActiveChamberChanged;
                ammoController.OnChamberStateChanged += HandleChamberStateChanged;
                ammoController.OnCylinderReloaded += HandleCylinderReloaded;
            }
        }

        private void OnDisable()
        {
            if (_resolvedAttackAction != null)
            {
                _resolvedAttackAction.Disable();
            }

            if (ammoController != null)
            {
                ammoController.OnActiveChamberChanged -= HandleActiveChamberChanged;
                ammoController.OnChamberStateChanged -= HandleChamberStateChanged;
                ammoController.OnCylinderReloaded -= HandleCylinderReloaded;
            }
        }

        private void Update()
        {
            CheckColorSwitchInput();

            if (CheckFireInput())
            {
                AttemptFire();
            }
        }

        private void CheckColorSwitchInput()
        {
            // If cylinder ammo system is present, mouse scroll wheel and Q/E rotate the cylinder!
            if (rotationController != null && !rotationController.IsRotating)
            {
                // 1. Mouse Scroll Wheel
                if (Mouse.current != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (scroll > 0.1f)
                    {
                        rotationController.RotateLeft(); // Scroll UP (away) -> Previous chamber
                        return;
                    }
                    else if (scroll < -0.1f)
                    {
                        rotationController.RotateRight(); // Scroll DOWN (towards) -> Next chamber
                        return;
                    }
                }

                // 2. Q / E manual rotation
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.qKey.wasPressedThisFrame)
                    {
                        rotationController.RotateLeft();
                        return;
                    }
                    else if (Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        rotationController.RotateRight();
                        return;
                    }
                }
            }
            else if (ammoController == null)
            {
                // Fallback for standalone prototype without cylinder HUD: Keys 1, 2, 3 and legacy scroll
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                    {
                        SetColorMode(SpectrumColorMode.Red);
                    }
                    else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                    {
                        SetColorMode(SpectrumColorMode.Green);
                    }
                    else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                    {
                        SetColorMode(SpectrumColorMode.Blue);
                    }
                }

                if (Mouse.current != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (scroll > 0.1f)
                    {
                        CycleColorMode(-1);
                    }
                    else if (scroll < -0.1f)
                    {
                        CycleColorMode(1);
                    }
                }
            }

            // Test reload (R key)
            if (ammoController != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ammoController.TestReloadRandom();
                Debug.Log("<color=#FFE84D><b>[Spectrum Gun]</b> Test Reload (R): Filled all 6 chambers with randomized RED, GREEN, CYAN distribution.</color>");
            }
        }

        public void CycleColorMode(int direction)
        {
            int total = 3;
            int next = ((int)currentMode + direction) % total;
            if (next < 0) next += total;
            SetColorMode((SpectrumColorMode)next);
        }

        private bool CheckFireInput()
        {
            // 1. If an InputAction is bound, check it
            if (_resolvedAttackAction != null)
            {
                return isAutomatic
                    ? _resolvedAttackAction.IsPressed()
                    : _resolvedAttackAction.WasPressedThisFrame();
            }

            // 2. Direct New Input System Mouse check (failsafe & standalone testing)
            if (Mouse.current != null)
            {
                return isAutomatic
                    ? Mouse.current.leftButton.isPressed
                    : Mouse.current.leftButton.wasPressedThisFrame;
            }

            return false;
        }

        /// <summary>
        /// Execute shot if cooldown has elapsed and cylinder has ammo at the 12 o'clock firing position.
        /// Consumes the round, fires raycast with matching color, and auto-advances the cylinder.
        /// </summary>
        public bool AttemptFire()
        {
            if (Time.time < _nextFireTime) return false;

            if (rotationController != null && rotationController.IsRotating)
            {
                return false; // Prevent firing while cylinder is actively rotating
            }

            // Cylinder Ammo Logic
            if (ammoController != null)
            {
                if (!ammoController.HasActiveRound)
                {
                    // Dry fire: Chamber at 12 o'clock is empty
                    Debug.Log("<color=#888888><b>[Spectrum Gun]</b> *CLICK* (Dry Fire) - 12 o'clock chamber is EMPTY. No shot fired, cylinder does not advance.</color>");
                    _nextFireTime = Time.time + 0.25f; // Short dry-fire debounce
                    return false;
                }

                if (!ammoController.TryConsumeActiveRound(out Ammo.AmmoType firedAmmo))
                {
                    return false;
                }

                _nextFireTime = Time.time + (1f / fireRate);

                Color firedColor = GetAmmoColor(firedAmmo);
                if (impactFeedback != null)
                {
                    impactFeedback.SetFeedbackColor(firedColor);
                }

                // Map fired ammo type to RGB object color state
                ObjectColorState shotColorState = ObjectColorState.None;
                switch (firedAmmo)
                {
                    case Ammo.AmmoType.Red:
                        shotColorState = ObjectColorState.Red;
                        break;
                    case Ammo.AmmoType.Green:
                        shotColorState = ObjectColorState.Green;
                        break;
                    case Ammo.AmmoType.Cyan:
                        shotColorState = ObjectColorState.Blue; // Cyan ammo maps to primary Blue flag
                        break;
                }

                if (raycastWeapon != null)
                {
                    Transform muzzle = raycastWeapon.MuzzlePoint;
                    Vector3 muzzlePos = muzzle != null ? muzzle.position : transform.position;

                    bool hit = raycastWeapon.FireRaycast(out RaycastHit hitInfo, out Vector3 targetPoint);

                    if (hit)
                    {
                        // Apply color state to hit target
                        if (shotColorState != ObjectColorState.None)
                        {
                            IColorReceivable target = hitInfo.collider.GetComponentInParent<IColorReceivable>();
                            if (target != null)
                            {
                                target.ApplyColor(shotColorState);
                            }
                        }

                        if (impactFeedback != null)
                        {
                            impactFeedback.PlayHitFeedback(muzzlePos, hitInfo);
                        }
                    }
                    else
                    {
                        if (impactFeedback != null)
                        {
                            impactFeedback.PlayMissFeedback(muzzlePos, targetPoint);
                        }
                    }
                }

                Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(firedColor)}><b>[Spectrum Gun]</b> Fired <b>{firedAmmo.ToString().ToUpper()}</b> round from active chamber! Auto-advancing cylinder...</color>");

                // Automatically advance the cylinder to the next chamber following a successful shot
                if (rotationController != null)
                {
                    rotationController.AutoAdvance();
                }

                return true;
            }

            // Fallback for standalone gun testing without cylinder
            _nextFireTime = Time.time + (1f / fireRate);

            if (raycastWeapon == null) return false;

            ObjectColorState fallbackState = ObjectColorState.None;
            switch (currentMode)
            {
                case SpectrumColorMode.Red: fallbackState = ObjectColorState.Red; break;
                case SpectrumColorMode.Green: fallbackState = ObjectColorState.Green; break;
                case SpectrumColorMode.Blue: fallbackState = ObjectColorState.Blue; break;
            }

            Transform fallbackMuzzle = raycastWeapon.MuzzlePoint;
            Vector3 fallbackMuzzlePos = fallbackMuzzle != null ? fallbackMuzzle.position : transform.position;

            bool fallbackHit = raycastWeapon.FireRaycast(out RaycastHit fallbackHitInfo, out Vector3 fallbackTargetPoint);

            if (fallbackHit)
            {
                if (fallbackState != ObjectColorState.None)
                {
                    IColorReceivable target = fallbackHitInfo.collider.GetComponentInParent<IColorReceivable>();
                    if (target != null)
                    {
                        target.ApplyColor(fallbackState);
                    }
                }

                if (impactFeedback != null)
                {
                    impactFeedback.PlayHitFeedback(fallbackMuzzlePos, fallbackHitInfo);
                }
            }
            else
            {
                if (impactFeedback != null)
                {
                    impactFeedback.PlayMissFeedback(fallbackMuzzlePos, fallbackTargetPoint);
                }
            }

            return true;
        }

        private void HandleActiveChamberChanged(int activeIndex)
        {
            SyncWithActiveChamber();
        }

        private void HandleChamberStateChanged(int chamberIndex, Ammo.AmmoType newAmmo)
        {
            if (ammoController != null && chamberIndex == ammoController.ActiveChamberIndex)
            {
                SyncWithActiveChamber();
            }
        }

        private void HandleCylinderReloaded()
        {
            SyncWithActiveChamber();
        }

        /// <summary>
        /// Synchronizes the 3D gun core visual, crosshair color, and impact feedback with the 12 o'clock chamber.
        /// </summary>
        public void SyncWithActiveChamber()
        {
            if (ammoController == null) return;

            Ammo.AmmoType activeAmmo = ammoController.ActiveAmmoType;
            Color activeColor = GetAmmoColor(activeAmmo);

            // 1. Update RGB Core Chamber material & emission
            ResolveCoreRenderer();
            if (rgbCoreRenderer != null && rgbCoreRenderer.material != null)
            {
                if (activeAmmo == Ammo.AmmoType.Empty)
                {
                    rgbCoreRenderer.material.color = new Color(0.08f, 0.08f, 0.1f, 1f);
                    if (rgbCoreRenderer.material.HasProperty("_EmissionColor"))
                    {
                        rgbCoreRenderer.material.SetColor("_EmissionColor", Color.black);
                    }
                }
                else
                {
                    rgbCoreRenderer.material.color = activeColor;
                    if (rgbCoreRenderer.material.HasProperty("_EmissionColor"))
                    {
                        rgbCoreRenderer.material.EnableKeyword("_EMISSION");
                        rgbCoreRenderer.material.SetColor("_EmissionColor", activeColor * 2.0f);
                    }
                }
            }

            // 2. Update Impact Feedback (Tracer line, Muzzle light, Impact flash)
            if (impactFeedback != null && activeAmmo != Ammo.AmmoType.Empty)
            {
                impactFeedback.SetFeedbackColor(activeColor);
            }

            // 3. Update Crosshair UI color
            if (crosshairUI == null)
            {
                crosshairUI = GetComponentInChildren<UI.CrosshairUI>();
                if (crosshairUI == null)
                {
                    crosshairUI = FindFirstObjectByType<UI.CrosshairUI>();
                }
            }

            if (crosshairUI != null)
            {
                crosshairUI.SetCrosshairColor(activeAmmo == Ammo.AmmoType.Empty ? new Color(0.4f, 0.4f, 0.45f, 0.5f) : activeColor);
            }
        }

        public void SetColorMode(SpectrumColorMode mode)
        {
            currentMode = mode;
            ApplyColorMode(mode);
        }

        private void ApplyColorMode(SpectrumColorMode mode)
        {
            Color activeColor = GetModeColor(mode);

            // 1. Update RGB Core Chamber material & emission
            ResolveCoreRenderer();
            if (rgbCoreRenderer != null && rgbCoreRenderer.material != null)
            {
                rgbCoreRenderer.material.color = activeColor;
                if (rgbCoreRenderer.material.HasProperty("_EmissionColor"))
                {
                    rgbCoreRenderer.material.EnableKeyword("_EMISSION");
                    rgbCoreRenderer.material.SetColor("_EmissionColor", activeColor * 2.0f);
                }
            }

            // 2. Update Impact Feedback (Tracer line, Muzzle light, Impact flash)
            if (impactFeedback != null)
            {
                impactFeedback.SetFeedbackColor(activeColor);
            }

            // 3. Update Crosshair UI color
            if (crosshairUI == null)
            {
                crosshairUI = GetComponentInChildren<UI.CrosshairUI>();
                if (crosshairUI == null)
                {
                    crosshairUI = FindFirstObjectByType<UI.CrosshairUI>();
                }
            }

            if (crosshairUI != null)
            {
                crosshairUI.SetCrosshairColor(activeColor);
            }

            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(activeColor)}><b>[Spectrum Gun]</b> Switched Mode: {mode.ToString().ToUpper()}</color>");
        }

        private void ResolveCoreRenderer()
        {
            if (rgbCoreRenderer == null)
            {
                Transform coreTransform = transform.Find("WeaponMockup/Part_RGBCoreChamber");
                if (coreTransform != null)
                {
                    rgbCoreRenderer = coreTransform.GetComponent<Renderer>();
                }
            }
        }

        public void SetCoreRenderer(Renderer coreRend)
        {
            rgbCoreRenderer = coreRend;
            if (rgbCoreRenderer != null)
            {
                if (ammoController != null)
                {
                    SyncWithActiveChamber();
                }
                else
                {
                    ApplyColorMode(currentMode);
                }
            }
        }

        public static Color GetAmmoColor(Ammo.AmmoType type)
        {
            switch (type)
            {
                case Ammo.AmmoType.Red: return ColorRed;
                case Ammo.AmmoType.Green: return ColorGreen;
                case Ammo.AmmoType.Cyan: return ColorBlue;
                default: return new Color(0.2f, 0.2f, 0.2f, 0.3f);
            }
        }

        public static Color GetModeColor(SpectrumColorMode mode)
        {
            switch (mode)
            {
                case SpectrumColorMode.Red: return ColorRed;
                case SpectrumColorMode.Green: return ColorGreen;
                case SpectrumColorMode.Blue: return ColorBlue;
                default: return ColorRed;
            }
        }
    }
}
