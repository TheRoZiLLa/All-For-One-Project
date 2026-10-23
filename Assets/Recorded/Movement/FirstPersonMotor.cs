using System;
using UnityEngine;

namespace Recorded
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [SerializeField] private PlayerMovementSettings settings = new PlayerMovementSettings();
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private PlayerInputReader input;
        private CharacterController controller;
        private FirstPersonLook look;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private readonly Collider[] clearanceHits = new Collider[16];
        private Vector3 movementVelocity, externalVelocity, groundNormal = Vector3.up;
        private float verticalVelocity, clock;
        private float lastGrounded = float.NegativeInfinity;
        private float lastJumpPressed = float.NegativeInfinity;

        public PlayerMovementSettings Settings => settings;
        public Vector3 Velocity { get; private set; }
        public Vector3 ExternalVelocity => externalVelocity;
        public float ControlledSpeed => movementVelocity.magnitude;
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public MovementState State => !IsGrounded ? MovementState.Airborne :
            IsCrouching ? MovementState.Crouching : MovementState.Grounded;
        public MovementSnapshot Snapshot => new MovementSnapshot(transform.position, transform.rotation,
            look != null ? look.Pitch : 0f, Velocity, State, IsGrounded);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            look = GetComponent<FirstPersonLook>();
            settings.Validate();
            controller.radius = settings.Radius;
            controller.height = settings.StandingHeight;
            controller.center = Vector3.up * (controller.height * 0.5f);
            controller.skinWidth = settings.SkinWidth;
            controller.slopeLimit = settings.SlopeLimit;
            controller.minMoveDistance = 0f;
            controller.stepOffset = Mathf.Min(settings.StepHeight, settings.CrouchingHeight - 2f * settings.Radius);
            UpdateCameraHeight();
        }

        private void OnValidate() => settings.Validate();

        private void Update() => Simulate(input != null && input.isActiveAndEnabled ? input.Current : default, Time.deltaTime);

        /// <summary>Disable this component's Update when driving it from recorded or test input.</summary>
        public void Simulate(MovementInput command, float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime == 0f || controller == null || !controller.enabled) return;
            Vector3 start = transform.position;
            // Small collision steps keep stairs and ground probes stable at lower frame rates.
            int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / (1f / 60f)));
            float dt = deltaTime / steps;
            if (command.JumpPressed) lastJumpPressed = clock + dt;
            for (int i = 0; i < steps; i++) Step(command, dt);
            Velocity = (transform.position - start) / deltaTime;
        }

        public void ApplyImpulse(Vector3 impulse)
        {
            if (!float.IsFinite(impulse.x) || !float.IsFinite(impulse.y) || !float.IsFinite(impulse.z))
                throw new ArgumentException("Impulse must be finite, in metres per second.", nameof(impulse));
            externalVelocity += impulse;
            if (impulse.y > 0f && IsGrounded)
            {
                verticalVelocity = 0f;
                IsGrounded = false;
                lastGrounded = float.NegativeInfinity;
            }
        }

        private void Step(MovementInput command, float dt)
        {
            clock += dt;
            IsGrounded = verticalVelocity + externalVelocity.y <= 0f && ProbeGround();
            if (IsGrounded)
            {
                lastGrounded = clock;
                verticalVelocity = -settings.GroundStickSpeed;
                if (externalVelocity.y < 0f) externalVelocity.y = 0f;
            }

            UpdateCrouch(command.CrouchHeld, dt);
            IsSprinting = command.SprintHeld && !IsCrouching && command.Move.sqrMagnitude > 0f;
            float speed = IsCrouching ? settings.CrouchSpeed : IsSprinting ? settings.SprintSpeed : settings.WalkSpeed;
            Vector3 desired = transform.TransformDirection(new Vector3(command.Move.x, 0f, command.Move.y)) * speed;
            float acceleration = command.Move.sqrMagnitude > 0f ? settings.GroundAcceleration : settings.GroundDeceleration;
            movementVelocity = Vector3.MoveTowards(movementVelocity, desired,
                acceleration * (IsGrounded ? 1f : settings.AirControl) * dt);

            if (clock - lastJumpPressed <= settings.JumpBuffer && clock - lastGrounded <= settings.CoyoteTime)
            {
                verticalVelocity = Mathf.Sqrt(-2f * settings.Gravity * settings.JumpHeight);
                IsGrounded = false;
                lastJumpPressed = lastGrounded = float.NegativeInfinity;
            }

            controller.stepOffset = IsGrounded ? Mathf.Max(0f,
                Mathf.Min(settings.StepHeight, controller.height - 2f * controller.radius)) : 0f;
            Vector3 controlled = IsGrounded ? Vector3.ProjectOnPlane(movementVelocity, groundNormal).normalized
                * movementVelocity.magnitude : movementVelocity;
            float damping = IsGrounded ? settings.GroundImpulseDamping : settings.AirImpulseDamping;
            float decay = Mathf.Exp(-damping * dt);
            Vector3 externalDisplacement = externalVelocity * (damping > 0f ? (1f - decay) / damping : dt);
            externalVelocity *= decay;
            Vector3 displacement = controlled * dt + externalDisplacement;
            displacement.y += verticalVelocity * dt + 0.5f * settings.Gravity * dt * dt;
            verticalVelocity += settings.Gravity * dt;
            CollisionFlags flags = controller.Move(displacement);
            if ((flags & CollisionFlags.Above) != 0)
            {
                verticalVelocity = Mathf.Min(0f, verticalVelocity);
                externalVelocity.y = Mathf.Min(0f, externalVelocity.y);
            }
            bool landed = verticalVelocity + externalVelocity.y <= 0f && ProbeGround();
            IsGrounded = landed;
            if (landed)
            {
                verticalVelocity = -settings.GroundStickSpeed;
                externalVelocity.y = Mathf.Max(0f, externalVelocity.y);
                lastGrounded = clock;
            }
        }

        private bool ProbeGround()
        {
            float radius = controller.radius - controller.skinWidth;
            Vector3 origin = transform.position + Vector3.up * (controller.radius + controller.skinWidth);
            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundHits,
                settings.GroundProbeDistance + 2f * controller.skinWidth, settings.CollisionMask, QueryTriggerInteraction.Ignore);
            float slopeCos = Mathf.Cos(settings.SlopeLimit * Mathf.Deg2Rad);
            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.normal.y < slopeCos || hit.distance >= nearest) continue;
                groundNormal = hit.normal;
                nearest = hit.distance;
                found = true;
            }
            // At a stair edge the capsule can be grounded while the smaller probe misses the tread.
            if (!found && controller.isGrounded)
            {
                count = Physics.RaycastNonAlloc(transform.position + Vector3.up * controller.skinWidth,
                    Vector3.down, groundHits, settings.StepHeight + 2f * controller.skinWidth,
                    settings.CollisionMask, QueryTriggerInteraction.Ignore);
                nearest = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = groundHits[i];
                    if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    groundNormal = hit.normal;
                    found = hit.normal.y >= slopeCos;
                }
            }
            return found;
        }

        private void UpdateCrouch(bool held, float dt)
        {
            bool crouch = held || (controller.height < settings.StandingHeight && !CanOccupyHeight(settings.StandingHeight));
            float target = crouch ? settings.CrouchingHeight : settings.StandingHeight;
            float height = Mathf.MoveTowards(controller.height, target,
                (settings.StandingHeight - settings.CrouchingHeight) / settings.CrouchTransition * dt);
            if (!Mathf.Approximately(controller.height, height))
            {
                controller.height = height;
                controller.center = Vector3.up * (height * 0.5f);
            }
            IsCrouching = held || height < settings.StandingHeight - 0.001f;
            UpdateCameraHeight();
        }

        private bool CanOccupyHeight(float height)
        {
            float radius = controller.radius - controller.skinWidth * 0.25f;
            // Only test the space added above the current capsule; floor skin penetration is harmless.
            Vector3 bottom = transform.position + Vector3.up * (controller.height - controller.radius);
            Vector3 top = transform.position + Vector3.up * (height - controller.radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, clearanceHits,
                settings.CollisionMask, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!clearanceHits[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private void UpdateCameraHeight()
        {
            if (cameraPivot == null) return;
            Vector3 position = cameraPivot.localPosition;
            position.y = controller.height - settings.EyeInset;
            cameraPivot.localPosition = position;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y >= Mathf.Cos(settings.SlopeLimit * Mathf.Deg2Rad)) return;
            // CharacterController resolves input against walls and stair risers without erasing the requested speed.
            if (Vector3.Dot(externalVelocity, hit.normal) < 0f)
                externalVelocity = Vector3.ProjectOnPlane(externalVelocity, hit.normal);
        }
    }
}
