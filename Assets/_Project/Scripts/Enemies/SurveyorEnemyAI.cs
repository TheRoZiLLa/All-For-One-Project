using Recorded.Phase;
using UnityEngine;

namespace Recorded.Enemies
{
    /// <summary>Small, readable tactical prototype: observes lanes, telegraphs an intercept, then repositions.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SurveyorEnemyAI : MonoBehaviour, IEnemyBrain
    {
        private enum Phase { Observe, Telegraph, Attack, Recover, Relocate }

        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform hologramTarget;
        [SerializeField] private Transform telegraphBeam;
        [SerializeField] private Renderer telegraphRenderer;
        [SerializeField] private SurveyorBolt boltPrefab;
        [SerializeField] private float observeSeconds = 1.2f;
        [SerializeField] private float telegraphSeconds = 1.1f;
        [SerializeField] private float recoverSeconds = 1.4f;
        [SerializeField] private float attackRange = 15f;
        [SerializeField] private float relocateDistance = 3f;

        private Rigidbody _body;
        private Transform _target;
        private Vector3 _lastTargetPosition;
        private Vector3 _targetVelocity;
        private Vector3 _aimPoint;
        private float _phaseTimer;
        private int _sameLaneReads;
        private Vector3 _previousLane;
        private Phase _phase;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            if (telegraphBeam == null) telegraphBeam = transform.Find("TelegraphBeam");
            if (telegraphRenderer == null && telegraphBeam != null) telegraphRenderer = telegraphBeam.GetComponent<Renderer>();
        }

        private void Start()
        {
            if (playerTarget == null && Camera.main != null)
                playerTarget = Camera.main.transform;
            if (hologramTarget == null)
                hologramTarget = FindNamedTransform("Hologram");
            SetTelegraph(false, Vector3.zero);
            _phase = Phase.Observe;
        }

        private void Update()
        {
            if (_dead) return;
            if (playerTarget == null && Camera.main != null)
                playerTarget = Camera.main.transform;
            CloseOnTarget();
            HologramPresence.SteerAgent(this, Aggro());

            if (_phase == Phase.Observe)
            {
                _phaseTimer += Time.deltaTime;
                if (_phaseTimer >= observeSeconds) ChoosePlan();
                return;
            }

            _phaseTimer -= Time.deltaTime;
            if (_phase == Phase.Telegraph)
            {
                SetTelegraph(true, _aimPoint);
                if (_phaseTimer <= 0f) ExecuteAttack();
            }
            else if (_phase == Phase.Attack && _phaseTimer <= 0f)
            {
                SetTelegraph(false, Vector3.zero);
                _phase = Phase.Recover;
                _phaseTimer = recoverSeconds;
            }
            else if (_phase == Phase.Recover && _phaseTimer <= 0f)
            {
                _phase = Phase.Relocate;
                _phaseTimer = 0.45f;
                Vector3 away = Vector3.Cross(Vector3.up, _aimPoint - transform.position).normalized;
                if (_sameLaneReads >= 2) away = (transform.position - _aimPoint).normalized;
                _body.AddForce(away * relocateDistance, ForceMode.VelocityChange);
            }
            else if (_phase == Phase.Relocate && _phaseTimer <= 0f)
            {
                _phase = Phase.Observe;
                _phaseTimer = 0f;
            }
        }

