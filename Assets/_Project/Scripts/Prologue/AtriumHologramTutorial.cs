using UnityEngine;
using Recorded.Phase;

namespace Recorded.Prologue
{
    /// <summary>
    /// Interactive tutorial and mission guide for Sector C Atrium.
    /// Guides the player step-by-step through the Hologram (Phase Recorder) mechanic,
    /// sanity management, and dual-lock puzzle completion.
    /// </summary>
    public sealed class AtriumHologramTutorial : MonoBehaviour
    {
        [SerializeField] private AtriumDualLockPuzzle puzzle;
        [SerializeField] private Transform terminalAlpha;
        [SerializeField] private Transform terminalBeta;
        [SerializeField] private Transform exitDoor;

        private PhaseRecorder _recorder;
        private Transform _playerTransform;
        private Camera _mainCamera;
        private bool _enteredAtrium;
        private float _briefingTimer;
        private bool _briefingDone;
        private bool _victoryTriggered;
        private float _victoryTimer;

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                _recorder = player.GetComponent<PhaseRecorder>();
            }
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                    _recorder = player.GetComponent<PhaseRecorder>();
                }
            }

            if (_mainCamera == null) _mainCamera = Camera.main;

            // Trigger when player enters Atrium (Z >= 42f)
            if (!_enteredAtrium && _playerTransform != null && _playerTransform.position.z >= 42f)
            {
                _enteredAtrium = true;
                _briefingTimer = 0f;
                Debug.Log("[AtriumHologramTutorial] Player entered Atrium. Initiating Hologram System briefing.");
            }

            if (_enteredAtrium && !_briefingDone)
            {
                _briefingTimer += Time.deltaTime;
                if (_briefingTimer > 18f) _briefingDone = true;
            }


            if (puzzle != null && puzzle.IsOverridden && !_victoryTriggered)
            {
                _victoryTriggered = true;
                _victoryTimer = 0f;
            }

            if (_victoryTriggered)
            {
                _victoryTimer += Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (!_enteredAtrium) return;

            DrawMissionObjectiveCard();
            DrawRadioDialogue();
            DrawStepBanner();
            Draw3DWorldMarkers();
            DrawSanityWarning();

            if (_victoryTriggered && _victoryTimer < 12f)
            {
                DrawVictoryScreen();
            }
        }

        private void DrawMissionObjectiveCard()
        {
            float w = 360f;
            float h = 135f;
            float x = 24f;
            float y = 24f;

            var rect = new Rect(x, y, w, h);
            GUI.color = new Color(0.015f, 0.035f, 0.06f, 0.90f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.2f, 0.88f, 1f);

            var itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12, y + 8, w - 24, 20), "MISSION OBJECTIVE: ATRIUM DUAL-LOCK", titleStyle);

            bool inPhase = _recorder != null && _recorder.IsRecording;
            bool hasTake = _recorder != null && _recorder.HasRecording;
            bool isReplay = _recorder != null && _recorder.IsReplaying;
            bool done = puzzle != null && puzzle.IsOverridden;

            DrawCheckItem(new Rect(x + 12, y + 32, w - 24, 18), "1. [F] เข้า Timeline คู่ขนาน & สับสวิตช์ A", inPhase || hasTake || done, inPhase, itemStyle);
            DrawCheckItem(new Rect(x + 12, y + 54, w - 24, 18), "2. [F] บันทึกข้อมูล & กลับโลกปัจจุบัน", hasTake || done, hasTake && !isReplay, itemStyle);
            DrawCheckItem(new Rect(x + 12, y + 76, w - 24, 18), "3. [G] ปล่อย Hologram + สับสวิตช์ B พร้อมกัน", isReplay || done, isReplay, itemStyle);
            DrawCheckItem(new Rect(x + 12, y + 98, w - 24, 18), "4. ประตู Area 2 ปลดล็อคสมบูรณ์", done, done, itemStyle);
        }

        private void DrawCheckItem(Rect rect, string text, bool complete, bool active, GUIStyle style)
        {
            if (complete)
            {
                style.normal.textColor = new Color(0.3f, 1f, 0.5f);
                GUI.Label(rect, "[OK] " + text, style);
            }
            else if (active)
            {
                style.normal.textColor = new Color(1f, 0.85f, 0.2f);
                GUI.Label(rect, "[->] " + text, style);
            }
            else
            {
                style.normal.textColor = new Color(0.65f, 0.7f, 0.75f);
                GUI.Label(rect, "[  ] " + text, style);
            }
        }

        private void DrawRadioDialogue()
        {
            if (_briefingDone) return;

            float w = 780f;
            float h = 75f;
            float x = (Screen.width - w) * 0.5f;
            float y = 30f;

            var rect = new Rect(x, y, w, h);
            GUI.color = new Color(0.01f, 0.03f, 0.06f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1f, 0.4f, 0.25f);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true
            };
            bodyStyle.normal.textColor = Color.white;

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 16, y + 8, w - 32, 20), "HQ [DIRECT TRANSMISSION — ATRIUM PROTOCOL]", titleStyle);

            if (_briefingTimer < 6.0f)
            {
                GUI.Label(new Rect(x + 16, y + 28, w - 32, 42),
                    "\"ECHO, ประตู Area 2 ล็อคด้วยระบบ Dual-Interlock คุณต้องเปิดคอนโซล A และ B พร้อมกันจึงจะผ่านได้\"", bodyStyle);
            }
            else if (_briefingTimer < 12.0f)
            {
                GUI.Label(new Rect(x + 16, y + 28, w - 32, 42),
                    "\"ใช้ระบบ Phase Hologram: กด [F] เพื่อเข้าสู่ Timeline คู่ขนาน (โลกหยุดนิ่ง) แล้ววิ่งไปกดสวิตช์ A\"", bodyStyle);
            }
            else
            {
                GUI.Label(new Rect(x + 16, y + 28, w - 32, 42),
                    "\"ระวังให้ดี! อยู่ในโลกโฮโลแกรมนานไม่ได้ หลอด SANITY จะลดลงเรื่อยๆ หากสติหมดคุณจะเสียการควบคุม!\"", bodyStyle);
            }
        }

        private void DrawStepBanner()
        {
            if (puzzle != null && puzzle.IsOverridden) return;

            float w = 680f;
            float h = 48f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 120f;

            var rect = new Rect(x, y, w, h);
            GUI.color = new Color(0.015f, 0.04f, 0.08f, 0.90f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            GUI.color = Color.white;
            bool inPhase = _recorder != null && _recorder.IsRecording;
            bool hasTake = _recorder != null && _recorder.HasRecording;
            bool isReplay = _recorder != null && _recorder.IsReplaying;

            if (inPhase)
            {
                style.normal.textColor = new Color(0.2f, 0.95f, 1f);
                GUI.Label(rect, "[TIMELINE RECORDING] วิ่งไปกด [E] ที่ TERMINAL ALPHA -> กด [F] เพื่อบันทึก!", style);
            }
            else if (!hasTake)
            {
                style.normal.textColor = new Color(1f, 0.85f, 0.25f);
                GUI.Label(rect, "กด [F] เพื่อเข้าสู่ TIMELINE คู่ขนาน (โหมดบันทึก HOLOGRAM)", style);
            }
            else if (!isReplay)
            {
                style.normal.textColor = new Color(0.3f, 1f, 0.6f);
                GUI.Label(rect, "บันทึกสำเร็จ! วิ่งไปที่ TERMINAL BETA (ขวา) แล้วกด [G] เพื่อปล่อยร่าง HOLOGRAM!", style);
            }
            else
            {
                style.normal.textColor = new Color(1f, 0.35f, 0.35f);
                GUI.Label(rect, "HOLOGRAM กำลังวิ่งไปเปิด TERMINAL A! กด [E] ที่ TERMINAL B พร้อมกันเลย!", style);
            }
        }

        private void Draw3DWorldMarkers()
        {
            if (_mainCamera == null) return;
            if (puzzle != null && puzzle.IsOverridden) return;

            DrawSingleMarker(terminalAlpha, "[A] TERMINAL ALPHA", new Color(0.2f, 0.85f, 1f));
            DrawSingleMarker(terminalBeta, "[B] TERMINAL BETA", new Color(1f, 0.75f, 0.2f));
            DrawSingleMarker(exitDoor, "[EXIT] AREA 2 BLAST DOORS", new Color(1f, 0.3f, 0.3f));
        }

        private void DrawSingleMarker(Transform target, string label, Color color)
        {
            if (target == null) return;

            Vector3 worldPos = target.position + Vector3.up * 1.8f;
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // Only draw if in front of camera
            if (screenPos.z <= 0f) return;

            float dist = _playerTransform != null ? Vector3.Distance(_playerTransform.position, target.position) : 0f;
            float x = screenPos.x;
            float y = Screen.height - screenPos.y;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = color;

            float w = 220f;
            float h = 32f;
            var rect = new Rect(x - w * 0.5f, y - h * 0.5f, w, h);

            GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(rect, $"{label}\n[{dist:0.0}m]", style);
        }

        private void DrawSanityWarning()
        {
            if (_recorder == null || !_recorder.IsRecording) return;

            float sanity = _recorder.Sanity;
            if (sanity < 0.30f)
            {
                // Pulsing red danger vignette
                float pulse = 0.12f + Mathf.Sin(Time.unscaledTime * 10f) * 0.06f;
                GUI.color = new Color(0.9f, 0.05f, 0.05f, pulse);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

                var alertStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold
                };
                alertStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, 110, Screen.width, 36), "WARNING: SANITY CRITICAL! EXIT TIMELINE [F] BEFORE OVERRUN!", alertStyle);
            }
        }

        private void DrawVictoryScreen()
        {
            float w = 760f;
            float h = 100f;
            float x = (Screen.width - w) * 0.5f;
            float y = 140f;

            var rect = new Rect(x, y, w, h);
            GUI.color = new Color(0.01f, 0.06f, 0.03f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 21,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15
            };
            subStyle.normal.textColor = new Color(0.85f, 0.95f, 0.9f);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 14, w, 32), "DUAL-LOCK OVERRIDDEN — AREA 2 ACCESS GRANTED!", titleStyle);
            GUI.Label(new Rect(x, y + 50, w, 32), "HQ: \"ยอดเยี่ยมมาก ECHO! คุณใช้ไทม์ไลน์คู่ขนานเจาะระบบได้สำเร็จ!\" (DEMO CLEARED)", subStyle);
        }
    }
}
