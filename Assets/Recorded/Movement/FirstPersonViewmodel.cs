using UnityEngine;

namespace Recorded
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonViewmodel : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Camera viewmodelCamera;
        [SerializeField, Min(0.1f)] private float wallDistance = 0.8f;
        [SerializeField, Min(0f)] private float clearance = 0.12f;
        [SerializeField, Min(0f)] private float retractDistance = 0.08f;
        [SerializeField, Range(0f, 30f)] private float lowerAngle = 10f;
        [SerializeField, Min(0.01f)] private float responseTime = 0.08f;
        [SerializeField] private LayerMask wallMask = Physics.DefaultRaycastLayers;
        [Header("Walking and look weight")]
        [SerializeField, Range(0f, 0.03f)] private float walkAmount = 0.012f;
        [SerializeField, Range(0f, 4f)] private float lookSway = 1.5f;
        [SerializeField, Min(0.01f)] private float motionSmoothTime = 0.10f;
        [Header("Idle and airborne weight")]
        [SerializeField, Range(0f, 0.01f)] private float idleAmount = 0.0025f;
        [SerializeField, Range(0f, 0.04f)] private float jumpAmount = 0.022f;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Transform ignoredRoot;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float proximity, proximityVelocity;
        private bool initialized;
        private FirstPersonMotor motor;
        private FirstPersonLook look;
        private Animator armsAnimator;
        private Quaternion previousCameraRotation;
        private Vector3 motionPosition, positionVelocity, motionAngles, angleVelocity;
        private float idlePhase, landingAge, landingStrength, previousVerticalSpeed;
        private bool hadGroundState, wasGrounded;

        public float WallProximity => proximity;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = GetComponentInParent<Camera>();
            if (worldCamera == null || viewmodelCamera == null)
            {
                Debug.LogError("Assign world and viewmodel cameras to FirstPersonViewmodel.", this);
                enabled = false;
                return;
            }
            var body = GetComponentInParent<CharacterController>();
            ignoredRoot = body != null ? body.transform : worldCamera.transform;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            motor = GetComponentInParent<FirstPersonMotor>();
            look = GetComponentInParent<FirstPersonLook>();
            armsAnimator = GetComponentInChildren<Animator>();
            previousCameraRotation = worldCamera.transform.rotation;
            initialized = true;
        }

        private void LateUpdate() => UpdateViewmodel(Time.deltaTime);

        private void UpdateViewmodel(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f) return;
            if (!Mathf.Approximately(viewmodelCamera.fieldOfView, worldCamera.fieldOfView))
                viewmodelCamera.fieldOfView = worldCamera.fieldOfView;
            float distance = Mathf.Max(0.1f, wallDistance);
            int count = Physics.SphereCastNonAlloc(worldCamera.transform.position, 0.06f,
                worldCamera.transform.forward, hits, distance, wallMask, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(ignoredRoot)) nearest = Mathf.Min(nearest, hits[i].distance);
            float target = Mathf.InverseLerp(distance, Mathf.Min(clearance, distance - 0.01f), nearest);
            proximity = Mathf.SmoothDamp(proximity, target, ref proximityVelocity,
                Mathf.Max(0.01f, responseTime), Mathf.Infinity, deltaTime);
            UpdateMotion(deltaTime);
            float motionWeight = 1f - proximity * 0.8f;
            transform.localPosition = homePosition + Vector3.back * (retractDistance * proximity) + motionPosition * motionWeight;
            transform.localRotation = homeRotation * Quaternion.Euler(new Vector3(lowerAngle * proximity, 0f, 0f) + motionAngles * motionWeight);
        }

        private void UpdateMotion(float deltaTime)
        {
            var cameraRotation = worldCamera.transform.rotation;
            var turn = (Quaternion.Inverse(previousCameraRotation) * cameraRotation).eulerAngles;
            previousCameraRotation = cameraRotation;
            Vector3 targetPosition = Vector3.zero, targetAngles = Vector3.zero;
            if (motor != null && look != null)
            {
                Vector3 controlled = motor.transform.InverseTransformDirection(motor.ControlledVelocity);
                float speed = Mathf.Min(new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude,
                    new Vector2(controlled.x, controlled.z).magnitude);
                float weight = motor.IsGrounded ? Mathf.Clamp(speed / Mathf.Max(0.1f, motor.Settings.WalkSpeed), 0f, 1.3f) : 0f;
                if (motor.IsCrouching) weight *= 0.5f;
                float phase = look.GaitPhase, side = Mathf.Sin(phase * 0.5f), step = Mathf.Sin(phase);
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(0f, turn.y) / (180f * deltaTime), -1f, 1f);
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, turn.x) / (180f * deltaTime), -1f, 1f);
                Vector3 direction = Vector3.ClampMagnitude(controlled / Mathf.Max(0.1f, motor.Settings.WalkSpeed), 1f);
                targetPosition = new Vector3(side, step * 0.5f, Mathf.Cos(phase) * 0.25f) * (walkAmount * weight)
                    + new Vector3(-direction.x * 0.006f * weight - yaw * 0.003f, pitch * 0.002f, -direction.z * 0.008f * weight);
                targetAngles = new Vector3(Mathf.Cos(phase) * 0.4f * weight + pitch * lookSway,
                    -yaw * lookSway, -side * 0.8f * weight - direction.x * 0.85f * weight);

                idlePhase = Mathf.Repeat(idlePhase + deltaTime * (0.22f * Mathf.PI * 2f), Mathf.PI * 2f);
                float idleWeight = motor.IsGrounded ? 1f - Mathf.Clamp01(speed / Mathf.Max(0.1f, motor.Settings.WalkSpeed * 0.25f)) : 0f;
                if (motor.IsCrouching) idleWeight *= 0.65f;
                float breath = Mathf.Sin(idlePhase), drift = Mathf.Cos(idlePhase);
                targetPosition += new Vector3(drift * 0.35f, breath, breath * 0.15f) * (idleAmount * idleWeight);
                targetAngles += new Vector3(breath * 0.15f, 0f, drift * 0.12f) * (idleWeight * idleAmount / 0.0025f);

                if (hadGroundState && motor.IsGrounded && !wasGrounded && previousVerticalSpeed < -1f)
                {
                    landingStrength = Mathf.Clamp01(-previousVerticalSpeed / 8f);
                    landingAge = 0f;
                }
                hadGroundState = true;
                wasGrounded = motor.IsGrounded;
                previousVerticalSpeed = motor.Velocity.y;
                landingAge = Mathf.Min(landingAge + deltaTime, 1f);
                float landing = landingStrength * Mathf.Exp(-landingAge * 12f) * Mathf.Sin(landingAge * 20f);
                float airborne = motor.IsGrounded ? 0f : -Mathf.Clamp(motor.Velocity.y / 8f, -1f, 1f);
                targetPosition += new Vector3(0f, airborne - landing * 2f, -landing * 0.5f) * jumpAmount;
                targetAngles.x += (-airborne * 2f + landing * 3f) * (jumpAmount / 0.022f);
                // The whole holder moves together, so both hands and the reload pickup keep their contact.
                bool reloading = armsAnimator != null && armsAnimator.isActiveAndEnabled &&
                    (armsAnimator.GetCurrentAnimatorStateInfo(0).IsName("Reload") ||
                    (armsAnimator.IsInTransition(0) && armsAnimator.GetNextAnimatorStateInfo(0).IsName("Reload")));
                if (reloading) { targetPosition *= 0.2f; targetAngles *= 0.2f; }
            }
            float smoothTime = Mathf.Max(0.01f, motionSmoothTime);
            motionPosition = Vector3.SmoothDamp(motionPosition, targetPosition, ref positionVelocity, smoothTime, Mathf.Infinity, deltaTime);
            motionAngles = Vector3.SmoothDamp(motionAngles, targetAngles, ref angleVelocity, smoothTime, Mathf.Infinity, deltaTime);
        }

        private void OnDisable()
        {
            if (!initialized) return;
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            proximity = proximityVelocity = 0f;
            motionPosition = positionVelocity = motionAngles = angleVelocity = Vector3.zero;
            previousCameraRotation = worldCamera.transform.rotation;
            idlePhase = landingAge = landingStrength = previousVerticalSpeed = 0f;
            hadGroundState = wasGrounded = false;
        }
    }
}
