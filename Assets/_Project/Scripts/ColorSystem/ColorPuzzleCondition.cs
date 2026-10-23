using System;
using UnityEngine;
using UnityEngine.Events;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Reusable Puzzle Integration component for RECORDED.
    /// Monitors one or more RGBColorTarget objects and triggers UnityEvents / C# events
    /// when all watched targets match the required ObjectColorState.
    /// Enables puzzle creators to hook doors, bridges, platforms, or mechanisms without modifying weapon code.
    /// </summary>
    public class ColorPuzzleCondition : MonoBehaviour
    {
        [Header("Puzzle Condition Settings")]
        [Tooltip("Targets that must match the required state. If empty, checks this GameObject.")]
        [SerializeField] private RGBColorTarget[] watchedTargets;

        [Tooltip("The color combination required on each watched target.")]
        [SerializeField] private ObjectColorState requiredState = (ObjectColorState.Red | ObjectColorState.Green);

        [Tooltip("If true, target state must equal requiredState exactly. If false, target only needs to include the required flags.")]
        [SerializeField] private bool requireExactMatch = true;

        [Header("Puzzle Events")]
        [SerializeField] private UnityEvent onConditionMet;
        [SerializeField] private UnityEvent onConditionLost;

        private bool _isConditionMet;

        public bool IsConditionMet => _isConditionMet;
        public ObjectColorState RequiredState
        {
            get => requiredState;
            set
            {
                requiredState = value;
                EvaluateCondition();
            }
        }

        public event Action<bool> OnConditionStateChanged;

        private void Awake()
        {
            if (watchedTargets == null || watchedTargets.Length == 0)
            {
                var selfTarget = GetComponent<RGBColorTarget>();
                if (selfTarget != null)
                {
                    watchedTargets = new RGBColorTarget[] { selfTarget };
                }
            }
        }

        private void OnEnable()
        {
            if (watchedTargets != null)
            {
                for (int i = 0; i < watchedTargets.Length; i++)
                {
                    if (watchedTargets[i] != null)
                    {
                        watchedTargets[i].OnColorStateChanged += HandleTargetColorChanged;
                    }
                }
            }

            EvaluateCondition();
        }

        private void OnDisable()
        {
            if (watchedTargets != null)
            {
                for (int i = 0; i < watchedTargets.Length; i++)
                {
                    if (watchedTargets[i] != null)
                    {
                        watchedTargets[i].OnColorStateChanged -= HandleTargetColorChanged;
                    }
                }
            }
        }

        public void SetWatchedTargets(RGBColorTarget[] targets, ObjectColorState required, bool exactMatch = true)
        {
            if (watchedTargets != null)
            {
                for (int i = 0; i < watchedTargets.Length; i++)
                {
                    if (watchedTargets[i] != null)
                        watchedTargets[i].OnColorStateChanged -= HandleTargetColorChanged;
                }
            }

            watchedTargets = targets;
            requiredState = required;
            requireExactMatch = exactMatch;

            if (isActiveAndEnabled && watchedTargets != null)
            {
                for (int i = 0; i < watchedTargets.Length; i++)
                {
                    if (watchedTargets[i] != null)
                        watchedTargets[i].OnColorStateChanged += HandleTargetColorChanged;
                }
            }

            EvaluateCondition();
        }

        private void HandleTargetColorChanged(ObjectColorState _)
        {
            EvaluateCondition();
        }

        public bool EvaluateCondition()
        {
            bool allMatch = true;

            if (watchedTargets == null || watchedTargets.Length == 0)
            {
                allMatch = false;
            }
            else
            {
                for (int i = 0; i < watchedTargets.Length; i++)
                {
                    RGBColorTarget target = watchedTargets[i];
                    if (target == null)
                    {
                        allMatch = false;
                        break;
                    }

                    ObjectColorState state = target.CurrentColorState;
                    bool matches = requireExactMatch
                        ? (state == requiredState)
                        : ((state & requiredState) == requiredState);

                    if (!matches)
                    {
                        allMatch = false;
                        break;
                    }
                }
            }

            if (allMatch != _isConditionMet)
            {
                _isConditionMet = allMatch;
                OnConditionStateChanged?.Invoke(_isConditionMet);

                if (_isConditionMet)
                {
                    onConditionMet?.Invoke();
                }
                else
                {
                    onConditionLost?.Invoke();
                }
            }

            return _isConditionMet;
        }
    }
}