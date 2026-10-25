using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Recorded.ColorSystem
{
    /// <summary>Unlocks the configured gate when the tagged player presses E inside this trigger.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SecurityKioskEInteraction : MonoBehaviour
    {
        [SerializeField] private KioskCyanAirlockGate airlockGate;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Light kioskLight;

        private readonly HashSet<Collider> _nearbyPlayerColliders = new HashSet<Collider>();
        private bool _isUnlocked;

        public static bool HasInteractionFor(Transform player)
        {
            if (player == null) return false;
            foreach (var kiosk in FindObjectsByType<SecurityKioskEInteraction>(FindObjectsSortMode.None))
            {
                if (!kiosk.isActiveAndEnabled || kiosk.airlockGate == null) continue;
                foreach (var collider in kiosk._nearbyPlayerColliders)
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && collider.transform.IsChildOf(player))
                        return true;
            }
            return false;
        }

        private void Awake()
        {
            if (!GetComponent<Collider>().isTrigger)
                Debug.LogError("Security kiosk interaction requires its Collider to have Is Trigger enabled.", this);
        }

        private void Start()
        {
            UpdateVisuals();
        }

        private void Update()
        {
            if (airlockGate != null && airlockGate.IsKioskUnlocked)
            {
                if (!_isUnlocked)
                {
                    _isUnlocked = true;
                    UpdateVisuals();
                }
            }

            if (_nearbyPlayerColliders.Count > 0 && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                var recorder = Recorded.Phase.PhaseRecorder.ActiveRecorder;
                if (recorder != null && recorder.IsRecording)
                {
                    foreach (var collider in _nearbyPlayerColliders)
                        if (collider != null && collider.transform.IsChildOf(recorder.transform))
                        {
                            recorder.RecordKiosk(airlockGate);
                            return;
                        }
                }
                if (!Recorded.Phase.PhaseRecorder.IsWorldPaused)
                {
                    airlockGate?.UnlockSecurityKiosk();
                    _isUnlocked = true;
                    UpdateVisuals();
                    PlayChime();
                }
            }
        }

        private bool IsPlayer(Collider other)
        {
            if (other == null) return false;
            if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return true;
            if (other.GetComponentInParent<CharacterController>() != null) return true;
            if (other.transform.root.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
                _nearbyPlayerColliders.Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            _nearbyPlayerColliders.Remove(other);
        }

        private void OnDisable()
        {
            _nearbyPlayerColliders.Clear();
        }

        public void SetAirlockGate(KioskCyanAirlockGate gate)
        {
            airlockGate = gate;
        }

        public void SetVisualComponents(Renderer screen, Light light)
        {
            screenRenderer = screen;
            kioskLight = light;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Color targetColor = _isUnlocked ? new Color(0.18f, 0.88f, 1f) : new Color(1f, 0.45f, 0.1f);
            if (kioskLight != null)
            {
                kioskLight.color = targetColor;
                kioskLight.intensity = _isUnlocked ? 2.5f : 1.5f;
            }

            if (screenRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                screenRenderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", targetColor * (_isUnlocked ? 3f : 1.8f));
                block.SetColor("_BaseColor", targetColor * 0.4f);
                screenRenderer.SetPropertyBlock(block);
            }
        }

        private void PlayChime()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float freq = t < 0.15f ? 523.25f : 659.25f; // C5 to E5 cyber chime
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
            }
            var clip = AudioClip.Create("Kiosk_Chime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Kiosk_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.5f;
            src.volume = 0.8f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void OnGUI()
        {
            if (_nearbyPlayerColliders.Count == 0) return;

            float width = 640f;
            float height = 48f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 180f;

            var bgRect = new Rect(x, y, width, height);
            GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.85f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            if (!_isUnlocked)
            {
                style.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUI.color = Color.white;
                GUI.Label(bgRect, "[E] OVERRIDE SECURITY TERMINAL", style);
            }
            else
            {
                bool isOpen = airlockGate != null && airlockGate.IsAirlockOpen;
                if (!isOpen)
                {
                    style.normal.textColor = new Color(0.2f, 0.95f, 1f);
                    GUI.color = Color.white;
                    GUI.Label(bgRect, "LOCK OVERRIDDEN — SHOOT AIRLOCK WITH CYAN ROUND", style);
                }
                else
                {
                    style.normal.textColor = new Color(0.4f, 1f, 0.5f);
                    GUI.color = Color.white;
                    GUI.Label(bgRect, "AIRLOCK BREACHED — PROCEED TO SECTOR C", style);
                }
            }
        }
    }
}