        private void CloseOnTarget()
        {
            Transform aim = Aggro();
            if (_phase != Phase.Observe || _body == null || _body.isKinematic || aim == null) return;
            Vector3 to = aim.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.25f) return;
            float dist = to.magnitude;
            Vector3 dir = to / dist;
            if (dist <= 7.5f)
            {
                Vector3 stopped = _body.linearVelocity;
                stopped.x = 0f;
                stopped.z = 0f;
                _body.linearVelocity = stopped;
                return;
            }
            Vector3 vel = dir * 6f;
            vel.y = _body.linearVelocity.y > 0f ? 0f : _body.linearVelocity.y;
            _body.linearVelocity = vel;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        private void ChoosePlan()
        {
            Transform selected = Aggro();
            bool hologram = HologramPresence.IsLive;
            if (selected == null) return;
            if (!hologram && !IsUsable(selected)) return;

            Vector3 observed = selected.position;
            _targetVelocity = (observed - _lastTargetPosition) / Mathf.Max(observeSeconds, 0.01f);
            _lastTargetPosition = observed;
            Vector3 lane = (observed - transform.position).normalized;
            _sameLaneReads = Vector3.Dot(lane, _previousLane) > 0.94f ? _sameLaneReads + 1 : 1;
            _previousLane = lane;
            _target = selected;

            // Repeatedly holding one lane makes the Surveyor predict a short lead; movement can invalidate it.
            float lead = _sameLaneReads >= 2 ? 0.35f : 0f;
            _aimPoint = observed + Vector3.ClampMagnitude(_targetVelocity, 5f) * lead;
            _phase = Phase.Telegraph;
            _phaseTimer = telegraphSeconds;
        }

        private void ExecuteAttack()
        {
            SetTelegraph(false, Vector3.zero);
            if (IsUsable(_target) && Vector3.Distance(transform.position, _target.position) <= attackRange)
            {
                Vector3 origin = transform.position + Vector3.up * 0.35f;
                Vector3 direction = (_aimPoint - origin).normalized;
                if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
                SurveyorBolt bolt = boltPrefab != null
                    ? Instantiate(boltPrefab, origin, Quaternion.LookRotation(direction))
                    : CreateBolt(origin);
                bolt.Launch(direction, transform);
            }
            _phase = Phase.Attack;
            _phaseTimer = 0.2f;
        }

        private SurveyorBolt CreateBolt(Vector3 origin)
        {
            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = "Surveyor_Pulse";
            pulse.transform.position = origin;
            pulse.transform.localScale = Vector3.one * 0.28f;
            SphereCollider sphere = pulse.GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            Rigidbody pulseBody = pulse.AddComponent<Rigidbody>();
            pulseBody.isKinematic = true;
            SurveyorBolt bolt = pulse.AddComponent<SurveyorBolt>();
            return bolt;
        }

        private void SetTelegraph(bool visible, Vector3 destination)
        {
            if (telegraphBeam == null || telegraphRenderer == null) return;
            telegraphRenderer.enabled = visible;
            if (!visible) return;
            Vector3 origin = transform.position + Vector3.up * 1.1f;
            Vector3 target = destination + Vector3.up * 0.8f;
            Vector3 delta = target - origin;
            telegraphBeam.position = (origin + target) * 0.5f;
            telegraphBeam.rotation = Quaternion.LookRotation(delta.normalized);
            telegraphBeam.localScale = new Vector3(0.055f, 0.045f, Mathf.Max(0.1f, delta.magnitude));
        }

        private Transform Aggro()
        {
            if (playerTarget == null && Camera.main != null) playerTarget = Camera.main.transform;
            return HologramPresence.Resolve(playerTarget);
        }

        private bool IsUsable(Transform candidate) => candidate != null && candidate.gameObject.activeInHierarchy
            && Vector3.Distance(transform.position, candidate.position) <= attackRange;

        private static Transform FindNamedTransform(string contains)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (candidate.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            return null;
        }

        public void SetTargets(Transform player, Transform hologram = null)
        {
            playerTarget = player;
            hologramTarget = hologram;
        }

        public void PauseForColor() => enabled = false;
        public void ResumeAfterColor() { enabled = true; _phase = Phase.Observe; _phaseTimer = 0f; }

        public void InterruptForRed()
        {
            if (_dead || !enabled) return;
            SetTelegraph(false, Vector3.zero);
            _phase = Phase.Observe;
            _phaseTimer = 0f;
        }

        public bool IsDead => _dead;

        /// <summary>Ragdoll. The reactor respawns the body after the knockback reads.</summary>
        public void Die()
        {
            if (_dead) return;
            _dead = true;
            enabled = false;
            SetTelegraph(false, Vector3.zero);
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
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
            _phase = Phase.Observe;
            _phaseTimer = 0f;
            SetTelegraph(false, Vector3.zero);
        }

        private bool _dead;
    }
}
