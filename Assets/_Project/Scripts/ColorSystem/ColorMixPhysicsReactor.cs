using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Target-side mix reactor: listens for primary flag transitions and runs Yellow / Magenta / Cyan / White physics.
    /// Primaries (Red impulse, Green tether, Blue stasis) stay on <see cref="SpectrumSkillResponder"/>.
    /// Visual mixed colors come from <see cref="RGBColorTarget.GetColorForState"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RGBColorTarget))]
    public class ColorMixPhysicsReactor : MonoBehaviour
    {
        private const ObjectColorState AllPrimaries =
            ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue;

        [Header("Magenta — Time Bomb")]
        [SerializeField] private float magentaFuseSeconds = 2.5f;
        [SerializeField] private float magentaExplosionForce = 28f;
        [SerializeField] private float magentaExplosionRadius = 5f;
        [SerializeField] private float magentaUpwardModifier = 1.2f;
        [SerializeField] private float magentaSelfVelocityScale = 1.35f;

        [Header("Yellow — Kinetic Snap")]
        [SerializeField] private float yellowSnapImpulse = 22f;
        [SerializeField] private float yellowPushWeight = 1f;
        [SerializeField] private float yellowPullWeight = 1f;

        [Header("Cyan — Telekinesis")]
        [SerializeField] private float cyanFollowDistance = 4f;
        [SerializeField] private float cyanFollowSmoothTime = 0.4f;
        [SerializeField] private float cyanFollowMaxSpeed = 10f;
        [SerializeField] private float maxTelekinesisMass = 50f;

        [Header("VFX Hooks (Elias)")]
        public UnityEvent OnMagentaTriggered = new UnityEvent();
        public UnityEvent OnMagentaDetonated = new UnityEvent();
        public UnityEvent OnCyanTriggered = new UnityEvent();
        public UnityEvent OnYellowTriggered = new UnityEvent();
        public UnityEvent OnWhiteTriggered = new UnityEvent();

        private RGBColorTarget _colorTarget;
        private SpectrumSkillResponder _skillResponder;
        private Rigidbody _rb;

        private ObjectColorState _previousPrimaryMask = ObjectColorState.None;
        private bool _subscribed;

        private bool _magentaArmed;
        private Coroutine _magentaFuseRoutine;
        private bool _clearing;

        private bool _cyanZeroG;
        private float _preCyanLinearDamping;
        private float _preCyanAngularDamping;
        private bool _preCyanUseGravity;
        private RigidbodyConstraints _preCyanConstraints;

        private void Awake()
        {
            _colorTarget = GetComponent<RGBColorTarget>();
            _skillResponder = GetComponent<SpectrumSkillResponder>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            CancelMagentaFuse(false);
            EndCyanZeroG();
        }

        private void Subscribe()
        {
            if (_colorTarget == null || _subscribed) return;
            _colorTarget.OnColorStateChanged += HandleColorStateChanged;
            _subscribed = true;
            _previousPrimaryMask = PrimaryMask(_colorTarget.CurrentColorState);
        }

        private void Unsubscribe()
        {
            if (_colorTarget == null || !_subscribed) return;
            _colorTarget.OnColorStateChanged -= HandleColorStateChanged;
            _subscribed = false;
        }

        private void FixedUpdate()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;
            if (!_cyanZeroG || _rb == null) return;

            _rb.useGravity = false;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            if (_rb.isKinematic)
            {
                _rb.isKinematic = false;
            }
            if (_rb.constraints == RigidbodyConstraints.FreezeAll)
            {
                _rb.constraints = RigidbodyConstraints.None;
            }

            if (!Application.isPlaying) return;

            Camera view = Camera.main;
            if (view == null) return;

            Vector3 targetPoint = GetTelekinesisPoint(view);
            float smooth = Mathf.Max(0.08f, cyanFollowSmoothTime);
            Vector3 desiredVelocity = Vector3.ClampMagnitude((targetPoint - _rb.position) / smooth, cyanFollowMaxSpeed);
            float blend = 1f - Mathf.Exp(-6f * Time.fixedDeltaTime);
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, desiredVelocity, blend);
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, blend);
        }

        private Vector3 GetTelekinesisPoint(Camera view)
        {
            float distance = Mathf.Clamp(cyanFollowDistance, 3f, 5f);
            Transform cam = view.transform;
            Vector3 origin = cam.position;
            Vector3 forward = cam.forward;
            float hold = distance;

            RaycastHit[] hits = Physics.RaycastAll(origin, forward, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.attachedRigidbody == _rb) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (col.CompareTag("Player") || col.GetComponentInParent<CharacterController>() != null) continue;

                if (hits[i].distance < hold)
                {
                    hold = hits[i].distance;
                }
            }

            hold = Mathf.Max(0.75f, hold - 0.35f);
            return origin + (forward * hold);
        }

        private void HandleColorStateChanged(ObjectColorState newState)
        {
            if (_clearing) return;

            ObjectColorState current = PrimaryMask(newState);
            ObjectColorState previous = _previousPrimaryMask;

            if (current == previous)
            {
                return;
            }

            // White outranks all mixes (edge into full RGB).
            if (current == AllPrimaries)
            {
                ExecuteWhiteReset();
                _previousPrimaryMask = ObjectColorState.None;
                return;
            }

            if (current == (ObjectColorState.Red | ObjectColorState.Blue) && previous != (ObjectColorState.Red | ObjectColorState.Blue))
            {
                EnterMagenta(previous);
            }
            else if (current == (ObjectColorState.Green | ObjectColorState.Blue) && previous != (ObjectColorState.Green | ObjectColorState.Blue))
            {
                if (IsTooHeavyToLift())
                {
                    _previousPrimaryMask = current;
                    _colorTarget.CollapseOverweightCyanToStasis();
                    return;
                }

                EnterCyanSuspension();
            }
            else if (current == (ObjectColorState.Red | ObjectColorState.Green) && previous != (ObjectColorState.Red | ObjectColorState.Green))
            {
                ExecuteYellowSnap();
            }
            else
            {
                HandleMixBreak(previous, current);
            }

            _previousPrimaryMask = current;
        }

        private void HandleMixBreak(ObjectColorState previous, ObjectColorState current)
        {
            if (previous == (ObjectColorState.Green | ObjectColorState.Blue)
                && current == ObjectColorState.Blue
                && _colorTarget.ConsumeCyanStasisOverride())
            {
                EndCyanZeroG(restorePhysics: false);
                if (_rb != null && !_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                return;
            }

            if (IsMix(previous) && current != previous)
            {
                ClearAllToNone(zeroVelocity: false);
            }
        }

        private void EnterMagenta(ObjectColorState previous)
        {
            CancelMagentaFuse(false);
            EndCyanZeroG();

            if (_skillResponder != null && !_skillResponder.IsBlueStasis)
            {
                _skillResponder.ActivateCyanStasis(RGBColorTarget.ColorDuration, true);
            }

            _colorTarget.SetPrimaryTimerPaused(ObjectColorState.Red | ObjectColorState.Blue, true);

            _magentaArmed = true;
            OnMagentaTriggered?.Invoke();
            if (Application.isPlaying && isActiveAndEnabled)
            {
                _magentaFuseRoutine = StartCoroutine(MagentaFuseRoutine());
            }
        }

        private IEnumerator MagentaFuseRoutine()
        {
            float fuse = Mathf.Clamp(magentaFuseSeconds, 2f, 3f);
            yield return Recorded.Phase.PhaseRecorder.WaitWorldSeconds(fuse);

            if (!_magentaArmed)
            {
                yield break;
            }

            DetonateMagenta();
        }

        private void DetonateMagenta()
        {
            _magentaArmed = false;
            _magentaFuseRoutine = null;

            Vector3 center = _rb != null ? _rb.worldCenterOfMass : transform.position;
            Vector3 storedVelocity = _skillResponder != null ? _skillResponder.StoredStasisVelocity : Vector3.zero;

            if (_skillResponder != null)
            {
                _skillResponder.DeactivateCyanStasis(restoreSnapshotVelocity: false);
            }
            else if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.constraints = RigidbodyConstraints.None;
            }

            Collider[] hits = Physics.OverlapSphere(center, magentaExplosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                Rigidbody other = hits[i].attachedRigidbody;
                if (other == null || other.isKinematic) continue;
                other.AddExplosionForce(
                    magentaExplosionForce,
                    center,
                    magentaExplosionRadius,
                    magentaUpwardModifier,
                    ForceMode.Impulse);
            }

            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = storedVelocity * magentaSelfVelocityScale;
            }

            OnMagentaDetonated?.Invoke();
            ClearAllToNone(zeroVelocity: false);
        }

        private void CancelMagentaFuse(bool detonate)
        {
            if (!_magentaArmed && _magentaFuseRoutine == null) return;

            if (_magentaFuseRoutine != null)
            {
                StopCoroutine(_magentaFuseRoutine);
                _magentaFuseRoutine = null;
            }

            _magentaArmed = false;
            _colorTarget.SetPrimaryTimerPaused(ObjectColorState.Red | ObjectColorState.Blue, false);

            if (detonate)
            {
                DetonateMagenta();
            }
        }

        private bool IsTooHeavyToLift()
        {
            return _rb != null && _rb.mass > maxTelekinesisMass;
        }

        private void EnterCyanSuspension()
        {
            CancelMagentaFuse(false);

            if (_rb == null)
            {
                OnCyanTriggered?.Invoke();
                return;
            }

            if (_skillResponder != null && _skillResponder.IsBlueStasis)
            {
                _skillResponder.DeactivateCyanStasis(restoreSnapshotVelocity: true);
            }

            if (!_cyanZeroG)
            {
                _preCyanLinearDamping = _rb.linearDamping;
                _preCyanAngularDamping = _rb.angularDamping;
                _preCyanUseGravity = _rb.useGravity;
                _preCyanConstraints = _rb.constraints;
                _cyanZeroG = true;
            }

            _rb.isKinematic = false;
            _rb.useGravity = false;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            if (_rb.constraints == RigidbodyConstraints.FreezeAll)
            {
                _rb.constraints = RigidbodyConstraints.None;
            }

            _colorTarget.SetPrimaryTimerPaused(ObjectColorState.Green | ObjectColorState.Blue, false);
            OnCyanTriggered?.Invoke();
        }

        private void EndCyanZeroG(bool restorePhysics = true)
        {
            if (!_cyanZeroG || _rb == null)
            {
                _cyanZeroG = false;
                return;
            }

            _cyanZeroG = false;
            if (!restorePhysics) return;

            _rb.useGravity = _preCyanUseGravity;
            _rb.linearDamping = _preCyanLinearDamping;
            _rb.angularDamping = _preCyanAngularDamping;
            if (_rb.constraints == RigidbodyConstraints.None)
            {
                _rb.constraints = _preCyanConstraints == RigidbodyConstraints.FreezeAll
                    ? RigidbodyConstraints.None
                    : _preCyanConstraints;
            }
        }

        private void ExecuteYellowSnap()
        {
            if (_rb == null)
            {
                return;
            }

            if (_skillResponder != null && _skillResponder.IsBlueStasis)
            {
                _skillResponder.DeactivateCyanStasis(restoreSnapshotVelocity: false);
            }

            EndCyanZeroG();
            OnYellowTriggered?.Invoke();

            Vector3 redDir = Vector3.zero;
            Vector3 greenAnchor = Vector3.zero;

            if (_skillResponder != null)
            {
                redDir = _skillResponder.LastRedShotDirection;
                greenAnchor = _skillResponder.StoredGreenAnchorWorld;
                if (greenAnchor.sqrMagnitude < 0.0001f)
                {
                    greenAnchor = _skillResponder.GreenBindAnchorPoint;
                }
            }

            if (redDir.sqrMagnitude < 0.0001f && _skillResponder != null)
            {
                redDir = _skillResponder.LastRedImpulseApplied.normalized;
            }

            Vector3 bodyPos = _rb.worldCenterOfMass;
            Vector3 toAnchor = greenAnchor - bodyPos;
            if (redDir.sqrMagnitude < 0.0001f || toAnchor.sqrMagnitude < 0.05f)
            {
                return;
            }

            Vector3 pullDirection = toAnchor.normalized;
            Vector3 snapDirection = (redDir.normalized * yellowPushWeight + pullDirection * yellowPullWeight).normalized;
            if (snapDirection.sqrMagnitude < 0.0001f)
            {
                snapDirection = pullDirection;
            }

            if (_rb.isKinematic)
            {
                _rb.isKinematic = false;
                _rb.constraints = RigidbodyConstraints.None;
            }

            _rb.useGravity = true;
            _rb.AddForce(snapDirection * yellowSnapImpulse, ForceMode.Impulse);
        }

        private void ExecuteWhiteReset()
        {
            OnWhiteTriggered?.Invoke();
            ClearAllToNone(zeroVelocity: true);
        }

        /// <summary>
        /// Drops every primary, paint timer, and mix hold so the material returns to Neutral Grey.
        /// </summary>
        private void ClearAllToNone(bool zeroVelocity)
        {
            if (_clearing || _colorTarget == null) return;

            _clearing = true;
            CancelMagentaFuse(false);
            EndCyanZeroG();
            _colorTarget.SetPrimaryTimerPaused(AllPrimaries, false);

            if (_skillResponder != null)
            {
                if (_skillResponder.IsBlueStasis)
                {
                    _skillResponder.DeactivateCyanStasis(restoreSnapshotVelocity: false);
                }
                _skillResponder.DeactivateGreenBind();
                _skillResponder.ClearStoredReactionData();
            }

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                if (_rb.constraints == RigidbodyConstraints.FreezeAll)
                {
                    _rb.constraints = RigidbodyConstraints.None;
                }
                if (zeroVelocity)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }

            _colorTarget.ResetColors();
            _previousPrimaryMask = ObjectColorState.None;
            _clearing = false;
        }

        private static bool IsMix(ObjectColorState state)
        {
            ObjectColorState masked = state & AllPrimaries;
            return masked == (ObjectColorState.Red | ObjectColorState.Green)
                || masked == (ObjectColorState.Red | ObjectColorState.Blue)
                || masked == (ObjectColorState.Green | ObjectColorState.Blue);
        }

        private static ObjectColorState PrimaryMask(ObjectColorState state)
        {
            return state & AllPrimaries;
        }
    }
}
