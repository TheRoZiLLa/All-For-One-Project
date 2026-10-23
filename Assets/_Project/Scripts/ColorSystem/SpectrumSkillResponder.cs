using UnityEngine;
using Recorded.Weapons.Ammo;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Reusable component that binds provisional Spectrum Gun gameplay skills to an object's RGBColorTarget state.
    ///
    /// Provisional Gameplay Mapping (Inspector-configurable for puzzle/level designers):
    /// - RED (AmmoType.Red -> ObjectColorState.Red):
    ///   "Impulse" — Applies an immediate directional physical push to a Rigidbody on shot impact.
    ///   Works on every Red shot hit via IColorShotImpact without resetting/extending the 5s Red color timer.
    /// - GREEN (AmmoType.Green -> ObjectColorState.Green):
    ///   "Bind" — Anchors/tethers the object toward the position where Green was activated while the Green flag holds.
    /// - CYAN (AmmoType.Cyan -> ObjectColorState.Blue):
    ///   "Stasis" — Arrests motion, suspends gravity, and applies heavy damping (or freeze) while the Blue flag holds.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpectrumSkillResponder : MonoBehaviour, IColorShotImpact
    {
        [Header("RED — Impulse (Immediate on Shot Impact)")]
        [Tooltip("Enable physical impulse push when hit by Red ammo.")]
        [SerializeField] private bool enableRedImpulse = true;

        [Tooltip("Force magnitude applied in the shot direction when hit by Red ammo.")]
        [SerializeField] private float redImpulseForce = 6.5f;

        [Tooltip("Upward lift bias added to Red impulse for sandbox physics feel.")]
        [SerializeField] private float redUpwardLift = 1.25f;

        [Header("GREEN — Bind (Active While Green Flag Holds)")]
        [Tooltip("Enable positional anchor/tether damping while Green state is active.")]
        [SerializeField] private bool enableGreenBind = true;

        [Tooltip("Spring strength pulling the object toward its bind anchor point.")]
        [SerializeField] private float greenBindSpring = 35.0f;

        [Tooltip("Linear damping applied while bound.")]
        [SerializeField] private float greenBindDamping = 12.0f;

        [Header("CYAN / BLUE Flag — Stasis (Active While Blue Flag Holds)")]
        [Tooltip("Enable stasis motion arrest/slowdown while Blue flag (from Cyan ammo) is active.")]
        [SerializeField] private bool enableCyanStasis = true;

        [Tooltip("If true, completely freezes Rigidbody motion during Stasis. If false, applies heavy drag slowdown.")]
        [SerializeField] private bool fullFreezeOnStasis = false;

        [Tooltip("Velocity multiplier when Stasis begins (0.15 = 85% immediate speed reduction).")]
        [Range(0f, 1f)]
        [SerializeField] private float stasisInitialVelocityScale = 0.15f;

        [Tooltip("Linear damping applied while in Stasis slowdown mode.")]
        [SerializeField] private float stasisLinearDamping = 18.0f;

        private IColorReceivable _colorTarget;
        private Rigidbody _rb;
        private bool _subscribed;

        // Cached original Rigidbody state for clean restoration
        private bool _hasCachedPhysics;
        private float _originalLinearDamping;
        private float _originalAngularDamping;
        private bool _originalUseGravity;
        private RigidbodyConstraints _originalConstraints;

        private bool _isGreenBound;
        private Vector3 _greenBindAnchorPoint;
        private bool _isBlueStasis;
        private Vector3 _lastRedImpulseApplied;
        private int _redImpulseHitCount;

        public bool IsGreenBound => _isGreenBound;
        public bool IsBlueStasis => _isBlueStasis;
        public Vector3 GreenBindAnchorPoint => _greenBindAnchorPoint;
        public Vector3 LastRedImpulseApplied => _lastRedImpulseApplied;
        public int RedImpulseHitCount => _redImpulseHitCount;

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

            RestoreOriginalPhysics();
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
        /// Executes immediate shot effects (such as RED Impulse) even if the color flag is already active.
        /// </summary>
        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            EnsureInitialized();

            // RED — Impulse: Immediate physical push on impact
            if (enableRedImpulse && (firedAmmo == AmmoType.Red || (mappedColorFlag & ObjectColorState.Red) != 0))
            {
                Vector3 pushDir = shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : -hitInfo.normal;
                Vector3 impulse = (pushDir * redImpulseForce) + (Vector3.up * redUpwardLift);
                _lastRedImpulseApplied = impulse;
                _redImpulseHitCount++;

                if (_rb != null && !_rb.isKinematic)
                {
                    if (Application.isPlaying)
                    {
                        _rb.AddForceAtPosition(impulse, hitInfo.point, ForceMode.Impulse);
                    }
                    else
                    {
                        float mass = Mathf.Max(0.001f, _rb.mass);
                        _rb.linearVelocity += impulse / mass;
                    }
                }
            }
        }

        private void HandleColorStateChanged(ObjectColorState newState)
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody>();
            }
            CacheOriginalPhysics();

            bool greenActive = enableGreenBind && (newState & ObjectColorState.Green) != 0;
            bool blueActive = enableCyanStasis && (newState & ObjectColorState.Blue) != 0;

            // Transition into Green Bind
            if (greenActive && !_isGreenBound)
            {
                _isGreenBound = true;
                _greenBindAnchorPoint = transform.position;
            }
            else if (!greenActive && _isGreenBound)
            {
                _isGreenBound = false;
            }

            // Transition into Blue (Cyan) Stasis
            if (blueActive && !_isBlueStasis)
            {
                _isBlueStasis = true;
                if (_rb != null && !_rb.isKinematic)
                {
                    _rb.linearVelocity *= stasisInitialVelocityScale;
                    _rb.angularVelocity *= stasisInitialVelocityScale;
                }
            }
            else if (!blueActive && _isBlueStasis)
            {
                _isBlueStasis = false;
            }

            ApplyStatePhysicsModifiers();
        }

        private void FixedUpdate()
        {
            if (!_isGreenBound || _rb == null || _rb.isKinematic) return;

            // Pull gently toward anchor position while Green Bind is active
            Vector3 offset = _greenBindAnchorPoint - _rb.position;
            if (offset.sqrMagnitude > 0.0001f)
            {
                _rb.AddForce(offset * greenBindSpring, ForceMode.Acceleration);
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