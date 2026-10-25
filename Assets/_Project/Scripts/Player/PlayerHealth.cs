using System.Collections;
using UnityEngine;

namespace Recorded.Player
{
    /// <summary>
    /// Phase Suit Health, Damage & Recovery system.
    /// Handles enemy damage, invulnerability frames, chasm fall respawns,
    /// screen damage flash, camera trauma shake, and health HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float invulnerabilityDuration = 0.7f;

        public static PlayerHealth LocalPlayer { get; private set; }
        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; } = 100f;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => _invulnerableTimer > 0f;

        private float _invulnerableTimer;
        private float _damageFlashAlpha;
        private Vector3 _damageShakeOffset;
        private float _shakeIntensity;
        private CharacterController _controller;
        private Vector3 _checkpointPosition;
        private Quaternion _checkpointRotation;
        private float _fallRespawnCooldown;
        private float _fallNotificationTimer;

        private void Awake()
        {
            LocalPlayer = this;
            CurrentHealth = maxHealth;
            _controller = GetComponent<CharacterController>();
            _checkpointPosition = transform.position;
            _checkpointRotation = transform.rotation;
        }

        private void OnEnable()
        {
            LocalPlayer = this;
        }

        private void Start()
        {
            SetCheckpoint(transform.position, transform.rotation);
        }

        private void Update()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;

            float dt = Time.deltaTime;

            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;
            if (_fallRespawnCooldown > 0f) _fallRespawnCooldown -= dt;
            if (_fallNotificationTimer > 0f) _fallNotificationTimer -= dt;

            // Fade damage flash
            if (_damageFlashAlpha > 0f)
            {
                _damageFlashAlpha = Mathf.MoveTowards(_damageFlashAlpha, 0f, dt * 2.2f);
            }

            // Decay camera shake
            if (_shakeIntensity > 0f)
            {
                _shakeIntensity = Mathf.MoveTowards(_shakeIntensity, 0f, dt * 6f);
                _damageShakeOffset = Random.insideUnitSphere * (_shakeIntensity * 0.12f);
            }
            else
            {
                _damageShakeOffset = Vector3.zero;
            }

            // Chasm fall protection: If player falls into the chasm or off ledges (Y < -2.5f)
            if (transform.position.y < -2.5f && !IsDead && _fallRespawnCooldown <= 0f)
            {
                RespawnFromChasmFall();
            }

            // Periodic checkpoint updates in safe zones
            if (transform.position.z >= 44f && _checkpointPosition.z < 44f)
            {
                SetCheckpoint(new Vector3(0f, 0.23f, 44f), transform.rotation);
            }
            else if (transform.position.z >= 35f && _checkpointPosition.z < 35f)
            {
                SetCheckpoint(new Vector3(-0.75f, 0.23f, 35f), transform.rotation);
            }
            else if (transform.position.z >= 27f && _checkpointPosition.z < 27f)
            {
                SetCheckpoint(new Vector3(0f, 0.23f, 27.5f), transform.rotation);
            }
        }

        public void TakeDamage(float amount, Vector3 hitDirection)
        {
            if (IsDead || IsInvulnerable) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            _invulnerableTimer = invulnerabilityDuration;
            _damageFlashAlpha = 0.55f;
            _shakeIntensity = 1.0f;

            PlayHitAudio();
            Debug.Log($"[PlayerHealth] Hit! Damage: {amount:0}, HP Remaining: {CurrentHealth:0}/{maxHealth:0}");

            // Apply slight knockback to controller
            if (_controller != null && _controller.enabled)
            {
                Vector3 shove = (hitDirection.sqrMagnitude > 0.01f ? hitDirection.normalized : -transform.forward) * 1.5f;
                shove.y = 0.2f;
                _controller.Move(shove);
            }

            if (CurrentHealth <= 0f)
            {
                StartCoroutine(HandlePlayerDeath());
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        public void SetCheckpoint(Vector3 pos, Quaternion rot)
        {
            _checkpointPosition = pos;
            _checkpointRotation = rot;
        }

        /// <summary>
        /// Called when player falls into the Sector A chasm or off ledges.
        /// Immediately rollbacks player position to the safe ledge and restores stability.
        /// </summary>
        public void RespawnFromChasmFall(Vector3? overridePosition = null)
        {
            _fallRespawnCooldown = 1.0f;
            _fallNotificationTimer = 3.0f;
            _damageFlashAlpha = 0.45f;
            _shakeIntensity = 0.8f;

            PlayFallAudio();
            PlayFallRespawnAudio();

            // Determine safe ledge location
            Vector3 targetPos;
            Quaternion targetRot = Quaternion.identity;

            if (overridePosition.HasValue)
            {
                targetPos = overridePosition.Value;
            }
            else if (transform.position.z >= 26.5f && transform.position.z < 35f)
            {
                // Sector B entrance ledge after chasm
                targetPos = new Vector3(0f, 0.23f, 27.5f);
                targetRot = Quaternion.Euler(0f, 0f, 0f);
            }
            else if (transform.position.z >= 35f)
            {
                targetPos = _checkpointPosition;
                targetRot = _checkpointRotation;
            }
            else
            {
                // Sector A entrance ledge before chasm
                targetPos = new Vector3(0f, 0.23f, 18.5f);
                targetRot = Quaternion.Euler(0f, 0f, 0f);
            }

            if (_controller != null) _controller.enabled = false;
            transform.position = targetPos;
            transform.rotation = targetRot;
            if (_controller != null) _controller.enabled = true;

            Debug.Log($"[PlayerHealth] Chasm fall detected! Quantum state restored at {targetPos}.");
        }

        private IEnumerator HandlePlayerDeath()
        {
            IsDead = true;
            Debug.Log("[PlayerHealth] Player suit integrity failed! Initiating timeline rollback...");

            // Play timeline glitch death audio
            PlayGlitchAudio();

            yield return new WaitForSeconds(1.2f);

            // Rollback to checkpoint
            if (_controller != null) _controller.enabled = false;
            transform.position = _checkpointPosition;
            transform.rotation = _checkpointRotation;
            if (_controller != null) _controller.enabled = true;

            CurrentHealth = maxHealth;
            _invulnerableTimer = 1.5f;
            IsDead = false;
            _damageFlashAlpha = 0f;
            Debug.Log("[PlayerHealth] Suit integrity restored at checkpoint.");
        }

        private void PlayHitAudio()
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float lowImpact = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.75f;
                float shieldSpark = (Random.value * 2f - 1f) * 0.45f;
                float painGrunt = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.3f;
                samples[i] = (lowImpact + shieldSpark + painGrunt) * env;
            }
            var clip = AudioClip.Create("Player_Hit", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Hit_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.2f;
            src.volume = 0.95f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void PlayFallAudio()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float pitch = Mathf.Lerp(480f, 120f, t / duration);
                float whoosh = Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.5f;
                float noise = (Random.value * 2f - 1f) * 0.25f;
                samples[i] = (whoosh + noise) * env;
            }
            var clip = AudioClip.Create("Fall_Whoosh", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Fall_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.1f;
            src.volume = 0.85f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void PlayFallRespawnAudio()
        {
            int sampleRate = 44100;
            float duration = 0.6f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                // Dual chord quantum phase chime (523Hz + 659Hz + 784Hz)
                float c = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.35f;
                float e = Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.3f;
                float g = Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.25f;
                float sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(200f, 900f, t / duration) * t) * 0.2f;
                samples[i] = (c + e + g + sweep) * env;
            }
            var clip = AudioClip.Create("Quantum_Respawn_Chime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Respawn_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.1f;
            src.volume = 0.9f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void PlayGlitchAudio()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float freq = 120f + Mathf.Sin(t * 50f) * 60f;
                float buzz = (Mathf.Sin(2f * Mathf.PI * freq * t) > 0 ? 0.5f : -0.5f) * 0.6f;
                samples[i] = buzz * env;
            }
            var clip = AudioClip.Create("Glitch_Death", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Glitch_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.1f;
            src.volume = 0.95f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void OnGUI()
        {
            // Damage red screen vignette flash
            if (_damageFlashAlpha > 0.01f)
            {
                GUI.color = new Color(0.9f, 0.02f, 0.04f, _damageFlashAlpha * 0.65f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }

            // Fall / Chasm Stabilization Banner
            if (_fallNotificationTimer > 0.01f && !IsDead)
            {
                float bannerW = 600f;
                float bannerH = 44f;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = 85f;

                GUI.color = new Color(0.01f, 0.04f, 0.08f, 0.9f);
                GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, bannerH), Texture2D.whiteTexture);

                var bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                bannerStyle.normal.textColor = new Color(0.25f, 0.95f, 1f);
                GUI.color = Color.white;
                GUI.Label(new Rect(bannerX, bannerY, bannerW, bannerH), "QUANTUM STABILIZATION: POSITION RESTORED [CHASM HAZARD RESET]", bannerStyle);
            }

            // Death Screen Overlay
            if (IsDead)
            {
                GUI.color = new Color(0.05f, 0.01f, 0.02f, 0.92f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

                var deathStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold
                };
                deathStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, 40), "TIMELINE ANOMALY: SUIT INTEGRITY BREACHED", deathStyle);

                var subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15
                };
                subStyle.normal.textColor = new Color(0.3f, 0.85f, 1f);
                GUI.Label(new Rect(0, Screen.height * 0.50f, Screen.width, 30), "RESTORING PHASE QUANTUM STATE...", subStyle);
                return;
            }

            // Health HUD (Bottom-Left)
            DrawHealthHUD();
        }

        private void DrawHealthHUD()
        {
            float x = 24f;
            float y = Screen.height - 80f;
            float w = 280f;
            float h = 48f;

            // Background panel
            var bgRect = new Rect(x, y, w, h);
            GUI.color = new Color(0.015f, 0.035f, 0.06f, 0.88f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

            // Title label
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.2f, 0.85f, 1f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10, y + 4, w - 20, 16), "SUIT INTEGRITY", titleStyle);

            // HP bar rail
            float railX = x + 10;
            float railY = y + 22;
            float railW = w - 20;
            float railH = 12;

            GUI.color = new Color(0.08f, 0.12f, 0.16f, 0.9f);
            GUI.DrawTexture(new Rect(railX, railY, railW, railH), Texture2D.whiteTexture);

            // HP bar fill
            float pct = Mathf.Clamp01(CurrentHealth / maxHealth);
            Color barColor = pct > 0.5f ? new Color(0.2f, 0.95f, 0.45f) : (pct > 0.25f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.25f, 0.25f));
            if (pct <= 0.25f && Mathf.Sin(Time.unscaledTime * 8f) > 0f)
            {
                barColor = Color.white;
            }

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(railX, railY, railW * pct, railH), Texture2D.whiteTexture);

            // HP numbers
            var numStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            numStyle.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 2, w - 10, 18), $"{Mathf.CeilToInt(CurrentHealth)} / {Mathf.CeilToInt(maxHealth)}", numStyle);
        }
    }
}
