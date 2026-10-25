using UnityEngine;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Spins a child rotor at a constant speed and parks it while Blue stasis is held.
    /// The hold follows SpectrumSkillResponder, which already lasts for the 10s Blue timer.
    /// A seal collider fills the walk gap only while the rotor is spinning, because a
    /// CharacterController steps through thin blades between frames.
    /// </summary>
    [DisallowMultipleComponent]
    public class StasisPausedRotor : MonoBehaviour
    {
        [SerializeField] private Transform rotor;
        [SerializeField] private Collider spinSeal;
        [SerializeField] private Vector3 localAxis = Vector3.forward;
        [SerializeField] private float degreesPerSecond = 1080f;
        [SerializeField] private float openLocalAngle = 0f;
        [Tooltip("Blade spacing. Parking snaps to the nearest pose that puts a blade on local +Y.")]
        [SerializeField] private float parkIntervalDegrees = 120f;
        [SerializeField] private float snapDegreesPerSecond = 720f;

        private SpectrumSkillResponder _skill;
        private float _angle;
        private float _parkAngle;
        private bool _held;
        private bool _snapping;

        public bool IsHeld => _held;
        public float CurrentAngle => _angle;

        private void Awake()
        {
            _skill = GetComponent<SpectrumSkillResponder>();
            _angle = openLocalAngle;
            ApplyAngle();
        }

        private void OnEnable()
        {
            if (_skill != null)
                _skill.OnStasisStateChanged += HandleStasis;
        }

        private void Start()
        {
            SetHeld(_skill != null && _skill.IsBlueStasis);
        }

        private void OnDisable()
        {
            if (_skill != null)
                _skill.OnStasisStateChanged -= HandleStasis;
        }

        private void Update()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_held)
            {
                if (!_snapping) return;
                float delta = Mathf.DeltaAngle(_angle, _parkAngle);
                if (Mathf.Abs(delta) <= 0.4f)
                {
                    _angle = _parkAngle;
                    _snapping = false;
                    if (spinSeal != null)
                        spinSeal.enabled = false;
                }
                else
                {
                    float step = snapDegreesPerSecond * dt;
                    _angle += Mathf.Clamp(delta, -step, step);
                }
            }
            else
            {
                _angle += degreesPerSecond * dt;
            }

            ApplyAngle();
        }

        private void HandleStasis(bool held)
        {
            SetHeld(held);
        }

        private void SetHeld(bool held)
        {
            _held = held;
            if (!held)
            {
                _snapping = false;
                if (spinSeal != null)
                    spinSeal.enabled = true;
                return;
            }

            _parkAngle = NearestParkAngle(_angle);
            _snapping = Mathf.Abs(Mathf.DeltaAngle(_angle, _parkAngle)) > 0.4f;
            if (spinSeal != null)
                spinSeal.enabled = true;
            if (!_snapping && spinSeal != null)
                spinSeal.enabled = false;
        }

        private float NearestParkAngle(float angle)
        {
            float interval = Mathf.Max(1f, parkIntervalDegrees);
            float shifted = angle - openLocalAngle;
            float steps = Mathf.Round(shifted / interval);
            return openLocalAngle + steps * interval;
        }

        private void ApplyAngle()
        {
            if (rotor == null) return;
            rotor.localRotation = Quaternion.AngleAxis(_angle, localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.forward);
        }
    }
}
