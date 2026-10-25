using System;
using UnityEngine;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Implements the RECORDED RGB object color-state system.
    /// Manages an additive RGB state flag (None, Red, Green, Blue, Yellow, Magenta, Cyan, White),
    /// independent 10-second expiration timers per primary color,
    /// duplicate-hit protection (hitting an already active color does not reset or extend its timer),
    /// and visual representation via MaterialPropertyBlock (_BaseColor / _Color) across single or multiple renderers.
    /// </summary>
    public class RGBColorTarget : MonoBehaviour, IColorTarget
    {
        public const float ColorDuration = 10.0f;

        [Header("State")]
        [Tooltip("Active color combination flags.")]
        [SerializeField] private ObjectColorState currentState = ObjectColorState.None;

        [Tooltip("Puzzle pieces respawn when they fall. Ordinary props do not.")]
        [SerializeField] private bool isPuzzleCritical;

        [Header("Renderer Configuration")]
        [Tooltip("If true, applies color state to all child Renderers as well as the root Renderer.")]
        [SerializeField] private bool includeChildRenderers = true;

        [Header("Color Palette Configuration")]
        public static readonly Color ColorNeutralGrey = new Color(0.5f, 0.5f, 0.5f, 1f);     // #7F7F7F
        public static readonly Color ColorRed         = new Color(0.95f, 0.15f, 0.15f, 1f);
        public static readonly Color ColorGreen       = new Color(0.15f, 0.88f, 0.25f, 1f);
        public static readonly Color ColorBlue        = new Color(0.15f, 0.50f, 0.95f, 1f);
        public static readonly Color ColorYellow      = new Color(0.95f, 0.90f, 0.15f, 1f); // Red + Green
        public static readonly Color ColorMagenta     = new Color(0.92f, 0.20f, 0.88f, 1f); // Red + Blue
        public static readonly Color ColorCyan        = new Color(0.18f, 0.92f, 0.95f, 1f); // Green + Blue
        public static readonly Color ColorWhite       = new Color(0.96f, 0.96f, 0.96f, 1f); // Red + Green + Blue

        // Independent expiration timestamps (Time.time)
        private float _expireRed = -1f;
        private float _expireGreen = -1f;
        private float _expireBlue = -1f;

        private bool _pauseExpireRed;
        private bool _pauseExpireGreen;
        private bool _pauseExpireBlue;
        private bool _cyanStasisOverride;

        private Renderer _renderer;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        public ObjectColorState CurrentColorState => currentState;
        public Rigidbody PhysicsBody => GetComponent<Rigidbody>();
        public bool IsPuzzleCritical => isPuzzleCritical;
        public event Action<ObjectColorState> OnColorStateChanged;

        public void SetPuzzleCritical(bool value)
        {
            isPuzzleCritical = value;
        }

        /// <summary>
        /// Mass is over the telekinesis limit. Drop Green and freeze as Blue.
        /// </summary>
        public void CollapseOverweightCyanToStasis()
        {
            ObjectColorState active = currentState & (ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue);
            if (active != (ObjectColorState.Green | ObjectColorState.Blue)) return;
            PromoteCyanToBlueStasis();
        }

        private void Awake()
        {
            CacheRenderers();
            _propBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            // Target starts in None state and appears Neutral Grey
            UpdateVisuals();
        }

        public void CacheRenderers()
        {
            _renderer = GetComponent<Renderer>();
            if (includeChildRenderers)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }
            else if (_renderer != null)
            {
                _renderers = new Renderer[] { _renderer };
            }
            else
            {
                _renderers = Array.Empty<Renderer>();
            }
        }

        private void Update()
        {
            if (Recorded.Phase.PhaseRecorder.IsWorldPaused)
            {
                _expireRed += Time.deltaTime;
                _expireGreen += Time.deltaTime;
                _expireBlue += Time.deltaTime;
                return;
            }
            if (currentState == ObjectColorState.None) return;

            float now = Time.time;
            bool stateChanged = false;

            // Check Red expiration
            if (!_pauseExpireRed && (currentState & ObjectColorState.Red) != 0 && now >= _expireRed)
            {
                currentState &= ~ObjectColorState.Red;
                _expireRed = -1f;
                stateChanged = true;
            }

            // Check Green expiration
            if (!_pauseExpireGreen && (currentState & ObjectColorState.Green) != 0 && now >= _expireGreen)
            {
                currentState &= ~ObjectColorState.Green;
                _expireGreen = -1f;
                stateChanged = true;
            }

            // Check Blue expiration
            if (!_pauseExpireBlue && (currentState & ObjectColorState.Blue) != 0 && now >= _expireBlue)
            {
                currentState &= ~ObjectColorState.Blue;
                _expireBlue = -1f;
                stateChanged = true;
            }

            if (stateChanged)
            {
                UpdateVisuals();
                OnColorStateChanged?.Invoke(currentState);
            }
        }

        /// <summary>
        /// Applies one or more primary colors to this target.
        /// Adheres strictly to the duplicate-hit rule:
        /// - If a color is not currently active, it is added and its 10-second lifetime starts now.
        /// - If a color is already active, this call does NOT reset, extend, or restart its timer.
        /// </summary>
        public void ApplyColor(ObjectColorState color)
        {
            ObjectColorState incoming = color & (ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue);
            ObjectColorState active = currentState & (ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue);
            if (incoming == ObjectColorState.Blue && active == (ObjectColorState.Green | ObjectColorState.Blue))
            {
                PromoteCyanToBlueStasis();
                return;
            }

            bool stateChanged = false;
            float now = Time.time;

            // Apply Red
            if ((color & ObjectColorState.Red) != 0)
            {
                if ((currentState & ObjectColorState.Red) == 0)
                {
                    currentState |= ObjectColorState.Red;
                    _expireRed = now + ColorDuration;
                    stateChanged = true;
                }
                // Duplicate hit on active Red: timer is preserved without modification
            }

            // Apply Green
            if ((color & ObjectColorState.Green) != 0)
            {
                if ((currentState & ObjectColorState.Green) == 0)
                {
                    currentState |= ObjectColorState.Green;
                    _expireGreen = now + ColorDuration;
                    stateChanged = true;
                }
                // Duplicate hit on active Green: timer is preserved without modification
            }

            // Apply Blue
            if ((color & ObjectColorState.Blue) != 0)
            {
                if ((currentState & ObjectColorState.Blue) == 0)
                {
                    currentState |= ObjectColorState.Blue;
                    _expireBlue = now + ColorDuration;
                    stateChanged = true;
                }
                // Duplicate hit on active Blue: timer is preserved without modification
            }

            if (stateChanged)
            {
                UpdateVisuals();
                OnColorStateChanged?.Invoke(currentState);
            }
        }

        /// <summary>
        /// Resets all active colors immediately, returning the target to Neutral Grey.
        /// </summary>
        public void ResetColors()
        {
            currentState = ObjectColorState.None;
            _expireRed = -1f;
            _expireGreen = -1f;
            _expireBlue = -1f;
            _pauseExpireRed = false;
            _pauseExpireGreen = false;
            _pauseExpireBlue = false;
            _cyanStasisOverride = false;
            UpdateVisuals();
            OnColorStateChanged?.Invoke(currentState);
        }

        /// <summary>
        /// True only for the state-change caused by shooting Blue again while Cyan is active.
        /// The reactor consumes this so that shot locks Blue stasis instead of clearing to None.
        /// </summary>
        public bool ConsumeCyanStasisOverride()
        {
            if (!_cyanStasisOverride) return false;
            _cyanStasisOverride = false;
            return true;
        }

        /// <summary>
        /// Cyan + another Blue shot: drop Green immediately and leave only Blue.
        /// The existing Blue timer is not refreshed.
        /// </summary>
        private void PromoteCyanToBlueStasis()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            currentState = ObjectColorState.Blue;
            _expireGreen = -1f;
            _pauseExpireGreen = false;
            _cyanStasisOverride = true;
            UpdateVisuals();
            OnColorStateChanged?.Invoke(currentState);
        }

        /// <summary>
        /// Pauses expiration for the given primary flags (used by mix reactions: Magenta, Cyan).
        /// </summary>
        public void SetPrimaryTimerPaused(ObjectColorState primaries, bool paused)
        {
            if ((primaries & ObjectColorState.Red) != 0) _pauseExpireRed = paused;
            if ((primaries & ObjectColorState.Green) != 0) _pauseExpireGreen = paused;
            if ((primaries & ObjectColorState.Blue) != 0) _pauseExpireBlue = paused;
        }

        /// <summary>
        /// Clears the specified primary bits and their timers without touching other active primaries.
        /// </summary>
        public void ClearPrimaries(ObjectColorState primaries)
        {
            bool stateChanged = false;

            if ((primaries & ObjectColorState.Red) != 0 && (currentState & ObjectColorState.Red) != 0)
            {
                currentState &= ~ObjectColorState.Red;
                _expireRed = -1f;
                _pauseExpireRed = false;
                stateChanged = true;
            }

            if ((primaries & ObjectColorState.Green) != 0 && (currentState & ObjectColorState.Green) != 0)
            {
                currentState &= ~ObjectColorState.Green;
                _expireGreen = -1f;
                _pauseExpireGreen = false;
                stateChanged = true;
            }

            if ((primaries & ObjectColorState.Blue) != 0 && (currentState & ObjectColorState.Blue) != 0)
            {
                currentState &= ~ObjectColorState.Blue;
                _expireBlue = -1f;
                _pauseExpireBlue = false;
                stateChanged = true;
            }

            if (stateChanged)
            {
                UpdateVisuals();
                OnColorStateChanged?.Invoke(currentState);
            }
        }

        /// <summary>
        /// Resolves the displayed color for any given ObjectColorState flags.
        /// Derives the color strictly from the combined flags, never from previous state.
        /// </summary>
        public static Color GetColorForState(ObjectColorState state)
        {
            switch (state)
            {
                case ObjectColorState.Red:
                    return ColorRed;
                case ObjectColorState.Green:
                    return ColorGreen;
                case ObjectColorState.Blue:
                    return ColorBlue;
                case ObjectColorState.Red | ObjectColorState.Green:
                    return ColorYellow;
                case ObjectColorState.Red | ObjectColorState.Blue:
                    return ColorMagenta;
                case ObjectColorState.Green | ObjectColorState.Blue:
                    return ColorCyan;
                case ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue:
                    return ColorWhite;
                case ObjectColorState.None:
                default:
                    return ColorNeutralGrey;
            }
        }

        /// <summary>
        /// Recalculates and applies the visual color via MaterialPropertyBlock across all target renderers.
        /// Compatible with URP Lit and avoids instantiating new materials.
        /// </summary>
        public void UpdateVisuals()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                CacheRenderers();
            }

            if (_renderers == null || _renderers.Length == 0) return;

            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            Color displayColor = GetColorForState(currentState);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorPropId, displayColor);
                _propBlock.SetColor(ColorPropId, displayColor);
                r.SetPropertyBlock(_propBlock);
            }
        }

        /// <summary>
        /// Checks whether allspecified color flags are currently active on this target.
        /// </summary>
        public bool HasColor(ObjectColorState requiredFlags)
        {
            return (currentState & requiredFlags) == requiredFlags;
        }

        /// <summary>
        /// Returns remaining lifetime in seconds for an active primary color (0 if inactive).
        /// </summary>
        public float GetRemainingTime(ObjectColorState singleColor)
        {
            float now = Time.time;
            if (singleColor == ObjectColorState.Red && (currentState & ObjectColorState.Red) != 0)
                return Mathf.Max(0f, _expireRed - now);
            if (singleColor == ObjectColorState.Green && (currentState & ObjectColorState.Green) != 0)
                return Mathf.Max(0f, _expireGreen - now);
            if (singleColor == ObjectColorState.Blue && (currentState & ObjectColorState.Blue) != 0)
                return Mathf.Max(0f, _expireBlue - now);
            return 0f;
        }

        public float GetRawExpireTime(ObjectColorState singleColor)
        {
            if (singleColor == ObjectColorState.Red) return _expireRed;
            if (singleColor == ObjectColorState.Green) return _expireGreen;
            if (singleColor == ObjectColorState.Blue) return _expireBlue;
            return -1f;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// For automated testing and verification: manually adjusts expiration timestamps.
        /// </summary>
        public void SetExpirationsForTesting(float expireRed, float expireGreen, float expireBlue)
        {
            _expireRed = expireRed;
            _expireGreen = expireGreen;
            _expireBlue = expireBlue;
        }

        public void SimulateExpirationCheck(float currentTime)
        {
            bool stateChanged = false;
            if (!_pauseExpireRed && (currentState & ObjectColorState.Red) != 0 && currentTime >= _expireRed)
            {
                currentState &= ~ObjectColorState.Red;
                _expireRed = -1f;
                stateChanged = true;
            }
            if (!_pauseExpireGreen && (currentState & ObjectColorState.Green) != 0 && currentTime >= _expireGreen)
            {
                currentState &= ~ObjectColorState.Green;
                _expireGreen = -1f;
                stateChanged = true;
            }
            if (!_pauseExpireBlue && (currentState & ObjectColorState.Blue) != 0 && currentTime >= _expireBlue)
            {
                currentState &= ~ObjectColorState.Blue;
                _expireBlue = -1f;
                stateChanged = true;
            }
            if (stateChanged)
            {
                UpdateVisuals();
                OnColorStateChanged?.Invoke(currentState);
            }
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheRenderers();
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            UpdateVisuals();
        }
#endif
    }
}
