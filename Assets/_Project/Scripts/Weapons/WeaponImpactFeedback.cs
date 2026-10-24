using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Recorded.Weapons
{
    /// <summary>
    /// Handles visual feedback for Spectrum Gun shots:
    /// - Crisp barrel-tip muzzle flash light & glowing muzzle flare disc
    /// - High-speed energy projectile bolt + fading beam tracer originating strictly from MuzzlePoint
    /// - Surface impact flash & expanding energy ring at hit point
    /// Uses lightweight pooling to avoid runtime GC allocations.
    /// </summary>
    public class WeaponImpactFeedback : MonoBehaviour
    {
        [Header("Muzzle Flash")]
        [SerializeField] private Light muzzleLight;
        [SerializeField] private float muzzleFlashDuration = 0.06f;

        [Header("Projectile Bolt & Beam Tracer")]
        [SerializeField] private Color tracerColor = new Color(0.2f, 0.88f, 1f, 0.95f); // Sci-Fi Cyan
        [SerializeField] private float tracerDuration = 0.08f;
        [SerializeField] private float tracerWidth = 0.022f;
        [SerializeField] private float projectileBoltSpeed = 140f;

        [Header("Impact Effect")]
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private Color impactColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private float impactDuration = 0.35f;

        private WaitForSeconds _muzzleWait;
        private LineRenderer _tracerLine;
        private GameObject _projectileBolt;
        private Renderer _projectileBoltRenderer;
        private GameObject _muzzleFlareVisual;
        private Renderer _muzzleFlareRenderer;
        private Coroutine _muzzleCoroutine;
        private Coroutine _tracerCoroutine;

        // Simple Object Pool for impact visual markers
        private readonly Queue<GameObject> _impactPool = new Queue<GameObject>();
        private const int InitialPoolSize = 8;

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
                        _muzzleFlareVisual.transform.localScale = new Vector3(0.032f, 0.032f, 0.055f);

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
            Transform existingLine = transform.Find("WeaponTracer_Line");
            GameObject tracerObj = existingLine != null ? existingLine.gameObject : new GameObject("WeaponTracer_Line");
            if (existingLine == null)
            {
                tracerObj.transform.SetParent(transform, false);
            }

            _tracerLine = tracerObj.GetComponent<LineRenderer>();
            if (_tracerLine == null)
            {
                _tracerLine = tracerObj.AddComponent<LineRenderer>();
            }
            _tracerLine.startWidth = tracerWidth;
            _tracerLine.endWidth = tracerWidth * 0.35f;
            _tracerLine.positionCount = 2;
            _tracerLine.useWorldSpace = true;
            _tracerLine.numCapVertices = 4;
            _tracerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerLine.receiveShadows = false;

            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null) lineShader = Shader.Find("Sprites/Default");

            Material lineMat = new Material(lineShader) { color = tracerColor };
            _tracerLine.sharedMaterial = lineMat;
            _tracerLine.enabled = false;

            // High-speed visible energy bolt sphere/capsule that travels from barrel tip to target
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
            _projectileBolt.transform.localScale = new Vector3(0.035f, 0.035f, 0.18f);

            Collider boltCol = _projectileBolt.GetComponent<Collider>();
            if (boltCol != null)
            {
                boltCol.enabled = false;
            }

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

            // Reuse any existing ImpactEffect_Procedural children (safe inside Prefab instances)
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("ImpactEffect_Procedural"))
                {
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
            impactLight.range = 3.5f;
            impactLight.color = impactColor;
            impactLight.intensity = 4f;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.transform.SetParent(impactObj.transform, false);
            core.transform.localScale = Vector3.one * 0.14f;

            Collider col = core.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
            Material mat = new Material(unlitShader) { color = impactColor };

            Renderer rend = core.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            return impactObj;
        }

        /// <summary>
        /// Trigger muzzle flash, projectile bolt, tracer, and impact feedback when a hit occurs.
        /// </summary>
        public void PlayHitFeedback(Vector3 muzzlePosition, RaycastHit hit)
        {
            PlayMuzzleFlash();
            PlayTracer(muzzlePosition, hit.point);
            PlayImpact(hit.point, hit.normal);
        }

        /// <summary>
        /// Trigger muzzle flash, projectile bolt, and tracer when the shot misses.
        /// </summary>
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
            if (_muzzleFlareVisual != null) _muzzleFlareVisual.SetActive(true);

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

            _tracerLine.SetPosition(0, from);
            _tracerLine.SetPosition(1, to);
            _tracerLine.startWidth = tracerWidth;
            _tracerLine.endWidth = tracerWidth * 0.35f;
            _tracerLine.enabled = true;

            if (_projectileBolt != null)
            {
                _projectileBolt.transform.position = from;
                _projectileBolt.transform.rotation = Quaternion.LookRotation(normDir);
                _projectileBolt.SetActive(true);
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.03f, tracerDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Animate high-speed bolt along the ray path
                if (_projectileBolt != null)
                {
                    float boltTravel = Mathf.Clamp01((elapsed * projectileBoltSpeed) / Mathf.Max(0.5f, dist));
                    _projectileBolt.transform.position = Vector3.Lerp(from, to, boltTravel);
                    if (boltTravel >= 1f)
                    {
                        _projectileBolt.SetActive(false);
                    }
                }

                // Smoothly taper beam width as it dissipates
                float widthScale = 1f - (t * 0.75f);
                _tracerLine.startWidth = tracerWidth * widthScale;
                _tracerLine.endWidth = tracerWidth * 0.35f * widthScale;

                yield return null;
            }

            if (_projectileBolt != null) _projectileBolt.SetActive(false);
            _tracerLine.enabled = false;
            _tracerCoroutine = null;
        }

        private void PlayImpact(Vector3 point, Vector3 normal)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            GameObject impactObj = _impactPool.Count > 0 ? _impactPool.Dequeue() : CreateDefaultImpactObject();
            impactObj.transform.position = point + (normal * 0.04f);
            if (normal.sqrMagnitude > 0.001f)
            {
                impactObj.transform.rotation = Quaternion.LookRotation(normal);
            }

            Light l = impactObj.GetComponent<Light>();
            if (l != null) l.color = impactColor;
            Renderer r = impactObj.GetComponentInChildren<Renderer>();
            if (r != null && r.sharedMaterial != null) r.sharedMaterial.color = impactColor;

            impactObj.SetActive(true);
            StartCoroutine(RecycleImpactRoutine(impactObj));
        }

        private IEnumerator RecycleImpactRoutine(GameObject impactObj)
        {
            float elapsed = 0f;
            Transform coreTr = impactObj.transform.childCount > 0 ? impactObj.transform.GetChild(0) : null;

            while (elapsed < impactDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / impactDuration);
                if (coreTr != null)
                {
                    float scale = Mathf.Lerp(0.16f, 0.02f, t);
                    coreTr.localScale = Vector3.one * scale;
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
