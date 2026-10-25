using UnityEngine;

namespace Recorded.Prologue
{
    /// <summary>
    /// Implements Scene 03 "The 4-Second Loop":
    /// Security guard drops coffee cup every 4 seconds, shards scatter,
    /// cyan scanner sweeps, cup and shards rewind into hand.
    /// Radio dialogue plays between ECHO and HQ when player approaches.
    /// </summary>
    public sealed class The4SecondLoop : MonoBehaviour
    {
        private const float LoopDuration = 4.0f;

        [SerializeField] private Transform handAnchor;
        [SerializeField] private GameObject cupInstance;
        [SerializeField] private Transform scanLine;
        [SerializeField] private Light scanLight;

        private Vector3 _handHoldLocalPos;
        private Vector3 _floorDropLocalPos;
        private Transform[] _shards;
        private Vector3[] _shardScatterOffsets;
        private float _timer;
        private bool _dialogueTriggered;
        private float _dialogueTimer;
        private Transform _playerTransform;
        private bool _shatterSoundPlayed;
        private bool _rewindSoundPlayed;

        private void Start()
        {
            var armR = transform.Find("ArmR");
            handAnchor = armR != null ? armR : transform;

            SetupGuardVisor();
            SetupCupAndShards();
            SetupScanLine();

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                var cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) player = cc.gameObject;
            }
            if (player != null) _playerTransform = player.transform;
        }

        private void SetupGuardVisor()
        {
            var head = transform.Find("Head");
            if (head == null || head.Find("SecurityVisor") != null) return;

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "SecurityVisor";
            visor.transform.SetParent(head, false);
            visor.transform.localPosition = new Vector3(0f, 0.02f, 0.09f);
            visor.transform.localScale = new Vector3(0.19f, 0.05f, 0.06f);
            Destroy(visor.GetComponent<Collider>());

            var rend = visor.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.1f, 0.85f, 1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.1f, 0.85f, 1f) * 3f);
            rend.sharedMaterial = mat;
        }

        private void SetupCupAndShards()
        {
            if (cupInstance != null) return;

            cupInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cupInstance.name = "LoopCoffeeCup";
            cupInstance.transform.SetParent(transform, false);
            cupInstance.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);

            var col = cupInstance.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = cupInstance.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.92f, 0.90f, 0.84f);
                rend.sharedMaterial = mat;
            }

            _handHoldLocalPos = handAnchor != null ? transform.InverseTransformPoint(handAnchor.position + Vector3.down * 0.28f + transform.forward * 0.18f) : new Vector3(0.26f, 0.95f, 0.2f);
            _floorDropLocalPos = new Vector3(_handHoldLocalPos.x, 0.08f, _handHoldLocalPos.z + 0.35f);
            cupInstance.transform.localPosition = _handHoldLocalPos;

            // Setup 3 ceramic shards
            _shards = new Transform[3];
            _shardScatterOffsets = new Vector3[]
            {
                new Vector3(0.18f, 0.04f, 0.12f),
                new Vector3(-0.15f, 0.04f, 0.16f),
                new Vector3(0.04f, 0.04f, -0.15f)
            };

            for (int i = 0; i < 3; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = $"CupShard_{i}";
                shard.transform.SetParent(transform, false);
                shard.transform.localScale = new Vector3(0.04f, 0.02f, 0.05f);
                Destroy(shard.GetComponent<Collider>());
                var shardRend = shard.GetComponent<Renderer>();
                var shardMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                shardMat.color = new Color(0.4f, 0.25f, 0.15f); // coffee stain brown/white
                shardRend.sharedMaterial = shardMat;
                shard.SetActive(false);
                _shards[i] = shard.transform;
            }
        }

        private void SetupScanLine()
        {
            if (scanLine != null) return;

            var scanObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scanObj.name = "CyanScanBeam";
            scanObj.transform.SetParent(transform, false);
            scanObj.transform.localScale = new Vector3(1.2f, 0.02f, 0.8f);
            scanObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            var col = scanObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = scanObj.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.1f, 0.9f, 1f, 0.7f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.1f, 0.9f, 1f) * 4f);
                rend.sharedMaterial = mat;
            }

            var lightObj = new GameObject("ScanLight");
            lightObj.transform.SetParent(scanObj.transform, false);
            scanLight = lightObj.AddComponent<Light>();
            scanLight.type = LightType.Point;
            scanLight.color = new Color(0.18f, 0.88f, 1f);
            scanLight.range = 3.5f;
            scanLight.intensity = 3.0f;

            scanLine = scanObj.transform;
            scanLine.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;

            _timer += Time.deltaTime;
            float loopTime = _timer % LoopDuration;

            if (cupInstance != null)
            {
                if (loopTime < 1.4f)
                {
                    // Guard holding cup (subtle jitter)
                    _shatterSoundPlayed = false;
                    _rewindSoundPlayed = false;
                    float jitter = Mathf.Sin(Time.time * 28f) * 0.003f;
                    cupInstance.transform.localPosition = _handHoldLocalPos + new Vector3(jitter, 0f, jitter);
                    cupInstance.transform.localRotation = Quaternion.identity;
                    if (scanLine != null) scanLine.gameObject.SetActive(false);
                    SetShardsActive(false);
                }
                else if (loopTime < 1.9f)
                {
                    // Cup falling
                    float dropProgress = Mathf.Clamp01((loopTime - 1.4f) / 0.5f);
                    float easedDrop = dropProgress * dropProgress;
                    cupInstance.transform.localPosition = Vector3.Lerp(_handHoldLocalPos, _floorDropLocalPos, easedDrop);
                    cupInstance.transform.localRotation = Quaternion.Euler(dropProgress * 45f, 0f, dropProgress * 30f);
                    SetShardsActive(false);
                }
                else if (loopTime < 2.4f)
                {
                    // Cup shattered on floor
                    cupInstance.transform.localPosition = _floorDropLocalPos;
                    if (!_shatterSoundPlayed)
                    {
                        _shatterSoundPlayed = true;
                        PlayShatterAudio();
                    }
                    SetShardsActive(true);
                    for (int i = 0; i < 3; i++)
                    {
                        if (_shards[i] != null) _shards[i].localPosition = _floorDropLocalPos + _shardScatterOffsets[i];
                    }
                    if (scanLine != null) scanLine.gameObject.SetActive(false);
                }
                else if (loopTime < 3.4f)
                {
                    // Scanner sweep down
                    if (scanLine != null)
                    {
                        scanLine.gameObject.SetActive(true);
                        float scanProgress = (loopTime - 2.4f) / 1.0f;
                        float scanY = Mathf.Lerp(2.2f, 0.05f, scanProgress);
                        scanLine.localPosition = new Vector3(0f, scanY, 0f);
                    }
                    cupInstance.transform.localPosition = _floorDropLocalPos;
                }
                else
                {
                    // 3.4s - 4.0s: Rewind back into hand
                    if (!_rewindSoundPlayed)
                    {
                        _rewindSoundPlayed = true;
                        PlayRewindAudio();
                    }
                    if (scanLine != null) scanLine.gameObject.SetActive(false);
                    float rewindProgress = Mathf.Clamp01((loopTime - 3.4f) / 0.6f);
                    float smoothRewind = Mathf.SmoothStep(0f, 1f, rewindProgress);
                    cupInstance.transform.localPosition = Vector3.Lerp(_floorDropLocalPos, _handHoldLocalPos, smoothRewind);
                    cupInstance.transform.localRotation = Quaternion.Euler((1f - smoothRewind) * 45f, 0f, (1f - smoothRewind) * 30f);

                    // Shards pull back into cup
                    for (int i = 0; i < 3; i++)
                    {
                        if (_shards[i] != null)
                        {
                            Vector3 shardTarget = _floorDropLocalPos + _shardScatterOffsets[i];
                            _shards[i].localPosition = Vector3.Lerp(shardTarget, _handHoldLocalPos, smoothRewind);
                        }
                    }
                }
            }

            // Dialogue proximity trigger
            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist < 9.0f && !_dialogueTriggered)
                {
                    _dialogueTriggered = true;
                    _dialogueTimer = 0f;
                }
            }

            if (_dialogueTriggered && _dialogueTimer < 14.0f)
            {
                _dialogueTimer += Time.deltaTime;
            }
        }

        private void SetShardsActive(bool active)
        {
            if (_shards == null) return;
            for (int i = 0; i < _shards.Length; i++)
            {
                if (_shards[i] != null) _shards[i].gameObject.SetActive(active);
            }
        }

        private void PlayShatterAudio()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float crack = (Random.value * 2f - 1f) * 0.7f;
                float ring = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.4f;
                samples[i] = (crack + ring) * env;
            }
            var clip = AudioClip.Create("Cup_Shatter", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Shatter_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.4f;
            src.volume = 0.8f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void PlayRewindAudio()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float freq = Mathf.Lerp(120f, 950f, t / duration); // rising rewind pitch
                float tapeWhine = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f;
                samples[i] = tapeWhine * env;
            }
            var clip = AudioClip.Create("Rewind_Sound", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("Rewind_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.4f;
            src.volume = 0.7f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }

        private void OnGUI()
        {
            if (!_dialogueTriggered || _dialogueTimer > 12.0f) return;

            float width = 760f;
            float height = 70f;
            float x = (Screen.width - width) * 0.5f;
            float y = 50f;

            var bgRect = new Rect(x, y, width, height);
            GUI.color = new Color(0.01f, 0.03f, 0.06f, 0.90f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                wordWrap = true
            };

            GUI.color = Color.white;
            if (_dialogueTimer < 5.0f)
            {
                titleStyle.normal.textColor = new Color(0.2f, 0.85f, 1f);
                bodyStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x + 16, y + 8, width - 32, 20), "ECHO [RADIO COMMS]", titleStyle);
                GUI.Label(new Rect(x + 16, y + 26, width - 32, 38), "\"HQ... ผมเจอผู้รอดชีวิต แต่มันไม่ใช่โฮโลแกรม... หมอนี่ติดอยู่ในลูปเวลา\"", bodyStyle);
            }
            else
            {
                titleStyle.normal.textColor = new Color(1f, 0.4f, 0.3f);
                bodyStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f);
                GUI.Label(new Rect(x + 16, y + 8, width - 32, 20), "HQ [DIRECT TRANSMISSION]", titleStyle);
                GUI.Label(new Rect(x + 16, y + 26, width - 32, 38), "\"นั่นไม่ใช่ลูปเวลา ECHO ระบบรักษาความปลอดภัยมันกำลัง 'เขียนทับ' ความเป็นจริง... ปลดล็อคเทอร์มินัลแล้วใช้อาวุธของคุณยิงฝ่าออกไป!\"", bodyStyle);
            }
        }
    }
}
