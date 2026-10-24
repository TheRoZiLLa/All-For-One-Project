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
        [Header("Stair camera")]
        [Tooltip("Seconds to soften stair movement. Zero disables smoothing.")]
        [SerializeField, Min(0f)] private float stepSmoothing = 0.08f;
        [Header("Gentle camera motion")]
        [SerializeField] private bool enableBob;
        [SerializeField, Range(0f, 0.02f)] private float bobAmount = 0.0035f;
        [SerializeField, Range(0.1f, 4f)] private float bobFrequency = 1.6f;
        [SerializeField] private bool enableTilt;
        [SerializeField, Range(0f, 5f)] private float strafeTilt = 0.7f;
        [SerializeField, Min(0.01f)] private float motionSmoothTime = 0.10f;
        private FirstPersonMotor motor;
        private Camera viewCamera;
        private Vector3 viewHome;
        private Quaternion viewRotation;
        private Vector3 bobOffset, bobVelocity;
        private float bobPhase, roll, rollVelocity;
        private float previousRootY, stairOffset;
        private bool wasGrounded;
        public float Pitch { get; private set; }
        public float GaitPhase => bobPhase;

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
            if (viewCamera != null)
            {
                viewHome = viewCamera.transform.localPosition;
                viewRotation = viewCamera.transform.localRotation;
            }
            previousRootY = transform.position.y;
        }

        private void OnEnable() => SetCursor(true);
        private void OnDisable()
        {
            if (viewCamera != null)
            {
                viewCamera.transform.localPosition = viewHome;
                viewCamera.transform.localRotation = viewRotation;
            }
            bobOffset = bobVelocity = Vector3.zero;
            bobPhase = roll = rollVelocity = 0f;
            stairOffset = 0f;
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

        public void SnapPitch(float pitch)
        {
            Pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
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

            Vector3 controlled = transform.InverseTransformDirection(motor.ControlledVelocity);
            float speed = Mathf.Min(new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude,
                new Vector2(controlled.x, controlled.z).magnitude);
            bool walking = motor.IsGrounded && speed > 0.05f;
            float weight = walking ? Mathf.Clamp01(speed / Mathf.Max(0.1f, settings.WalkSpeed)) : 0f;
            Vector3 targetBob = Vector3.zero;
            if (walking)
            {
                // Keep cadence comfortable at FPS movement speeds; do not count metres as footsteps.
                float cadence = bobFrequency * Mathf.Clamp(speed / Mathf.Max(0.1f, settings.WalkSpeed), 0.65f, 1.35f);
                bobPhase = Mathf.Repeat(bobPhase + cadence * Mathf.PI * 2f * deltaTime, Mathf.PI * 4f);
                if (enableBob)
                    targetBob = new Vector3(Mathf.Sin(bobPhase * 0.5f) * 0.4f, Mathf.Sin(bobPhase), 0f)
                        * (bobAmount * weight * (motor.IsCrouching ? 0.5f : 1f));
            }
            float smoothTime = Mathf.Max(0.01f, motionSmoothTime);
            bobOffset = Vector3.SmoothDamp(bobOffset, targetBob, ref bobVelocity, smoothTime, Mathf.Infinity, deltaTime);
            float targetRoll = enableTilt && walking
                ? -Mathf.Clamp(controlled.x / Mathf.Max(0.1f, settings.WalkSpeed), -1f, 1f) * strafeTilt : 0f;
            roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, smoothTime, Mathf.Infinity, deltaTime);

            // Keep the eye and its near plane below the capsule top, including on descending stairs.
            float headroom = Mathf.Max(0f, settings.EyeInset - viewCamera.nearClipPlane - settings.SkinWidth - 0.02f);
            stairOffset = Mathf.Clamp(stairOffset, -settings.StepHeight, headroom);
            Vector3 offset = bobOffset;
            offset.y = Mathf.Clamp(offset.y + stairOffset, -settings.StepHeight, headroom);
            viewCamera.transform.localPosition = viewHome + offset;
            viewCamera.transform.localRotation = viewRotation * Quaternion.Euler(0f, 0f, roll);
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
