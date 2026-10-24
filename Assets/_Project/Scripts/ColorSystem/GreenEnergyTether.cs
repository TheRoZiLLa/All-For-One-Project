using UnityEngine;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Walkable energy bridge between exactly two Green targets.
    /// The LineRenderer is the rope. A kinematic BoxCollider matches that segment every
    /// physics step so the player can stand on it, and both follow the endpoints when they move.
    /// A later Green shot on one end reels the other end in along the rope. This component
    /// does not mix colors and does not attract unpainted objects.
    /// </summary>
    [DisallowMultipleComponent]
    public class GreenEnergyTether : MonoBehaviour
    {
        private SpectrumSkillResponder _endpointA;
        private SpectrumSkillResponder _endpointB;
        private LineRenderer _rope;
        private BoxCollider _bridge;
        private Rigidbody _bridgeBody;
        private PhysicsMaterial _walkMaterial;

        private float _width;
        private float _thickness;
        private float _pullSpeed;
        private float _stopPadding;
        private bool _isPulling;
        private bool _broken;
        private SpectrumSkillResponder _pullAnchor;
        private SpectrumSkillResponder _pullTarget;

        private static Shader _unlitShader;

        public SpectrumSkillResponder EndpointA => _endpointA;
        public SpectrumSkillResponder EndpointB => _endpointB;
        public BoxCollider BridgeCollider => _bridge;
        public LineRenderer Rope => _rope;
        public bool IsPulling => _isPulling;
        public SpectrumSkillResponder PullAnchor => _pullAnchor;
        public float CurrentLength { get; private set; }

        public static GreenEnergyTether Create(
            SpectrumSkillResponder a,
            SpectrumSkillResponder b,
            float maxRange,
            float width,
            float thickness,
            float pullSpeed,
            float stopPadding)
        {
            if (a == null || b == null || a == b) return null;
            if (a.IsTethered || b.IsTethered) return null;

            float distance = Vector3.Distance(a.TetherPoint, b.TetherPoint);
            if (distance <= 0.05f || distance > maxRange) return null;

            GameObject bridgeObject = new GameObject($"GreenEnergyTether [{a.name} ↔ {b.name}]");
            bridgeObject.hideFlags = HideFlags.DontSave;
            GreenEnergyTether tether = bridgeObject.AddComponent<GreenEnergyTether>();
            tether.Initialize(a, b, width, thickness, pullSpeed, stopPadding);
            return tether;
        }

        public SpectrumSkillResponder Other(SpectrumSkillResponder self)
        {
            if (self == _endpointA) return _endpointB;
            if (self == _endpointB) return _endpointA;
            return null;
        }

        /// <summary>
        /// The re-shot Green object stays put. The other Green object is reeled toward it.
        /// </summary>
        public bool BeginPullToward(SpectrumSkillResponder anchor)
        {
            if (_broken || anchor == null) return false;
            if (anchor != _endpointA && anchor != _endpointB) return false;

            SpectrumSkillResponder moving = Other(anchor);
            if (moving == null || moving.IsBlueStasis) return false;

            Rigidbody movingBody = moving.Body;
            if (movingBody == null || movingBody.isKinematic) return false;

            _pullAnchor = anchor;
            _pullTarget = moving;
            _isPulling = true;
            movingBody.WakeUp();
            return true;
        }

        public void TickPull(float deltaTime)
        {
            if (!_isPulling || _broken || deltaTime <= 0f) return;
            if (_pullAnchor == null || _pullTarget == null)
            {
                _isPulling = false;
                return;
            }

            if (_pullTarget.IsBlueStasis)
            {
                _isPulling = false;
                return;
            }

            Rigidbody movingBody = _pullTarget.Body;
            if (movingBody == null || movingBody.isKinematic)
            {
                _isPulling = false;
                return;
            }

            Vector3 from = _pullTarget.TetherPoint;
            Vector3 to = _pullAnchor.TetherPoint;
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance <= ComputeStopDistance(offset))
            {
                movingBody.linearVelocity = Vector3.zero;
                movingBody.angularVelocity = Vector3.zero;
                _isPulling = false;
                return;
            }

            float step = Mathf.Max(0.01f, _pullSpeed) * deltaTime;
            Vector3 next = Vector3.MoveTowards(from, to, step);
            Vector3 delta = next - from;

            if (!Application.isPlaying)
            {
                // Edit Mode tests have no physics step. Move the transform directly.
                _pullTarget.transform.position += delta;
                return;
            }

            // Sample MoveTowards into velocity so the reel follows the rope and still collides.
            movingBody.linearVelocity = delta / deltaTime;
            movingBody.angularVelocity = Vector3.zero;
            movingBody.AddForce(-Physics.gravity, ForceMode.Acceleration);
        }

        public void RefreshBridge()
        {
            if (_broken || _endpointA == null || _endpointB == null || _bridge == null || _rope == null) return;

            Vector3 start = _endpointA.TetherPoint;
            Vector3 end = _endpointB.TetherPoint;
            Vector3 delta = end - start;
            float length = delta.magnitude;
            CurrentLength = length;

            _rope.positionCount = 2;
            _rope.SetPosition(0, start);
            _rope.SetPosition(1, end);

            if (length < 0.001f) return;

            Vector3 direction = delta / length;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(direction, up);
            Vector3 midpoint = (start + end) * 0.5f;

            transform.SetPositionAndRotation(midpoint, rotation);
            if (_bridgeBody != null)
            {
                _bridgeBody.position = midpoint;
                _bridgeBody.rotation = rotation;
            }

            // Top face sits on the rope so the visible line is the surface the player walks on.
            _bridge.size = new Vector3(Mathf.Max(0.2f, _width), Mathf.Max(0.05f, _thickness), length);
            _bridge.center = new Vector3(0f, -_bridge.size.y * 0.5f, 0f);
            IgnoreEndpointCollisions();
        }

        public void Break()
        {
            if (_broken) return;
            _broken = true;
            _isPulling = false;

            SpectrumSkillResponder endpointA = _endpointA;
            SpectrumSkillResponder endpointB = _endpointB;
            _endpointA = null;
            _endpointB = null;
            _pullAnchor = null;
            _pullTarget = null;

            if (endpointA != null) endpointA.ClearTetherReference(this);
            if (endpointB != null) endpointB.ClearTetherReference(this);

            // A still-green endpoint may link to a different green object. The endpoint that
            // just lost Green has already cleared IsGreenBound, so it will not pair again.
            if (endpointA != null && endpointA.IsGreenBound) endpointA.TryCreateTether();
            if (endpointB != null && endpointB.IsGreenBound) endpointB.TryCreateTether();

            DestroyMaterial(_rope != null ? _rope.sharedMaterial : null);
            DestroyMaterial(_walkMaterial);
            _walkMaterial = null;

            if (this != null && gameObject != null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private void Initialize(
            SpectrumSkillResponder a,
            SpectrumSkillResponder b,
            float width,
            float thickness,
            float pullSpeed,
            float stopPadding)
        {
            _endpointA = a;
            _endpointB = b;
            _width = width > 0.05f ? width : 1.0f;
            _thickness = thickness > 0.02f ? thickness : 0.3f;
            _pullSpeed = pullSpeed > 0.1f ? pullSpeed : 14f;
            _stopPadding = stopPadding >= 0f ? stopPadding : 0.05f;

            _bridge = gameObject.AddComponent<BoxCollider>();
            _bridge.isTrigger = false;
            _walkMaterial = new PhysicsMaterial("GreenTetherWalk")
            {
                dynamicFriction = 0.9f,
                staticFriction = 1.0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            _bridge.material = _walkMaterial;

            _bridgeBody = gameObject.AddComponent<Rigidbody>();
            _bridgeBody.isKinematic = true;
            _bridgeBody.useGravity = false;
            _bridgeBody.interpolation = RigidbodyInterpolation.None;
            _bridgeBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            _rope = gameObject.AddComponent<LineRenderer>();
            _rope.useWorldSpace = true;
            _rope.loop = false;
            _rope.positionCount = 2;
            _rope.startWidth = 0.12f;
            _rope.endWidth = 0.12f;
            _rope.numCapVertices = 4;
            _rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rope.receiveShadows = false;
            _rope.sharedMaterial = new Material(GetUnlitShader())
            {
                color = new Color(0.15f, 0.95f, 0.28f, 1f)
            };

            a.AttachTether(this);
            b.AttachTether(this);
            RefreshBridge();
        }

        private void FixedUpdate()
        {
            if (_broken) return;
            if (_endpointA == null || _endpointB == null || !_endpointA.IsGreenBound || !_endpointB.IsGreenBound)
            {
                Break();
                return;
            }

            TickPull(Time.fixedDeltaTime);
            RefreshBridge();
        }

        private void LateUpdate()
        {
            if (_broken) return;
            RefreshBridge();
        }

        private void OnDestroy()
        {
            if (_broken) return;
            _broken = true;
            _isPulling = false;
            if (_endpointA != null) _endpointA.ClearTetherReference(this);
            if (_endpointB != null) _endpointB.ClearTetherReference(this);
            _endpointA = null;
            _endpointB = null;
            DestroyMaterial(_rope != null ? _rope.sharedMaterial : null);
            DestroyMaterial(_walkMaterial);
        }

        private float ComputeStopDistance(Vector3 offset)
        {
            float padding = _stopPadding > 0f ? _stopPadding : 0.05f;
            if (offset.sqrMagnitude < 0.0001f) return 0.75f + padding;
            Vector3 direction = offset.normalized;
            return ExtentAlong(_pullAnchor, direction) + ExtentAlong(_pullTarget, direction) + padding;
        }

        private static float ExtentAlong(SpectrumSkillResponder body, Vector3 direction)
        {
            if (body == null) return 0.5f;
            Collider collider = body.GetComponent<Collider>();
            if (collider == null) return 0.5f;
            Vector3 extents = collider.bounds.extents;
            return Mathf.Abs(direction.x) * extents.x
                + Mathf.Abs(direction.y) * extents.y
                + Mathf.Abs(direction.z) * extents.z;
        }

        private void IgnoreEndpointCollisions()
        {
            IgnoreAgainst(_endpointA);
            IgnoreAgainst(_endpointB);
        }

        private void IgnoreAgainst(SpectrumSkillResponder endpoint)
        {
            if (endpoint == null || _bridge == null) return;
            Collider[] colliders = endpoint.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider other = colliders[i];
                if (other == null || other == _bridge) continue;
                Physics.IgnoreCollision(_bridge, other, true);
            }
        }

        private static void DestroyMaterial(Object material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        private static Shader GetUnlitShader()
        {
            if (_unlitShader == null)
            {
                _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (_unlitShader == null) _unlitShader = Shader.Find("Sprites/Default");
                if (_unlitShader == null) _unlitShader = Shader.Find("Unlit/Color");
            }
            return _unlitShader;
        }
    }
}
