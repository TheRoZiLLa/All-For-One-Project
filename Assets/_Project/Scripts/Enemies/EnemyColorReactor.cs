using System.Collections;
using Recorded.ColorSystem;
using Recorded.Weapons.Ammo;
using UnityEngine;
using UnityEngine.AI;

namespace Recorded.Enemies
{
    /// <summary>
    /// Bridges additive RGB states to enemy AI. Normal color hits never kill.
    /// The third consecutive shot kills only when it is Red and the enemy is already mixed.
    /// A green reel drops the NavMeshAgent and un-kinematic the body until the pull settles.
    /// </summary>
    [RequireComponent(typeof(RGBColorTarget))]
    public sealed class EnemyColorReactor : MonoBehaviour, IColorShotImpact, ITetherPullReceiver
    {
        private const float FinisherDelay = 0.35f;
        private const float RespawnDelay = 2.5f;
        private const float SettleSeconds = 0.2f;
        private const float SettleSpeedSqr = 0.04f;

        private RGBColorTarget _colors;
        private IEnemyBrain _ai;
        private SpectrumSkillResponder _skill;
        private NavMeshAgent _agent;
        private Animator _animator;
        private Rigidbody _body;
        private bool _agentWasEnabled;
        private bool _agentWasStopped;
        private float _animatorSpeed = 1f;
        private bool _paused;
        private bool _stateCheckPending;
        private bool _releasedForPull;
        private float _settleTimer;
        private int _shotCount;
        private bool _finisherQueued;
        private bool _dead;
        private Coroutine _finisherRoutine;
        private Coroutine _respawnRoutine;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private RigidbodyConstraints _spawnConstraints;
        private int _spawnLock;

        public int ShotCount => _shotCount;
        public bool FinisherQueued => _finisherQueued;
        public bool IsDead => _dead || (_ai != null && _ai.IsDead);

        private void Awake()
        {
            _colors = GetComponent<RGBColorTarget>();
            _ai = GetComponent<IEnemyBrain>();
            _skill = GetComponent<SpectrumSkillResponder>();
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _body = GetComponent<Rigidbody>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _spawnConstraints = _body != null ? _body.constraints : RigidbodyConstraints.FreezeRotation;
        }

        private void OnEnable()
        {
            if (_colors != null) _colors.OnColorStateChanged += OnColorChanged;
        }

        private void Start()
        {
            if (_colors == null || _dead) return;
            ApplyCurrentState(_colors.CurrentColorState);
        }

        private void OnDisable()
        {
            if (_colors != null) _colors.OnColorStateChanged -= OnColorChanged;
            if (_finisherRoutine != null)
            {
                StopCoroutine(_finisherRoutine);
                _finisherRoutine = null;
            }
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }
            if (!_dead) RestoreMotion();
        }

        private void OnColorChanged(ObjectColorState _)
        {
            _stateCheckPending = true;
        }

        private void LateUpdate()
        {
            if (!_stateCheckPending || _dead) return;
            _stateCheckPending = false;
            if (_colors == null) return;
            ApplyCurrentState(_colors.CurrentColorState);
        }

        private void FixedUpdate()
        {
            if (_spawnLock > 0)
            {
                _spawnLock--;
                PlaceAtSpawn();
                if (_spawnLock == 0 && _ai != null) _ai.Revive();
                return;
            }
            if (_dead || !_releasedForPull) return;

            if (IsBeingPulled())
            {
                _settleTimer = 0f;
                KeepPhysicsFree();
                return;
            }

            bool slow = _body == null || _body.linearVelocity.sqrMagnitude <= SettleSpeedSqr;
            bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, 0.7f, ~0, QueryTriggerInteraction.Ignore);
            if (slow || grounded) _settleTimer += Time.fixedDeltaTime;
            else _settleTimer = 0f;

