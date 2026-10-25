using System;
using System.Collections;
using UnityEngine;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Controls the physical rotation angle and animation of the inner cylinder.
    /// Manages Q/E and Mouse Scroll manual rotation and automatic advancement after firing.
    /// Synchronizes rotation angle with the active chamber index at the fixed 12 o'clock firing position.
    /// </summary>
    public class CylinderRotationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CylinderAmmoController ammoController;
        [SerializeField] private RectTransform rotatingInnerCylinder;

        [Header("Animation Settings")]
        [Tooltip("Duration in seconds for a 45-degree chamber rotation.")]
        [SerializeField] private float rotationDuration = 0.16f;

        [Tooltip("Curve for mechanical snap easing.")]
        [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public const float ChamberAngleStep = 360f / CylinderAmmoController.CylinderCapacity;

        private float _currentAngle = 0f;
        private float _targetAngle = 0f;
        private Coroutine _rotationCoroutine;
        private bool _isRotating = false;
        private float _reloadVisualAngle;

        public bool IsRotating => _isRotating;
        public float CurrentAngle => _currentAngle;

        public event Action<int> OnRotationStarted;
        public event Action<int> OnRotationCompleted;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void Start()
        {
            ResolveDependencies();

            // Sync initial visual orientation (Chamber activeIndex aligned at 12 o'clock)
            int activeIndex = ammoController != null ? ammoController.ActiveChamberIndex : 0;
            _currentAngle = activeIndex * ChamberAngleStep;
            _targetAngle = _currentAngle;
            ApplyVisualAngle(_currentAngle);
        }

        public void ResolveDependencies()
        {
            if (ammoController == null)
            {
                ammoController = GetComponent<CylinderAmmoController>();
                if (ammoController == null)
                {
                    ammoController = FindFirstObjectByType<CylinderAmmoController>();
                }
            }

            if (rotatingInnerCylinder == null)
            {
                // Auto-resolve the live Rotating_InnerCylinder under this HUD hierarchy
                Transform[] children = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] != null && children[i].name == "Rotating_InnerCylinder" && children[i].gameObject.activeInHierarchy)
                    {
                        rotatingInnerCylinder = children[i].GetComponent<RectTransform>();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// <summary>
        /// Rotate cylinder to the left (Q key): brings the left chamber (10 o'clock, -1 index) into 12 o'clock.
        /// </summary>
        public void RotateLeft()
        {
            RotateChamberStep(-1);
        }

        /// <summary>
        /// <summary>
        /// Rotate cylinder to the right (E key): brings the right chamber (2 o'clock, +1 index) into 12 o'clock.
        /// </summary>
        public void RotateRight()
        {
            RotateChamberStep(1);
        }

        /// <summary>
        /// Automatically advance cylinder in real revolver direction (+1 index, counter-clockwise from rear view) following a successful shot.
        /// </summary>
        public void AutoAdvance()
        {
            RotateChamberStep(1); // Advances next clockwise-indexed chamber (at 2 o'clock) into 12 o'clock
        }

        public void RotateChamberStep(int indexDelta)
        {
            ResolveDependencies();
            if (ammoController == null) return;

            int currentActive = ammoController.ActiveChamberIndex;
            int nextActive = (currentActive + indexDelta + CylinderAmmoController.CylinderCapacity) % CylinderAmmoController.CylinderCapacity;

            // Update logical active chamber immediately so rapid inputs or tests never desync
            ammoController.SetActiveChamber(nextActive);

            // Target visual angle: each index change shifts the cylinder by 45 degrees
            _targetAngle += (indexDelta * ChamberAngleStep);

            if (!Application.isPlaying || !isActiveAndEnabled || rotationDuration <= 0.001f)
            {
                _currentAngle = _targetAngle;
                NormalizeAngles();
                ApplyVisualAngle(_currentAngle);
                OnRotationStarted?.Invoke(nextActive);
                OnRotationCompleted?.Invoke(nextActive);
                return;
            }

            if (_rotationCoroutine != null)
            {
                StopCoroutine(_rotationCoroutine);
            }

            _rotationCoroutine = StartCoroutine(AnimateRotationRoutine(nextActive));
        }

        private IEnumerator AnimateRotationRoutine(int targetIndex)
        {
            _isRotating = true;
            OnRotationStarted?.Invoke(targetIndex);

            float startAngle = _currentAngle;
            float elapsed = 0f;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);
                float curveT = rotationCurve != null ? rotationCurve.Evaluate(t) : t;

                _currentAngle = Mathf.LerpUnclamped(startAngle, _targetAngle, curveT);
                ApplyVisualAngle(_currentAngle);
                yield return null;
            }

            _currentAngle = _targetAngle;
            NormalizeAngles();
            ApplyVisualAngle(_currentAngle);

            _isRotating = false;
            _rotationCoroutine = null;
            OnRotationCompleted?.Invoke(targetIndex);
        }

        private void NormalizeAngles()
        {
            if (_currentAngle >= 360f || _currentAngle <= -360f)
            {
                _currentAngle %= 360f;
                _targetAngle = _currentAngle;
            }
        }

        private void OnDisable()
        {
            if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
            _rotationCoroutine = null;
            _isRotating = false;
            _currentAngle = _targetAngle;
            NormalizeAngles();
            if (rotatingInnerCylinder != null) ApplyVisualAngle(_currentAngle);
        }

        private void ApplyVisualAngle(float zAngle)
        {
            if (rotatingInnerCylinder == null)
            {
                ResolveDependencies();
            }

            if (rotatingInnerCylinder != null)
            {
                rotatingInnerCylinder.localRotation = Quaternion.Euler(0f, 0f, zAngle + _reloadVisualAngle);
            }
        }

        public void SetReloadVisualAngle(float angle)
        {
            _reloadVisualAngle = angle;
            ApplyVisualAngle(_currentAngle);
        }

        public void SetRotatingInnerCylinder(RectTransform rect)
        {
            rotatingInnerCylinder = rect;
            ApplyVisualAngle(_currentAngle);
        }

        public void SyncToActiveChamber()
        {
            if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
            _rotationCoroutine = null;
            _isRotating = false;
            _currentAngle = _targetAngle = ammoController != null ? ammoController.ActiveChamberIndex * ChamberAngleStep : 0f;
            _reloadVisualAngle = 0f;
            ApplyVisualAngle(_currentAngle);
        }
    }
}
