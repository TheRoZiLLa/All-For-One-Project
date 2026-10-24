using UnityEngine;
using UnityEngine.Events;
using Recorded.Weapons.Ammo;

namespace Recorded.ColorSystem
{
    /// <summary>Opens after the security kiosk is unlocked and a subsequent Cyan shot hits this object.</summary>
    public sealed class KioskCyanAirlockGate : MonoBehaviour, IColorShotImpact
    {
        [SerializeField] private UnityEvent onAirlockOpened;
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField, Min(0f)] private float doorOpenDistance = 1.5f;
        [SerializeField, Min(0.01f)] private float doorOpenSpeed = 2f;

        public bool IsKioskUnlocked { get; private set; }
        public bool IsAirlockOpen { get; private set; }

        private Vector3 _leftClosedPosition;
        private Vector3 _rightClosedPosition;

        private void Awake()
        {
            if (leftDoor != null) _leftClosedPosition = leftDoor.localPosition;
            if (rightDoor != null) _rightClosedPosition = rightDoor.localPosition;
        }

        private void Update()
        {
            if (!IsAirlockOpen) return;

            if (leftDoor != null)
                leftDoor.localPosition = Vector3.MoveTowards(leftDoor.localPosition, _leftClosedPosition + Vector3.left * doorOpenDistance, doorOpenSpeed * Time.deltaTime);
            if (rightDoor != null)
                rightDoor.localPosition = Vector3.MoveTowards(rightDoor.localPosition, _rightClosedPosition + Vector3.right * doorOpenDistance, doorOpenSpeed * Time.deltaTime);
        }

        /// <summary>Wire this to the kiosk's existing interaction event.</summary>
        public void UnlockSecurityKiosk()
        {
            IsKioskUnlocked = true;
        }

        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            if (!IsKioskUnlocked || IsAirlockOpen || firedAmmo != AmmoType.Cyan)
                return;

            IsAirlockOpen = true;
            onAirlockOpened?.Invoke();
        }
    }
}
