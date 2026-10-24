using UnityEngine;

namespace Recorded
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonViewmodel : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Camera viewmodelCamera;
        [SerializeField, Min(0.1f)] private float wallDistance = 0.8f;
        [SerializeField, Min(0f)] private float clearance = 0.12f;
        [SerializeField, Min(0f)] private float retractDistance = 0.08f;
        [SerializeField, Range(0f, 30f)] private float lowerAngle = 10f;
        [SerializeField, Min(0.01f)] private float responseTime = 0.08f;
        [SerializeField] private LayerMask wallMask = Physics.DefaultRaycastLayers;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Transform ignoredRoot;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float proximity, proximityVelocity;
        private bool initialized;

        public float WallProximity => proximity;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = GetComponentInParent<Camera>();
            if (worldCamera == null || viewmodelCamera == null)
            {
                Debug.LogError("Assign world and viewmodel cameras to FirstPersonViewmodel.", this);
                enabled = false;
                return;
            }
            var body = GetComponentInParent<CharacterController>();
            ignoredRoot = body != null ? body.transform : worldCamera.transform;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            initialized = true;
        }

        private void LateUpdate() => UpdateViewmodel(Time.deltaTime);

        private void UpdateViewmodel(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f) return;
            if (!Mathf.Approximately(viewmodelCamera.fieldOfView, worldCamera.fieldOfView))
                viewmodelCamera.fieldOfView = worldCamera.fieldOfView;
            float distance = Mathf.Max(0.1f, wallDistance);
            int count = Physics.SphereCastNonAlloc(worldCamera.transform.position, 0.06f,
                worldCamera.transform.forward, hits, distance, wallMask, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(ignoredRoot)) nearest = Mathf.Min(nearest, hits[i].distance);
            float target = Mathf.InverseLerp(distance, Mathf.Min(clearance, distance - 0.01f), nearest);
            proximity = Mathf.SmoothDamp(proximity, target, ref proximityVelocity,
                Mathf.Max(0.01f, responseTime), Mathf.Infinity, deltaTime);
            transform.localPosition = homePosition + Vector3.back * (retractDistance * proximity);
            transform.localRotation = homeRotation * Quaternion.Euler(lowerAngle * proximity, 0f, 0f);
        }

        private void OnDisable()
        {
            if (!initialized) return;
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            proximity = proximityVelocity = 0f;
        }
    }
}
