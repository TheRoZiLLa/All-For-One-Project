using System;
using Recorded.ColorSystem;
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
            IncludeColorInteractableLayer();
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
            IncludeColorInteractableLayer();

            // 1. Primary aim ray originating from the exact center of the camera screen (0.5, 0.5)
            Ray aimRay = playerCamera != null
                ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position, transform.forward);

            if (playerCamera != null) aimRay.origin = playerCamera.transform.position;
            bool foundAimHit = TryHit(aimRay, maxRange, out RaycastHit bestAimHit);

            Vector3 desiredTargetPoint = foundAimHit
                ? bestAimHit.point
                : aimRay.origin + (aimRay.direction * maxRange);

            // 2. Close-range barrel-to-target obstruction check so shots cannot clip through walls right in front of the barrel
            if (muzzlePoint != null && muzzlePoint != transform)
            {
                Vector3 muzzlePos = muzzlePoint.position;
                // The overlay muzzle can be beyond a wall even though the hands remain visible.
                Vector3 toMuzzle = muzzlePos - aimRay.origin;
                if (TryHit(new Ray(aimRay.origin, toMuzzle.normalized), toMuzzle.magnitude, out hitInfo))
                {
                    targetPoint = hitInfo.point;
                    OnTargetHit?.Invoke(hitInfo);
                    return true;
                }
                Vector3 toTarget = desiredTargetPoint - muzzlePos;
                float distToTarget = toTarget.magnitude;

                if (distToTarget > 0.05f)
                {
                    if (TryHit(new Ray(muzzlePos, toTarget / distToTarget), distToTarget - 0.02f, out RaycastHit muzzleHit))
                    {
                        // If an obstacle blocks the barrel before reaching the crosshair target
                        if (!foundAimHit || muzzleHit.collider != bestAimHit.collider)
                        {
                            hitInfo = muzzleHit;
                            targetPoint = hitInfo.point;
                            OnTargetHit?.Invoke(hitInfo);
                            return true;
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

        /// <summary>
        /// The player prefab stored a mask from before ColorInteractable existed, so shots
        /// passed through every paintable prop. Keep that layer in the mask.
        /// </summary>
        private void IncludeColorInteractableLayer()
        {
            int colorLayer = ColorInteractable.Layer;
            if (colorLayer >= 0)
                hitLayers |= 1 << colorLayer;
        }

        private bool TryHit(Ray ray, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(ray, distance, hitLayers, triggerInteraction))
            {
                if (IsSelfCollider(hit.collider) || (found && hit.distance >= nearest.distance)) continue;
                nearest = hit;
                found = true;
            }
            return found;
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
