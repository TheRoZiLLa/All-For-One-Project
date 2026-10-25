using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Recorded.Enemies;
using Recorded.ColorSystem;
using Recorded.Weapons.Ammo;

namespace Recorded.Prologue
{
    /// <summary>
    /// Implements Scene 04 "Syntax Error":
    /// Triggers when the player enters Sector C past the airlock door.
    /// Spawns 3 stylized geometric deletion drones ("The Editors") with rotating shield fins and glowing sensors.
    /// CURATOR AI announces Deletion, combat tutorial pops up, enemies attack with erasure bolts.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Scene04SyntaxErrorEncounter : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private List<GameObject> activeEditors = new List<GameObject>();

        private bool _triggered;
        private float _encounterTimer;
        private bool _allCleared;
        private float _clearTimer;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (other.CompareTag(playerTag) || (other.GetComponentInParent<CharacterController>() != null))
            {
                TriggerEncounter();
            }
        }

        public void TriggerEncounter()
        {
            if (_triggered) return;
            _triggered = true;
            _encounterTimer = 0f;

            Debug.Log("[Scene04] Syntax Error Encounter triggered! CURATOR announces deletion.");
            PlayAlarmAudio();
            SpawnEditors();
        }

        private void SpawnEditors()
        {
            Vector3[] spawnOffsets = new Vector3[]
            {
                new Vector3(0f, 1.8f, 50f),
                new Vector3(-4.5f, 1.8f, 53f),
                new Vector3(4.5f, 1.8f, 53f)
            };

            var player = GameObject.FindWithTag("Player");
            Transform playerT = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : null);

            for (int i = 0; i < spawnOffsets.Length; i++)
            {
                var editor = CreateEditorDrone($"The_Editor_0{i + 1}", spawnOffsets[i], playerT);
                activeEditors.Add(editor);
            }
        }

        private GameObject CreateEditorDrone(string name, Vector3 pos, Transform playerT)
        {
            var bot = new GameObject(name);
            bot.transform.position = pos;

            // Central core
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "EditorCore";
            core.transform.SetParent(bot.transform, false);
            core.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            core.transform.localRotation = Quaternion.Euler(45f, 45f, 0f); // diamond orientation

            var coreMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            coreMat.color = new Color(0.12f, 0.14f, 0.18f);
            core.GetComponent<Renderer>().sharedMaterial = coreMat;

            // Glowing deletion eye
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "DeletionEye";
            eye.transform.SetParent(bot.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, 0.42f);
            eye.transform.localScale = new Vector3(0.35f, 0.35f, 0.2f);
            Destroy(eye.GetComponent<Collider>());

            var eyeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            eyeMat.color = new Color(1f, 0.15f, 0.15f);
            eyeMat.EnableKeyword("_EMISSION");
            eyeMat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.15f) * 4f);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;

            // Rotating orbital shield fins
            var finRoot = new GameObject("OrbitalFins").transform;
            finRoot.SetParent(bot.transform, false);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fin.name = $"Fin_{i}";
                fin.transform.SetParent(finRoot, false);
                fin.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.75f, 0f, Mathf.Sin(angle) * 0.75f);
                fin.transform.localRotation = Quaternion.Euler(0f, i * 90f, 25f);
                fin.transform.localScale = new Vector3(0.08f, 0.65f, 0.28f);
                Destroy(fin.GetComponent<Collider>());

                var finMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                finMat.color = new Color(0.2f, 0.85f, 1f, 0.75f);
                finMat.EnableKeyword("_EMISSION");
                finMat.SetColor("_EmissionColor", new Color(0.18f, 0.88f, 1f) * 1.5f);
                fin.GetComponent<Renderer>().sharedMaterial = finMat;
            }
            finRoot.gameObject.AddComponent<DroneFinSpinner>();

            // Collider & Rigidbody on root
            var col = bot.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1.2f, 1.2f);

            var rb = bot.AddComponent<Rigidbody>();
            rb.mass = 3.2f;
            rb.useGravity = false; // Hovering drone
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = 1.2f;

            bot.AddComponent<RGBColorTarget>();
            bot.AddComponent<SpectrumSkillResponder>();
            bot.AddComponent<ColorMixPhysicsReactor>();
            bot.AddComponent<EditorDroneDestructible>();

            // Telegraph beam child
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "TelegraphBeam";
            beam.transform.SetParent(bot.transform, false);
            beam.transform.localScale = new Vector3(0.05f, 0.05f, 1.8f);
            beam.transform.localPosition = new Vector3(0f, 0f, 1.0f);
            Destroy(beam.GetComponent<Collider>());
            var beamMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            beamMat.color = new Color(1f, 0.1f, 0.1f, 0.85f);
            beam.GetComponent<Renderer>().sharedMaterial = beamMat;
            beam.GetComponent<Renderer>().enabled = false;

            var ai = bot.AddComponent<SurveyorEnemyAI>();
            ai.SetTargets(playerT, null);

            return bot;
        }

        private void Update()
        {
            if (!_triggered) return;
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;

            _encounterTimer += Time.deltaTime;

            if (!_allCleared)
            {
                activeEditors.RemoveAll(e => e == null);
                if (activeEditors.Count == 0 && _encounterTimer > 3f)
                {
                    _allCleared = true;
                    _clearTimer = 0f;
                    Debug.Log("[Scene04] All Editors defeated! Demo objective completed.");
                }
            }
            else
            {
                _clearTimer += Time.deltaTime;
            }
        }

        private void PlayAlarmAudio()
        {
            var audioObj = new GameObject("Encounter_Alarm");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.2f;
            src.volume = 0.7f;

            int sampleRate = 44100;
            float duration = 1.5f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float freq = (t % 0.4f < 0.2f) ? 880f : 660f;
                samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) > 0 ? 0.4f : -0.4f) * env;
            }
            var clip = AudioClip.Create("Alarm_Sound", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void OnGUI()
        {
            if (!_triggered) return;

            // Curator dialogue banner
            if (_encounterTimer < 8.0f)
            {
                float width = 800f;
                float height = 75f;
                float x = (Screen.width - width) * 0.5f;
                float y = 40f;

                var bgRect = new Rect(x, y, width, height);
                GUI.color = new Color(0.04f, 0.01f, 0.02f, 0.92f);
                GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);

                var bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                bodyStyle.normal.textColor = Color.white;

                GUI.color = Color.white;
                GUI.Label(new Rect(x + 18, y + 8, width - 36, 22), "CURATOR [AUTOMATED FACILITY AI]", titleStyle);
                GUI.Label(new Rect(x + 18, y + 30, width - 36, 38), "\"Warning. Unauthorized Variable detected. Integrity disrupted... Initiating Deletion.\"", bodyStyle);
            }

            // Tutorial Prompt Banner
            if (_encounterTimer > 2.0f && !_allCleared)
            {
                float tutWidth = 720f;
                float tutHeight = 52f;
                float tutX = (Screen.width - tutWidth) * 0.5f;
                float tutY = Screen.height - 180f;

                var tutRect = new Rect(tutX, tutY, tutWidth, tutHeight);
                GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.88f);
                GUI.DrawTexture(tutRect, Texture2D.whiteTexture);

                var tutStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
                tutStyle.normal.textColor = new Color(1f, 0.35f, 0.35f);

                GUI.color = Color.white;
                GUI.Label(tutRect, "[ RED IMPULSE ] — ยิงผลักหุ่น EDITORS ให้ชนกำแพงหรือยิงซ้ำเพื่อทำลาย! | หลบหลังเสาเพื่อกันลำแสงลบข้อมูล", tutStyle);
            }

            // Victory Banner
            if (_allCleared && _clearTimer < 10.0f)
            {
                float winWidth = 720f;
                float winHeight = 85f;
                float winX = (Screen.width - winWidth) * 0.5f;
                float winY = 120f;

                var winRect = new Rect(winX, winY, winWidth, winHeight);
                GUI.color = new Color(0.01f, 0.05f, 0.03f, 0.94f);
                GUI.DrawTexture(winRect, Texture2D.whiteTexture);

                var winStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 19,
                    fontStyle = FontStyle.Bold
                };
                winStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);

                var subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14
                };
                subStyle.normal.textColor = new Color(0.8f, 0.95f, 0.85f);

                GUI.color = Color.white;
                GUI.Label(new Rect(winX, winY + 12, winWidth, 32), "AREA SECURED — EDITORS NEUTRALIZED", winStyle);
                GUI.Label(new Rect(winX, winY + 44, winWidth, 28), "CURATOR: \"Deletion failed. High variable resistance...\" (DEMO CLEARED)", subStyle);
            }
        }
    }

    /// <summary>Rotates the geometric shield fins around The Editor drone.</summary>
    public sealed class DroneFinSpinner : MonoBehaviour
    {
        [SerializeField] private float spinSpeed = 65f;

        private void Update()
        {
            if (!Recorded.Phase.PhaseRecorder.IsWorldPaused)
            {
                transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            }
        }
    }

    /// <summary>
    /// Handles damage and destruction for The Editor drones.
    /// Can be damaged by Red Impulse shots or high-speed wall collisions.
    /// </summary>
    public sealed class EditorDroneDestructible : MonoBehaviour, IColorShotImpact
    {
        [SerializeField] private float maxHealth = 50f;
        private float _currentHealth;
        private Rigidbody _body;
        private bool _isDestroyed;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _body = GetComponent<Rigidbody>();
        }

        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            if (_isDestroyed) return;

            float damage = (firedAmmo == AmmoType.Red || (mappedColorFlag & ObjectColorState.Red) != 0) ? 25f : 15f;
            _currentHealth -= damage;

            // Apply physical knockback
            if (_body != null && !_body.isKinematic)
            {
                Vector3 shove = (shotDirection.sqrMagnitude > 0.01f ? shotDirection.normalized : transform.forward) * 6f;
                shove.y = 0.5f;
                _body.AddForce(shove, ForceMode.Impulse);
            }

            Debug.Log($"[EditorDrone] Hit! Damage: {damage}, Health: {_currentHealth}/{maxHealth}");

            if (_currentHealth <= 0f)
            {
                ExplodeAndDestroy();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isDestroyed) return;
            if (collision.relativeVelocity.magnitude > 3.5f)
            {
                _currentHealth -= 20f;
                Debug.Log($"[EditorDrone] Wall Impact! Velocity: {collision.relativeVelocity.magnitude:0.0}, Health: {_currentHealth}/{maxHealth}");
                if (_currentHealth <= 0f)
                {
                    ExplodeAndDestroy();
                }
            }
        }

        private void ExplodeAndDestroy()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            var ai = GetComponent<SurveyorEnemyAI>();
            if (ai != null) ai.Die();

            // Spawn destruction sparks
            PlayExplosionSparks();

            Destroy(gameObject, 0.25f);
        }

        private void PlayExplosionSparks()
        {
            for (int i = 0; i < 6; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.transform.position = transform.position + Random.insideUnitSphere * 0.4f;
                spark.transform.localScale = Vector3.one * 0.15f;
                Destroy(spark.GetComponent<Collider>());
                var rb = spark.AddComponent<Rigidbody>();
                rb.linearVelocity = (Random.insideUnitSphere + Vector3.up * 1.5f) * 4f;

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(1f, 0.3f, 0.1f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.4f, 0.1f) * 4f);
                spark.GetComponent<Renderer>().sharedMaterial = mat;

                Destroy(spark, 1.2f);
            }
        }
    }
}
