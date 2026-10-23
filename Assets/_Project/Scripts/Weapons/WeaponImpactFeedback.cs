using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Recorded.Weapons
{
    /// <summary>
    /// Handles visual and audio feedback for shooting, muzzle flash, tracer, and impact effects.
    /// Uses lightweight pooling and coroutines to minimize GC allocations.
    /// </summary>
    public class WeaponImpactFeedback : MonoBehaviour
    {
        [Header("Muzzle Flash")]
        [SerializeField] private Light muzzleLight;
        [SerializeField] private float muzzleFlashDuration = 0.05f;

        [Header("Tracer Line")]
        [SerializeField] private Color tracerColor = new Color(0.2f, 0.8f, 1f, 0.9f); // Sci-Fi Cyan
        [SerializeField] private float tracerDuration = 0.04f;
        [SerializeField] private float tracerWidth = 0.025f;

        [Header("Impact Effect")]
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private Color impactColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private float impactDuration = 0.4f;

        private WaitForSeconds _muzzleWait;
        private WaitForSeconds _tracerWait;
        private LineRenderer _tracerLine;
        private Coroutine _muzzleCoroutine;
        private Coroutine _tracerCoroutine;

        // Simple Object Pool for impact visual markers
        private readonly Queue<GameObject> _impactPool = new Queue<GameObject>();
        private const int InitialPoolSize = 8;

        private void Awake()
        {
            _muzzleWait = new WaitForSeconds(muzzleFlashDuration);
            _tracerWait = new WaitForSeconds(tracerDuration);

            InitializeTracer();
            InitializeImpactPool();

            if (muzzleLight != null)
            {
                muzzleLight.enabled = false;
            }
        }

        private void InitializeTracer()
        {
            GameObject tracerObj = new GameObject("WeaponTracer_Line");
            tracerObj.transform.SetParent(transform, false);

            _tracerLine = tracerObj.AddComponent<LineRenderer>();
            _tracerLine.startWidth = tracerWidth;
            _tracerLine.endWidth = tracerWidth * 0.4f;
            _tracerLine.positionCount = 2;
            _tracerLine.useWorldSpace = true;
            _tracerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerLine.receiveShadows = false;

            // Shader: Unlit or Particles/Standard Unlit
            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
            
            Material lineMat = new Material(lineShader);
            lineMat.color = tracerColor;
            _tracerLine.material = lineMat;
            _tracerLine.enabled = false;
        }

        private void InitializeImpactPool()
        {
            for (int i = 0; i < InitialPoolSize; i++)
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

            // Procedural Sci-Fi Impact Flash & Spark Marker
            GameObject impactObj = new GameObject("ImpactEffect_Procedural");
            impactObj.transform.SetParent(transform);

            // Light flash at impact
            Light impactLight = impactObj.AddComponent<Light>();
            impactLight.type = LightType.Point;
            impactLight.range = 3.5f;
            impactLight.color = impactColor;
            impactLight.intensity = 4f;

            // Small glowing sphere core
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.transform.SetParent(impactObj.transform, false);
            core.transform.localScale = Vector3.one * 0.12f;
            
            // Remove collider so it doesn't interfere with raycasts
            Collider col = core.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
            Material mat = new Material(unlitShader);
            mat.color = impactColor;
            
            Renderer rend = core.GetComponent<Renderer>();
            if (rend != null) rend.material = mat;

            return impactObj;
        }

        /// <summary>
        /// Trigger muzzle flash, tracer, and impact feedback when a hit occurs.
        /// </summary>
        public void PlayHitFeedback(Vector3 muzzlePosition, RaycastHit hit)
        {
            PlayMuzzleFlash();
            PlayTracer(muzzlePosition, hit.point);
            PlayImpact(hit.point, hit.normal);
        }

        /// <summary>
        /// Trigger muzzle flash and tracer when the shot misses.
        /// </summary>
        public void PlayMissFeedback(Vector3 muzzlePosition, Vector3 targetPoint)
        {
            PlayMuzzleFlash();
            PlayTracer(muzzlePosition, targetPoint);
        }

        private void PlayMuzzleFlash()
        {
            if (muzzleLight == null) return;

            if (_muzzleCoroutine != null) StopCoroutine(_muzzleCoroutine);
            _muzzleCoroutine = StartCoroutine(MuzzleFlashRoutine());
        }

        private IEnumerator MuzzleFlashRoutine()
        {
            muzzleLight.enabled = true;
            yield return _muzzleWait;
            muzzleLight.enabled = false;
            _muzzleCoroutine = null;
        }

        private void PlayTracer(Vector3 from, Vector3 to)
        {
            if (_tracerLine == null) return;

            if (_tracerCoroutine != null) StopCoroutine(_tracerCoroutine);
            _tracerCoroutine = StartCoroutine(TracerRoutine(from, to));
        }

        private IEnumerator TracerRoutine(Vector3 from, Vector3 to)
        {
            _tracerLine.SetPosition(0, from);
            _tracerLine.SetPosition(1, to);
            _tracerLine.enabled = true;

            yield return _tracerWait;

            _tracerLine.enabled = false;
            _tracerCoroutine = null;
        }

        private void PlayImpact(Vector3 point, Vector3 normal)
        {
            GameObject impactObj = _impactPool.Count > 0 ? _impactPool.Dequeue() : CreateDefaultImpactObject();
            impactObj.transform.position = point + (normal * 0.05f);
            impactObj.transform.rotation = Quaternion.LookRotation(normal);

            // Update impact color dynamically
            Light l = impactObj.GetComponent<Light>();
            if (l != null) l.color = impactColor;
            Renderer r = impactObj.GetComponentInChildren<Renderer>();
            if (r != null && r.material != null) r.material.color = impactColor;

            impactObj.SetActive(true);

            StartCoroutine(RecycleImpactRoutine(impactObj));
        }

        private IEnumerator RecycleImpactRoutine(GameObject impactObj)
        {
            yield return new WaitForSeconds(impactDuration);
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

            if (_tracerLine != null && _tracerLine.material != null)
            {
                _tracerLine.material.color = color;
            }

            if (muzzleLight != null)
            {
                muzzleLight.color = color;
            }
        }
    }
}
