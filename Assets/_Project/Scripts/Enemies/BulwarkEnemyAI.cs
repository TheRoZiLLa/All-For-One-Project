using Recorded.ColorSystem;
using UnityEngine;

namespace Recorded.Enemies
{
    /// <summary>
    /// Advances behind a front plate. The plate holds one primary and throws that color back.
    /// A second primary, Yellow most of all, rips the plate off. The body uses the shared finisher.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BulwarkEnemyAI : MonoBehaviour, IEnemyBrain
    {
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float holdDistance = 2.8f;

        private Rigidbody _body;
        private Transform _player;
        private BulwarkShield _shield;
        private bool _dead;

        public bool IsDead => _dead;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _shield = GetComponentInChildren<BulwarkShield>(true);
        }

        private void Start()
        {
            if (_shield == null) _shield = GetComponentInChildren<BulwarkShield>(true);
            if (Camera.main != null) _player = Camera.main.transform;
        }

        private void Update()
        {
            if (_dead) return;
            if (_player == null && Camera.main != null) _player = Camera.main.transform;
            if (_player == null || _body == null || _body.isKinematic) return;

            Vector3 to = _player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.04f) return;
            float distance = to.magnitude;
            Vector3 direction = to / distance;
            transform.rotation = Quaternion.LookRotation(direction);

            Vector3 velocity = _body.linearVelocity;
            if (distance > holdDistance)
            {
                velocity.x = direction.x * moveSpeed;
                velocity.z = direction.z * moveSpeed;
            }
            else
            {
                velocity.x = 0f;
                velocity.z = 0f;
            }
            bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            if (grounded || velocity.y > 0f) velocity.y = 0f;
            _body.linearVelocity = velocity;
        }

        public void PauseForColor() => enabled = false;

        public void ResumeAfterColor() => enabled = true;

        public void InterruptForRed()
        {
        }

        public void Die()
        {
            if (_dead) return;
            _dead = true;
            enabled = false;
            if (_body != null)
            {
                _body.isKinematic = false;
                _body.constraints = RigidbodyConstraints.None;
                _body.WakeUp();
            }
        }

        public void Revive()
        {
            _dead = false;
            enabled = true;
            if (_shield == null) _shield = GetComponentInChildren<BulwarkShield>(true);
            if (_shield != null) _shield.Restore();
        }
    }

    /// <summary>Front plate. Breaks off once two primaries are on the body, harder on Yellow.</summary>
    public sealed class BulwarkShield : MonoBehaviour
    {
        private Transform _home;
        private Vector3 _localPosition;
        private Quaternion _localRotation;
        private Vector3 _localScale;
        private RGBColorTarget _colors;
        private Renderer _renderer;
        private Material _material;
        private bool _broken;

        private void Awake()
        {
            _home = transform.parent;
            _localPosition = transform.localPosition;
            _localRotation = transform.localRotation;
            _localScale = transform.localScale;
            _colors = _home != null ? _home.GetComponent<RGBColorTarget>() : null;
            _renderer = GetComponent<Renderer>();
            if (_renderer != null) _material = _renderer.material;
            Tint(ObjectColorState.None);
            if (_colors != null) OnColorChanged(_colors.CurrentColorState);
        }

        private void OnEnable()
        {
            if (_colors != null) _colors.OnColorStateChanged += OnColorChanged;
        }

        private void OnDisable()
        {
            if (_colors != null) _colors.OnColorStateChanged -= OnColorChanged;
        }

        private void OnColorChanged(ObjectColorState state)
        {
            Tint(state);
            if (_broken) return;
            int primaries = ((state & ObjectColorState.Red) != 0 ? 1 : 0)
                + ((state & ObjectColorState.Green) != 0 ? 1 : 0)
                + ((state & ObjectColorState.Blue) != 0 ? 1 : 0);
            if (primaries >= 2) BreakOff(state);
            else if (primaries == 1) Reflect(state);
        }

        public void Restore()
        {
            _broken = false;
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                if (Application.isPlaying) Destroy(body);
                else DestroyImmediate(body);
            }
            if (_home != null) transform.SetParent(_home, false);
            transform.localPosition = _localPosition;
            transform.localRotation = _localRotation;
            transform.localScale = _localScale;
            Collider plate = GetComponent<Collider>();
            if (plate != null) plate.enabled = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Tint(ObjectColorState.None);
        }

        private void BreakOff(ObjectColorState state)
        {
            _broken = true;
            Collider plate = GetComponent<Collider>();
            if (plate != null) plate.enabled = false;
            Vector3 push = _home != null ? _home.forward : transform.forward;
            bool yellow = (state & ObjectColorState.Red) != 0 && (state & ObjectColorState.Green) != 0;
            if (yellow) push *= 1.8f;
            transform.SetParent(null, true);
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.mass = 2.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.AddForce(push * 5f + Vector3.up * 2.2f, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
        }

        private void Reflect(ObjectColorState state)
        {
            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = "Bulwark_Reflect";
            pulse.transform.position = transform.position + transform.forward * 0.25f;
            pulse.transform.localScale = Vector3.one * 0.22f;
            Collider sphere = pulse.GetComponent<Collider>();
            if (sphere != null) Destroy(sphere);
            Vector3 direction = transform.forward;
            if (Camera.main != null)
            {
                Vector3 toCamera = Camera.main.transform.position - pulse.transform.position;
                if (toCamera.sqrMagnitude > 0.01f) direction = toCamera.normalized;
            }
            pulse.AddComponent<BulwarkReflectPulse>().Launch(direction, ColorOf(state));
            Destroy(pulse, 1.25f);
        }

        private void Tint(ObjectColorState state)
        {
            if (_material == null) return;
            Color color = ColorOf(state);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", color);
            else _material.color = color;
            if (_material.HasProperty("_EmissionColor"))
            {
                _material.EnableKeyword("_EMISSION");
                _material.SetColor("_EmissionColor", color * 0.35f);
            }
        }

        private static Color ColorOf(ObjectColorState state)
        {
            bool red = (state & ObjectColorState.Red) != 0;
            bool green = (state & ObjectColorState.Green) != 0;
            bool blue = (state & ObjectColorState.Blue) != 0;
            if (red && green && blue) return Color.white;
            if (red && green) return new Color(1f, 0.86f, 0.15f);
            if (red && blue) return new Color(0.85f, 0.2f, 0.75f);
            if (green && blue) return new Color(0.2f, 0.85f, 0.9f);
            if (red) return new Color(0.9f, 0.16f, 0.12f);
            if (green) return new Color(0.15f, 0.85f, 0.28f);
            if (blue) return new Color(0.2f, 0.45f, 1f);
            return new Color(0.16f, 0.18f, 0.22f);
        }
    }

    public sealed class BulwarkReflectPulse : MonoBehaviour
    {
        private Vector3 _velocity;

        public void Launch(Vector3 direction, Color color)
        {
            _velocity = direction.normalized * 9f;
            Renderer renderer = GetComponent<Renderer>();
            if (renderer == null) return;
            Material material = renderer.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }
        }

        private void Update()
        {
            transform.position += _velocity * Time.deltaTime;
        }
    }
}
