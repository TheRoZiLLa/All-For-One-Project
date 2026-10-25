using System.Collections.Generic;
using Recorded.ColorSystem;
using Recorded.Weapons;
using Recorded.Weapons.Ammo;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Recorded.Phase
{
    /// <summary>
    /// The live hologram, if one exists. Cleared before the object is destroyed
    /// so enemy targeting never reads a dead transform.
    /// </summary>
    public static class HologramPresence
    {
        public static Transform Active { get; private set; }
        public static bool IsLive => Active != null;

        public static void Set(Transform hologram) => Active = hologram;

        public static void Clear(Transform hologram)
        {
            if (hologram == null || Active == hologram) Active = null;
        }

        public static Transform Resolve(Transform player) => Active != null ? Active : player;

        public static void SteerAgent(Component owner, Transform aim)
        {
            if (owner == null || aim == null) return;
            NavMeshAgent agent = owner.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.SetDestination(aim.position);
        }
    }

    /// <summary>
    /// F enters/exits the planning world. G plays the saved take in the main world.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PhaseRecorder : MonoBehaviour
    {
        [SerializeField] private GameObject hologramPrefab;
        [SerializeField] private float maxRecordSeconds = 12f;
        [SerializeField] private float holdAfterPlayback = 0.6f;
        [SerializeField] private float rewindSpeed = 4f;
        [SerializeField] private Material worldHologramMaterial;
        [SerializeField, Min(0.01f)] private float sanityRecoveryPerSecond = 0.08f;
        public static PhaseRecorder ActiveRecorder { get; private set; }
        public static bool IsWorldPaused => ActiveRecorder != null && (ActiveRecorder._recording || ActiveRecorder._rewinding);
        public static System.Collections.IEnumerator WaitWorldSeconds(float seconds)
        {
            while (seconds > 0f)
            {
                yield return null;
                if (!IsWorldPaused) seconds -= Time.deltaTime;
            }
        }

        private readonly List<RecordFrame> _frames = new List<RecordFrame>(512);
        private readonly List<SpectrumShotRecord> _pendingShots = new List<SpectrumShotRecord>(4);
        private readonly List<RecordFrame> _savedFrames = new List<RecordFrame>(512);
        private readonly PhaseWorldPreview _world = new PhaseWorldPreview();
        private readonly AmmoType[] _savedAmmo = new AmmoType[CylinderAmmoController.CylinderCapacity];
        private readonly List<KioskCyanAirlockGate> _pendingKiosks = new List<KioskCyanAirlockGate>();
        private int _savedChamber;
        private bool _lookWasEnabled;
        private float _possessionTime;
        public float Sanity { get; private set; } = 1f;
        public bool HasRecording => _savedFrames.Count > 1;
        public string Status { get; private set; } = "MAIN WORLD";

        private CharacterController _controller;
        private FirstPersonMotor _motor;
        private FirstPersonLook _look;
        private SpectrumGunController _gun;
        private Rigidbody _body;
        private GameObject _hologram;
        private bool _recording;
        private bool _rewinding;
        private bool _replaying;
        private bool _controllerWasEnabled = true;
        private bool _motorWasEnabled = true;
        private bool _gunWasEnabled = true;
        private bool _bodyWasKinematic;
        private float _clock;
        private float _replayClock;
        private float _rewindTime;
        private int _nextShot;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _startPitch;

        public bool IsRecording => _recording;
        public bool IsRewinding => _rewinding;
        public bool IsReplaying => _replaying;
        public IReadOnlyList<RecordFrame> Frames => _recording || _rewinding ? _frames : _savedFrames;

        private void Awake() => ResolveRefs();

        private void ResolveRefs()
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_motor == null) _motor = GetComponent<FirstPersonMotor>();
            if (_look == null) _look = GetComponent<FirstPersonLook>();
            if (_gun == null) _gun = GetComponentInChildren<SpectrumGunController>(true);
            if (_body == null) _body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (_gun != null) _gun.ShotFired += OnShotFired;
        }

        private void OnDisable()
        {
            if (_gun != null) _gun.ShotFired -= OnShotFired;
            if (_recording) { SuspendGameplay(); SnapToStart(); ResumeGameplay(); }
            if (_rewinding) ResumeGameplay();
            if (_recording || _rewinding) RestoreAmmo();
            _world.Exit();
            if (ActiveRecorder == this) ActiveRecorder = null;
            ClearHologram();
            _recording = false;
            _rewinding = false;
            _replaying = false;
            _possessionTime = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_rewinding)
            {
                TickRewind(dt);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool inputAllowed = Cursor.lockState == CursorLockMode.Locked;
            if (keyboard != null && inputAllowed)
            {
                if (keyboard.backspaceKey.wasPressedThisFrame) CancelRecording();
                else if (keyboard.fKey.wasPressedThisFrame) Toggle();
                else if (keyboard.gKey.wasPressedThisFrame) PlayRecording();
            }
            if (_recording) TickRecording(dt);
            else Sanity = Mathf.Min(1f, Sanity + Mathf.Max(0.01f, sanityRecoveryPerSecond) * dt);
            if (_replaying) TickReplay(dt);
        }

        private void TickRecording(float deltaTime)
        {
            _clock += deltaTime;
            AppendSamples(_clock);
            Sanity = Mathf.Max(0f, Sanity - deltaTime / Mathf.Max(1f, maxRecordSeconds));
            if (Sanity > 0f) return;
            _recording = false;
            Status = "OVERRUN - CONTROL LOST / TAKE DISCARDED";
            _pendingShots.Clear(); _pendingKiosks.Clear();
            _possessionTime = 2f;
            BeginRewind();
        }

        private void Toggle()
        {
            if (_recording) StopRecording();
            else StartRecording();
        }

        public bool StartRecording()
        {
            if (_recording || _rewinding || (ActiveRecorder != null && ActiveRecorder != this)) return false;
            if (Sanity < 0.25f) { Status = "MAIN WORLD - RECOVER SANITY BEFORE RECORDING"; return false; }
            ResolveRefs();
            if (_gun != null && _gun.IsReloading) return false;
            ClearHologram();
            _replaying = false;
            _frames.Clear();
            _pendingShots.Clear();
            _pendingKiosks.Clear();
            _clock = 0f;
            _possessionTime = 0f;
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            _startPitch = _look != null ? _look.Pitch : 0f;
            _recording = true;
            ActiveRecorder = this;
            Status = "HOLOGRAM WORLD - RECORDING";
            if (_gun != null && _gun.AmmoController != null)
            {
                _savedChamber = _gun.AmmoController.ActiveChamberIndex;
                for (int i = 0; i < _savedAmmo.Length; i++) _savedAmmo[i] = _gun.AmmoController.GetChamberAmmo(i);
            }
            _world.Enter(transform, worldHologramMaterial);
            AppendSamples(0f);
            return true;
        }

        public void StopRecording()
        {
            if (!_recording) return;
            AppendSamples(_clock);
            _recording = false;
            _pendingShots.Clear();
            _savedFrames.Clear();
            _savedFrames.AddRange(_frames);
            Status = "RETURNING - TAKE SAVED";
            BeginRewind();
        }

        public void CancelRecording()
        {
            if (!_recording) return;
            AppendSamples(_clock);
            _recording = false;
            _pendingShots.Clear(); _pendingKiosks.Clear();
            Status = "RETURNING - TAKE CANCELLED";
            BeginRewind();
        }

        public void RecordKiosk(KioskCyanAirlockGate kiosk)
        {
            if (_recording && kiosk != null && !_pendingKiosks.Contains(kiosk)) _pendingKiosks.Add(kiosk);
        }

        private void RestoreAmmo()
        {
            if (_gun == null || _gun.AmmoController == null) return;
            if (_gun.IsReloading)
            {
                bool enabled = _gun.enabled;
                _gun.enabled = false;
                _gun.enabled = enabled;
            }
            for (int i = 0; i < _savedAmmo.Length; i++) _gun.AmmoController.LoadChamber(i, _savedAmmo[i]);
            _gun.AmmoController.SetActiveChamber(_savedChamber);
            if (_gun.RotationController != null) _gun.RotationController.SyncToActiveChamber();
        }

        private void OnShotFired(SpectrumShotRecord shot)
        {
            if (_recording) _pendingShots.Add(shot);
        }

        private void AppendSamples(float time)
        {
            for (int i = 0; i < _pendingKiosks.Count; i++)
            {
                var frame = MakeFrame(time, false, default);
                frame.Kiosk = _pendingKiosks[i];
                _frames.Add(frame);
            }
            _pendingKiosks.Clear();
            if (_pendingShots.Count == 0) _frames.Add(MakeFrame(time, false, default));
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

        private void BeginRewind()
        {
            if (_frames.Count == 0)
            {
                SnapToStart();
                FinishReturn();
                return;
            }

            _rewinding = true;
            _rewindTime = _frames[_frames.Count - 1].Time;
            SuspendGameplay();
            ApplyRewindPose(_rewindTime);
        }

        private void TickRewind(float deltaTime)
        {
            if (_possessionTime > 0f) { _possessionTime -= deltaTime; return; }
            _rewindTime -= Mathf.Max(0.01f, rewindSpeed) * deltaTime;
            if (_rewindTime > 0f)
            {
                ApplyRewindPose(_rewindTime);
                return;
            }

            _rewinding = false;
            SnapToStart();
            FinishReturn();
        }

        private void FinishReturn()
        {
            ResumeGameplay();
            RestoreAmmo();
            _world.Exit();
            if (ActiveRecorder == this) ActiveRecorder = null;
            Status = HasRecording ? "MAIN WORLD - TAKE READY [G]" : "MAIN WORLD";
        }

        private void SuspendGameplay()
        {
            ResolveRefs();
            _controllerWasEnabled = _controller == null || _controller.enabled;
            _motorWasEnabled = _motor == null || _motor.enabled;
            _gunWasEnabled = _gun == null || _gun.enabled;
            _lookWasEnabled = _look == null || _look.enabled;
            _bodyWasKinematic = _body != null && _body.isKinematic;
            if (_motor != null) _motor.enabled = false;
            if (_gun != null) _gun.enabled = false;
            if (_look != null) _look.enabled = false;
            if (_controller != null) _controller.enabled = false;
            if (_body != null)
            {
                if (!_body.isKinematic) { _body.linearVelocity = Vector3.zero; _body.angularVelocity = Vector3.zero; }
                _body.isKinematic = true;
            }
        }

        private void ResumeGameplay()
        {
            if (_body != null)
            {
                _body.isKinematic = _bodyWasKinematic;
                if (!_body.isKinematic) { _body.linearVelocity = Vector3.zero; _body.angularVelocity = Vector3.zero; }
            }
            if (_motor != null)
            {
                _motor.enabled = _motorWasEnabled;
                _motor.Teleport(_startPosition, _startRotation);
            }
            if (_controller != null) _controller.enabled = _controllerWasEnabled;
            if (_gun != null) _gun.enabled = _gunWasEnabled;
            if (_look != null) { _look.SnapPitch(_startPitch); _look.enabled = _lookWasEnabled; }
        }

        private void SnapToStart()
        {
            transform.SetPositionAndRotation(_startPosition, _startRotation);
            if (_look != null) _look.SnapPitch(_startPitch);
        }

        private void ApplyRewindPose(float time)
        {
            Sample(time, out Vector3 position, out Quaternion rotation, out float pitch);
            transform.SetPositionAndRotation(position, rotation);
            if (_look != null) _look.SnapPitch(pitch);
        }

        public bool PlayRecording()
        {
            if (_recording || _rewinding || _replaying || !HasRecording || hologramPrefab == null) return false;
            ResolveRefs();
            _frames.Clear(); _frames.AddRange(_savedFrames);
            _hologram = Instantiate(hologramPrefab, _frames[0].Position, _frames[0].Rotation);
            _hologram.name = "Hologram";
            HologramPresence.Set(_hologram.transform);
            _replayClock = 0f;
            _nextShot = 0;
            _replaying = true;
            Status = "MAIN WORLD - PLAYING HOLOGRAM";
            ApplyPose(0);
            PlayDueShots();
            return true;
        }

        private void TickReplay(float deltaTime)
        {
            if (_hologram == null || _frames.Count == 0)
            {
                _replaying = false;
                HologramPresence.Clear(null);
                return;
            }

            _replayClock += deltaTime;
            ApplyPose(_replayClock);
            PlayDueShots();

            float end = _frames[_frames.Count - 1].Time + holdAfterPlayback;
            if (_replayClock < end) return;
            ClearHologram();
            _replaying = false;
            Status = "MAIN WORLD - TAKE READY [G]";
        }

        private void ApplyPose(float time)
        {
            if (_hologram == null) return;
            Sample(time, out Vector3 position, out Quaternion rotation, out _);
            _hologram.transform.SetPositionAndRotation(position, rotation);
        }

        private void Sample(float time, out Vector3 position, out Quaternion rotation, out float pitch)
        {
            RecordFrame from = _frames[0];
            RecordFrame to = _frames[_frames.Count - 1];
            if (time <= from.Time)
            {
                position = from.Position;
                rotation = from.Rotation;
                pitch = from.Pitch;
                return;
            }
            if (time >= to.Time)
            {
                position = to.Position;
                rotation = to.Rotation;
                pitch = to.Pitch;
                return;
            }

            for (int i = 0; i < _frames.Count - 1; i++)
            {
                if (_frames[i].Time <= time && time <= _frames[i + 1].Time)
                {
                    from = _frames[i];
                    to = _frames[i + 1];
                    break;
                }
            }

            float span = Mathf.Max(0.0001f, to.Time - from.Time);
            float blend = Mathf.Clamp01((time - from.Time) / span);
            position = Vector3.Lerp(from.Position, to.Position, blend);
            rotation = Quaternion.Slerp(from.Rotation, to.Rotation, blend);
            pitch = Mathf.Lerp(from.Pitch, to.Pitch, blend);
        }

        private void PlayDueShots()
        {
            while (_nextShot < _frames.Count && _frames[_nextShot].Time <= _replayClock)
            {
                RecordFrame frame = _frames[_nextShot];
                _nextShot++;
                if (frame.Kiosk != null) frame.Kiosk.UnlockSecurityKiosk();
                if (!frame.Fired || _gun == null) continue;
                Vector3 origin = frame.AimOrigin;
                Vector3 direction = frame.AimDirection;
                AmmoType ammo = frame.Ammo;
                ObjectColorState color = frame.Color;
                Transform ignore = _hologram != null ? _hologram.transform : null;
                _gun.ReplayRecordedShot(origin, direction, ammo, color, ignore);
                SpawnTracer(origin, direction, ammo);
            }
        }

        private static void SpawnTracer(Vector3 origin, Vector3 direction, AmmoType ammo)
        {
            GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tracer.name = "Hologram_Shot";
            tracer.transform.position = origin;
            tracer.transform.localScale = Vector3.one * 0.12f;
            Collider sphere = tracer.GetComponent<Collider>();
            if (sphere != null) { sphere.enabled = false; Destroy(sphere); }
            tracer.layer = 2;
            Renderer renderer = tracer.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color tint = SpectrumGunController.GetAmmoColor(ammo);
                Material material = renderer.material;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                else material.color = tint;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", tint);
                }
            }
            tracer.AddComponent<HologramTracer>().Launch(direction);
        }

        private void ClearHologram()
        {
            if (_hologram == null)
            {
                HologramPresence.Clear(null);
                return;
            }
            Transform leaving = _hologram.transform;
            HologramPresence.Clear(leaving);
            if (Application.isPlaying) Destroy(_hologram);
            else DestroyImmediate(_hologram);
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

        private void OnDestroy()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) Destroy(renderer.sharedMaterial);
        }
    }
}
