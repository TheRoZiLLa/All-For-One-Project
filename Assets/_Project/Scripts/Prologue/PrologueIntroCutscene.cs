using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Recorded.Prologue
{
    /// <summary>
    /// Implements Scene 01 "The Descent" opening cutscene:
    /// Starts with pitch black screen, howling wind and tactical radio comms between HQ and ECHO,
    /// fades in to the Drop Zone, plays Phase Suit online boot-up chime, and enables player control.
    /// Press Space or Enter to skip.
    /// </summary>
    public sealed class PrologueIntroCutscene : MonoBehaviour
    {
        [SerializeField] private float cutsceneDuration = 8.5f;

        private float _timer;
        private bool _isPlaying = true;
        private bool _suitBooted;
        private float _fadeAlpha = 1.0f;
        private FirstPersonMotor _motor;
        private FirstPersonLook _look;

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _motor = player.GetComponent<FirstPersonMotor>();
                _look = player.GetComponent<FirstPersonLook>();
                if (_motor != null) _motor.enabled = false;
            }

            PlayAtmosphereAudio();
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _timer += Time.deltaTime;

            // Allow skipping with Space or Enter
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                EndCutscene();
                return;
            }

            // Fade from black between 1.5s and 4.0s
            if (_timer > 1.5f && _timer < 4.5f)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, Time.deltaTime * 0.45f);
            }
            else if (_timer >= 4.5f)
            {
                _fadeAlpha = 0f;
            }

            // Suit Boot at 6.0s
            if (_timer >= 6.0f && !_suitBooted)
            {
                _suitBooted = true;
                PlaySuitBootAudio();
            }

            if (_timer >= cutsceneDuration)
            {
                EndCutscene();
            }
        }

        private void EndCutscene()
        {
            _isPlaying = false;
            _fadeAlpha = 0f;
            if (_motor != null) _motor.enabled = true;
            if (_look != null) _look.enabled = true;
            Debug.Log("[PrologueIntroCutscene] Intro cutscene complete. Player control enabled.");
        }

        private void PlayAtmosphereAudio()
        {
            var audioObj = new GameObject("Intro_WindAudio");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.volume = 0.75f;

            int sampleRate = 44100;
            float duration = 6.0f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float wind = (Random.value * 2f - 1f) * 0.45f + Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.35f;
                samples[i] = wind * env * 0.5f;
            }
            var clip = AudioClip.Create("Intro_Wind", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.5f);
        }

        private void PlaySuitBootAudio()
        {
            var audioObj = new GameObject("Intro_SuitBootAudio");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.volume = 0.85f;

            int sampleRate = 44100;
            float duration = 1.2f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float freq = Mathf.Lerp(220f, 880f, t / duration); // rising high-tech power boot tone
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            }
            var clip = AudioClip.Create("Suit_Boot", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.2f);
        }

        private void OnGUI()
        {
            if (!_isPlaying && _timer > cutsceneDuration + 4f) return;

            // Full screen black fade
            if (_fadeAlpha > 0.005f)
            {
                GUI.color = new Color(0f, 0f, 0f, _fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }

            // Cinematic Letterbox Bars
            if (_isPlaying)
            {
                float barHeight = Screen.height * 0.11f;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, barHeight), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - barHeight, Screen.width, barHeight), Texture2D.whiteTexture);
            }

            // Subtitle Display during cutscene
            if (_isPlaying)
            {
                float w = 840f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height - Screen.height * 0.11f - 80f;

                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };

                var bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };

                GUI.color = Color.white;
                if (_timer < 5.0f)
                {
                    titleStyle.normal.textColor = new Color(1f, 0.4f, 0.25f);
                    bodyStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(x, y, w, 20), "HQ [TACTICAL COMMS]", titleStyle);
                    GUI.Label(new Rect(x, y + 20, w, 45),
                        "\"ECHO ยืนยันพิกัดเหนือสถานีวิจัย VESPER... เตาปฏิกรณ์พลังงานพุ่งสูงถึง 400% นำ Blackbox ออกมา แล้วลบทุกอย่างที่ขวางหน้า\"", bodyStyle);
                }
                else if (_timer < 7.5f)
                {
                    titleStyle.normal.textColor = new Color(0.2f, 0.85f, 1f);
                    bodyStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(x, y, w, 20), "ECHO", titleStyle);
                    GUI.Label(new Rect(x, y + 20, w, 45),
                        "\"Copy that. กำลังเข้าสู่เขตปลอดตัวแปร\"", bodyStyle);
                }
                else
                {
                    titleStyle.normal.textColor = new Color(0.2f, 1f, 0.5f);
                    bodyStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
                    GUI.Label(new Rect(x, y, w, 20), "SYSTEM ONLINE", titleStyle);
                    GUI.Label(new Rect(x, y + 20, w, 45),
                        "PHASE SUIT BOOTED. SPECTRUM GUN INITIALIZED [RED IMPULSE].", bodyStyle);
                }

                // Skip prompt
                var skipStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 11
                };
                skipStyle.normal.textColor = new Color(0.5f, 0.55f, 0.6f);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width - 240, Screen.height - 30, 220, 20), "PRESS SPACE TO SKIP", skipStyle);
            }
        }
    }
}
