using UnityEngine;
using UnityEngine.InputSystem;
using Recorded.Phase;

namespace Recorded.Prologue
{
    /// <summary>
    /// Dual-lock security console for the Atrium puzzle.
    /// Can be activated by the Player pressing E or by the Hologram standing near it.
    /// Remains active for activeDuration seconds.
    /// </summary>
    public sealed class AtriumDualLockTerminal : MonoBehaviour
    {
        [SerializeField] private string terminalId = "A";
        [SerializeField] private float interactRadius = 2.4f;
        [SerializeField] private float activeDuration = 5.0f;
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Light statusLight;

        public string TerminalId => terminalId;
        public bool IsActive => _activeTimer > 0f;
        public float ActiveTimeRemaining => Mathf.Max(0f, _activeTimer);
        public bool IsPlayerNearby => _playerNearby;
        public bool IsHologramNearby => _hologramNearby;

        private float _activeTimer;
        private bool _playerNearby;
        private bool _hologramNearby;
        private Transform _playerTransform;

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            UpdateVisuals();
        }

        private void Update()
        {
            if (PhaseRecorder.IsWorldPaused)
            {
                // In Phase/Hologram planning world, allow player to record pressing E here
                CheckPlanningInteraction();
                return;
            }

            if (_activeTimer > 0f)
            {
                _activeTimer -= Time.deltaTime;
                if (_activeTimer <= 0f)
                {
                    _activeTimer = 0f;
                    UpdateVisuals();
                }
            }

            // Check player proximity
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) _playerTransform = player.transform;
            }

            if (_playerTransform != null)
            {
                float pDist = Vector3.Distance(transform.position, _playerTransform.position);
                _playerNearby = pDist <= interactRadius;
            }

            // Check hologram proximity during replay
            _hologramNearby = false;
            if (HologramPresence.IsLive && HologramPresence.Active != null)
            {
                float hDist = Vector3.Distance(transform.position, HologramPresence.Active.position);
                if (hDist <= interactRadius)
                {
                    _hologramNearby = true;
                    // Automatically trigger when the recorded hologram reaches this console
                    if (!IsActive)
                    {
                        Activate();
                        Debug.Log($"[AtriumDualLock] Terminal {terminalId} triggered by Hologram presence!");
                    }
                }
            }

            // Player press E in real world
            if (_playerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                Activate();
                Debug.Log($"[AtriumDualLock] Terminal {terminalId} triggered by Player!");
            }
        }

        private void CheckPlanningInteraction()
        {
            // Inside the Hologram timeline
            if (_playerTransform != null)
            {
                float pDist = Vector3.Distance(transform.position, _playerTransform.position);
                _playerNearby = pDist <= interactRadius;
                if (_playerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    PlayChime();
                    Debug.Log($"[AtriumDualLock] Terminal {terminalId} recorded in Hologram timeline!");
                }
            }
        }

        public void Activate()
        {
            _activeTimer = activeDuration;
            UpdateVisuals();
            PlayChime();
        }

        public void ForceDeactivate()
        {
            _activeTimer = 0f;
            UpdateVisuals();
        }

        public void SetVisualComponents(Renderer screen, Light lightComp)
        {
            screenRenderer = screen;
            statusLight = lightComp;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Color color = IsActive ? new Color(0.18f, 0.88f, 1f) : new Color(1f, 0.45f, 0.1f);
            if (statusLight != null)
            {
                statusLight.color = color;
                statusLight.intensity = IsActive ? 3.0f : 1.5f;
            }

            if (screenRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                screenRenderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", color * (IsActive ? 3.5f : 1.5f));
                block.SetColor("_BaseColor", color * 0.4f);
                screenRenderer.SetPropertyBlock(block);
            }
        }

        private void PlayChime()
        {
            int sampleRate = 44100;
            float duration = 0.3f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float freq = terminalId == "A" ? 587.33f : 783.99f; // D5 or G5 tone
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            }
            var clip = AudioClip.Create($"Terminal_{terminalId}_Chime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject($"Terminal_{terminalId}_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.4f;
            src.volume = 0.85f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void OnGUI()
        {
            if (!_playerNearby) return;

            float width = 580f;
            float height = 44f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 180f;

            var bgRect = new Rect(x, y, width, height);
            GUI.color = new Color(0.015f, 0.035f, 0.06f, 0.90f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };

            GUI.color = Color.white;
            if (PhaseRecorder.IsWorldPaused)
            {
                style.normal.textColor = new Color(0.2f, 0.88f, 1f);
                GUI.Label(bgRect, $"[E] RECORD TERMINAL {terminalId} INTERACTION (TIMELINE PLANNING)", style);
            }
            else if (!IsActive)
            {
                style.normal.textColor = new Color(1f, 0.8f, 0.2f);
                GUI.Label(bgRect, $"[E] ACTIVATE TERMINAL {terminalId} (REQUIRES DUAL CONFIRMATION)", style);
            }
            else
            {
                style.normal.textColor = new Color(0.2f, 1f, 0.5f);
                GUI.Label(bgRect, $"TERMINAL {terminalId} ENGAGED! ({_activeTimer:0.0}s REMAINING)", style);
            }
        }
    }
}
