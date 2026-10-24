using System;
using UnityEngine;

namespace Recorded.Weapons
{
    /// <summary>
    /// Handles target detection and raycasting along the camera's forward view while ensuring
    /// visual projectile tracers always originate accurately from the barrel tip (MuzzlePoint).
    /// Also checks for close-range obstruction between the camera/muzzle and the crosshair target.
    /// </summary>
    public class RaycastWeapon : MonoBehaviour
    {
        [Header("Camera & Aiming")]
        [Tooltip("The camera used to determine the aim direction. If null, Camera.main will be cached.")]
        [SerializeField] private Camera playerCamera;

        [Header("Muzzle & Tracer")]
        [Tooltip("Origin point of projectile visual tracers and muzzle flashes at the barrel tip.")]
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

        public Transform MuzzlePoint
        {
            get
            {
                ResolveMuzzlePoint();
                return muzzlePoint;
            }
        }

        public Camera PlayerCamera => playerCamera;
        public float MaxRange { get => maxRange; set => maxRange = value; }
        public LayerMask HitLayers { get => hitLayers; set => hitLayers = value; }

        private void Awake()
        {
            ResolveCamera();
            ResolveMuzzlePoint();
        }

        public void ResolveCamera()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInParent<Camera>();
                if (playerCamera == null)
                {
                    playerCamera = Camera.main;
                }
            }
        }

        public void ResolveMuzzlePoint()
        {
            // If muzzlePoint is null or accidentally points to the weapon root instead of the barrel tip child, resolve MuzzlePoint child
            if (muzzlePoint == null || muzzlePoint == transform)
            {
                Transform childMuzzle = transform.Find("MuzzlePoint");
                if (childMuzzle != null)
                {
                    muzzlePoint = childMuzzle;
                    return;
                }

                // Deep search for MuzzlePoint in hierarchy
                Transform[] allChildren = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allChildren.Length; i++)
                {
                    if (allChildren[i] != null && allChildren[i].name == "MuzzlePoint")
                    {
                        muzzlePoint = allChildren[i];
                        return;
                    }
                }

                if (muzzlePoint == null)
                {
                    muzzlePoint = transform;
                }
            }
        }

        /// <summary>
        /// Perform a raycast along the center of the camera viewport, with close-range muzzle obstruction verification.
        /// </summary>
        /// <param name="hitInfo">Information about the hit target if successful.</param>
        /// <param name="targetPoint">World position of the hit point or the max range point if missed.</param>
        /// <returns>True if an object with a collider was hit within maxRange, otherwise false.</returns>
        public bool FireRaycast(out RaycastHit hitInfo, out Vector3 targetPoint)
        {
            ResolveCamera();
            ResolveMuzzlePoint();

            // 1. Primary aim ray originating from the exact center of the camera screen (0.5, 0.5)
            Ray aimRay = playerCamera != null
                ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position, transform.forward);

            bool foundAimHit = false;
            RaycastHit bestAimHit = default;

            RaycastHit[] hits = Physics.RaycastAll(aimRay, maxRange, hitLayers, triggerInteraction);
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    if (IsSelfCollider(hits[i].collider))
                    {
                        continue;
                    }

                    bestAimHit = hits[i];
                    foundAimHit = true;
                    break;
                }
            }

            Vector3 desiredTargetPoint = foundAimHit
                ? bestAimHit.point
                : aimRay.origin + (aimRay.direction * maxRange);

            // 2. Close-range barrel-to-target obstruction check so shots cannot clip through walls right in front of the barrel
            if (muzzlePoint != null && muzzlePoint != transform)
            {
                Vector3 muzzlePos = muzzlePoint.position;
                Vector3 toTarget = desiredTargetPoint - muzzlePos;
                float distToTarget = toTarget.magnitude;

                if (distToTarget > 0.05f)
                {
                    RaycastHit[] muzzleHits = Physics.RaycastAll(muzzlePos, toTarget / distToTarget, distToTarget - 0.02f, hitLayers, triggerInteraction);
                    if (muzzleHits != null && muzzleHits.Length > 0)
                    {
                        Array.Sort(muzzleHits, (a, b) => a.distance.CompareTo(b.distance));
                        for (int i = 0; i < muzzleHits.Length; i++)
                        {
                            if (IsSelfCollider(muzzleHits[i].collider))
                            {
                                continue;
                            }

                            // If an obstacle blocks the barrel before reaching the crosshair target
                            if (!foundAimHit || muzzleHits[i].collider != bestAimHit.collider)
                            {
                                hitInfo = muzzleHits[i];
                                targetPoint = hitInfo.point;
                                OnTargetHit?.Invoke(hitInfo);
                                return true;
                            }
                            break;
                        }
                    }
                }
            }

            if (foundAimHit)
            {
                hitInfo = bestAimHit;
                targetPoint = bestAimHit.point;
                OnTargetHit?.Invoke(hitInfo);
                return true;
            }

            hitInfo = default;
            targetPoint = desiredTargetPoint;
            OnTargetMiss?.Invoke(aimRay.origin, aimRay.direction);
            return false;
        }

        private bool IsSelfCollider(Collider col)
        {
            if (col == null) return true;
            if (col.transform.root == transform.root) return true;
            if (col.GetComponentInParent<CharacterController>() != null && playerCamera != null &&
                col.transform.root == playerCamera.transform.root)
            {
                return true;
            }
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
