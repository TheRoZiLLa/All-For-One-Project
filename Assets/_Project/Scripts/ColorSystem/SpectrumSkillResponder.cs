using System;
using UnityEngine;
using Recorded.Weapons.Ammo;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Reusable component that implements the 3 physical Spectrum Gun skills:
    /// - RED (AmmoType.Red): "Impulse" — Immediate directional physical push along shot trajectory/normal.
    /// - GREEN (AmmoType.Green): "Bind" — Physical tether (SpringJoint + glowing energy line) anchoring the object
    ///   to its hit anchor point for an independent configurable bindDuration.
    /// - CYAN (AmmoType.Cyan): "Stasis" — Arrests motion, suspends gravity, and applies stasis field for an
    ///   independent configurable stasisDuration.
    ///
    /// Decoupled from the 5-second RGBColorTarget puzzle color timer so physical skills can have their own
    /// independent durations while remaining 100% compatible with direct ApplyColor() state changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpectrumSkillResponder : MonoBehaviour, IColorShotImpact
    {
        [Header("RED — Impulse (Immediate on Shot Impact)")]
        [Tooltip("Enable physical impulse push when hit by Red ammo.")]
        [SerializeField] private bool enableRedImpulse = true;

        [Tooltip("Force magnitude applied in the shot direction when hit by Red ammo.")]
        [SerializeField] private float redImpulseForce = 7.5f;

        [Tooltip("Upward lift bias added to Red impulse for responsive physics feel.")]
        [SerializeField] private float redUpwardLift = 1.6f;

        [Header("GREEN — Bind (Physical Tether & Spring Joint)")]
        [Tooltip("Enable positional anchor/tether while Green Bind is active.")]
        [SerializeField] private bool enableGreenBind = true;

        [Tooltip("Independent duration in seconds for Green Bind when triggered by a shot (<= 0 uses color state flag).")]
        [SerializeField] private float greenBindDuration = 5.0f;

        [Tooltip("Spring strength pulling the object toward its bind anchor point.")]
        [SerializeField] private float greenBindSpring = 55.0f;

        [Tooltip("Linear damping applied while bound.")]
        [SerializeField] private float greenBindDamping = 12.0f;

        [Tooltip("Maximum distance tolerance before the spring pulls at full force.")]
        [SerializeField] private float greenBindMaxDistance = 0.25f;

        [Header("CYAN — Stasis (Motion Arrest & Gravity Suspension)")]
        [Tooltip("Enable stasis motion arrest/slowdown when hit by Cyan ammo.")]
        [SerializeField] private bool enableCyanStasis = true;

        [Tooltip("Independent duration in seconds for Cyan Stasis when triggered by a shot (<= 0 uses color state flag).")]
        [SerializeField] private float cyanStasisDuration = 5.0f;

        [Tooltip("If true, completely freezes Rigidbody motion during Stasis. If false, applies heavy drag slowdown.")]
        [SerializeField] private bool fullFreezeOnStasis = false;

        [Tooltip("Velocity multiplier when Stasis begins (0.08 = 92% immediate speed reduction).")]
        [Range(0f, 1f)]
        [SerializeField] private float stasisInitialVelocityScale = 0.08f;

        [Tooltip("Linear damping applied while in Stasis slowdown mode.")]
        [SerializeField] private float stasisLinearDamping = 22.0f;

        private IColorReceivable _colorTarget;
        private Rigidbody _rb;
        private bool _subscribed;

        // Cached original Rigidbody state for clean restoration
        private bool _hasCachedPhysics;
        private float _originalLinearDamping;
        private float _originalAngularDamping;
        private bool _originalUseGravity;
        private RigidbodyConstraints _originalConstraints;

        // Independent skill timers & state
        private bool _isGreenBound;
        private float _greenBindTimer;
        private bool _greenFromColorFlag;
        private Vector3 _greenBindAnchorPoint;
        private Vector3 _greenLocalAttachPoint;
        private SpringJoint _activeBindJoint;
        private LineRenderer _bindTetherLine;

        private bool _isBlueStasis;
        private float _blueStasisTimer;
        private bool _blueFromColorFlag;

        private Vector3 _lastRedImpulseApplied;
        private int _redImpulseHitCount;

        public bool IsGreenBound => _isGreenBound;
        public bool IsBlueStasis => _isBlueStasis;
        public float RemainingBindTime => _isGreenBound ? Mathf.Max(0f, _greenBindTimer) : 0f;
        public float RemainingStasisTime => _isBlueStasis ? Mathf.Max(0f, _blueStasisTimer) : 0f;
        public Vector3 GreenBindAnchorPoint => _greenBindAnchorPoint;
        public SpringJoint ActiveBindJoint => _activeBindJoint;
        public Vector3 LastRedImpulseApplied => _lastRedImpulseApplied;
        public int RedImpulseHitCount => _redImpulseHitCount;

        public event Action<bool> OnBindStateChanged;
        public event Action<bool> OnStasisStateChanged;

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

            ClearBindJointAndVisual();
            _isGreenBound = false;
            _isBlueStasis = false;
            RestoreOriginalPhysics();
        }

        private void OnDestroy()
        {
            ClearBindJointAndVisual();
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
            _originalConstraints = _rb.constraints;
            _hasCachedPhysics = true;
        }

        /// <summary>
        /// Invoked on every shot hit before/alongside color state application.
        /// Executes immediate shot effects (RED Impulse, GREEN Bind tether, CYAN Stasis) with independent skill timers.
        /// </summary>
        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            EnsureInitialized();

            // 1. RED — Impulse: Immediate physical push on impact
            if (enableRedImpulse && (firedAmmo == AmmoType.Red || (mappedColorFlag & ObjectColorState.Red) != 0))
            {
                Vector3 pushDir = shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : -hitInfo.normal;
                Vector3 impulse = (pushDir * redImpulseForce) + (Vector3.up * redUpwardLift);
                _lastRedImpulseApplied = impulse;
                _redImpulseHitCount++;

                if (_rb != null && !_rb.isKinematic)
                {
                    Vector3 hitPoint = hitInfo.point.sqrMagnitude > 0.0001f ? hitInfo.point : _rb.worldCenterOfMass;
                    if (Application.isPlaying)
                    {
                        _rb.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);
                    }
                    else
                    {
                        float mass = Mathf.Max(0.001f, _rb.mass);
                        _rb.linearVelocity += impulse / mass;
                    }
                }
            }

            // 2. GREEN — Bind: Physical tether & SpringJoint with independent duration
            if (enableGreenBind && (firedAmmo == AmmoType.Green || (mappedColorFlag & ObjectColorState.Green) != 0))
            {
                Vector3 anchorPos = hitInfo.point.sqrMagnitude > 0.0001f ? hitInfo.point : transform.position;
                ActivateGreenBind(anchorPos, greenBindDuration > 0f ? greenBindDuration : 5.0f, false);
            }

            // 3. CYAN — Stasis: Motion arrest & gravity suspension with independent duration
            if (enableCyanStasis && (firedAmmo == AmmoType.Cyan || (mappedColorFlag & ObjectColorState.Blue) != 0))
            {
                ActivateCyanStasis(cyanStasisDuration > 0f ? cyanStasisDuration : 5.0f, false);
            }
        }

        public void ActivateGreenBind(Vector3 worldAnchor, float duration, bool fromColorFlag = false)
        {
            EnsureInitialized();

            bool wasBound = _isGreenBound;
            _isGreenBound = true;
            _greenFromColorFlag = fromColorFlag;
            _greenBindTimer = Mathf.Max(_greenBindTimer, duration);
            _greenBindAnchorPoint = worldAnchor;
            _greenLocalAttachPoint = transform.InverseTransformPoint(worldAnchor);

            CreateOrUpdateBindJoint(worldAnchor);
            UpdateBindTetherVisual();
            ApplyStatePhysicsModifiers();

            if (!wasBound)
            {
                OnBindStateChanged?.Invoke(true);
            }
        }

        public void DeactivateGreenBind()
        {
            if (!_isGreenBound) return;

            _isGreenBound = false;
            _greenBindTimer = 0f;
            _greenFromColorFlag = false;
            ClearBindJointAndVisual();
            ApplyStatePhysicsModifiers();
            OnBindStateChanged?.Invoke(false);
        }

        public void ActivateCyanStasis(float duration, bool fromColorFlag = false)
        {
            EnsureInitialized();

            bool wasStasis = _isBlueStasis;
            _isBlueStasis = true;
            _blueFromColorFlag = fromColorFlag;
            _blueStasisTimer = Mathf.Max(_blueStasisTimer, duration);

            if (!wasStasis && _rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity *= stasisInitialVelocityScale;
                _rb.angularVelocity *= stasisInitialVelocityScale;
            }

            ApplyStatePhysicsModifiers();

            if (!wasStasis)
            {
                OnStasisStateChanged?.Invoke(true);
            }
        }

        public void DeactivateCyanStasis()
        {
            if (!_isBlueStasis) return;

            _isBlueStasis = false;
            _blueStasisTimer = 0f;
            _blueFromColorFlag = false;
            ApplyStatePhysicsModifiers();
            OnStasisStateChanged?.Invoke(false);
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

            // Support direct ApplyColor() calls (e.g., from puzzle scripts or unit tests)
            if (greenFlag && !_isGreenBound)
            {
                ActivateGreenBind(transform.position, greenBindDuration > 0f ? greenBindDuration : 5.0f, true);
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
            if (_isGreenBound)
            {
                UpdateBindTetherVisual();
            }
        }

        /// <summary>
        /// Advances independent skill timers (callable in Edit Mode tests or Play Mode Update).
        /// </summary>
        public void TickSkillTimers(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (_isGreenBound && _greenBindTimer > 0f)
            {
                _greenBindTimer -= deltaTime;
                if (_greenBindTimer <= 0f)
                {
                    DeactivateGreenBind();
                }
            }

            if (_isBlueStasis && _blueStasisTimer > 0f)
            {
                _blueStasisTimer -= deltaTime;
                if (_blueStasisTimer <= 0f)
                {
                    DeactivateCyanStasis();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!_isGreenBound || _rb == null || _rb.isKinematic) return;

            // Reinforce spring pull toward anchor position while Green Bind is active
            Vector3 currentAttachWorld = transform.TransformPoint(_greenLocalAttachPoint);
            Vector3 offset = _greenBindAnchorPoint - currentAttachWorld;
            if (offset.sqrMagnitude > 0.0001f)
            {
                _rb.AddForceAtPosition(offset * greenBindSpring, currentAttachWorld, ForceMode.Acceleration);
            }
        }

        private void CreateOrUpdateBindJoint(Vector3 worldAnchor)
        {
            if (_rb == null) return;

            // Reuse existing joint to prevent duplicate joint accumulation
            if (_activeBindJoint == null)
            {
                _activeBindJoint = gameObject.GetComponent<SpringJoint>();
                if (_activeBindJoint == null)
                {
                    _activeBindJoint = gameObject.AddComponent<SpringJoint>();
                }
            }

            _activeBindJoint.autoConfigureConnectedAnchor = false;
            _activeBindJoint.connectedBody = null;
            _activeBindJoint.anchor = _greenLocalAttachPoint;
            _activeBindJoint.connectedAnchor = worldAnchor;
            _activeBindJoint.spring = greenBindSpring * Mathf.Max(1f, _rb.mass);
            _activeBindJoint.damper = greenBindDamping;
            _activeBindJoint.minDistance = 0f;
            _activeBindJoint.maxDistance = greenBindMaxDistance;
            _activeBindJoint.enableCollision = true;
        }

        private void UpdateBindTetherVisual()
        {
            if (!Application.isPlaying) return;

            if (_bindTetherLine == null)
            {
                GameObject lineObj = new GameObject("GreenBind_TetherLine");
                lineObj.transform.SetParent(transform, false);
                _bindTetherLine = lineObj.AddComponent<LineRenderer>();
                _bindTetherLine.positionCount = 2;
                _bindTetherLine.useWorldSpace = true;
                _bindTetherLine.startWidth = 0.035f;
                _bindTetherLine.endWidth = 0.02f;
                _bindTetherLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _bindTetherLine.receiveShadows = false;

                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit == null) unlit = Shader.Find("Sprites/Default");
                Material mat = new Material(unlit);
                mat.color = new Color(0.2f, 1.0f, 0.35f, 0.95f);
                _bindTetherLine.material = mat;
            }

            _bindTetherLine.enabled = true;
            Vector3 attachWorld = transform.TransformPoint(_greenLocalAttachPoint);
            _bindTetherLine.SetPosition(0, _greenBindAnchorPoint);
            _bindTetherLine.SetPosition(1, attachWorld);
        }

        private void ClearBindJointAndVisual()
        {
            if (_activeBindJoint != null)
            {
                if (Application.isPlaying)
                    Destroy(_activeBindJoint);
                else
                    DestroyImmediate(_activeBindJoint);
                _activeBindJoint = null;
            }

            if (_bindTetherLine != null)
            {
                _bindTetherLine.enabled = false;
            }
        }

        private void ApplyStatePhysicsModifiers()
        {
            if (_rb == null || !_hasCachedPhysics) return;

            if (!_isGreenBound && !_isBlueStasis)
            {
                RestoreOriginalPhysics();
                return;
            }

            float targetLinearDamping = _originalLinearDamping;
            float targetAngularDamping = _originalAngularDamping;
            bool targetGravity = _originalUseGravity;
            RigidbodyConstraints targetConstraints = _originalConstraints;

            if (_isGreenBound)
            {
                targetLinearDamping = Mathf.Max(targetLinearDamping, greenBindDamping);
                targetAngularDamping = Mathf.Max(targetAngularDamping, greenBindDamping);
            }

            if (_isBlueStasis)
            {
                targetLinearDamping = Mathf.Max(targetLinearDamping, stasisLinearDamping);
                targetAngularDamping = Mathf.Max(targetAngularDamping, stasisLinearDamping);
                targetGravity = false;

                if (fullFreezeOnStasis)
                {
                    targetConstraints = RigidbodyConstraints.FreezeAll;
                }
            }

            _rb.linearDamping = targetLinearDamping;
            _rb.angularDamping = targetAngularDamping;
            _rb.useGravity = targetGravity;
            _rb.constraints = targetConstraints;
        }

        private void RestoreOriginalPhysics()
        {
            if (_rb == null || !_hasCachedPhysics) return;

            _rb.linearDamping = _originalLinearDamping;
            _rb.angularDamping = _originalAngularDamping;
            _rb.useGravity = _originalUseGravity;
            _rb.constraints = _originalConstraints;
        }
    }
}
