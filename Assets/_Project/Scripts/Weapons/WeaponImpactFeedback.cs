using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Recorded.Weapons
{
    /// <summary>
    /// Handles high-impact Sci-Fi visual feedback for Spectrum Gun shots:
    /// - Crisp barrel-tip muzzle flash light & collapsing six-blade crown
    /// - Dual-layer energy beam (high-luminance inner core + animated outer helical energy sheath) + high-speed bolt
    /// - Two hollow surface shock rings, a short star burst and world-space sparks in the fired ammo color
    ///   (never occludes the target's RGB mixed surface color!)
    /// </summary>
    public class WeaponImpactFeedback : MonoBehaviour
    {
        [Header("Muzzle Flash")]
        [SerializeField] private Light muzzleLight;
        [SerializeField] private float muzzleFlashDuration = 0.06f;

        [Header("Projectile Bolt & Dual-Layer Beam Tracer")]
        [SerializeField] private Color tracerColor = new Color(0.2f, 0.88f, 1f, 0.95f);
        [SerializeField] private float tracerDuration = 0.11f;
        [SerializeField] private float tracerWidth = 0.028f;
        [SerializeField] private float projectileBoltSpeed = 140f;

        [Header("Impact Shock Ring Effect")]
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private Color impactColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private float impactDuration = 0.28f;
        [SerializeField] private Material energyMaterial;

        private Material _burstMaterial;
        private Mesh _muzzleMesh;
        private float _muzzleBaseIntensity = 1f;
        private LineRenderer _tracerLine;
        private LineRenderer _outerSheathLine;
        private GameObject _projectileBolt;
        private Renderer _projectileBoltRenderer;
        private GameObject _muzzleFlareVisual;
        private Renderer _muzzleFlareRenderer;
        private Coroutine _muzzleCoroutine;
        private Coroutine _tracerCoroutine;
        private Transform _worldEffects;
        private readonly List<Material> _ownedMaterials = new List<Material>();

        private readonly Queue<GameObject> _impactPool = new Queue<GameObject>();
        private const int InitialPoolSize = 8;
        private const int ImpactRingSegments = 24;
        private const int SheathSegments = 16;

        private void Awake()
        {
            // World hits must not inherit hand-bone scale or follow the moving viewmodel.
            _worldEffects = new GameObject("WeaponWorldEffects").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_worldEffects.gameObject, gameObject.scene);
            _burstMaterial = CreateEnergyMaterial(new Color(2.5f, 2.5f, 2.5f, 1f));

            InitializeTracerAndBolt();
            InitializeImpactPool();
            ResolveMuzzleLight();
            if (muzzleLight != null) _muzzleBaseIntensity = muzzleLight.intensity;

            if (muzzleLight != null)
            {
                muzzleLight.enabled = false;
            }
        }

        private void ResolveMuzzleLight()
        {
            if (muzzleLight == null)
            {
                Transform mp = transform.Find("MuzzlePoint");
                if (mp != null)
                {
                    muzzleLight = mp.GetComponent<Light>();
                }
            }

            if (_muzzleFlareVisual == null)
            {
                Transform mp = muzzleLight != null ? muzzleLight.transform : transform.Find("MuzzlePoint");
                if (mp != null)
                {
                    Transform existingFlare = mp.Find("MuzzleFlare_Visual");
                    if (existingFlare != null)
                    {
                        _muzzleFlareVisual = existingFlare.gameObject;
                        _muzzleFlareRenderer = _muzzleFlareVisual.GetComponent<Renderer>();
                    }
                    else
                    {
                        _muzzleFlareVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        _muzzleFlareVisual.name = "MuzzleFlare_Visual";
                        _muzzleFlareVisual.layer = mp.gameObject.layer;
                        _muzzleFlareVisual.transform.SetParent(mp, false);
                        _muzzleFlareVisual.transform.localPosition = Vector3.zero;
                        _muzzleFlareVisual.transform.localScale = new Vector3(0.042f, 0.042f, 0.075f);

                        Collider col = _muzzleFlareVisual.GetComponent<Collider>();
                        if (col != null)
                        {
                            col.enabled = false;
                            if (Application.isPlaying) Destroy(col);
                            else DestroyImmediate(col);
                        }

                        Material flareMat = CreateEnergyMaterial(tracerColor);
                        _muzzleFlareRenderer = _muzzleFlareVisual.GetComponent<Renderer>();
                        if (_muzzleFlareRenderer != null)
                        {
                            _muzzleFlareRenderer.sharedMaterial = flareMat;
                            _muzzleFlareRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            _muzzleFlareRenderer.receiveShadows = false;
                        }
                    }
                    if (_muzzleFlareRenderer != null && !_ownedMaterials.Contains(_muzzleFlareRenderer.sharedMaterial))
                        _muzzleFlareRenderer.sharedMaterial = CreateEnergyMaterial(tracerColor);
                    var filter = _muzzleFlareVisual.GetComponent<MeshFilter>();
                    if (filter != null)
                    {
                        if (_muzzleMesh == null) _muzzleMesh = CreateMuzzleMesh();
                        filter.sharedMesh = _muzzleMesh;
                        _muzzleFlareVisual.transform.localScale = Vector3.one;
                    }
                    _muzzleFlareVisual.SetActive(false);
                }
            }
        }

        private Material CreateEnergyMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var material = energyMaterial != null ? new Material(energyMaterial) : new Material(shader);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.color = color;
            _ownedMaterials.Add(material);
            return material;
        }

        private Mesh CreateMuzzleMesh()
        {
            var vertices = new Vector3[18];
            var colors = new Color[18];
            var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                int v = i * 3;
                vertices[v] = Vector3.zero;
                vertices[v + 1] = new Vector3(Mathf.Cos(angle) * 0.055f, Mathf.Sin(angle) * 0.055f, 0.035f);
                vertices[v + 2] = Vector3.forward * 0.16f;
                colors[v] = Color.white;
                colors[v + 1] = colors[v + 2] = new Color(1f, 1f, 1f, 0f);
                for (int j = 0; j < 3; j++) triangles[v + j] = v + j;
            }
            var mesh = new Mesh { name = "Spectrum muzzle crown", vertices = vertices, colors = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void InitializeTracerAndBolt()
        {
            // 1. Inner Core Beam
            Transform existingLine = transform.Find("WeaponTracer_Line");
            GameObject tracerObj = existingLine != null ? existingLine.gameObject : new GameObject("WeaponTracer_Line");
            tracerObj.transform.SetParent(_worldEffects, false);

            _tracerLine = tracerObj.GetComponent<LineRenderer>();
            if (_tracerLine == null) _tracerLine = tracerObj.AddComponent<LineRenderer>();
            _tracerLine.startWidth = tracerWidth;
            _tracerLine.endWidth = tracerWidth * 0.45f;
            _tracerLine.positionCount = 2;
            _tracerLine.useWorldSpace = true;
            _tracerLine.numCapVertices = 4;
            _tracerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerLine.receiveShadows = false;


            Material lineMat = CreateEnergyMaterial(tracerColor);
            _tracerLine.sharedMaterial = lineMat;
            _tracerLine.enabled = false;

            // 2. Outer Helical Energy Sheath
            Transform existingSheath = transform.Find("WeaponTracer_HelixSheath");
            GameObject sheathObj = existingSheath != null ? existingSheath.gameObject : new GameObject("WeaponTracer_HelixSheath");
            sheathObj.transform.SetParent(_worldEffects, false);
            _outerSheathLine = sheathObj.GetComponent<LineRenderer>();
            if (_outerSheathLine == null) _outerSheathLine = sheathObj.AddComponent<LineRenderer>();
            _outerSheathLine.startWidth = tracerWidth * 0.55f;
            _outerSheathLine.endWidth = tracerWidth * 0.2f;
            _outerSheathLine.positionCount = SheathSegments;
            _outerSheathLine.useWorldSpace = true;
            _outerSheathLine.numCapVertices = 3;
            _outerSheathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outerSheathLine.receiveShadows = false;
            var sheathMaterial = CreateEnergyMaterial(tracerColor);
            _outerSheathLine.sharedMaterial = sheathMaterial;
            _outerSheathLine.enabled = false;

            // 3. High-speed visible energy bolt
            Transform existingBolt = transform.Find("WeaponProjectile_Bolt");
            if (existingBolt != null)
            {
                _projectileBolt = existingBolt.gameObject;
            }
            else
            {
                _projectileBolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _projectileBolt.name = "WeaponProjectile_Bolt";
            }
            _projectileBolt.transform.SetParent(_worldEffects, false);
            _projectileBolt.transform.localScale = new Vector3(0.045f, 0.045f, 0.22f);

            Collider boltCol = _projectileBolt.GetComponent<Collider>();
            if (boltCol != null) boltCol.enabled = false;

            Material boltMat = CreateEnergyMaterial(tracerColor);
            _projectileBoltRenderer = _projectileBolt.GetComponent<Renderer>();
            if (_projectileBoltRenderer != null)
            {
                _projectileBoltRenderer.sharedMaterial = boltMat;
                _projectileBoltRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _projectileBoltRenderer.receiveShadows = false;
            }
            _projectileBolt.SetActive(false);
        }

        private void InitializeImpactPool()
        {
            _impactPool.Clear();

            // Reuse any existing ImpactEffect_Procedural children (safe inside Prefab instances)
            for (int i = 0; i < _worldEffects.childCount; i++)
            {
                Transform child = _worldEffects.GetChild(i);
                if (child.name.StartsWith("ImpactEffect_Procedural"))
                {
                    EnsureImpactShockRingSetup(child.gameObject);
                    child.gameObject.SetActive(false);
                    _impactPool.Enqueue(child.gameObject);
                }
            }

            while (_impactPool.Count < InitialPoolSize)
            {
                GameObject impact = CreateDefaultImpactObject();
                impact.SetActive(false);
                _impactPool.Enqueue(impact);
            }
        }

        private void EnsureImpactShockRingSetup(GameObject impactObj)
        {
            // Hide any legacy opaque sphere child so target RGB surface color is never occluded
            for (int i = 0; i < impactObj.transform.childCount; i++)
            {
                Transform c = impactObj.transform.GetChild(i);
                if (c.GetComponent<MeshRenderer>() != null)
                {
                    c.gameObject.SetActive(false);
                }
            }

            LineRenderer ring = impactObj.GetComponent<LineRenderer>();
            if (ring == null)
            {
                ring = impactObj.AddComponent<LineRenderer>();
            }

            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = ImpactRingSegments;
            ring.startWidth = 0.045f;
            ring.endWidth = 0.045f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            if (ring.sharedMaterial == null)
            {
                var ringMaterial = CreateEnergyMaterial(impactColor);
                ring.sharedMaterial = ringMaterial;
            }

            for (int i = 0; i < ImpactRingSegments; i++)
            {
                float angle = (i / (float)ImpactRingSegments) * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
            var corona = CreateImpactLine(impactObj.transform, "ImpactCorona", ImpactRingSegments, 0.018f, true);
            for (int i = 0; i < ImpactRingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ImpactRingSegments;
                corona.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.45f, Mathf.Sin(angle) * 1.45f, 0.008f));
            }
            var star = CreateImpactLine(impactObj.transform, "ImpactBurstStar", 25, 0.035f, false);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6f;
                float length = i % 3 == 0 ? 1.7f : 0.85f;
                star.SetPosition(i * 2, new Vector3(0f, 0f, 0.015f));
                star.SetPosition(i * 2 + 1, new Vector3(Mathf.Cos(angle) * length, Mathf.Sin(angle) * length, 0.015f));
            }
            star.SetPosition(24, new Vector3(0f, 0f, 0.015f));
            EnsureImpactSparks(impactObj.transform);
        }

        private LineRenderer CreateImpactLine(Transform parent, string name, int points, float width, bool loop)
        {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            var line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = _burstMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void EnsureImpactSparks(Transform parent)
        {
            var child = parent.Find("ImpactSparks");
            if (child != null) { child.GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return; }
            child = new GameObject("ImpactSparks").transform;
            child.SetParent(parent, false);
            var sparks = child.gameObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.032f);
            main.gravityModifier = 0.7f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = sparks.emission;
            emission.enabled = false;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 72f;
            shape.radius = 0.012f;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            var color = sparks.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.5f;
            renderer.velocityScale = 0.06f;
            renderer.sharedMaterial = _burstMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private GameObject CreateDefaultImpactObject()
        {
            if (impactPrefab != null)
            {
                return Instantiate(impactPrefab, _worldEffects);
            }

            GameObject impactObj = new GameObject("ImpactEffect_Procedural");
            impactObj.transform.SetParent(_worldEffects, false);

            Light impactLight = impactObj.AddComponent<Light>();
            impactLight.type = LightType.Point;
            impactLight.range = 2.5f;
            impactLight.color = impactColor;
            impactLight.intensity = 5.5f;

            EnsureImpactShockRingSetup(impactObj);
            return impactObj;
        }

        public void PlayHitFeedback(Vector3 muzzlePosition, RaycastHit hit)
        {
            PlayMuzzleFlash();
            PlayTracer(muzzlePosition, hit.point);
            PlayImpact(hit.point, hit.normal);
        }

        public void PlayMissFeedback(Vector3 muzzlePosition, Vector3 targetPoint)
        {
            PlayMuzzleFlash();
            PlayTracer(muzzlePosition, targetPoint);
        }

        private void PlayMuzzleFlash()
        {
            ResolveMuzzleLight();
            if (muzzleLight == null && _muzzleFlareVisual == null) return;
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            if (muzzleLight != null) muzzleLight.color = tracerColor;
            if (_muzzleFlareRenderer != null) _muzzleFlareRenderer.sharedMaterial.color = Color.Lerp(tracerColor, Color.white, 0.25f) * new Color(2f, 2f, 2f, 1f);
            if (_muzzleCoroutine != null) StopCoroutine(_muzzleCoroutine);
            _muzzleCoroutine = StartCoroutine(MuzzleFlashRoutine());
        }

        private IEnumerator MuzzleFlashRoutine()
        {
            if (muzzleLight != null) muzzleLight.enabled = true;
            if (_muzzleFlareVisual != null)
            {
                _muzzleFlareVisual.transform.Rotate(0f, 0f, 45f);
                _muzzleFlareVisual.SetActive(true);
            }

            float elapsed = 0f, duration = Mathf.Max(0.02f, muzzleFlashDuration);
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                if (muzzleLight != null) muzzleLight.intensity = Mathf.Lerp(Mathf.Max(8f, _muzzleBaseIntensity), 0f, t * t);
                if (_muzzleFlareVisual != null) _muzzleFlareVisual.transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.25f, t);
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (muzzleLight != null) muzzleLight.enabled = false;
            if (muzzleLight != null) muzzleLight.intensity = _muzzleBaseIntensity;
            if (_muzzleFlareVisual != null) _muzzleFlareVisual.SetActive(false);
            _muzzleCoroutine = null;
        }

        private void PlayTracer(Vector3 from, Vector3 to)
        {
            if (_tracerLine == null) return;
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            _tracerLine.sharedMaterial.color = tracerColor;
            if (_outerSheathLine != null) _outerSheathLine.sharedMaterial.color = tracerColor;
            if (_projectileBoltRenderer != null) _projectileBoltRenderer.sharedMaterial.color = tracerColor * new Color(3f, 3f, 3f, 1f);
            if (_tracerCoroutine != null) StopCoroutine(_tracerCoroutine);
            _tracerCoroutine = StartCoroutine(TracerRoutine(from, to));
        }

        private IEnumerator TracerRoutine(Vector3 from, Vector3 to)
        {
            Vector3 dir = (to - from);
            float dist = dir.magnitude;
            Vector3 normDir = dist > 0.001f ? (dir / dist) : transform.forward;

            Vector3 upRef = Mathf.Abs(Vector3.Dot(normDir, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(normDir, upRef).normalized;
            Vector3 up = Vector3.Cross(right, normDir).normalized;

            _tracerLine.SetPosition(0, from);
            _tracerLine.SetPosition(1, to);
            _tracerLine.startWidth = tracerWidth;
            _tracerLine.endWidth = tracerWidth * 0.45f;
            _tracerLine.enabled = true;

            if (_outerSheathLine != null)
            {
                _outerSheathLine.widthMultiplier = 1f;
                _outerSheathLine.enabled = true;
            }

            if (_projectileBolt != null)
            {
                _projectileBolt.transform.position = from;
                _projectileBolt.transform.rotation = Quaternion.LookRotation(normDir);
                _projectileBolt.SetActive(true);
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.04f, tracerDuration);

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);

                // Animate spiral energy sheath around the main laser core
                if (_outerSheathLine != null)
                {
                    float phase = elapsed * 45f;
                    float spiralRadius = Mathf.Lerp(0.022f, 0.002f, t);
                    for (int i = 0; i < SheathSegments; i++)
                    {
                        float st = i / (float)(SheathSegments - 1);
                        Vector3 basePos = Vector3.Lerp(from, to, st);
                        float envelope = Mathf.Sin(st * Mathf.PI);
                        float angle = (st * Mathf.PI * 6f) - phase;
                        Vector3 offset = ((right * Mathf.Cos(angle)) + (up * Mathf.Sin(angle))) * (spiralRadius * envelope);
                        _outerSheathLine.SetPosition(i, basePos + offset);
                    }
                    _outerSheathLine.widthMultiplier = 1f - (t * 0.8f);
                }

                if (_projectileBolt != null)
                {
                    float travelTime = Mathf.Clamp(dist / Mathf.Max(1f, projectileBoltSpeed), 0.015f, duration * 0.75f);
                    float boltTravel = Mathf.Clamp01(elapsed / travelTime);
                    _projectileBolt.transform.position = Vector3.Lerp(from, to, boltTravel);
                    if (boltTravel >= 1f)
                    {
                        _projectileBolt.SetActive(false);
                    }
                }

                float widthScale = 1f - (t * 0.78f);
                _tracerLine.startWidth = tracerWidth * widthScale;
                _tracerLine.endWidth = tracerWidth * 0.45f * widthScale;
                float alpha = (1f - t) * (1f - t);
                _tracerLine.startColor = _tracerLine.endColor = new Color(3.5f, 3.5f, 3.5f, alpha);
                if (_outerSheathLine != null) _outerSheathLine.startColor = _outerSheathLine.endColor = new Color(1.5f, 1.5f, 1.5f, alpha * 0.65f);

                yield return null;
                elapsed += Time.deltaTime;
            }

            if (_projectileBolt != null) _projectileBolt.SetActive(false);
            if (_outerSheathLine != null) _outerSheathLine.enabled = false;
            _tracerLine.enabled = false;
            _tracerCoroutine = null;
        }

        private void PlayImpact(Vector3 point, Vector3 normal)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            GameObject impactObj = _impactPool.Count > 0 ? _impactPool.Dequeue() : CreateDefaultImpactObject();
            impactObj.transform.localScale = Vector3.one;
            impactObj.transform.position = point + (normal * 0.03f);
            if (normal.sqrMagnitude > 0.001f)
            {
                impactObj.transform.rotation = Quaternion.LookRotation(normal);
            }

            Light l = impactObj.GetComponent<Light>();
            if (l != null)
            {
                l.color = impactColor;
                l.intensity = 8f;
            }

            LineRenderer ring = impactObj.GetComponent<LineRenderer>();
            if (ring != null && ring.sharedMaterial != null)
            {
                ring.sharedMaterial.color = impactColor;
            }

            impactObj.SetActive(true);
            var star = impactObj.transform.Find("ImpactBurstStar")?.GetComponent<LineRenderer>();
            var corona = impactObj.transform.Find("ImpactCorona")?.GetComponent<LineRenderer>();
            if (star != null) { star.enabled = true; star.startColor = star.endColor = Color.Lerp(impactColor, Color.white, 0.65f); }
            if (corona != null) corona.startColor = corona.endColor = impactColor;
            var sparks = impactObj.transform.Find("ImpactSparks")?.GetComponent<ParticleSystem>();
            if (sparks != null)
            {
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                sparks.Play();
                sparks.Emit(new ParticleSystem.EmitParams { startColor = impactColor }, 14);
            }
            StartCoroutine(RecycleImpactRoutine(impactObj, ring, l));
        }

        private IEnumerator RecycleImpactRoutine(GameObject impactObj, LineRenderer ring, Light impactLight)
        {
            float elapsed = 0f;
            var corona = impactObj.transform.Find("ImpactCorona")?.GetComponent<LineRenderer>();
            var star = impactObj.transform.Find("ImpactBurstStar")?.GetComponent<LineRenderer>();
            var sparks = impactObj.transform.Find("ImpactSparks")?.GetComponent<ParticleSystem>();
            Color starColor = star != null ? star.startColor : Color.white;
            Color coronaColor = corona != null ? corona.startColor : Color.white;

            while (elapsed < impactDuration)
            {
                float t = Mathf.Clamp01(elapsed / impactDuration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                // Expand hollow shock ring outward so the target's RGB surface color remains 100% visible in the center
                float radius = Mathf.Lerp(0.06f, 0.5f, easeOut);
                impactObj.transform.localScale = new Vector3(radius, radius, 1f);

                if (ring != null)
                {
                    ring.widthMultiplier = Mathf.Lerp(1.0f, 0.1f, t);
                    ring.startColor = ring.endColor = new Color(1.6f, 1.6f, 1.6f, (1f - t) * (1f - t));
                }
                if (corona != null) { coronaColor.a = (1f - t) * 0.6f; corona.startColor = corona.endColor = coronaColor; }
                if (star != null) { star.enabled = elapsed < 0.075f; starColor.a = 1f - Mathf.Clamp01(elapsed / 0.075f); star.startColor = star.endColor = starColor; }
                if (impactLight != null)
                {
                    impactLight.intensity = 8f * Mathf.Exp(-elapsed * 35f);
                }
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            impactObj.SetActive(false);
            _impactPool.Enqueue(impactObj);
        }

        public void SetMuzzleLight(Light light)
        {
            muzzleLight = light;
            if (muzzleLight != null) _muzzleBaseIntensity = muzzleLight.intensity;
            if (muzzleLight != null)
            {
                muzzleLight.color = tracerColor;
            }
        }

        public void SetFeedbackColor(Color color)
        {
            tracerColor = color;
            impactColor = color;
            // Capture colors when firing so advancing the cylinder cannot recolor a shot in flight.
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _muzzleCoroutine = _tracerCoroutine = null;
            if (muzzleLight != null) muzzleLight.enabled = false;
            if (muzzleLight != null) muzzleLight.intensity = _muzzleBaseIntensity;
            if (_muzzleFlareVisual != null) _muzzleFlareVisual.SetActive(false);
            if (_tracerLine != null) _tracerLine.enabled = false;
            if (_outerSheathLine != null) _outerSheathLine.enabled = false;
            if (_projectileBolt != null) _projectileBolt.SetActive(false);
            if (_worldEffects != null) InitializeImpactPool();
        }

        private void OnDestroy()
        {
            if (_worldEffects != null) Destroy(_worldEffects.gameObject);
            if (_muzzleMesh != null) Destroy(_muzzleMesh);
            foreach (var material in _ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
