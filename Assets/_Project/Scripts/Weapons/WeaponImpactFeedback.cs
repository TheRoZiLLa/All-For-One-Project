using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Recorded.Weapons
{
    /// <summary>
    /// Handles high-impact Sci-Fi visual feedback for Spectrum Gun shots:
    /// - Crisp barrel-tip muzzle flash light & rotating star flare
    /// - Dual-layer energy beam (high-luminance inner core + animated outer helical energy sheath) + high-speed bolt
    /// - Hollow expanding surface shockwave ring + radial energy sparks tinted 100% by ammo color
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

        private WaitForSeconds _muzzleWait;
        private LineRenderer _tracerLine;
        private LineRenderer _outerSheathLine;
        private GameObject _projectileBolt;
        private Renderer _projectileBoltRenderer;
        private GameObject _muzzleFlareVisual;
        private Renderer _muzzleFlareRenderer;
        private Coroutine _muzzleCoroutine;
        private Coroutine _tracerCoroutine;

        private readonly Queue<GameObject> _impactPool = new Queue<GameObject>();
        private const int InitialPoolSize = 8;
        private const int ImpactRingSegments = 24;
        private const int SheathSegments = 16;

        private void Awake()
        {
            _muzzleWait = new WaitForSeconds(muzzleFlashDuration);

            InitializeTracerAndBolt();
            InitializeImpactPool();
            ResolveMuzzleLight();

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
                        _muzzleFlareVisual.transform.SetParent(mp, false);
                        _muzzleFlareVisual.transform.localPosition = Vector3.zero;
                        _muzzleFlareVisual.transform.localScale = new Vector3(0.042f, 0.042f, 0.075f);

                        Collider col = _muzzleFlareVisual.GetComponent<Collider>();
                        if (col != null)
                        {
                            if (Application.isPlaying) Destroy(col);
                            else DestroyImmediate(col);
                        }

                        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                        if (unlit == null) unlit = Shader.Find("Sprites/Default");
                        Material flareMat = new Material(unlit) { color = tracerColor };
                        _muzzleFlareRenderer = _muzzleFlareVisual.GetComponent<Renderer>();
                        if (_muzzleFlareRenderer != null)
                        {
                            _muzzleFlareRenderer.sharedMaterial = flareMat;
                            _muzzleFlareRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            _muzzleFlareRenderer.receiveShadows = false;
                        }
                    }
                    _muzzleFlareVisual.SetActive(false);
                }
            }
        }

        private void InitializeTracerAndBolt()
        {
            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null) lineShader = Shader.Find("Sprites/Default");

            // 1. Inner Core Beam
            Transform existingLine = transform.Find("WeaponTracer_Line");
            GameObject tracerObj = existingLine != null ? existingLine.gameObject : new GameObject("WeaponTracer_Line");
            if (existingLine == null)
            {
                tracerObj.transform.SetParent(transform, false);
            }

            _tracerLine = tracerObj.GetComponent<LineRenderer>();
            if (_tracerLine == null) _tracerLine = tracerObj.AddComponent<LineRenderer>();
            _tracerLine.startWidth = tracerWidth;
            _tracerLine.endWidth = tracerWidth * 0.45f;
            _tracerLine.positionCount = 2;
            _tracerLine.useWorldSpace = true;
            _tracerLine.numCapVertices = 4;
            _tracerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerLine.receiveShadows = false;
            _tracerLine.sharedMaterial = new Material(lineShader) { color = tracerColor };
            _tracerLine.enabled = false;

            // 2. Outer Helical Energy Sheath
            Transform existingSheath = transform.Find("WeaponTracer_HelixSheath");
            GameObject sheathObj = existingSheath != null ? existingSheath.gameObject : new GameObject("WeaponTracer_HelixSheath");
            if (existingSheath == null)
            {
                sheathObj.transform.SetParent(transform, false);
            }

            _outerSheathLine = sheathObj.GetComponent<LineRenderer>();
            if (_outerSheathLine == null) _outerSheathLine = sheathObj.AddComponent<LineRenderer>();
            _outerSheathLine.startWidth = tracerWidth * 0.55f;
            _outerSheathLine.endWidth = tracerWidth * 0.2f;
            _outerSheathLine.positionCount = SheathSegments;
            _outerSheathLine.useWorldSpace = true;
            _outerSheathLine.numCapVertices = 3;
            _outerSheathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outerSheathLine.receiveShadows = false;
            _outerSheathLine.sharedMaterial = new Material(lineShader) { color = tracerColor };
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
                _projectileBolt.transform.SetParent(transform, false);
            }
            _projectileBolt.transform.localScale = new Vector3(0.045f, 0.045f, 0.22f);

            Collider boltCol = _projectileBolt.GetComponent<Collider>();
            if (boltCol != null) boltCol.enabled = false;

            Material boltMat = new Material(lineShader) { color = tracerColor };
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

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
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

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = ImpactRingSegments;
            ring.startWidth = 0.045f;
            ring.endWidth = 0.045f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            if (ring.sharedMaterial == null)
            {
                ring.sharedMaterial = new Material(unlitShader) { color = impactColor };
            }

            for (int i = 0; i < ImpactRingSegments; i++)
            {
                float angle = (i / (float)ImpactRingSegments) * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
        }

        private GameObject CreateDefaultImpactObject()
        {
            if (impactPrefab != null)
            {
                return Instantiate(impactPrefab, transform);
            }

            GameObject impactObj = new GameObject("ImpactEffect_Procedural");
            impactObj.transform.SetParent(transform, false);

            Light impactLight = impactObj.AddComponent<Light>();
            impactLight.type = LightType.Point;
            impactLight.range = 4.2f;
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

            yield return _muzzleWait;

            if (muzzleLight != null) muzzleLight.enabled = false;
            if (_muzzleFlareVisual != null) _muzzleFlareVisual.SetActive(false);
            _muzzleCoroutine = null;
        }

        private void PlayTracer(Vector3 from, Vector3 to)
        {
            if (_tracerLine == null) return;
            if (!Application.isPlaying || !isActiveAndEnabled) return;

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
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Animate spiral energy sheath around the main laser core
                if (_outerSheathLine != null)
                {
                    float phase = elapsed * 45f;
                    float spiralRadius = Mathf.Lerp(0.045f, 0.005f, t);
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
                    float boltTravel = Mathf.Clamp01((elapsed * projectileBoltSpeed) / Mathf.Max(0.5f, dist));
                    _projectileBolt.transform.position = Vector3.Lerp(from, to, boltTravel);
                    if (boltTravel >= 1f)
                    {
                        _projectileBolt.SetActive(false);
                    }
                }

                float widthScale = 1f - (t * 0.78f);
                _tracerLine.startWidth = tracerWidth * widthScale;
                _tracerLine.endWidth = tracerWidth * 0.45f * widthScale;

                yield return null;
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
            impactObj.transform.position = point + (normal * 0.03f);
            if (normal.sqrMagnitude > 0.001f)
            {
                impactObj.transform.rotation = Quaternion.LookRotation(normal);
            }

            Light l = impactObj.GetComponent<Light>();
            if (l != null)
            {
                l.color = impactColor;
                l.intensity = 5.5f;
            }

            LineRenderer ring = impactObj.GetComponent<LineRenderer>();
            if (ring != null && ring.sharedMaterial != null)
            {
                ring.sharedMaterial.color = impactColor;
            }

            impactObj.SetActive(true);
            StartCoroutine(RecycleImpactRoutine(impactObj, ring, l));
        }

        private IEnumerator RecycleImpactRoutine(GameObject impactObj, LineRenderer ring, Light impactLight)
        {
            float elapsed = 0f;

            while (elapsed < impactDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / impactDuration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                // Expand hollow shock ring outward so the target's RGB surface color remains 100% visible in the center
                float radius = Mathf.Lerp(0.08f, 0.65f, easeOut);
                impactObj.transform.localScale = new Vector3(radius, radius, 1f);

                if (ring != null)
                {
                    ring.widthMultiplier = Mathf.Lerp(1.0f, 0.1f, t);
                }
                if (impactLight != null)
                {
                    impactLight.intensity = Mathf.Lerp(5.5f, 0f, t);
                }
                yield return null;
            }

            impactObj.SetActive(false);
            _impactPool.Enqueue(impactObj);
        }

        public void SetMuzzleLight(Light light)
        {
            muzzleLight = light;
            if (muzzleLight != null)
            {
                muzzleLight.color = tracerColor;
            }
        }

        public void SetFeedbackColor(Color color)
        {
            tracerColor = color;
            impactColor = color;

            if (_tracerLine != null && _tracerLine.sharedMaterial != null)
            {
                _tracerLine.sharedMaterial.color = color;
            }

            if (_outerSheathLine != null && _outerSheathLine.sharedMaterial != null)
            {
                _outerSheathLine.sharedMaterial.color = color;
            }

            if (_projectileBoltRenderer != null && _projectileBoltRenderer.sharedMaterial != null)
            {
                _projectileBoltRenderer.sharedMaterial.color = color;
            }

            if (_muzzleFlareRenderer != null && _muzzleFlareRenderer.sharedMaterial != null)
            {
                _muzzleFlareRenderer.sharedMaterial.color = color;
            }

            if (muzzleLight != null)
            {
                muzzleLight.color = color;
            }
        }
    }
}
