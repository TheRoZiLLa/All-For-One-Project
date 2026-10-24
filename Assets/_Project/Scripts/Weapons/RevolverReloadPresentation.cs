using UnityEngine;

namespace Recorded.Weapons
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(SpectrumGunController))]
    public sealed class RevolverReloadPresentation : MonoBehaviour
    {
        private SpectrumGunController _gun;
        private Transform _cylinder, _loader, _carrier;
        private Transform _leftGrip, _pickup;
        private Transform _upper, _forearm, _hand;
        private Vector3 _knobLocal;
        private Bounds _bounds;
        private Vector3 _closedCenter, _basePosition;
        private Quaternion _closedRotation, _baseRotation;
        private bool _hasCylinderPose;
        internal float CylinderIndexAngle { get; private set; }
        private Material _caseMaterial;
        private Material _primerMaterial;
        private Material _carrierMaterial;
        private Material _tipMaterial;
        private readonly Renderer[] _primers = new Renderer[Ammo.CylinderAmmoController.CylinderCapacity];
        private MaterialPropertyBlock _primerProperties;

        private void Awake()
        {
            _gun = GetComponent<SpectrumGunController>();
            _cylinder = transform.Find("Barrel");
            _leftGrip = GetComponentInParent<Animator>()?.transform.Find("upper_arm.L/forearm.L/hand.L/ReloadGrip.L");
            _pickup = GetComponentInParent<Camera>()?.transform.Find("Viewmodel/ReloadPickup");
            _hand = _leftGrip != null ? _leftGrip.parent : null;
            _forearm = _hand != null ? _hand.parent : null;
            _upper = _forearm != null ? _forearm.parent : null;
            var mesh = _cylinder != null ? _cylinder.GetComponent<MeshFilter>() : null;
            if (mesh != null && mesh.sharedMesh != null)
            {
                _bounds = mesh.sharedMesh.bounds;
                _basePosition = _cylinder.localPosition;
                _baseRotation = _closedRotation = _cylinder.localRotation;
                _closedCenter = _basePosition + _baseRotation * _bounds.center;
                _hasCylinderPose = true;
            }
        }

        private void Start()
        {
            if (BuildLoader()) _loader.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            ApplyCylinderIndex();
            bool visible = _gun.IsReloading && _leftGrip != null && _pickup != null;
            if (!visible) { if (_loader != null) _loader.gameObject.SetActive(false); return; }
            if (_loader == null && !BuildLoader()) return;
            _loader.gameObject.SetActive(true);
            float progress = _gun.ReloadProgress;
            CorrectInsertionContact(progress);
            _loader.localRotation = Quaternion.AngleAxis(-CylinderIndexAngle, Vector3.right);
            _loader.localPosition = _bounds.center - _loader.localRotation * _bounds.center;
            _carrier.localPosition = Vector3.zero;
            _carrier.localRotation = Quaternion.identity;
            // Transfers happen only at matching poses: pouch -> hand -> seated rounds -> pouch.
            Transform grip = progress < 0.18f || progress >= 0.93f ? _pickup : _leftGrip;
            Transform held = progress < SpectrumGunController.ReloadRefillPoint ? _loader : _carrier;
            held.rotation = grip.rotation;
            held.position = grip.position - held.TransformVector(_knobLocal);
            for (int i = 0; i < _primers.Length; i++)
            {
                Color color = _gun.ReloadAmmoLoaded && _gun.AmmoController != null
                    ? SpectrumGunController.GetAmmoColor(_gun.AmmoController.GetChamberAmmo(i)) : new Color(0.35f, 0.38f, 0.4f);
                _primerProperties.SetColor("_BaseColor", color);
                _primerProperties.SetColor("_Color", color);
                _primers[i].SetPropertyBlock(_primerProperties);
            }
        }

        private void Update()
        {
            if (!_hasCylinderPose) return;
            // Remove last frame's additive index before Animator, including when its speed is zero.
            _cylinder.SetLocalPositionAndRotation(_basePosition, _baseRotation);
        }

        private void ApplyCylinderIndex()
        {
            if (!_hasCylinderPose) return;
            _baseRotation = _gun.IsReloading ? _cylinder.localRotation : _closedRotation;
            var center = _gun.IsReloading ? _cylinder.localPosition + _baseRotation * _bounds.center : _closedCenter;
            _basePosition = center - _baseRotation * _bounds.center;
            CylinderIndexAngle = _gun.RotationController != null ? _gun.RotationController.CurrentAngle : 0f;
            var rotation = _baseRotation * Quaternion.AngleAxis(CylinderIndexAngle, Vector3.right);
            _cylinder.SetLocalPositionAndRotation(center - rotation * _bounds.center, rotation);
        }

        private void CorrectInsertionContact(float progress)
        {
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.40f, progress))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.78f, progress)));
            if (weight <= 0f || _upper == null) return;
            // Avatar quaternion resampling can shift the baked wrist; keep pads and bores in contact.
            float withdrawal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.78f, progress));
            float travel = _bounds.extents.x * 0.72f * 2.6f * (1f - _gun.ReloadInsertion + withdrawal);
            var point = _cylinder.TransformPoint(_knobLocal + Vector3.left * travel);
            var gripRotation = _cylinder.parent.rotation * _baseRotation * Quaternion.AngleAxis(
                25f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 0.72f, progress)), Vector3.right);
            var handRotation = gripRotation * Quaternion.Inverse(_leftGrip.localRotation);
            var target = point - handRotation * Vector3.Scale(_leftGrip.localPosition, _hand.lossyScale);
            target = Vector3.Lerp(_hand.position, target, weight);
            handRotation = Quaternion.Slerp(_hand.rotation, handRotation, weight);
            var root = _upper.position;
            float a = Vector3.Distance(root, _forearm.position), b = Vector3.Distance(_forearm.position, _hand.position);
            float distance = Mathf.Clamp(Vector3.Distance(root, target), Mathf.Abs(a - b) + 0.0001f, a + b - 0.0001f);
            var direction = (target - root).normalized;
            var pole = Vector3.ProjectOnPlane(_forearm.position - root, direction).normalized;
            if (pole.sqrMagnitude < 0.001f) pole = Vector3.ProjectOnPlane(-_upper.parent.right, direction).normalized;
            float projection = (a * a + distance * distance - b * b) / (2f * distance);
            var elbow = root + direction * projection + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - projection * projection));
            _upper.rotation = Quaternion.FromToRotation(_forearm.position - root, elbow - root) * _upper.rotation;
            _forearm.rotation = Quaternion.FromToRotation(_hand.position - _forearm.position, root + direction * distance - _forearm.position) * _forearm.rotation;
            _hand.rotation = handRotation;
        }

        private bool BuildLoader()
        {
            if (_loader != null) return true;
            var filter = _cylinder != null ? _cylinder.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return false;
            _bounds = filter.sharedMesh.bounds;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _caseMaterial = new Material(shader) { color = new Color(0.58f, 0.43f, 0.23f) };
            _caseMaterial.SetFloat("_Metallic", 0.8f);
            _caseMaterial.SetFloat("_Smoothness", 0.65f);
            _carrierMaterial = new Material(shader) { color = new Color(0.10f, 0.12f, 0.14f) };
            _carrierMaterial.SetFloat("_Metallic", 0.7f);
            _carrierMaterial.SetFloat("_Smoothness", 0.5f);
            _tipMaterial = new Material(shader) { color = new Color(0.60f, 0.28f, 0.12f) };
            _tipMaterial.SetFloat("_Metallic", 0.75f);
            _tipMaterial.SetFloat("_Smoothness", 0.5f);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _primerMaterial = new Material(unlit);
            _primerProperties = new MaterialPropertyBlock();
            _loader = new GameObject("Reload_Cartridges").transform;
            _loader.SetParent(_cylinder, false);
            float radius = _bounds.extents.y * 0.55372f;
            float diameter = _bounds.extents.y * 0.33f;
            float length = _bounds.extents.x * 0.72f;
            // Radius and diameter match the eight bores measured on the REV mesh.
            for (int i = 0; i < _primers.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / _primers.Length;
                var center = new Vector3(_bounds.min.x + length * 0.5f, _bounds.center.y + Mathf.Cos(angle) * radius, _bounds.center.z + Mathf.Sin(angle) * radius);
                CreatePart("Cartridge_" + i, _loader, center, new Vector3(diameter, length * 0.5f, diameter), _caseMaterial);
                CreatePart("Rim_" + i, _loader, center + Vector3.left * length * 0.51f,
                    new Vector3(diameter * 1.12f, diameter * 0.07f, diameter * 1.12f), _caseMaterial);
                CreatePart("BulletTip_" + i, _loader, center + Vector3.right * length * 0.52f,
                    new Vector3(diameter * 0.8f, diameter * 0.9f, diameter * 0.9f), _tipMaterial, PrimitiveType.Sphere);
                var primer = CreatePart("Primer_" + i, _loader, center + Vector3.left * length * 0.51f,
                    new Vector3(diameter * 0.65f, diameter * 0.08f, diameter * 0.65f), _primerMaterial);
                _primers[i] = primer.GetComponent<Renderer>();
            }
            _carrier = new GameObject("Speedloader").transform;
            _carrier.SetParent(_loader, false);
            var rear = new Vector3(_bounds.min.x - diameter * 0.25f, _bounds.center.y, _bounds.center.z);
            float plateDiameter = radius * 2f + diameter * 1.2f;
            CreatePart("LoaderPlate", _carrier, rear, new Vector3(plateDiameter, diameter * 0.16f, plateDiameter), _carrierMaterial);
            _knobLocal = rear + Vector3.left * diameter * 0.6f;
            CreatePart("ReleaseKnob", _carrier, _knobLocal,
                new Vector3(diameter * 0.8f, diameter * 0.5f, diameter * 0.8f), _carrierMaterial);
            return true;
        }

        private GameObject CreatePart(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PrimitiveType primitive = PrimitiveType.Cylinder)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.layer = gameObject.layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = primitive == PrimitiveType.Cylinder ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part;
        }

        private void OnDisable()
        {
            if (_loader != null) _loader.gameObject.SetActive(false);
            if (_hasCylinderPose)
            {
                _baseRotation = _closedRotation;
                _basePosition = _closedCenter - _closedRotation * _bounds.center;
                _cylinder.SetLocalPositionAndRotation(_basePosition, _baseRotation);
                CylinderIndexAngle = 0f;
            }
        }
        private void OnDestroy()
        {
            if (_loader != null) Destroy(_loader.gameObject);
            if (_caseMaterial != null) Destroy(_caseMaterial);
            if (_primerMaterial != null) Destroy(_primerMaterial);
            if (_carrierMaterial != null) Destroy(_carrierMaterial);
            if (_tipMaterial != null) Destroy(_tipMaterial);
        }
    }
}
