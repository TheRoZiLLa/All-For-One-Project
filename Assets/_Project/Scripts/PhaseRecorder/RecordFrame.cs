using Recorded.ColorSystem;
using Recorded.Weapons.Ammo;
using UnityEngine;

namespace Recorded.Phase
{
    /// <summary>One sample of the player while the Phase Recorder is listening.</summary>
    public struct RecordFrame
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Pitch;
        public bool Fired;
        public AmmoType Ammo;
        public ObjectColorState Color;
        public Vector3 AimOrigin;
        public Vector3 AimDirection;
        public KioskCyanAirlockGate Kiosk;
    }
}