            if (_settleTimer >= SettleSeconds) SeatAfterPull();
        }

        private void ApplyCurrentState(ObjectColorState state)
        {
            if (state == ObjectColorState.None && !_finisherQueued) _shotCount = 0;

            int colorCount = CountPrimaries(state);
            bool blue = (state & ObjectColorState.Blue) != 0;
            bool suspend = blue || (state & ObjectColorState.Green) != 0 || colorCount > 1;
            bool disableAgent = colorCount > 1;
            if (suspend)
            {
                if (!_paused) SaveMotion();
                if (blue && _animator != null) _animator.speed = 0f;
                if (!_releasedForPull)
                {
                    if (disableAgent && _agent != null && _agent.enabled) _agent.enabled = false;
                    else if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
                }
                if (_ai != null && !_ai.IsDead) _ai.PauseForColor();
            }
            else if (!_releasedForPull)
            {
                RestoreMotion();
                if (_ai != null && !_ai.IsDead)
                {
                    _ai.ResumeAfterColor();
                    if ((state & ObjectColorState.Red) != 0) _ai.InterruptForRed();
                }
            }
        }

        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            if (_dead || (_ai != null && _ai.IsDead)) return;

            _shotCount++;
            bool incomingRed = firedAmmo == AmmoType.Red || (mappedColorFlag & ObjectColorState.Red) != 0;
            if (incomingRed && _ai != null) _ai.InterruptForRed();

            if (_colors == null) _colors = GetComponent<RGBColorTarget>();
            ObjectColorState before = _colors != null ? _colors.CurrentColorState : ObjectColorState.None;
            FinisherMixGate gate = GetComponent<FinisherMixGate>();
            bool mixAllowed = gate == null || gate.Allows(before);
            if (_shotCount == 3 && incomingRed && CountPrimaries(before) >= 2 && mixAllowed && !_finisherQueued)
                QueueFinisher();
        }

        public void ReleaseForPull()
        {
            if (_dead) return;
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_ai == null) _ai = GetComponent<IEnemyBrain>();

            _releasedForPull = true;
            _settleTimer = 0f;
            if (_agent != null && _agent.enabled)
            {
                _agentWasEnabled = true;
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                _agent.enabled = false;
            }
            KeepPhysicsFree();
            if (_ai != null && !_ai.IsDead) _ai.PauseForColor();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_finisherQueued || _dead || collision == null || collision.contactCount == 0) return;
            if (collision.collider != null && collision.collider.GetComponentInParent<CharacterController>() != null) return;
            Vector3 normal = collision.GetContact(0).normal;
            if (Mathf.Abs(normal.y) > 0.45f) return;
            KillNow();
        }

        private void QueueFinisher()
        {
            _finisherQueued = true;
            if (_finisherRoutine != null) StopCoroutine(_finisherRoutine);
            _finisherRoutine = StartCoroutine(FinishAfterKnockback());
        }

        private IEnumerator FinishAfterKnockback()
        {
            yield return new WaitForSeconds(FinisherDelay);
            _finisherRoutine = null;
            if (!_dead) KillNow();
        }

        private void KillNow()
        {
            if (_dead) return;
            _dead = true;
            _finisherQueued = false;
            _releasedForPull = false;
            if (_finisherRoutine != null)
            {
                StopCoroutine(_finisherRoutine);
                _finisherRoutine = null;
            }
            if (_agent != null) _agent.enabled = false;
            if (_ai != null) _ai.Die();
            else if (_body != null)
            {
                _body.isKinematic = false;
                _body.constraints = RigidbodyConstraints.None;
                _body.WakeUp();
            }
            if (_respawnRoutine != null) StopCoroutine(_respawnRoutine);
            _respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(RespawnDelay);
            _respawnRoutine = null;
            Respawn();
        }

        private void Respawn()
        {
            _dead = false;
            _finisherQueued = false;
            _shotCount = 0;
            _paused = false;
            _releasedForPull = false;
            _settleTimer = 0f;
            if (_skill != null)
            {
                if (_skill.IsGreenBound) _skill.DeactivateGreenBind();
                if (_skill.IsBlueStasis) _skill.DeactivateCyanStasis(false);
            }
            if (_colors != null) _colors.ResetColors();
            if (_agent != null) _agent.enabled = false;
            if (_ai != null && !_ai.IsDead) _ai.PauseForColor();
            _spawnLock = 3;
            PlaceAtSpawn();
        }

        private void PlaceAtSpawn()
        {
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                _body.position = _spawnPosition;
                _body.rotation = _spawnRotation;
                _body.constraints = _spawnConstraints;
                _body.isKinematic = false;
                _body.WakeUp();
            }
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        }

        private bool IsBeingPulled()
        {
            if (_skill == null) _skill = GetComponent<SpectrumSkillResponder>();
            GreenEnergyTether tether = _skill != null ? _skill.ActiveTether : null;
            return tether != null && tether.IsPulling && tether.PullAnchor != _skill;
        }

        private void KeepPhysicsFree()
        {
            if (_body == null) return;
            if (_body.isKinematic) _body.isKinematic = false;
            _body.WakeUp();
            if (_agent != null && _agent.enabled) _agent.enabled = false;
        }

        private void SeatAfterPull()
        {
            _releasedForPull = false;
            _settleTimer = 0f;
            if (_dead || _agent == null || !_agentWasEnabled) return;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                _agentWasEnabled = false;
                return;
            }
            _agent.enabled = true;
            if (!_agent.isOnNavMesh) return;
            _agent.Warp(hit.position);
            _agent.isStopped = true;
        }

        private void SaveMotion()
        {
            _paused = true;
            if (_agent != null)
            {
                _agentWasEnabled = _agent.enabled;
                _agentWasStopped = _agent.enabled && _agent.isOnNavMesh && _agent.isStopped;
                if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            }
            if (_animator != null) _animatorSpeed = _animator.speed;
        }

        private void RestoreMotion()
        {
            if (!_paused || _dead || _releasedForPull)
            {
                if (_releasedForPull && _animator != null) _animator.speed = _animatorSpeed;
                if (!_releasedForPull) _paused = false;
                return;
            }
            if (_animator != null) _animator.speed = _animatorSpeed;
            if (_agent != null && _agentWasEnabled && !_agent.enabled)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    _agent.enabled = true;
                    if (_agent.isOnNavMesh)
                    {
                        _agent.Warp(hit.position);
                        _agent.isStopped = _agentWasStopped;
                    }
                }
            }
            else if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = _agentWasStopped;
            }
            _paused = false;
        }

        private static int CountPrimaries(ObjectColorState state)
        {
            return ((state & ObjectColorState.Red) != 0 ? 1 : 0)
                 + ((state & ObjectColorState.Green) != 0 ? 1 : 0)
                 + ((state & ObjectColorState.Blue) != 0 ? 1 : 0);
        }
    }
}
