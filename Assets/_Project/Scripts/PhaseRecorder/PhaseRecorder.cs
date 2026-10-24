using System.Collections.Generic;
using Recorded.ColorSystem;
using Recorded.Weapons;
using Recorded.Weapons.Ammo;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Recorded.Phase
{
    /// <summary>
    /// F starts a take. F again rewinds the player to the take's start and plays a hologram
    /// that walks the recorded path and fires the recorded Spectrum shots.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PhaseRecorder : MonoBehaviour
    {
        [SerializeField] private GameObject hologramPrefab;
        [SerializeField] private float maxRecordSeconds = 12f;
        [SerializeField] private float holdAfterPlayback = 0.6f;

        private readonly List<RecordFrame> _frames = new List<RecordFrame>(512);
        private readonly List<SpectrumShotRecord> _pendingShots = new List<SpectrumShotRecord>(4);

        private CharacterController _controller;
        private FirstPersonMotor _motor;
        private FirstPersonLook _look;
        private SpectrumGunController _gun;
        private GameObject _hologram;
        private bool _recording;
        private bool _replaying;
        private float _clock;
        private float _replayClock;
        private int _nextShot;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _startPitch;

        public bool IsRecording => _recording;
        public bool IsReplaying => _replaying;
        public IReadOnlyList<RecordFrame> Frames => _frames;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _motor = GetComponent<FirstPersonMotor>();
            _look = GetComponent<FirstPersonLook>();
            _gun = GetComponentInChildren<SpectrumGunController>(true);
        }

        private void OnEnable()
        {
            if (_gun != null) _gun.ShotFired += OnShotFired;
        }

        private void OnDisable()
        {
            if (_gun != null) _gun.ShotFired -= OnShotFired;
            ClearHologram();
            _recording = false;
            _replaying = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame) Toggle();
            if (_replaying) TickReplay(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (!_recording) return;
            AppendSamples(_clock);
            _clock += Time.fixedDeltaTime;
            if (_clock >= maxRecordSeconds) StopRecording();
        }

        private void Toggle()
        {
            if (_recording) StopRecording();
            else StartRecording();
        }

        private void StartRecording()
        {
            ClearHologram();
            _replaying = false;
            _frames.Clear();
            _pendingShots.Clear();
            _clock = 0f;
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            _startPitch = _look != null ? _look.Pitch : 0f;
            _recording = true;
            AppendSamples(0f);
        }

        private void StopRecording()
        {
            if (!_recording) return;
            AppendSamples(_clock);
            _recording = false;
            _pendingShots.Clear();
            RewindPlayer();
            BeginPlayback();
        }

        private void OnShotFired(SpectrumShotRecord shot)
        {
            if (_recording) _pendingShots.Add(shot);
        }

        private void AppendSamples(float time)
        {
            if (_pendingShots.Count == 0)
            {
                _frames.Add(MakeFrame(time, false, default));
                return;
            }

            for (int i = 0; i < _pendingShots.Count; i++)
                _frames.Add(MakeFrame(time, true, _pendingShots[i]));
            _pendingShots.Clear();
        }

        private RecordFrame MakeFrame(float time, bool fired, SpectrumShotRecord shot)
        {
            return new RecordFrame
            {
                Time = time,
                Position = transform.position,
                Rotation = transform.rotation,
                Pitch = _look != null ? _look.Pitch : _startPitch,
                Fired = fired,
                Ammo = fired ? shot.Ammo : AmmoType.Empty,
                Color = fired ? shot.Color : ObjectColorState.None,
                AimOrigin = fired ? shot.Origin : transform.position + Vector3.up * 1.6f,
                AimDirection = fired ? shot.Direction : transform.forward
            };
        }

        private void RewindPlayer()
        {
            if (_motor != null) _motor.Teleport(_startPosition, _startRotation);
            else
            {
                if (_controller != null) _controller.enabled = false;
                transform.SetPositionAndRotation(_startPosition, _startRotation);
                if (_controller != null) _controller.enabled = true;
            }
            if (_look != null) _look.SnapPitch(_startPitch);
        }

        private void BeginPlayback()
        {
            if (_frames.Count == 0 || hologramPrefab == null) return;
            _hologram = Instantiate(hologramPrefab, _frames[0].Position, _frames[0].Rotation);
            _hologram.name = "Hologram";
            _replayClock = 0f;
            _nextShot = 0;
            _replaying = true;
            ApplyPose(0);
        }

        private void TickReplay(float deltaTime)
        {
            if (_hologram == null || _frames.Count == 0)
            {
                _replaying = false;
                return;
            }

            _replayClock += deltaTime;
            ApplyPose(_replayClock);
            PlayDueShots();

            float end = _frames[_frames.Count - 1].Time + holdAfterPlayback;
            if (_replayClock < end) return;
            ClearHologram();
            _replaying = false;
        }

        private void ApplyPose(float time)
        {
            RecordFrame from = _frames[0];
            RecordFrame to = _frames[_frames.Count - 1];
            for (int i = 0; i < _frames.Count - 1; i++)
            {
                if (_frames[i].Time <= time && time <= _frames[i + 1].Time)
                {
                    from = _frames[i];
                    to = _frames[i + 1];
                    break;
                }
            }

            if (time >= to.Time)
            {
                _hologram.transform.SetPositionAndRotation(to.Position, to.Rotation);
                return;
            }

            float span = Mathf.Max(0.0001f, to.Time - from.Time);
            float blend = Mathf.Clamp01((time - from.Time) / span);
            _hologram.transform.SetPositionAndRotation(
                Vector3.Lerp(from.Position, to.Position, blend),
                Quaternion.Slerp(from.Rotation, to.Rotation, blend));
        }

        private void PlayDueShots()
        {
            if (_gun == null) return;
            while (_nextShot < _frames.Count && _frames[_nextShot].Time <= _replayClock)
            {
                RecordFrame frame = _frames[_nextShot];
                if (frame.Fired)
                {
                    _gun.ReplayRecordedShot(frame.AimOrigin, frame.AimDirection, frame.Ammo, frame.Color, _hologram.transform);
                    SpawnTracer(frame);
                }
                _nextShot++;
            }
        }

        private static void SpawnTracer(RecordFrame frame)
        {
            GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tracer.name = "Hologram_Shot";
            tracer.transform.position = frame.AimOrigin;
            tracer.transform.localScale = Vector3.one * 0.12f;
            Collider sphere = tracer.GetComponent<Collider>();
            if (sphere != null) Destroy(sphere);
            tracer.layer = 2;
            Renderer renderer = tracer.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = SpectrumGunController.GetAmmoColor(frame.Ammo);
                Material material = renderer.material;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                else material.color = color;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color);
                }
            }
            tracer.AddComponent<HologramTracer>().Launch(frame.AimDirection);
        }

        private void ClearHologram()
        {
            if (_hologram == null) return;
            Destroy(_hologram);
            _hologram = null;
        }
    }

    public sealed class HologramTracer : MonoBehaviour
    {
        private Vector3 _velocity;

        public void Launch(Vector3 direction)
        {
            _velocity = (direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward) * 28f;
            Destroy(gameObject, 0.35f);
        }

        private void Update()
        {
            transform.position += _velocity * Time.deltaTime;
        }
    }
}
