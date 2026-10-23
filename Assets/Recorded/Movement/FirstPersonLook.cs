using UnityEngine;

namespace Recorded
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private PlayerInputReader input;
        [SerializeField, Min(0f)] private float sensitivity = 0.1f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;
        [Header("Walking camera")]
        [SerializeField] private bool walkingMotion;
        [SerializeField, Min(0f)] private float bobAmount = 0.012f;
        [SerializeField, Min(0.1f)] private float strideLength = 0.75f;
        [Tooltip("Seconds to soften stair movement. Zero disables smoothing.")]
        [SerializeField, Min(0f)] private float stepSmoothing = 0.08f;
        private FirstPersonMotor motor;
        private Camera viewCamera;
        private Vector3 viewHome;
        private float previousRootY, stairOffset, gaitPhase, gaitWeight;
        private bool wasGrounded;
        public float Pitch { get; private set; }

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (cameraPivot == null)
            {
                Debug.LogError("Assign CameraPivot to FirstPersonLook.", this);
                enabled = false;
                return;
            }
            Pitch = Mathf.DeltaAngle(0f, cameraPivot.localEulerAngles.x);
            motor = GetComponent<FirstPersonMotor>();
            viewCamera = cameraPivot.GetComponentInChildren<Camera>();
            if (viewCamera != null) viewHome = viewCamera.transform.localPosition;
            previousRootY = transform.position.y;
        }

        private void OnEnable() => SetCursor(true);
        private void OnDisable()
        {
            if (viewCamera != null) viewCamera.transform.localPosition = viewHome;
            stairOffset = gaitWeight = 0f;
            wasGrounded = false;
            SetCursor(false);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCursor(false);
        }

        private void Update()
        {
            if (input == null) return;
            if (input.EscapePressed) SetCursor(false);
            else if (input.ResumePressed && Application.isFocused) SetCursor(true);
            else if (Cursor.lockState == CursorLockMode.Locked) ApplyLook(input.LookDelta);
        }

        public void ApplyLook(Vector2 delta)
        {
            if (cameraPivot == null) return;
            transform.Rotate(0f, delta.x * sensitivity, 0f, Space.Self);
            Pitch = Mathf.Clamp(Pitch - delta.y * sensitivity, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
        }

        private void LateUpdate() => UpdateCameraMotion(Time.deltaTime);

        private void UpdateCameraMotion(float deltaTime)
        {
            if (viewCamera == null || motor == null || deltaTime <= 0f) return;
            var settings = motor.Settings;
            float rise = transform.position.y - previousRootY;
            previousRootY = transform.position.y;
            if (motor.IsGrounded && wasGrounded && Mathf.Abs(rise) <= settings.StepHeight + settings.SkinWidth * 2f)
                stairOffset -= rise;
            wasGrounded = motor.IsGrounded;
            stairOffset = stepSmoothing > 0f ? stairOffset * Mathf.Exp(-deltaTime / stepSmoothing) : 0f;

            float horizontalSpeed = new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude;
            float gaitSpeed = Mathf.Min(horizontalSpeed, motor.ControlledSpeed);
            bool walking = walkingMotion && motor.IsGrounded && gaitSpeed > 0.05f;
            float weight = walking ? Mathf.Min(1f, gaitSpeed / Mathf.Max(0.1f, settings.WalkSpeed))
                * (motor.IsCrouching ? 0.5f : 1f) : 0f;
            gaitWeight = Mathf.MoveTowards(gaitWeight, weight, deltaTime * 8f);
            if (walking)
                gaitPhase = Mathf.Repeat(gaitPhase + gaitSpeed / Mathf.Max(0.1f, strideLength)
                    * Mathf.PI * deltaTime, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Sin(gaitPhase) * 0.5f, Mathf.Cos(gaitPhase * 2f), 0f)
                * bobAmount * gaitWeight;
            // Keep the eye and its near plane below the capsule top, including on descending stairs.
            float headroom = Mathf.Max(0f, settings.EyeInset - viewCamera.nearClipPlane - settings.SkinWidth - 0.02f);
            stairOffset = Mathf.Clamp(stairOffset, -settings.StepHeight, headroom);
            offset.y = Mathf.Clamp(offset.y + stairOffset, -settings.StepHeight, headroom);
            viewCamera.transform.localPosition = viewHome + offset;
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
