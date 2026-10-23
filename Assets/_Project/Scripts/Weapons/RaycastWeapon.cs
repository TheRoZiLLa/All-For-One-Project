using System;
using UnityEngine;

namespace Recorded.Weapons
{
    /// <summary>
    /// Handles target detection and raycasting along the camera's forward view.
    /// Decoupled from weapon visual presentation and input controllers.
    /// </summary>
    public class RaycastWeapon : MonoBehaviour
    {
        [Header("Camera & Aiming")]
        [Tooltip("The camera used to determine the aim direction. If null, Camera.main will be cached.")]
        [SerializeField] private Camera playerCamera;

        [Header("Muzzle & Tracer")]
        [Tooltip("Origin point of projectile visual tracers and muzzle flashes.")]
        [SerializeField] private Transform muzzlePoint;

        [Header("Raycast Settings")]
        [Tooltip("Maximum shooting distance in meters.")]
        [SerializeField] private float maxRange = 100f;

        [Tooltip("Layers that can be hit by the raycast.")]
        [SerializeField] private LayerMask hitLayers = ~0; // Default: Everything

        [Tooltip("Query trigger colliders as valid hit targets.")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        // Events for decoupled feedback and game logic
        public event Action<RaycastHit> OnTargetHit;
        public event Action<Vector3, Vector3> OnTargetMiss; // origin, direction

        public Transform MuzzlePoint => muzzlePoint;
        public float MaxRange { get => maxRange; set => maxRange = value; }
        public LayerMask HitLayers { get => hitLayers; set => hitLayers = value; }

        private void Awake()
        {
            // Cache camera reference according to Game Developer optimization rules (avoid Camera.main in Update)
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (muzzlePoint == null)
            {
                muzzlePoint = transform;
            }
        }

        /// <summary>
        /// Perform a raycast along the center of the camera viewport.
        /// </summary>
        /// <param name="hitInfo">Information about the hit target if successful.</param>
        /// <param name="targetPoint">World position of the hit point or the max range point if missed.</param>
        /// <returns>True if an object with a collider was hit within maxRange, otherwise false.</returns>
        public bool FireRaycast(out RaycastHit hitInfo, out Vector3 targetPoint)
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            // Ray originating from the exact center of the camera screen (0.5, 0.5)
            Ray ray = playerCamera != null
                ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position, transform.forward);

            RaycastHit[] hits = Physics.RaycastAll(ray, maxRange, hitLayers, triggerInteraction);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    // Ignore self / shooter's own character controller or body
                    if (hits[i].transform.root == transform.root)
                    {
                        continue;
                    }

                    hitInfo = hits[i];
                    targetPoint = hitInfo.point;
                    OnTargetHit?.Invoke(hitInfo);

                    return true;
                }
            }

            hitInfo = default;
            targetPoint = ray.origin + (ray.direction * maxRange);
            OnTargetMiss?.Invoke(ray.origin, ray.direction);
            return false;
        }

        public void SetCamera(Camera cam)
        {
            playerCamera = cam;
        }

        public void SetMuzzlePoint(Transform muzzle)
        {
            muzzlePoint = muzzle;
        }
    }
}
