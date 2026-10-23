using System;
using UnityEngine;

namespace Recorded
{
    [Serializable]
    public sealed class PlayerMovementSettings
    {
        [Header("Speed (m/s)")]
        [Min(0f)] public float WalkSpeed = 4.5f;
        [Min(0f)] public float SprintSpeed = 7f;
        [Min(0f)] public float CrouchSpeed = 2.5f;
        [Min(0.01f)] public float GroundAcceleration = 18f;
        [Min(0.01f)] public float GroundDeceleration = 24f;
        [Range(0f, 1f)] public float AirControl = 0.35f;

        [Header("Jump and ground")]
        [Min(0f)] public float JumpHeight = 1.4f;
        [Tooltip("Negative acceleration in m/s².")]
        public float Gravity = -22f;
        [Min(0f)] public float CoyoteTime = 0.12f;
        [Min(0f)] public float JumpBuffer = 0.12f;
        [Range(0f, 0.3f)] public float GroundProbeDistance = 0.12f;
        [Min(0.1f)] public float GroundStickSpeed = 2f;
        [Range(0f, 89f)] public float SlopeLimit = 50f;
        [Min(0f)] public float StepHeight = 0.3f;
        public LayerMask CollisionMask = ~0;

        [Header("Capsule (root at feet, unit scale)")]
        [Min(0.1f)] public float Radius = 0.3f;
        [Min(0.2f)] public float StandingHeight = 1.8f;
        [Min(0.2f)] public float CrouchingHeight = 1.1f;
        [Min(0.01f)] public float CrouchTransition = 0.15f;
        [Min(0.01f)] public float EyeInset = 0.15f;
        [Range(0.01f, 0.1f)] public float SkinWidth = 0.03f;

        [Header("External velocity damping (1/s)")]
        [Min(0f)] public float GroundImpulseDamping = 5f;
        [Min(0f)] public float AirImpulseDamping = 0.8f;

        public void Validate()
        {
            Radius = Mathf.Max(0.1f, Radius);
            StandingHeight = Mathf.Max(Radius * 2f + 0.02f, StandingHeight);
            CrouchingHeight = Mathf.Clamp(CrouchingHeight, Radius * 2f + 0.02f, StandingHeight);
            SkinWidth = Mathf.Clamp(SkinWidth, 0.01f, Radius * 0.5f);
            EyeInset = Mathf.Clamp(EyeInset, SkinWidth + 0.05f, CrouchingHeight * 0.5f);
            CrouchTransition = Mathf.Max(0.01f, CrouchTransition);
            Gravity = Mathf.Min(-0.01f, Gravity);
            JumpHeight = Mathf.Max(0f, JumpHeight);
            GroundAcceleration = Mathf.Max(0.01f, GroundAcceleration);
            GroundDeceleration = Mathf.Max(0.01f, GroundDeceleration);
            GroundImpulseDamping = Mathf.Max(0f, GroundImpulseDamping);
            AirImpulseDamping = Mathf.Max(0f, AirImpulseDamping);
        }
    }
}
