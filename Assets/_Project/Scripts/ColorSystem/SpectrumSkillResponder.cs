using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Recorded.Weapons.Ammo;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// High-Impact Gameplay & Procedural Sci-Fi VFX Responder for the Spectrum Gun:
    /// - RED (AmmoType.Red): "Kinetic Shockwave & Ricochet Knockback"
    ///   Heavy directional launch + spin torque + AoE radial shockwave knocking nearby objects +
    ///   kinetic chain collision transfer + dual expanding shockwave rings, burst light & kinetic motion trail.
    /// - GREEN (AmmoType.Green): "Tether & Pull"
    ///   The first Green hit only paints the target. A second Green object inside range grows one
    ///   walkable energy bridge (LineRenderer + kinematic BoxCollider). Shooting an already tethered
    ///   Green object again makes that object the anchor and reels the other Green object in along the rope.
    ///   Unpainted objects are never pulled.
    /// - CYAN (AmmoType.Cyan): "Stasis / Time Stop"
    ///   The hit body stops immediately. Linear and angular velocity are snapshotted, then cleared,
    ///   and the Rigidbody is held kinematic with gravity off until Blue ends. Motion is restored from
    ///   the snapshot. Color mixes are not resolved in this component.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpectrumSkillResponder : MonoBehaviour, IColorShotImpact
    {
        [Header("RED — Kinetic Shockwave & Ricochet Knockback")]
        [SerializeField] private bool enableRedImpulse = true;
        [SerializeField] private float redImpulseForce = 18.0f;
        [SerializeField] private float redUpwardLift = 4.5f;
        [SerializeField] private float redTorqueImpulse = 12.0f;
        [SerializeField] private float redShockwaveRadius = 4.0f;
        [SerializeField] private float redShockwaveForce = 14.0f;
        [SerializeField] private float redShockwaveUpwardModifier = 1.2f;
        [SerializeField] private float redChainImpactWindow = 1.2f;
        [SerializeField] private float redChainTransferMultiplier = 0.85f;

        [Header("GREEN — Tether & Pull (two Green objects only)")]
        [SerializeField] private bool enableGreenBind = true;
        [SerializeField] private float greenBindDuration = 5.0f;
        [SerializeField] private float greenSearchRadius = 10.0f;
        [SerializeField] private float greenTetherWidth = 1.0f;
        [SerializeField] private float greenTetherThickness = 0.3f;
        [SerializeField] private float greenPullSpeed = 14.0f;
        [SerializeField] private float greenPullStopPadding = 0.05f;

        [Header("CYAN — Complete Stasis (Time Stop)")]
        [SerializeField] private bool enableCyanStasis = true;
        [SerializeField] private float cyanStasisDuration = 5.0f;

        [Header("COMBO SYNERGIES (Yellow / Magenta / White)")]
        [SerializeField] private float yellowSlingshotTensionMultiplier = 1.5f;
        [SerializeField] private float whiteVortexPullRadius = 6.0f;
        [SerializeField] private float whiteVortexPullForce = 9.0f;
        [SerializeField] private float whiteSupernovaRadius = 6.5f;
        [SerializeField] private float whiteSupernovaForce = 24.0f;

        private IColorReceivable _colorTarget;
        private Rigidbody _rb;
        private bool _subscribed;

        // Cached original Rigidbody state for clean restoration
        private bool _hasCachedPhysics;
        private float _originalLinearDamping;
        private float _originalAngularDamping;
        private bool _originalUseGravity;
        private bool _originalIsKinematic;
        private RigidbodyConstraints _originalConstraints;

        private static readonly List<SpectrumSkillResponder> GreenRoster = new List<SpectrumSkillResponder>();

        // RED state
        private Vector3 _lastRedImpulseApplied;
        private int _redImpulseHitCount;
        private float _redChainTimer;
        private TrailRenderer _kineticTrail;
        private Coroutine _trailFadeCoroutine;

        // GREEN tether state. The rope itself lives on GreenEnergyTether, shared by both ends.
        private bool _isGreenBound;
        private float _greenBindTimer;
        private bool _greenFromColorFlag;
        private GreenEnergyTether _tether;

        // CYAN stasis. The snapshot is the motion to restore when the freeze ends. Mixes do not consume it here.
        private bool _isBlueStasis;
        private float _blueStasisTimer;
        private bool _blueFromColorFlag;
        private Vector3 _stasisLinearSnapshot;
        private Vector3 _stasisAngularSnapshot;
        private GameObject _chronoFieldRoot;
        private LineRenderer _chronoRingXY;
        private LineRenderer _chronoRingYZ;
        private LineRenderer _chronoVectorArrowLine;
        private Light _chronoPointLight;

        // WHITE Prismatic Overdrive state
        private bool _wasWhiteOverdrive;

        private static Shader _cachedUnlitShader;

        private const int RingSegmentCount = 24;

        public bool IsGreenBound => _isGreenBound;
        public bool IsTethered => _tether != null;
        public bool IsBlueStasis => _isBlueStasis;
        public GreenEnergyTether ActiveTether => _tether;
        public Rigidbody Body => _rb;
        public float RemainingBindTime => _isGreenBound ? Mathf.Max(0f, _greenBindTimer) : 0f;
        public float RemainingStasisTime => _isBlueStasis ? Mathf.Max(0f, _blueStasisTimer) : 0f;
        public Vector3 TetherPoint => GetTetherPoint();
        public Vector3 GreenBindAnchorPoint => _tether != null && _tether.PullAnchor != null ? _tether.PullAnchor.TetherPoint : TetherPoint;
        public Rigidbody ConnectedPartnerBody => _tether != null && _tether.Other(this) != null ? _tether.Other(this).Body : null;
        public Vector3 LastRedImpulseApplied => _lastRedImpulseApplied;
        public int RedImpulseHitCount => _redImpulseHitCount;
        public Vector3 StoredStasisVelocity => _stasisLinearSnapshot;
        public Vector3 StoredStasisImpulse => _stasisLinearSnapshot;
        public int StoredStasisHitCount => 0;

        public event Action<bool> OnBindStateChanged;
        public event Action<bool> OnStasisStateChanged;

        private static Shader GetUnlitShader()
        {
            if (_cachedUnlitShader == null)
            {
                _cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (_cachedUnlitShader == null) _cachedUnlitShader = Shader.Find("Sprites/Default");
            }
            return _cachedUnlitShader;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            if (_colorTarget != null && _subscribed)
            {
                _colorTarget.OnColorStateChanged -= HandleColorStateChanged;
                _subscribed = false;
            }

            _isGreenBound = false;
            SetGreenRosterMembership(false);
            ReleaseGreenTether();
            SetChronoFieldActive(false);
            _isBlueStasis = false;
            _stasisLinearSnapshot = Vector3.zero;
            _stasisAngularSnapshot = Vector3.zero;
            RestoreOriginalPhysics();
        }

        private void OnDestroy()
        {
            _isGreenBound = false;
            SetGreenRosterMembership(false);
            ReleaseGreenTether();
        }

        /// <summary>
        /// Ensures component references and event subscriptions are initialized (safe in both Play Mode and Edit Mode).
        /// </summary>
        public void EnsureInitialized()
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody>();
            }
            CacheOriginalPhysics();

            if (_colorTarget == null)
            {
                _colorTarget = GetComponent<IColorReceivable>();
            }

            if (_colorTarget != null && !_subscribed)
            {
                _colorTarget.OnColorStateChanged += HandleColorStateChanged;
                _subscribed = true;
                HandleColorStateChanged(_colorTarget.CurrentColorState);
            }
        }

        private void CacheOriginalPhysics()
        {
            if (_rb == null || _hasCachedPhysics) return;

            _originalLinearDamping = _rb.linearDamping;
            _originalAngularDamping = _rb.angularDamping;
            _originalUseGravity = _rb.useGravity;
            _originalIsKinematic = _rb.isKinematic;
            _originalConstraints = _rb.constraints;
            _hasCachedPhysics = true;
        }

        /// <summary>
        /// Invoked on every shot hit before/alongside color state application.
        /// Executes high-impact physical skills and procedural Sci-Fi VFX.
        /// </summary>
        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            EnsureInitialized();

            Vector3 hitPoint = hitInfo.point.sqrMagnitude > 0.0001f
                ? hitInfo.point
                : (_rb != null ? _rb.worldCenterOfMass : transform.position);
            Vector3 hitNormal = hitInfo.normal.sqrMagnitude > 0.0001f ? hitInfo.normal : -shotDirection.normalized;
            Vector3 pushDir = shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : -hitNormal;

            // 1. RED — Kinetic Shockwave, Spin Torque, AoE Blast & Stasis Momentum Bank
            if (enableRedImpulse && (firedAmmo == AmmoType.Red || (mappedColorFlag & ObjectColorState.Red) != 0))
            {
                ExecuteRedImpulseSkill(hitPoint, hitNormal, pushDir);
            }

            // 2. GREEN — paint on the first hit. A second hit, only while already tethered, reels the other end in.
            if (enableGreenBind && (firedAmmo == AmmoType.Green || (mappedColorFlag & ObjectColorState.Green) != 0))
            {
                bool alreadyGreen = _isGreenBound
                    || (_colorTarget != null && (_colorTarget.CurrentColorState & ObjectColorState.Green) != 0);
                if (alreadyGreen && _tether != null)
                {
                    _tether.BeginPullToward(this);
                }
                else if (!alreadyGreen)
                {
                    ActivateGreenBind(greenBindDuration > 0f ? greenBindDuration : 5.0f, false);
                }
            }

            // 3. BLUE — complete time stop. A duplicate hit does not take a new snapshot.
            if (enableCyanStasis && (firedAmmo == AmmoType.Cyan || (mappedColorFlag & ObjectColorState.Blue) != 0))
            {
                ActivateCyanStasis(cyanStasisDuration > 0f ? cyanStasisDuration : 5.0f, false);
            }
        }

        private void ExecuteRedImpulseSkill(Vector3 hitPoint, Vector3 hitNormal, Vector3 pushDir)
        {
            // YELLOW Synergy (Red + Green Bungee Slingshot Whip): boost impulse when tethered
            float synergyMult = _isGreenBound ? yellowSlingshotTensionMultiplier : 1.0f;
            Vector3 impulse = ((pushDir * redImpulseForce) + (Vector3.up * redUpwardLift)) * synergyMult;

            _lastRedImpulseApplied = impulse;
            _redImpulseHitCount++;
            _redChainTimer = redChainImpactWindow;

            // A frozen body does not accept this impulse. Banking it for a later mix is out of scope.
            if (_isBlueStasis)
            {
                UpdateChronoFieldVisuals();
            }
            else if (_rb != null && !_rb.isKinematic)
            {
                if (Application.isPlaying)
                {
                    _rb.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);

                    // Dramatic spin torque so boxes tumble mid-air instead of sliding flat
                    Vector3 lever = hitPoint - _rb.worldCenterOfMass;
                    Vector3 torqueAxis = Vector3.Cross(lever.sqrMagnitude > 0.001f ? lever.normalized : Vector3.up, pushDir);
                    if (torqueAxis.sqrMagnitude < 0.01f)
                    {
                        torqueAxis = Vector3.Cross(Vector3.up, pushDir);
                    }
                    _rb.AddTorque(torqueAxis.normalized * redTorqueImpulse * synergyMult, ForceMode.Impulse);
                }
                else
                {
                    float mass = Mathf.Max(0.001f, _rb.mass);
                    _rb.linearVelocity += impulse / mass;
                }
            }

            // AoE Radial Shockwave: blast nearby physics props outward from impact point
            ApplyRadialShockwave(hitPoint, redShockwaveRadius, redShockwaveForce * synergyMult, redShockwaveUpwardModifier, _rb);

            if (Application.isPlaying)
            {
                SpawnRedShockwaveVFX(hitPoint, hitNormal, pushDir);
                ActivateKineticTrail(new Color(1f, 0.22f, 0.18f, 0.85f), 0.55f);
            }
        }

        private void ApplyRadialShockwave(Vector3 center, float radius, float force, float upwardMod, Rigidbody ignoreBody)
        {
            if (radius <= 0.01f || force <= 0.01f) return;

            Collider[] colliders = Physics.OverlapSphere(center, radius);
            HashSet<Rigidbody> affected = new HashSet<Rigidbody>();
            if (ignoreBody != null) affected.Add(ignoreBody);

            for (int i = 0; i < colliders.Length; i++)
            {
                Rigidbody otherRb = colliders[i].attachedRigidbody;
                if (otherRb == null || otherRb.isKinematic || affected.Contains(otherRb)) continue;
                // Never push the Player character
                if (otherRb.CompareTag("Player") || otherRb.GetComponent("PlayerMovement") != null) continue;

                affected.Add(otherRb);
                if (Application.isPlaying)
                {
                    otherRb.AddExplosionForce(force, center, radius, upwardMod, ForceMode.Impulse);
                }
                else
                {
                    Vector3 dir = (otherRb.worldCenterOfMass - center).normalized + (Vector3.up * 0.3f);
                    otherRb.linearVelocity += dir.normalized * (force / Mathf.Max(1f, otherRb.mass));
                }
            }
        }

        public void ActivateGreenBind(float duration, bool fromColorFlag = false)
        {
            EnsureInitialized();

            bool wasBound = _isGreenBound;
            _isGreenBound = true;
            _greenFromColorFlag = fromColorFlag;
            if (!wasBound)
            {
                _greenBindTimer = duration;
            }

            SetGreenRosterMembership(true);
            // First Green object waits. The rope appears only when a second Green object is in range.
            TryCreateTether();

            if (!wasBound)
            {
                OnBindStateChanged?.Invoke(true);
            }
        }

        public void DeactivateGreenBind()
        {
            if (!_isGreenBound && _tether == null) return;

            _isGreenBound = false;
            _greenBindTimer = 0f;
            _greenFromColorFlag = false;
            SetGreenRosterMembership(false);
            ReleaseGreenTether();
            OnBindStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Links this Green object to the nearest other untethered Green object inside range.
        /// Grey / unpainted rigidbodies are ignored.
        /// </summary>
        public void TryCreateTether()
        {
            if (!_isGreenBound || _tether != null || !enableGreenBind) return;

            SpectrumSkillResponder partner = FindUntetheredGreenPartner();
            if (partner == null) return;

            GreenEnergyTether.Create(
                this,
                partner,
                TetherMaxRange,
                greenTetherWidth,
                greenTetherThickness,
                greenPullSpeed,
                greenPullStopPadding);
        }

        public void AttachTether(GreenEnergyTether tether)
        {
            _tether = tether;
        }

        public void ClearTetherReference(GreenEnergyTether tether)
        {
            if (_tether == tether) _tether = null;
        }

        public void ActivateCyanStasis(float duration, bool fromColorFlag = false)
        {
            EnsureInitialized();

            // Duplicate Blue hits do not refresh the timer and do not overwrite the snapshot.
            if (_isBlueStasis) return;

            _isBlueStasis = true;
            _blueFromColorFlag = fromColorFlag;
            _blueStasisTimer = duration;

            if (_rb != null && !_rb.isKinematic)
            {
                _stasisLinearSnapshot = _rb.linearVelocity;
                _stasisAngularSnapshot = _rb.angularVelocity;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            else
            {
                _stasisLinearSnapshot = Vector3.zero;
                _stasisAngularSnapshot = Vector3.zero;
            }

            ApplyStatePhysicsModifiers();
            SetChronoFieldActive(true);
            UpdateChronoFieldVisuals();
            OnStasisStateChanged?.Invoke(true);
        }

        public void DeactivateCyanStasis()
        {
            if (!_isBlueStasis) return;

            Vector3 linear = _stasisLinearSnapshot;
            Vector3 angular = _stasisAngularSnapshot;

            _isBlueStasis = false;
            _blueStasisTimer = 0f;
            _blueFromColorFlag = false;
            _stasisLinearSnapshot = Vector3.zero;
            _stasisAngularSnapshot = Vector3.zero;

            SetChronoFieldActive(false);
            ApplyStatePhysicsModifiers();

            // Time resumes. Restore the motion captured at the freeze. Do not launch.
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = linear;
                _rb.angularVelocity = angular;
            }

            OnStasisStateChanged?.Invoke(false);
        }

        private Vector3 GetTetherPoint()
        {
            // Top face of the collider, so the rope meets the surface the player is standing on.
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Vector3 localTop = box.center + (Vector3.up * (box.size.y * 0.5f));
                return box.transform.TransformPoint(localTop);
            }

            if (_rb != null) return transform.TransformPoint(_rb.centerOfMass);
            return transform.position;
        }

        private SpectrumSkillResponder FindUntetheredGreenPartner()
        {
            PurgeGreenRoster();
            float maxRange = TetherMaxRange;
            Vector3 origin = TetherPoint;
            SpectrumSkillResponder best = null;
            float bestDistance = maxRange;

            for (int i = 0; i < GreenRoster.Count; i++)
            {
                SpectrumSkillResponder other = GreenRoster[i];
                if (other == null || other == this || !other.IsGreenBound || other.IsTethered) continue;
                if (other.GetComponentInParent<CharacterController>() != null) continue;

                float distance = Vector3.Distance(origin, other.TetherPoint);
                if (distance <= 0.05f || distance > maxRange) continue;
                if (best != null && distance >= bestDistance) continue;

                bestDistance = distance;
                best = other;
            }

            return best;
        }

        private float TetherMaxRange => greenSearchRadius > 0.1f ? greenSearchRadius : 10f;

        private void SetGreenRosterMembership(bool member)
        {
            if (member)
            {
                if (!GreenRoster.Contains(this)) GreenRoster.Add(this);
                return;
            }

            GreenRoster.Remove(this);
        }

        private static void PurgeGreenRoster()
        {
            for (int i = GreenRoster.Count - 1; i >= 0; i--)
            {
                if (GreenRoster[i] == null) GreenRoster.RemoveAt(i);
            }
        }

        private void ReleaseGreenTether()
        {
            if (_tether == null) return;
            GreenEnergyTether tether = _tether;
            _tether = null;
            tether.Break();
        }

        private void HandleColorStateChanged(ObjectColorState newState)
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody>();
            }
            CacheOriginalPhysics();

            bool greenFlag = enableGreenBind && (newState & ObjectColorState.Green) != 0;
            bool blueFlag = enableCyanStasis && (newState & ObjectColorState.Blue) != 0;
            bool isWhite = (newState & (ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue)) ==
                           (ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue);

            // WHITE Prismatic Supernova Burst when White overdrive state ends or triggers
            if (isWhite && !_wasWhiteOverdrive)
            {
                _wasWhiteOverdrive = true;
                if (Application.isPlaying)
                {
                    SpawnRedShockwaveVFX(transform.position, Vector3.up, Vector3.up);
                    ActivateKineticTrail(Color.white, 0.8f);
                }
            }
            else if (!isWhite && _wasWhiteOverdrive)
            {
                _wasWhiteOverdrive = false;
                ApplyRadialShockwave(transform.position, whiteSupernovaRadius, whiteSupernovaForce, 1.5f, _rb);
                if (Application.isPlaying)
                {
                    SpawnRedShockwaveVFX(transform.position, Vector3.up, Vector3.up);
                }
            }

            // Support direct ApplyColor() calls (e.g., from puzzle scripts or unit tests)
            if (greenFlag && !_isGreenBound)
            {
                ActivateGreenBind(greenBindDuration > 0f ? greenBindDuration : 5.0f, true);
            }
            else if (!greenFlag && _isGreenBound && _greenFromColorFlag)
            {
                DeactivateGreenBind();
            }

            if (blueFlag && !_isBlueStasis)
            {
                ActivateCyanStasis(cyanStasisDuration > 0f ? cyanStasisDuration : 5.0f, true);
            }
            else if (!blueFlag && _isBlueStasis && _blueFromColorFlag)
            {
                DeactivateCyanStasis();
            }
        }

        private void Update()
        {
            TickSkillTimers(Time.deltaTime);

            if (_isBlueStasis)
            {
                UpdateChronoFieldVisuals();
            }
        }

        /// <summary>
        /// Advances independent skill timers (callable in Edit Mode tests or Play Mode Update).
        /// </summary>
        public void TickSkillTimers(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (_redChainTimer > 0f)
            {
                _redChainTimer = Mathf.Max(0f, _redChainTimer - deltaTime);
            }

            if (_isGreenBound && !_greenFromColorFlag && _greenBindTimer > 0f)
            {
                _greenBindTimer -= deltaTime;
                if (_greenBindTimer <= 0f)
                {
                    bool colorStillGreen = _colorTarget != null && (_colorTarget.CurrentColorState & ObjectColorState.Green) != 0;
                    if (colorStillGreen)
                    {
                        // Paint is still on. The color flag now owns the tether lifetime.
                        _greenFromColorFlag = true;
                        _greenBindTimer = 0f;
                    }
                    else
                    {
                        DeactivateGreenBind();
                    }
                }
            }

            if (_isBlueStasis && !_blueFromColorFlag && _blueStasisTimer > 0f)
            {
                _blueStasisTimer -= deltaTime;
                if (_blueStasisTimer <= 0f)
                {
                    bool colorStillBlue = _colorTarget != null && (_colorTarget.CurrentColorState & ObjectColorState.Blue) != 0;
                    if (colorStillBlue)
                    {
                        _blueFromColorFlag = true;
                        _blueStasisTimer = 0f;
                    }
                    else
                    {
                        DeactivateCyanStasis();
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null || _rb.isKinematic) return;

            // WHITE Prismatic Overdrive Vortex Pull: draw nearby objects inward before Supernova burst
            if (_wasWhiteOverdrive)
            {
                Collider[] nearby = Physics.OverlapSphere(transform.position, whiteVortexPullRadius);
                for (int i = 0; i < nearby.Length; i++)
                {
                    Rigidbody otherRb = nearby[i].attachedRigidbody;
                    if (otherRb == null || otherRb == _rb || otherRb.isKinematic) continue;
                    if (otherRb.CompareTag("Player") || otherRb.GetComponent("PlayerMovement") != null) continue;

                    Vector3 pull = (_rb.worldCenterOfMass - otherRb.worldCenterOfMass);
                    if (pull.sqrMagnitude > 0.25f)
                    {
                        otherRb.AddForce(pull.normalized * whiteVortexPullForce, ForceMode.Acceleration);
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // RED Kinetic Chain Impact: transfer explosive knockback when a Red-launched object slams into another physics prop
            if (_redChainTimer <= 0f || collision.rigidbody == null || collision.rigidbody.isKinematic) return;
            if (collision.rigidbody.CompareTag("Player") || collision.rigidbody.GetComponentInParent<Recorded.FirstPersonMotor>() != null) return;

            Vector3 transferDir = (collision.rigidbody.worldCenterOfMass - transform.position).normalized + (Vector3.up * 0.3f);
            Vector3 chainImpulse = transferDir.normalized * (_lastRedImpulseApplied.magnitude * redChainTransferMultiplier);
            collision.rigidbody.AddForce(chainImpulse, ForceMode.Impulse);
            collision.rigidbody.AddTorque(Vector3.Cross(Vector3.up, transferDir) * (redTorqueImpulse * 0.7f), ForceMode.Impulse);

            if (Application.isPlaying && collision.contactCount > 0)
            {
                SpawnRedShockwaveVFX(collision.GetContact(0).point, collision.GetContact(0).normal, transferDir.normalized);
            }
        }

        // =====================================================================
        // PROCEDURAL SCI-FI VFX (Non-Occluding, Zero External Assets Required)
        // =====================================================================

        private void SpawnRedShockwaveVFX(Vector3 hitPoint, Vector3 hitNormal, Vector3 pushDir)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            StartCoroutine(AnimateRedShockwaveRoutine(hitPoint, hitNormal, pushDir));
        }

        private IEnumerator AnimateRedShockwaveRoutine(Vector3 hitPoint, Vector3 hitNormal, Vector3 pushDir)
        {
            GameObject vfxRoot = new GameObject("RedShockwave_BurstVFX");
            vfxRoot.transform.position = hitPoint + (hitNormal * 0.03f);
            Quaternion ringRot = pushDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(pushDir) : Quaternion.identity;

            Color hdrRed = new Color(1.0f, 0.22f, 0.16f, 0.95f);
            Color hdrOrange = new Color(1.0f, 0.62f, 0.15f, 0.9f);

            LineRenderer outerRing = CreateProceduralLine(vfxRoot.transform, "ShockRing_Outer", RingSegmentCount, true, 0.065f, hdrRed);
            LineRenderer innerMachRing = CreateProceduralLine(vfxRoot.transform, "ShockRing_MachCone", RingSegmentCount, true, 0.045f, hdrOrange);

            // 8 Radial kinetic spark streaks shooting outward
            const int sparkCount = 8;
            LineRenderer[] sparkLines = new LineRenderer[sparkCount];
            Vector3[] sparkDirs = new Vector3[sparkCount];
            Quaternion basis = Quaternion.LookRotation(hitNormal.sqrMagnitude > 0.001f ? hitNormal : Vector3.up);
            for (int s = 0; s < sparkCount; s++)
            {
                float angle = (s / (float)sparkCount) * Mathf.PI * 2f;
                Vector3 radial = basis * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.35f).normalized;
                sparkDirs[s] = radial;
                sparkLines[s] = CreateProceduralLine(vfxRoot.transform, $"KineticSpark_{s}", 2, false, 0.03f, hdrOrange);
            }

            Light blastLight = vfxRoot.AddComponent<Light>();
            blastLight.type = LightType.Point;
            blastLight.color = new Color(1f, 0.25f, 0.15f);
            blastLight.range = 5.5f;
            blastLight.intensity = 7.5f;

            float duration = 0.26f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                float outerRadius = Mathf.Lerp(0.12f, 1.65f, easeOut);
                float innerRadius = Mathf.Lerp(0.08f, 1.15f, easeOut);
                Vector3 innerCenter = vfxRoot.transform.position + (pushDir * (easeOut * 0.45f));

                SetCirclePositions(outerRing, vfxRoot.transform.position, ringRot, outerRadius);
                SetCirclePositions(innerMachRing, innerCenter, ringRot, innerRadius);

                float widthScale = 1f - t;
                outerRing. widthMultiplier = 0.07f * widthScale;
                innerMachRing.widthMultiplier = 0.045f * widthScale;

                for (int s = 0; s < sparkCount; s++)
                {
                    Vector3 p0 = vfxRoot.transform.position + sparkDirs[s] * (easeOut * 0.95f);
                    Vector3 p1 = vfxRoot.transform.position + sparkDirs[s] * (easeOut * 1.55f);
                    sparkLines[s].SetPosition(0, p0);
                    sparkLines[s].SetPosition(1, p1);
                    sparkLines[s].widthMultiplier = 0.032f * widthScale;
                }

                blastLight.intensity = Mathf.Lerp(7.5f, 0f, t);
                yield return null;
            }

            if (vfxRoot != null)
            {
                Destroy(vfxRoot);
            }
        }

        private void ActivateKineticTrail(Color trailColor, float duration)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            if (_kineticTrail == null)
            {
                GameObject trailObj = new GameObject("KineticMotion_Trail");
                trailObj.transform.SetParent(transform, false);
                trailObj.transform.localPosition = Vector3.zero;

                _kineticTrail = trailObj.AddComponent<TrailRenderer>();
                _kineticTrail.time = 0.32f;
                _kineticTrail.minVertexDistance = 0.05f;
                _kineticTrail.startWidth = 0.24f;
                _kineticTrail.endWidth = 0.0f;
                _kineticTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _kineticTrail.receiveShadows = false;
                _kineticTrail.sharedMaterial = new Material(GetUnlitShader()) { color = trailColor };
            }

            if (_kineticTrail.sharedMaterial != null)
            {
                _kineticTrail.sharedMaterial.color = trailColor;
            }
            _kineticTrail.Clear();
            _kineticTrail.emitting = true;

            if (_trailFadeCoroutine != null) StopCoroutine(_trailFadeCoroutine);
            _trailFadeCoroutine = StartCoroutine(DisableTrailAfterDelay(duration));
        }

        private IEnumerator DisableTrailAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_kineticTrail != null)
            {
                _kineticTrail.emitting = false;
            }
            _trailFadeCoroutine = null;
        }

        private void SetChronoFieldActive(bool active)
        {
            if (!Application.isPlaying) return;

            if (!active)
            {
                if (_chronoFieldRoot != null) _chronoFieldRoot.SetActive(false);
                return;
            }

            if (_chronoFieldRoot == null)
            {
                _chronoFieldRoot = new GameObject("CyanStasis_ChronoField");
                _chronoFieldRoot.transform.SetParent(transform, false);
                _chronoFieldRoot.transform.localPosition = Vector3.zero;

                Color cyanGlow = new Color(0.15f, 0.92f, 1.0f, 0.95f);
                Color electricBlue = new Color(0.35f, 0.65f, 1.0f, 0.85f);
                Color arrowColor = new Color(1.0f, 0.35f, 0.2f, 0.98f);

                _chronoRingXY = CreateProceduralLine(_chronoFieldRoot.transform, "ChronoGyroRing_XY", RingSegmentCount, true, 0.036f, cyanGlow);
                _chronoRingYZ = CreateProceduralLine(_chronoFieldRoot.transform, "ChronoGyroRing_YZ", RingSegmentCount, true, 0.028f, electricBlue);
                _chronoVectorArrowLine = CreateProceduralLine(_chronoFieldRoot.transform, "StasisMomentum_Arrow", 5, false, 0.055f, arrowColor);

                _chronoPointLight = _chronoFieldRoot.AddComponent<Light>();
                _chronoPointLight.type = LightType.Point;
                _chronoPointLight.color = cyanGlow;
                _chronoPointLight.range = 3.2f;
                _chronoPointLight.intensity = 2.8f;
            }

            _chronoFieldRoot.SetActive(true);
        }

        private void UpdateChronoFieldVisuals()
        {
            if (!Application.isPlaying || _chronoFieldRoot == null || !_chronoFieldRoot.activeSelf) return;

            Vector3 center = _rb != null ? _rb.worldCenterOfMass : transform.position;
            float baseRadius = 0.85f;
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                baseRadius = Mathf.Clamp(rend.bounds.extents.magnitude * 1.15f, 0.65f, 2.2f);
            }

            // Counter-rotating Gyro Holo-Rings around the suspended target (leaves surface color 100% visible)
            Quaternion rotXY = Quaternion.Euler(Time.time * 75f, Time.time * 45f, 0f);
            Quaternion rotYZ = Quaternion.Euler(0f, -Time.time * 65f, Time.time * 85f);

            // Strobe warning when Stasis is about to expire (<= 1.5s remaining)
            float strobe = 1f;
            if (_blueStasisTimer <= 1.5f)
            {
                float freq = Mathf.Lerp(22f, 8f, Mathf.Clamp01(_blueStasisTimer / 1.5f));
                strobe = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * freq));
            }

            _chronoRingXY.widthMultiplier = 0.038f * strobe;
            _chronoRingYZ.widthMultiplier = 0.028f * strobe;

            SetCirclePositions(_chronoRingXY, center, rotXY, baseRadius);
            SetCirclePositions(_chronoRingYZ, center, rotYZ, baseRadius * 0.88f);

            // Stasis holds the body still. The old launch arrow is not part of a full time stop.
            if (_chronoVectorArrowLine != null) _chronoVectorArrowLine.enabled = false;
        }

        private static LineRenderer CreateProceduralLine(Transform parent, string name, int positionCount, bool loop, float width, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject obj = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null)
            {
                obj.transform.SetParent(parent, false);
            }

            LineRenderer lr = obj.GetComponent<LineRenderer>();
            if (lr == null) lr = obj.AddComponent<LineRenderer>();

            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = positionCount;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = new Material(GetUnlitShader()) { color = color };
            return lr;
        }

        private static void SetCirclePositions(LineRenderer lr, Vector3 center, Quaternion rotation, float radius)
        {
            if (lr == null) return;
            int count = lr.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 localPos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                lr.SetPosition(i, center + (rotation * localPos));
            }
        }

        private void ApplyStatePhysicsModifiers()
        {
            if (_rb == null || !_hasCachedPhysics) return;

            if (!_isBlueStasis)
            {
                RestoreOriginalPhysics();
                return;
            }

            // Stop the body completely. Kinematic ignores gravity and future forces.
            // FreezeAll is also applied so a later isKinematic toggle cannot drift.
            _rb.linearDamping = _originalLinearDamping;
            _rb.angularDamping = _originalAngularDamping;
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeAll;
            _rb.isKinematic = true;
        }

        private void RestoreOriginalPhysics()
        {
            if (_rb == null || !_hasCachedPhysics) return;

            _rb.isKinematic = _originalIsKinematic;
            _rb.linearDamping = _originalLinearDamping;
            _rb.angularDamping = _originalAngularDamping;
            _rb.useGravity = _originalUseGravity;
            _rb.constraints = _originalConstraints;
        }
    }
}
