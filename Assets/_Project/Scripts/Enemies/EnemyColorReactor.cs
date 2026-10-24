using Recorded.ColorSystem;
using Recorded.Weapons.Ammo;
using UnityEngine;
using UnityEngine.AI;

namespace Recorded.Enemies
{
    /// <summary>Bridges additive RGB states to enemy AI, animation and optional NavMesh safely.</summary>
    [RequireComponent(typeof(RGBColorTarget))]
    public sealed class EnemyColorReactor : MonoBehaviour, IColorShotImpact
    {
        private RGBColorTarget _colors;
        private SurveyorEnemyAI _ai;
        private NavMeshAgent _agent;
        private Animator _animator;
        private bool _agentWasEnabled;
        private bool _agentWasStopped;
        private float _animatorSpeed = 1f;
        private bool _paused;
        private bool _stateCheckPending;

        private void Awake()
        {
            _colors = GetComponent<RGBColorTarget>();
            _ai = GetComponent<SurveyorEnemyAI>();
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (_colors != null) _colors.OnColorStateChanged += OnColorChanged;
        }

        private void OnDisable()
        {
            if (_colors != null) _colors.OnColorStateChanged -= OnColorChanged;
            RestoreMotion();
        }

        private void OnColorChanged(ObjectColorState _)
        {
            _stateCheckPending = true;
        }

        private void LateUpdate()
        {
            if (!_stateCheckPending) return;
            _stateCheckPending = false;
            ApplyCurrentState(_colors.CurrentColorState);
        }

        private void ApplyCurrentState(ObjectColorState state)
        {
            int colorCount = ((state & ObjectColorState.Red) != 0 ? 1 : 0)
                           + ((state & ObjectColorState.Green) != 0 ? 1 : 0)
                           + ((state & ObjectColorState.Blue) != 0 ? 1 : 0);
            bool blue = (state & ObjectColorState.Blue) != 0;
            bool suspend = blue || (state & ObjectColorState.Green) != 0 || colorCount > 1;
            bool disableAgent = colorCount > 1;
            if (suspend)
            {
                if (!_paused) SaveMotion();
                if (blue && _animator != null) _animator.speed = 0f;
                if (disableAgent && _agent != null && _agent.enabled) _agent.enabled = false;
                else if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
                if (_ai != null) _ai.PauseForColor();
            }
            else
            {
                RestoreMotion();
                if (_ai != null)
                {
                    _ai.ResumeAfterColor();
                    if ((state & ObjectColorState.Red) != 0) _ai.InterruptForRed();
                }
            }
        }

        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            if (firedAmmo == AmmoType.Red || (mappedColorFlag & ObjectColorState.Red) != 0)
                if (_ai != null) _ai.InterruptForRed();
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
            if (!_paused) return;
            if (_animator != null) _animator.speed = _animatorSpeed;
            if (_agent != null && _agentWasEnabled && !_agent.enabled)
            {
                _agent.enabled = true;
                if (_agent.isOnNavMesh) _agent.isStopped = _agentWasStopped;
            }
            else if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = _agentWasStopped;
            }
            _paused = false;
        }
    }
}
