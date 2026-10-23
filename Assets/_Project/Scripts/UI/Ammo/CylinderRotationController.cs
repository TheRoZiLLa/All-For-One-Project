using System;
using System.Collections;
using UnityEngine;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Controls the physical rotation angle and animation of the inner cylinder.
    /// Manages Q/E and Mouse Scroll manual rotation and automatic advancement after firing.
    /// Synchronizes rotation angle with the active chamber index at the fixed 6 o'clock firing position.
    /// </summary>
    public class CylinderRotationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CylinderAmmoController ammoController;
        [SerializeField] private RectTransform rotatingInnerCylinder;

        [Header("Animation Settings")]
        [Tooltip("Duration in seconds for a 60-degree chamber rotation.")]
        [SerializeField] private float rotationDuration = 0.12f;

        [Tooltip("Curve for mechanical snap easing.")]
        [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public const float ChamberAngleStep = 60f; // 360 / 6 = 60 degrees

        private float _currentAngle = 0f;
        private float _targetAngle = 0f;
        private Coroutine _rotationCoroutine;
        private bool _isRotating = false;

        public bool IsRotating => _isRotating;
        public float CurrentAngle => _currentAngle;

        public event Action<int> OnRotationStarted;
        public event Action<int> OnRotationCompleted;

        private void Awake()
        {
            if (ammoController == null)
            {
                ammoController = GetComponent<CylinderAmmoController>();
            }
        }

        private void Start()
        {
            // Sync initial visual orientation (Chamber activeIndex aligned at 6 o'clock)
            int activeIndex = ammoController != null ? ammoController.ActiveChamberIndex : 0;
            _currentAngle = activeIndex * ChamberAngleStep;
            _targetAngle = _currentAngle;
            ApplyVisualAngle(_currentAngle);
        }

        /// <summary>
        /// Rotate cylinder so the bottom (6 o'clock) chamber moves to the left according to player's rear view (Q key / Scroll Up).
        /// Brings the right-hand chamber (4 o'clock, +1 index) into the 6 o'clock firing indicator.
        /// </summary>
        public void RotateLeft()
        {
            if (_isRotating) return;
            RotateChamberStep(1);
        }

        /// <summary>
        /// Rotate cylinder so the bottom (6 o'clock) chamber moves to the right according to player's rear view (E key / Scroll Down).
        /// Brings the left-hand chamber (8 o'clock, -1 index) into the 6 o'clock firing indicator.
        /// </summary>
        public void RotateRight()
        {
            if (_isRotating) return;
            RotateChamberStep(-1);
        }

        /// <summary>
        /// Automatically advance cylinder to the next chamber following a successful shot.
        /// </summary>
        public void AutoAdvance()
        {
            if (_isRotating) return;
            RotateChamberStep(1); // Advances to next sequential chamber
        }

        public void RotateChamberStep(int indexDelta)
        {
            if (ammoController == null) return;

            int currentActive = ammoController.ActiveChamberIndex;
            int nextActive = (currentActive + indexDelta + CylinderAmmoController.CylinderCapacity) % CylinderAmmoController.CylinderCapacity;

            // Target visual angle: each index change shifts the cylinder by 60 degrees
            _targetAngle = _currentAngle + (indexDelta * ChamberAngleStep);

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
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);
                float curveT = rotationCurve != null ? rotationCurve.Evaluate(t) : t;

                _currentAngle = Mathf.Lerp(startAngle, _targetAngle, curveT);
                ApplyVisualAngle(_currentAngle);
                yield return null;
            }

            _currentAngle = _targetAngle;
            ApplyVisualAngle(_currentAngle);

            // Keep continuous angle within safe numerical range without jumps
            if (_currentAngle >= 360f || _currentAngle <= -360f)
            {
                _currentAngle %= 360f;
                _targetAngle = _currentAngle;
            }

            // Synchronize final active index with ammo controller
            ammoController.SetActiveChamber(targetIndex);

            _isRotating = false;
            _rotationCoroutine = null;
            OnRotationCompleted?.Invoke(targetIndex);
        }

        private void ApplyVisualAngle(float zAngle)
        {
            if (rotatingInnerCylinder != null)
            {
                rotatingInnerCylinder.localRotation = Quaternion.Euler(0f, 0f, zAngle);
            }
        }

        public void SetRotatingInnerCylinder(RectTransform rect)
        {
            rotatingInnerCylinder = rect;
            ApplyVisualAngle(_currentAngle);
        }
    }
}