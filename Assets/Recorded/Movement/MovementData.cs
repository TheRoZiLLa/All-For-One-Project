using UnityEngine;

namespace Recorded
{
    public enum MovementState { Grounded, Airborne, Crouching }

    public readonly struct MovementInput
    {
        public readonly Vector2 Move;
        public readonly bool JumpPressed;
        public readonly bool SprintHeld;
        public readonly bool CrouchHeld;

        public MovementInput(Vector2 move, bool jumpPressed = false, bool sprintHeld = false, bool crouchHeld = false)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            JumpPressed = jumpPressed;
            SprintHeld = sprintHeld;
            CrouchHeld = crouchHeld;
        }
    }

    public readonly struct MovementSnapshot
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float CameraPitch;
        public readonly Vector3 Velocity;
        public readonly MovementState State;
        public readonly bool IsGrounded;

        public MovementSnapshot(Vector3 position, Quaternion rotation, float cameraPitch,
            Vector3 velocity, MovementState state, bool isGrounded)
        {
            Position = position;
            Rotation = rotation;
            CameraPitch = cameraPitch;
            Velocity = velocity;
            State = state;
            IsGrounded = isGrounded;
        }
    }
}
