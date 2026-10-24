using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Recorded.ColorSystem
{
    /// <summary>Unlocks the configured gate when the tagged player presses E inside this trigger.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SecurityKioskEInteraction : MonoBehaviour
    {
        [SerializeField] private KioskCyanAirlockGate airlockGate;
        [SerializeField] private string playerTag = "Player";

        private readonly HashSet<Collider> _nearbyPlayerColliders = new HashSet<Collider>();

        public static bool HasInteractionFor(Transform player)
        {
            if (player == null) return false;
            foreach (var kiosk in FindObjectsByType<SecurityKioskEInteraction>(FindObjectsSortMode.None))
            {
                if (!kiosk.isActiveAndEnabled || kiosk.airlockGate == null) continue;
                foreach (var collider in kiosk._nearbyPlayerColliders)
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && collider.transform.IsChildOf(player))
                        return true;
            }
            return false;
        }

        private void Awake()
        {
            if (!GetComponent<Collider>().isTrigger)
                Debug.LogError("Security kiosk interaction requires its Collider to have Is Trigger enabled.", this);
        }

        private void Update()
        {
            if (_nearbyPlayerColliders.Count > 0 && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                airlockGate?.UnlockSecurityKiosk();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
                _nearbyPlayerColliders.Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            _nearbyPlayerColliders.Remove(other);
        }

        private void OnDisable()
        {
            _nearbyPlayerColliders.Clear();
        }
    }
}
