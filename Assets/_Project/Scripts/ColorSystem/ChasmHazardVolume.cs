using UnityEngine;
using Recorded.Player;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Catch volume placed beneath the Sector A chasm.
    /// Safely catches falling players and physics props, respawning them onto safe ground.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ChasmHazardVolume : MonoBehaviour
    {
        [SerializeField] private Vector3 playerRespawnPosition = new Vector3(0f, 0.23f, 18.5f);
        [SerializeField] private Vector3 propRespawnPosition = new Vector3(0f, 0.5f, 19.0f);

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;

            // Player caught
            var playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.RespawnFromChasmFall(playerRespawnPosition);
                return;
            }

            var cc = other.GetComponentInParent<CharacterController>();
            if (cc != null && PlayerHealth.LocalPlayer != null)
            {
                PlayerHealth.LocalPlayer.RespawnFromChasmFall(playerRespawnPosition);
                return;
            }

            // Physics puzzle prop caught (e.g. battery or movable cube)
            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = propRespawnPosition;
                rb.transform.position = propRespawnPosition;
                rb.transform.rotation = Quaternion.identity;
                rb.WakeUp();
                Debug.Log($"[ChasmHazardVolume] Reset dropped prop {other.name} back to {propRespawnPosition}");
            }
        }
    }
}
