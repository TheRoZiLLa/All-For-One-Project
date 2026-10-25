using UnityEngine;

namespace Recorded.Phase
{
    [RequireComponent(typeof(PhaseRecorder))]
    public sealed class PhaseRecorderHUD : MonoBehaviour
    {
        private PhaseRecorder _recorder;
        private GameObject _canvas;
        private UnityEngine.UI.Text _status;
        private UnityEngine.UI.Image _sanity, _warning;

        private void Start()
        {
            _recorder = GetComponent<PhaseRecorder>();
            _canvas = new GameObject("Phase_Status", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = _canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _warning = Image("Phase_Danger", _canvas.transform, new Color(0.8f, 0.02f, 0.06f, 0f));
            _warning.rectTransform.anchorMin = Vector2.zero; _warning.rectTransform.anchorMax = Vector2.one;
            _warning.rectTransform.offsetMin = _warning.rectTransform.offsetMax = Vector2.zero;
            var panel = Image("Panel", _canvas.transform, new Color(0.015f, 0.035f, 0.06f, 0.92f));
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            panel.rectTransform.pivot = new Vector2(0.5f, 0f);
            panel.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            panel.rectTransform.sizeDelta = new Vector2(640f, 106f);
            var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(panel.transform, false);
            _status = label.GetComponent<UnityEngine.UI.Text>();
            _status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _status.fontSize = 22;
            _status.alignment = TextAnchor.UpperCenter;
            _status.raycastTarget = false;
            _status.rectTransform.anchorMin = Vector2.zero; _status.rectTransform.anchorMax = Vector2.one;
            _status.rectTransform.offsetMin = new Vector2(12f, 30f); _status.rectTransform.offsetMax = new Vector2(-12f, -10f);
            var rail = Image("Sanity_Rail", panel.transform, new Color(0.06f, 0.12f, 0.16f));
            rail.rectTransform.anchorMin = new Vector2(0f, 0f); rail.rectTransform.anchorMax = new Vector2(1f, 0f);
            rail.rectTransform.offsetMin = new Vector2(16f, 14f); rail.rectTransform.offsetMax = new Vector2(-16f, 24f);
            _sanity = Image("Sanity", rail.transform, Color.cyan);
            _sanity.rectTransform.anchorMin = Vector2.zero; _sanity.rectTransform.anchorMax = Vector2.one;
            _sanity.rectTransform.offsetMin = _sanity.rectTransform.offsetMax = Vector2.zero;
        }

        private static UnityEngine.UI.Image Image(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        private void Update()
        {
            if (_status == null) return;
            float sanity = _recorder.Sanity;
            bool danger = PhaseRecorder.IsWorldPaused && sanity <= 0.25f;
            var color = danger ? new Color(1f, 0.28f, 0.18f) : new Color(0.25f, 0.9f, 1f);
            _status.color = _sanity.color = color;
            _status.text = $"{_recorder.Status}  |  SANITY {Mathf.CeilToInt(sanity * 100f)}%\n[F] Record / Finish   [G] Play   [Backspace] Cancel";
            _sanity.rectTransform.anchorMax = new Vector2(sanity, 1f);
            _warning.color = new Color(0.8f, 0.02f, 0.06f, danger ? 0.08f + Mathf.Sin(Time.unscaledTime * 9f) * 0.035f : 0f);
        }

        private void OnDisable() { if (_canvas != null) _canvas.SetActive(false); }
        private void OnEnable() { if (_canvas != null) _canvas.SetActive(true); }
        private void OnDestroy() { if (_canvas != null) Destroy(_canvas); }
    }
}
