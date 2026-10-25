using UnityEngine;
using Recorded.Player;

namespace Recorded.Enemies
{
    /// <summary>
    /// Erasure beam projectile fired by Surveyor / The Editors.
    /// Uses continuous swept raycasting & player capsule proximity detection
    /// to guarantee reliable damage delivery regardless of PhysX kinematic settings.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SurveyorBolt : MonoBehaviour
    {
        [SerializeField] private float speed = 18f;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float lifetime = 4f;

        private Vector3 _direction;
        private Transform _ownerRoot;
        private Rigidbody _body;
        private Vector3 _lastPosition;
        private bool _hitRegistered;
        private Light _boltLight;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;

            var col = GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;

            _lastPosition = transform.position;
            SetupVisuals();
        }

        private void SetupVisuals()
        {
            // Give glowing red energy material
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(1f, 0.15f, 0.15f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.15f) * 5f);
                renderer.sharedMaterial = mat;
            }

            // Red point light
            _boltLight = gameObject.AddComponent<Light>();
            _boltLight.type = LightType.Point;
            _boltLight.color = new Color(1f, 0.2f, 0.2f);
            _boltLight.range = 3.5f;
            _boltLight.intensity = 2.5f;
        }

        public void Launch(Vector3 direction, Transform owner = null)
        {
            _direction = direction.normalized;
            _ownerRoot = owner != null ? owner.root : null;
            _lastPosition = transform.position;
            PlayFireAudio();
            StartCoroutine(Expire());
        }

        private System.Collections.IEnumerator Expire()
        {
            yield return Recorded.Phase.PhaseRecorder.WaitWorldSeconds(lifetime);
            if (!_hitRegistered) Destroy(gameObject);
        }

        private void Update()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused || _hitRegistered) return;

            float dt = Time.deltaTime;
            Vector3 step = _direction * (speed * dt);
            Vector3 nextPos = transform.position + step;
            float stepDist = step.magnitude;

            // Tier 1: Swept raycast / spherecast along trajectory
            if (Physics.SphereCast(_lastPosition, 0.3f, _direction, out RaycastHit hit, stepDist + 0.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.root != transform.root && hit.collider.transform.root != _ownerRoot)
                {
                    OnImpact(hit.collider, hit.point);
                    return;
                }
            }

            // Tier 2: Direct proximity check to Player capsule
            if (PlayerHealth.LocalPlayer != null && !PlayerHealth.LocalPlayer.IsDead)
            {
                Vector3 playerCenter = PlayerHealth.LocalPlayer.transform.position + Vector3.up * 0.9f;
                if (Vector3.Distance(transform.position, playerCenter) <= 0.95f || Vector3.Distance(nextPos, playerCenter) <= 0.95f)
                {
                    OnHitPlayer(PlayerHealth.LocalPlayer);
                    return;
                }

                // Check closest point on line segment (_lastPosition -> nextPos) to playerCenter
                Vector3 seg = nextPos - _lastPosition;
                float segLen = seg.magnitude;
                if (segLen > 0.001f)
                {
                    Vector3 segDir = seg / segLen;
                    float proj = Vector3.Dot(playerCenter - _lastPosition, segDir);
                    proj = Mathf.Clamp(proj, 0f, segLen);
                    Vector3 closestPt = _lastPosition + segDir * proj;
                    if (Vector3.Distance(closestPt, playerCenter) <= 0.95f)
                    {
                        OnHitPlayer(PlayerHealth.LocalPlayer);
                        return;
                    }
                }
            }

            // Advance position
            transform.position = nextPos;
            if (_body != null) _body.position = nextPos;
            _lastPosition = nextPos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused || _hitRegistered) return;
            if (other.transform.root == transform.root || other.transform.root == _ownerRoot) return;

            OnImpact(other, transform.position);
        }

        private void OnImpact(Collider other, Vector3 impactPoint)
        {
            if (_hitRegistered) return;

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                OnHitPlayer(player);
                return;
            }

            // Hit environment / obstacle
            _hitRegistered = true;
            PlayImpactAudio(impactPoint, false);
            Destroy(gameObject);
        }

        private void OnHitPlayer(PlayerHealth player)
        {
            if (_hitRegistered) return;
            _hitRegistered = true;

            Debug.Log($"[SurveyorBolt] DIRECT HIT ON PLAYER! Dealing {damage:0} damage.");
            player.TakeDamage(damage, _direction);
            PlayImpactAudio(player.transform.position + Vector3.up * 0.9f, true);
            Destroy(gameObject);
        }

        private void PlayFireAudio()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                // Sharp pitch-drop laser zap: 980Hz down to 180Hz
                float pitch = Mathf.Lerp(980f, 180f, Mathf.Pow(t / duration, 0.6f));
                float laser = Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.65f;
                float noise = (Random.value * 2f - 1f) * 0.25f;
                samples[i] = (laser + noise) * env;
            }
            var clip = AudioClip.Create("Bolt_Fire", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Bolt_Fire_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.4f;
            src.volume = 0.85f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void PlayImpactAudio(Vector3 pos, bool hitPlayer)
        {
            int sampleRate = 44100;
            float duration = 0.32f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float crackle = (Random.value * 2f - 1f) * 0.5f;
                float zap = Mathf.Sin(2f * Mathf.PI * (hitPlayer ? 140f : 240f) * t) * 0.5f;
                samples[i] = (crackle + zap) * env;
            }
            var clip = AudioClip.Create("Bolt_Impact", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Bolt_Impact_Sound");
            audioObj.transform.position = pos;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.3f;
            src.volume = hitPlayer ? 1.0f : 0.7f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }
    }
}
