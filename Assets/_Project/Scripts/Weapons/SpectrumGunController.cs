using UnityEngine;
using UnityEngine.InputSystem;
using Recorded.ColorSystem;

namespace Recorded.Weapons
{
    public readonly struct SpectrumShotRecord
    {
        public readonly Ammo.AmmoType Ammo;
        public readonly ObjectColorState Color;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;

        public SpectrumShotRecord(Ammo.AmmoType ammo, ObjectColorState color, Vector3 origin, Vector3 direction)
        {
            Ammo = ammo;
            Color = color;
            Origin = origin;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }
    }

    public enum SpectrumColorMode
    {
        Red,    // Impulse
        Green,  // Bind
        Blue    // Stasis (Cyan)
    }

    /// <summary>
    /// Master controller for the RECORDED Spectrum Gun.
    /// Manages fire rate, input handling, 8-chamber cylinder integration, first-person viewmodel
    /// ergonomics/recoil animation, and real-time color synchronization across Weapon Core, Crosshair, and VFX.
    /// </summary>
    public class SpectrumGunController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RaycastWeapon raycastWeapon;
        [SerializeField] private WeaponImpactFeedback impactFeedback;
        [SerializeField] private Renderer rgbCoreRenderer;
        [SerializeField] private Light chamberGlowLight;
        [SerializeField] private UI.CrosshairUI crosshairUI;
        [SerializeField] private Animator shotAnimator;
        private static readonly int FireTrigger = Animator.StringToHash("Fire");
        private static readonly int ReloadTrigger = Animator.StringToHash("Reload");
        public bool IsReloading => _isReloading;
        public const float ReloadRefillPoint = ReloadInsertionEnd;
        public const float ReloadInsertionStart = 0.40f;
        public const float ReloadInsertionEnd = 0.66f;
        public bool ReloadAmmoLoaded { get; private set; }
        public float ReloadProgress => TryGetReloadState(out var state) ? Mathf.Clamp01(state.normalizedTime) : 0f;
        public event System.Action<SpectrumShotRecord> ShotFired;
        public Ammo.CylinderAmmoController AmmoController => ammoController;
        public Ammo.CylinderRotationController RotationController => rotationController;
        // Cartridges travel into the bores from normalized 0.40 to 0.66. Pauses freeze this with the clip.
        public float ReloadInsertion => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ReloadInsertionStart, ReloadInsertionEnd, ReloadProgress));
        private bool _isReloading;
        private Coroutine _reloadRoutine;

        [Header("Ammunition System Integration")]
        [SerializeField] private Ammo.CylinderAmmoController ammoController;
        [SerializeField] private Ammo.CylinderRotationController rotationController;

        [Header("Fire Settings")]
        [Tooltip("Rounds per second.")]
        [SerializeField] private float fireRate = 5f;

        [Tooltip("If true, gun fires continuously while holding fire button. If false, semi-auto.")]
        [SerializeField] private bool isAutomatic = false;

        [Header("First-Person Recoil & Sway Polish")]
        [SerializeField] private float recoilKickbackZ = 0.028f;
        [SerializeField] private float recoilKickUpPitch = 6.5f;
        [SerializeField] private float recoilRecoverySpeed = 14f;

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
        private Transform _revolverModelTransform;
        private Vector3 _modelBaseLocalPos = Vector3.zero;
        private Quaternion _modelBaseLocalRot = Quaternion.Euler(0f, -90f, 0f);
        private float _currentRecoil;
        private Light[] _glowLights;

        public SpectrumColorMode CurrentMode => currentMode;
        public float FireRate { get => fireRate; set => fireRate = Mathf.Max(0.1f, value); }
        public bool IsAutomatic { get => isAutomatic; set => isAutomatic = value; }

        private void Awake()
        {
            ResolveDependencies();
        }

        private void Start()
        {
            ResolveDependencies();
            EnsureViewmodelAndMuzzleAlignment();
            if (transform.Find("Barrel") != null && GetComponent<RevolverReloadPresentation>() == null)
                gameObject.AddComponent<RevolverReloadPresentation>();

            if (ammoController != null)
            {
                SyncWithActiveChamber();
            }
            else
            {
                ApplyColorMode(currentMode);
            }
        }

        public void ResolveDependencies()
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

            if (_revolverModelTransform == null)
            {
                _revolverModelTransform = transform.Find("RevolverModel");
                if (_revolverModelTransform != null)
                {
                    _modelBaseLocalPos = _revolverModelTransform.localPosition;
                    _modelBaseLocalRot = _revolverModelTransform.localRotation;
                }
            }

            ResolveCoreRenderer();
            if (shotAnimator == null) shotAnimator = GetComponentInParent<Animator>();
        }

        private void EnsureViewmodelAndMuzzleAlignment()
        {
            // A hand-mounted REV keeps its authored grip and calibrated muzzle, with no model recoil.
            if (_revolverModelTransform == null) return;
            // Ensure first-person viewmodel sits naturally close to the player's right hand rather than floating far away
            if (transform.parent != null && transform.parent.GetComponent<Camera>() != null)
            {
                if (transform.localPosition.z > 0.34f)
                {
                    transform.localPosition = RevolverWeaponBuilder.DefaultViewmodelLocalPosition;
                    transform.localRotation = Quaternion.Euler(RevolverWeaponBuilder.DefaultViewmodelLocalEuler);
                }
            }

            // Ensure MuzzlePoint is aligned with the upper barrel tip
            Transform mp = transform.Find("MuzzlePoint");
            if (mp != null)
            {
                MeshRenderer barrelRend = null;
                if (_revolverModelTransform != null)
                {
                    foreach (var r in _revolverModelTransform.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.gameObject.name.ToLower().Contains("barrel"))
                        {
                            barrelRend = r;
                            break;
                        }
                    }
                }
                mp.localPosition = RevolverWeaponBuilder.CalculateBarrelTipLocalPosition(transform, barrelRend);

                if (raycastWeapon != null)
                {
                    raycastWeapon.SetMuzzlePoint(mp);
                }

                if (impactFeedback != null)
                {
                    Light ml = mp.GetComponent<Light>();
                    if (ml != null) impactFeedback.SetMuzzleLight(ml);
                }
            }
        }

        private void OnEnable()
        {
            ResolveDependencies();

            if (attackAction != null && attackAction.action != null)
            {
                _resolvedAttackAction = attackAction.action;
                _resolvedAttackAction.Enable();
            }

            if (ammoController != null)
            {
                ammoController.OnActiveChamberChanged -= HandleActiveChamberChanged;
                ammoController.OnChamberStateChanged -= HandleChamberStateChanged;
                ammoController.OnCylinderReloaded -= HandleCylinderReloaded;

                ammoController.OnActiveChamberChanged += HandleActiveChamberChanged;
                ammoController.OnChamberStateChanged += HandleChamberStateChanged;
                ammoController.OnCylinderReloaded += HandleCylinderReloaded;
            }
        }

        private void OnDisable()
        {
            bool wasReloading = _isReloading;
            if (_reloadRoutine != null) StopCoroutine(_reloadRoutine);
            _reloadRoutine = null;
            _isReloading = false;
            ReloadAmmoLoaded = false;
            if (gameObject.activeInHierarchy && shotAnimator != null && shotAnimator.gameObject.activeInHierarchy
                && shotAnimator.isActiveAndEnabled && shotAnimator.isInitialized)
            {
                shotAnimator.ResetTrigger(FireTrigger);
                shotAnimator.ResetTrigger(ReloadTrigger);
                if (wasReloading) shotAnimator.Play("Ready", 0, 0f);
            }
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
            UpdateRecoilAnimation();
            if (crosshairUI != null && crosshairUI.TryGetComponent<UI.CrosshairSettingsUI>(out var settings) && settings.IsOpen) return;
            CheckColorSwitchInput();

            if (CheckFireInput())
            {
                AttemptFire();
            }
        }

        private void UpdateRecoilAnimation()
        {
            if (_revolverModelTransform == null) return;

            if (_currentRecoil > 0.0001f)
            {
                _currentRecoil = Mathf.MoveTowards(_currentRecoil, 0f, Time.deltaTime * recoilRecoverySpeed);
                Vector3 recoilOffset = new Vector3(0f, _currentRecoil * 0.008f, -_currentRecoil * recoilKickbackZ);
                // Because RevolverModel has localRotation Y=-90, local +Z of gunRoot corresponds to model recoil pitch around Z
                Quaternion pitchRot = Quaternion.Euler(0f, 0f, -_currentRecoil * recoilKickUpPitch);
                _revolverModelTransform.localPosition = _modelBaseLocalPos + recoilOffset;
                _revolverModelTransform.localRotation = _modelBaseLocalRot * pitchRot;
            }
            else
            {
                _revolverModelTransform.localPosition = _modelBaseLocalPos;
                _revolverModelTransform.localRotation = _modelBaseLocalRot;
            }
        }

        private void TriggerRecoilImpulse(float strength = 1.0f)
        {
            _currentRecoil = Mathf.Clamp01(_currentRecoil + strength);
        }

        private void CheckColorSwitchInput()
        {
            if (_isReloading) return;
            if (rotationController == null)
            {
                rotationController = FindFirstObjectByType<Ammo.CylinderRotationController>();
            }

            // If cylinder ammo system is present, mouse scroll wheel and Q/E rotate the cylinder!
            if (rotationController != null)
            {
                // 1. Mouse Scroll Wheel
                if (Mouse.current != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (scroll > 0.1f)
                    {
                        rotationController.RotateLeft(); // Scroll UP -> Previous/Left chamber
                        return;
                    }
                    else if (scroll < -0.1f)
                    {
                        rotationController.RotateRight(); // Scroll DOWN -> Next/Right chamber
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
                        var body = GetComponentInParent<CharacterController>();
                        var player = body != null ? body.transform : transform.root;
                        if (!SecurityKioskEInteraction.HasInteractionFor(player)) rotationController.RotateRight();
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

            // Fallback Test reload (R key) only if AmmoTestInput is not active on the ammo controller
            if (ammoController != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                var dedicatedTestInput = ammoController.GetComponent<Ammo.AmmoTestInput>();
                if (dedicatedTestInput == null || !dedicatedTestInput.isActiveAndEnabled)
                {
                    TriggerReload();
                }
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
            if (_isReloading) return false;
            if (_resolvedAttackAction != null)
            {
                return isAutomatic
                    ? _resolvedAttackAction.IsPressed()
                    : _resolvedAttackAction.WasPressedThisFrame();
            }

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
        /// Consumes the round, fires raycast from exact MuzzlePoint with matching color, and auto-advances the cylinder.
        /// </summary>
        public bool AttemptFire()
        {
            if (_isReloading || Time.time < _nextFireTime) return false;

            ResolveDependencies();
            if (raycastWeapon == null) return false;

            if (raycastWeapon == null) return false;

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
                    TriggerRecoilImpulse(0.22f);
                    _nextFireTime = Time.time + 0.22f; // Short dry-fire debounce
                    return false;
                }

                if (!ammoController.TryConsumeActiveRound(out Ammo.AmmoType firedAmmo))
                {
                    return false;
                }

                _nextFireTime = Time.time + (1f / fireRate);
                TriggerRecoilImpulse(1.0f);

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
                        Vector3 shotDir = (hitInfo.point - muzzlePos).sqrMagnitude > 0.0001f
                            ? (hitInfo.point - muzzlePos).normalized
                            : transform.forward;

                        DeliverColorPayload(hitInfo, firedAmmo, shotColorState, shotDir);
                        NotifyShotFired(muzzlePos, shotDir, firedAmmo, shotColorState);

                        if (impactFeedback != null)
                        {
                            impactFeedback.PlayHitFeedback(muzzlePos, hitInfo);
                        }
                    }
                    else
                    {
                        Vector3 missDir = (targetPoint - muzzlePos).sqrMagnitude > 0.0001f
                            ? (targetPoint - muzzlePos).normalized
                            : transform.forward;
                        NotifyShotFired(muzzlePos, missDir, firedAmmo, shotColorState);
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

                PlayShotAnimation();
                return true;
            }

            // Fallback for standalone gun testing without cylinder
            _nextFireTime = Time.time + (1f / fireRate);
            TriggerRecoilImpulse(1.0f);

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
            Ammo.AmmoType fallbackAmmo = Ammo.AmmoType.Red;
            if (currentMode == SpectrumColorMode.Green) fallbackAmmo = Ammo.AmmoType.Green;
            else if (currentMode == SpectrumColorMode.Blue) fallbackAmmo = Ammo.AmmoType.Cyan;

            if (fallbackHit)
            {
                Vector3 fallbackShotDir = (fallbackHitInfo.point - fallbackMuzzlePos).sqrMagnitude > 0.0001f
                    ? (fallbackHitInfo.point - fallbackMuzzlePos).normalized
                    : transform.forward;

                DeliverColorPayload(fallbackHitInfo, fallbackAmmo, fallbackState, fallbackShotDir);
                NotifyShotFired(fallbackMuzzlePos, fallbackShotDir, fallbackAmmo, fallbackState);

                if (impactFeedback != null)
                {
                    impactFeedback.PlayHitFeedback(fallbackMuzzlePos, fallbackHitInfo);
                }
            }
            else
            {
                Vector3 fallbackMissDir = (fallbackTargetPoint - fallbackMuzzlePos).sqrMagnitude > 0.0001f
                    ? (fallbackTargetPoint - fallbackMuzzlePos).normalized
                    : transform.forward;
                NotifyShotFired(fallbackMuzzlePos, fallbackMissDir, fallbackAmmo, fallbackState);
                if (impactFeedback != null)
                {
                    impactFeedback.PlayMissFeedback(fallbackMuzzlePos, fallbackTargetPoint);
                }
            }

            PlayShotAnimation();
            return true;
        }

        private void NotifyShotFired(Vector3 origin, Vector3 direction, Ammo.AmmoType ammo, ObjectColorState color)
        {
            ShotFired?.Invoke(new SpectrumShotRecord(ammo, color, origin, direction));
        }

        /// <summary>
        /// Hitscan from a recorded hologram shot. Does not spend a chamber.
        /// Skips the player and the hologram so the echo can paint the same target.
        /// </summary>
        public void ReplayRecordedShot(Vector3 origin, Vector3 direction, Ammo.AmmoType ammo, ObjectColorState color, Transform ignoreRoot)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();
            ResolveDependencies();
            float range = raycastWeapon != null ? raycastWeapon.MaxRange : 100f;
            int mask = raycastWeapon != null ? raycastWeapon.HitLayers.value : ~0;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                Transform root = col.transform.root;
                if (root == transform.root) continue;
                if (ignoreRoot != null && root == ignoreRoot.root) continue;
                DeliverColorPayload(hits[i], ammo, color, direction);
                return;
            }
        }

        /// <summary>
        /// Walls on Default still stop the ray. Color is delivered when the hit is on
        /// ColorInteractable, or when the object already carries a color target. Test props and
        /// enemies stay on Default so their own AI and physics are left alone.
        /// </summary>
        private void DeliverColorPayload(RaycastHit hitInfo, Ammo.AmmoType firedAmmo, ObjectColorState shotColorState, Vector3 shotDir)
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;
            if (hitInfo.collider == null) return;

            IColorShotImpact[] shotImpacts = hitInfo.collider.GetComponentsInParent<IColorShotImpact>();
            bool hasImpact = shotImpacts != null && shotImpacts.Length > 0;
            bool hasTarget = hitInfo.collider.GetComponentInParent<IColorTarget>() != null;
            if (!ColorInteractable.Is(hitInfo.collider) && !hasImpact && !hasTarget) return;

            if (shotImpacts != null)
            {
                for (int i = 0; i < shotImpacts.Length; i++)
                {
                    shotImpacts[i].OnColorShotHit(firedAmmo, shotColorState, hitInfo, shotDir);
                }
            }

            if (shotColorState == ObjectColorState.None) return;

            IColorTarget target = hitInfo.collider.GetComponentInParent<IColorTarget>();
            if (target != null)
            {
                target.ApplyColor(shotColorState);
            }
        }

        private void PlayShotAnimation()
        {
            if (shotAnimator == null || !shotAnimator.isActiveAndEnabled || shotAnimator.runtimeAnimatorController == null) return;
            shotAnimator.ResetTrigger(FireTrigger);
            shotAnimator.SetTrigger(FireTrigger);
        }

        private void HandleActiveChamberChanged(int activeIndex)
        {
            SyncWithActiveChamber();
        }

        public bool TriggerReload()
        {
            if (_isReloading) return false;
            ResolveDependencies();

            if (shotAnimator == null || !shotAnimator.isActiveAndEnabled || shotAnimator.runtimeAnimatorController == null)
            {
                if (ammoController != null) ammoController.TestReloadRandom();
                return true;
            }

            _reloadRoutine = StartCoroutine(ReloadSequence());
            return true;
        }

        private System.Collections.IEnumerator ReloadSequence()
        {
            _isReloading = true;
            ReloadAmmoLoaded = false;
            if (shotAnimator != null && shotAnimator.isActiveAndEnabled)
            {
                shotAnimator.ResetTrigger(FireTrigger);
                shotAnimator.ResetTrigger(ReloadTrigger);
                shotAnimator.SetTrigger(ReloadTrigger);
            }

            // Follow the clip so speed changes and pauses also pause ammo loading and the HUD.
            bool enteredReload = false;
            while (shotAnimator != null && shotAnimator.isActiveAndEnabled && shotAnimator.runtimeAnimatorController != null)
            {
                if (TryGetReloadState(out var state))
                {
                    enteredReload = true;
                    if (!ReloadAmmoLoaded && state.normalizedTime >= ReloadRefillPoint)
                    {
                        ReloadAmmoLoaded = true;
                        if (ammoController != null) ammoController.TestReloadRandom();
                        SyncWithActiveChamber();
                    }
                }
                else if (enteredReload) break;
                yield return null;
            }

            _isReloading = false;
            _reloadRoutine = null;
        }

        private bool TryGetReloadState(out AnimatorStateInfo state)
        {
            state = default;
            if (!_isReloading || shotAnimator == null || !shotAnimator.isInitialized) return false;
            state = shotAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Reload")) return true;
            if (shotAnimator.IsInTransition(0))
            {
                state = shotAnimator.GetNextAnimatorStateInfo(0);
                if (state.IsName("Reload")) return true;
            }
            return false;
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

            // 4. Update Chamber Glow Light
            UpdateGlowLights(activeColor, activeAmmo != Ammo.AmmoType.Empty);
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

            // 4. Update Chamber Glow Light
            UpdateGlowLights(activeColor, true);

            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(activeColor)}><b>[Spectrum Gun]</b> Switched Mode: {mode.ToString().ToUpper()}</color>");
        }

        private void UpdateGlowLights(Color color, bool hasRound)
        {
            if (_glowLights == null)
            {
                if (chamberGlowLight == null)
                    chamberGlowLight = transform.Find("ChamberGlow")?.GetComponent<Light>();
                _glowLights = new Light[11];
                _glowLights[0] = chamberGlowLight;
                for (int i = 1; i <= 10; i++)
                    _glowLights[i] = transform.Find("glow" + i)?.GetComponent<Light>();
            }
            foreach (var light in _glowLights)
            {
                if (light == null) continue;
                light.color = color;
                light.enabled = hasRound;
            }
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
