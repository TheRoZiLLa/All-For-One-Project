using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Recorded.UI
{
    /// <summary>Compact runtime presentation panel for the crosshair. It affects UI only, never player input.</summary>
    [RequireComponent(typeof(CrosshairUI))]
    public class CrosshairSettingsUI : MonoBehaviour
    {
        [SerializeField] private bool startOpen;
        [SerializeField] private KeyCode toggleKey = KeyCode.F7;
        private CrosshairUI _crosshair;
        private GameObject _panel;
        private Text _styleValue;
        private Slider _sizeSlider;
        private Slider _gapSlider;

        private void Awake()
        {
            _crosshair = GetComponent<CrosshairUI>();
            _crosshair.EnsureCrosshairHierarchy();
            BuildIfNeeded();
            _panel.SetActive(startOpen);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame) ToggleSettings();
        }
        public void ToggleSettings() { if (_panel != null) _panel.SetActive(!_panel.activeSelf); }

        private void BuildIfNeeded()
        {
            Canvas canvas = _crosshair.Canvas;
            if (canvas == null || canvas.transform.Find("Crosshair_Settings") != null) return;
            _panel = CreatePanel(canvas.transform, "Crosshair_Settings", new Vector2(-24, -24), new Vector2(330, 228));
            RectTransform rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
            CreateLabel(_panel.transform, "Title", "RETICLE  //  F7", new Vector2(16, -18), 16, TextAnchor.UpperLeft, new Color(.35f, .93f, 1f));
            _styleValue = CreateButton(_panel.transform, "Style", new Vector2(16, -56), new Vector2(298, 30), "STYLE: CROSS", CycleStyle).GetComponentInChildren<Text>();
            _sizeSlider = CreateSlider(_panel.transform, "Size", new Vector2(16, -104), "SIZE", 4, 32, _crosshair.Size, _crosshair.SetReticleSize);
            _gapSlider = CreateSlider(_panel.transform, "Gap", new Vector2(16, -150), "GAP", 1, 24, _crosshair.Gap, _crosshair.SetCenterGap);
            CreateButton(_panel.transform, "Color", new Vector2(16, -196), new Vector2(142, 26), "COLOR: CYCLE", CycleColor);
            CreateButton(_panel.transform, "Dot", new Vector2(172, -196), new Vector2(142, 26), "DOT: ON/OFF", ToggleDot);
            RefreshLabels();
        }

        private void CycleStyle() { _crosshair.SetStyle((CrosshairUI.ReticleStyle)(((int)_crosshair.Style + 1) % 4)); RefreshLabels(); }
        private void CycleColor()
        {
            Color c = _crosshair.Color;
            if (c.b > c.r && c.b > c.g) _crosshair.SetCrosshairColor(new Color(1f, .25f, .28f, .9f));
            else if (c.r > c.g) _crosshair.SetCrosshairColor(new Color(.25f, 1f, .48f, .9f));
            else _crosshair.SetCrosshairColor(new Color(.35f, .93f, 1f, .9f));
        }
        private void ToggleDot() => _crosshair.SetCenterDotVisible(!_crosshair.CenterDotVisible);
        private void RefreshLabels()
        {
            if (_styleValue != null) _styleValue.text = "STYLE: " + _crosshair.Style.ToString().ToUpperInvariant();
            if (_sizeSlider != null) _sizeSlider.value = _crosshair.Size;
            if (_gapSlider != null) _gapSlider.value = _crosshair.Gap;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>(); rt.anchoredPosition = position; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(.025f, .04f, .065f, .93f); return obj;
        }
        private static Text CreateLabel(Transform parent, string name, string value, Vector2 position, int fontSize, TextAnchor anchor, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = position; rt.sizeDelta = new Vector2(-32, 26);
            Text text = obj.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.alignment = anchor; text.color = color; text.text = value; return text;
        }
        private static Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, string value, UnityEngine.Events.UnityAction action)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = position; rt.sizeDelta = size;
            Image image = obj.GetComponent<Image>(); image.color = new Color(.10f, .17f, .22f, 1); Button button = obj.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Text label = CreateLabel(obj.transform, "Label", value, new Vector2(8, -2), 12, TextAnchor.MiddleLeft, new Color(.82f, .94f, 1)); label.rectTransform.sizeDelta = new Vector2(-16, -4); return button;
        }
        private static Slider CreateSlider(Transform parent, string name, Vector2 position, string caption, float min, float max, float value, UnityEngine.Events.UnityAction<float> action)
        {
            CreateLabel(parent, name + "_Label", caption, position, 12, TextAnchor.MiddleLeft, new Color(.63f, .74f, .82f));
            GameObject sliderObj = new GameObject(name, typeof(RectTransform), typeof(Slider)); sliderObj.transform.SetParent(parent, false);
            RectTransform rt = sliderObj.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = position + new Vector2(70, 3); rt.sizeDelta = new Vector2(228, 18);
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(sliderObj.transform, false); RectTransform bgRt = bg.GetComponent<RectTransform>(); bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero; bg.GetComponent<Image>().color = new Color(.08f,.12f,.16f,1);
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(bg.transform, false); RectTransform fillRt = fill.GetComponent<RectTransform>(); fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(.5f,1); fillRt.sizeDelta = Vector2.zero; fill.GetComponent<Image>().color = new Color(.23f,.86f,1f,1);
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(sliderObj.transform, false); handle.GetComponent<RectTransform>().sizeDelta = new Vector2(12, 22); handle.GetComponent<Image>().color = Color.white;
            Slider slider = sliderObj.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.value = value; slider.targetGraphic = handle.GetComponent<Image>(); slider.fillRect = fillRt; slider.handleRect = handle.GetComponent<RectTransform>(); slider.onValueChanged.AddListener(action); return slider;
        }
    }
}
