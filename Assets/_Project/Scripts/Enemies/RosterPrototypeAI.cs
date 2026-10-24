using Recorded.ColorSystem;
using UnityEngine;

namespace Recorded.Enemies
{
    /// <summary>Restricts the Red finisher to one mix. A third Red on any other mix does not kill.</summary>
    public sealed class FinisherMixGate : MonoBehaviour
    {
        public enum RequiredMix { Magenta, Cyan }

        [SerializeField] private RequiredMix required = RequiredMix.Magenta;

        public void SetRequired(RequiredMix value) => required = value;

        public bool Allows(ObjectColorState state)
        {
            bool red = (state & ObjectColorState.Red) != 0;
            bool green = (state & ObjectColorState.Green) != 0;
            bool blue = (state & ObjectColorState.Blue) != 0;
            if (required == RequiredMix.Magenta) return red && blue && !green;
            return green && blue && !red;
        }
    }

    /// <summary>Visible roster prototypes. Color rules still live on EnemyColorReactor.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RosterPrototypeAI : MonoBehaviour, IEnemyBrain
    {
        public enum Kind { Drone, Demolitionist, Stalker, Glint }

        [SerializeField] private Kind kind = Kind.Drone;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float hoverHeight = 4.2f;

        private Rigidbody _body;
        private Transform _player;
        private Vector3 _home;
        private float _seed;
        private bool _dead;

        public bool IsDead => _dead;

        public void Configure(Kind value, float speed, float hover)
        {
            kind = value;
            moveSpeed = speed;
            hoverHeight = hover;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _home = transform.position;
            _seed = transform.position.x * 0.37f + transform.position.z * 0.17f;
        }

        private void Start()
        {
            if (Camera.main != null) _player = Camera.main.transform;
        }

        private void Update()
        {
            if (_dead || _body == null || _body.isKinematic) return;
            if (_player == null && Camera.main != null) _player = Camera.main.transform;

            if (kind == Kind.Drone || kind == Kind.Glint) Fly();
            else Walk();
        }

        private void Fly()
        {
            Vector3 home = kind == Kind.Glint ? _home + GlintOffset() : _home + DroneOffset();
            if (_player != null && kind == Kind.Stalker) home = _player.position;
            Vector3 to = home - transform.position;
            Vector3 velocity = to * 1.6f;
            float vertical = hoverHeight - transform.position.y;
            velocity.y = Mathf.Clamp(vertical * 3f, -5f, 5f);
            if (velocity.magnitude > moveSpeed) velocity = velocity.normalized * moveSpeed;
            _body.linearVelocity = velocity;
            Vector3 flat = velocity;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.05f) transform.rotation = Quaternion.LookRotation(flat.normalized);
        }

        private Transform _hologram;
        private float _hologramScan;

        private void Walk()
        {
            Vector3 target = _player != null ? _player.position : _home;
            if (kind == Kind.Stalker)
            {
                _hologramScan -= Time.deltaTime;
                if (_hologramScan <= 0f)
                {
                    _hologram = FindHologram();
                    _hologramScan = 0.5f;
                }
                if (_hologram != null) target = _hologram.position;
                else target += StalkerWeave();
            }

            Vector3 to = target - transform.position;
            to.y = 0f;
            float stop = kind == Kind.Demolitionist ? 3.2f : 1.6f;
            Vector3 velocity = _body.linearVelocity;
            if (to.magnitude > stop)
            {
                Vector3 direction = to.normalized;
                velocity.x = direction.x * moveSpeed;
                velocity.z = direction.z * moveSpeed;
                transform.rotation = Quaternion.LookRotation(direction);
            }
            else
            {
                velocity.x = 0f;
                velocity.z = 0f;
            }
            bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore);
            if (grounded || velocity.y > 0f) velocity.y = 0f;
            _body.linearVelocity = velocity;
        }

        private Vector3 DroneOffset()
        {
            float t = Time.time + _seed;
            return new Vector3(Mathf.Sin(t * 0.7f) * 2.4f, 0f, Mathf.Cos(t * 0.45f) * 1.6f);
        }

        private Vector3 GlintOffset()
        {
            float t = Time.time * 1.7f + _seed;
            return new Vector3(Mathf.Sin(t) * 1.3f, Mathf.Sin(t * 2.1f) * 0.45f, Mathf.Cos(t * 0.8f) * 1.3f);
        }

        private Vector3 StalkerWeave()
        {
            return new Vector3(Mathf.Sin(Time.time * 3.2f + _seed) * 3.5f, 0f, 0f);
        }

        private static Transform FindHologram()
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (candidate.name.IndexOf("Hologram", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && candidate.gameObject.activeInHierarchy)
                    return candidate;
            }
            return null;
        }

        public void PauseForColor() => enabled = false;
        public void ResumeAfterColor() => enabled = true;
        public void InterruptForRed() { }

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
        }
    }
}
