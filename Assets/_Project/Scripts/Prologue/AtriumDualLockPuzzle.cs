using UnityEngine;
using UnityEngine.Events;

namespace Recorded.Prologue
{
    /// <summary>
    /// Dual-lock puzzle controller for the Atrium.
    /// Monitors Terminal Alpha and Terminal Beta. When both are active simultaneously,
    /// opens the Area 2 blast doors and marks the Atrium cleared.
    /// </summary>
    public sealed class AtriumDualLockPuzzle : MonoBehaviour
    {
        [SerializeField] private AtriumDualLockTerminal terminalAlpha;
        [SerializeField] private AtriumDualLockTerminal terminalBeta;
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField] private float doorOpenDistance = 3.2f;
        [SerializeField] private float doorOpenSpeed = 2.0f;
        [SerializeField] private UnityEvent onPuzzleCompleted;

        public bool IsOverridden { get; private set; }
        public bool IsDoorsOpen { get; private set; }
        public AtriumDualLockTerminal TerminalAlpha => terminalAlpha;
        public AtriumDualLockTerminal TerminalBeta => terminalBeta;

        private Vector3 _leftClosedLocalPos;
        private Vector3 _rightClosedLocalPos;
        private bool _positionsCached;

        private void Start()
        {
            CacheDoorPositions();
        }

        private void CacheDoorPositions()
        {
            if (!_positionsCached)
            {
                if (leftDoor != null) _leftClosedLocalPos = leftDoor.localPosition;
                if (rightDoor != null) _rightClosedLocalPos = rightDoor.localPosition;
                _positionsCached = true;
            }
        }

        private void Update()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused) return;

            if (!IsOverridden)
            {
                if (terminalAlpha != null && terminalBeta != null)
                {
                    if (terminalAlpha.IsActive && terminalBeta.IsActive)
                    {
                        OverrideSuccess();
                    }
                }
            }

            if (IsDoorsOpen)
            {
                CacheDoorPositions();
                if (leftDoor != null)
                    leftDoor.localPosition = Vector3.MoveTowards(leftDoor.localPosition, _leftClosedLocalPos + Vector3.left * doorOpenDistance, doorOpenSpeed * Time.deltaTime);
                if (rightDoor != null)
                    rightDoor.localPosition = Vector3.MoveTowards(rightDoor.localPosition, _rightClosedLocalPos + Vector3.right * doorOpenDistance, doorOpenSpeed * Time.deltaTime);
            }
        }

        private void OverrideSuccess()
        {
            IsOverridden = true;
            IsDoorsOpen = true;
            Debug.Log("[AtriumDualLockPuzzle] DUAL-LOCK OVERRIDDEN! Blast doors opening to Area 2.");
            PlayDoorOpenAudio();
            onPuzzleCompleted?.Invoke();
        }

        public void SetReferences(AtriumDualLockTerminal alpha, AtriumDualLockTerminal beta, Transform lDoor, Transform rDoor)
        {
            terminalAlpha = alpha;
            terminalBeta = beta;
            leftDoor = lDoor;
            rightDoor = rDoor;
            _positionsCached = false;
            CacheDoorPositions();
        }

        public void ForceOpen()
        {
            OverrideSuccess();
        }

        private void PlayDoorOpenAudio()
        {
            int sampleRate = 44100;
            float duration = 2.0f;
            int numSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float rumble = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.7f;
                float hiss = (Random.value * 2f - 1f) * 0.25f;
                samples[i] = (rumble + hiss) * env;
            }
            var clip = AudioClip.Create("BlastDoor_Open", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            var audioObj = new GameObject("BlastDoor_Sound");
            audioObj.transform.position = transform.position;
            var src = audioObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.3f;
            src.volume = 0.9f;
            src.clip = clip;
            src.Play();
            Destroy(audioObj, duration + 0.1f);
        }
    }
}
