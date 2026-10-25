using UnityEngine;
using UnityEngine.Events;
using Recorded.Weapons.Ammo;

namespace Recorded.ColorSystem
{
    /// <summary>Opens after the security kiosk is unlocked and a subsequent Cyan shot hits this object.</summary>
    public sealed class KioskCyanAirlockGate : MonoBehaviour, IColorShotImpact
    {
        [SerializeField] private UnityEvent onAirlockOpened;
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField, Min(0f)] private float doorOpenDistance = 1.8f;
        [SerializeField, Min(0.01f)] private float doorOpenSpeed = 2.5f;
        [SerializeField] private Vector3 leftClosedPosition;
        [SerializeField] private Vector3 rightClosedPosition;
        [SerializeField] private bool positionsCached;

        public bool IsKioskUnlocked { get; private set; }
        public bool IsAirlockOpen { get; private set; }

        public event System.Action OnKioskUnlocked;

        private void Awake()
        {
            EnsureClosedPositions();
        }

        private void Start()
        {
            EnsureClosedPositions();
        }

        private void OnEnable()
        {
            EnsureClosedPositions();
        }

        public void EnsureClosedPositions()
        {
            if (leftDoor != null && (!positionsCached || leftClosedPosition == Vector3.zero))
            {
                leftClosedPosition = leftDoor.localPosition;
                positionsCached = true;
            }
            if (rightDoor != null && (!positionsCached || rightClosedPosition == Vector3.zero))
            {
                rightClosedPosition = rightDoor.localPosition;
                positionsCached = true;
            }

            // Fallback safety if door positions were zero at inspect time
            if (leftClosedPosition == Vector3.zero && leftDoor != null)
                leftClosedPosition = new Vector3(-0.80f, 1.70f, 39.00f);
            if (rightClosedPosition == Vector3.zero && rightDoor != null)
                rightClosedPosition = new Vector3(0.80f, 1.70f, 39.00f);
        }

        private void Update()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;
            if (!IsAirlockOpen) return;

            EnsureClosedPositions();

            if (leftDoor != null)
                leftDoor.localPosition = Vector3.MoveTowards(leftDoor.localPosition, leftClosedPosition + Vector3.left * doorOpenDistance, doorOpenSpeed * Time.deltaTime);
            if (rightDoor != null)
                rightDoor.localPosition = Vector3.MoveTowards(rightDoor.localPosition, rightClosedPosition + Vector3.right * doorOpenDistance, doorOpenSpeed * Time.deltaTime);
        }

        /// <summary>Wire this to the kiosk's existing interaction event.</summary>
        public void UnlockSecurityKiosk()
        {
            IsKioskUnlocked = true;
            OnKioskUnlocked?.Invoke();
            Debug.Log("[KioskCyanAirlockGate] Security lockdown disengaged. Gate unlocked for Cyan impact.");
        }

        public void OpenAirlock()
        {
            if (IsAirlockOpen) return;
            IsAirlockOpen = true;
            onAirlockOpened?.Invoke();
            PlayAirlockOpenAudio();
            Debug.Log("[KioskCyanAirlockGate] Airlock doors opened.");
        }

        public void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection)
        {
            if (!IsKioskUnlocked || IsAirlockOpen || firedAmmo != AmmoType.Cyan)
                return;

            OpenAirlock();
        }

        private void PlayAirlockOpenAudio()
        {
            var audioObj = new GameObject("AirlockOpen_Sound");
            audioObj.transform.position = transform.position;
            var source = audioObj.AddComponent<AudioSource>();
            source.spatialBlend = 0.5f;
            source.volume = 0.85f;

            // Generate clean sci-fi pneumatic slide tone
            int sampleRate = 44100;
            float duration = 1.2f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float freq = Mathf.Lerp(160f, 80f, t / duration);
                float noise = (Random.value * 2f - 1f) * 0.15f;
                samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + noise) * env;
            }
            var clip = AudioClip.Create("Airlock_Slide", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            source.clip = clip;
            source.Play();
            Destroy(audioObj, duration + 0.1f);
        }
    }
}
