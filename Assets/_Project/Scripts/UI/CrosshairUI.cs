using UnityEngine;
using UnityEngine.UI;

namespace Recorded.UI
{
    /// <summary>Resolution-independent first-person reticle with runtime-safe presentation controls.</summary>
    public class CrosshairUI : MonoBehaviour
    {
        public enum ReticleStyle { Cross, Dot, Circle, Brackets }

        [Header("Crosshair Appearance")]
        [SerializeField] private ReticleStyle reticleStyle = ReticleStyle.Cross;
        [SerializeField] private Color crosshairColor = new Color(0.9f, 0.95f, 1f, 0.85f);
        [SerializeField, Range(4f, 32f)] private float reticleSize = 10f;
        [SerializeField, Range(1f, 6f)] private float reticleThickness = 2f;
        [SerializeField, Range(1f, 24f)] private float centerGap = 5f;
        [SerializeField] private bool showCenterDot = true;
        [SerializeField, Range(1f, 10f)] private float centerDotSize = 3f;

        private RectTransform _rootContainer;
        private Canvas _canvas;

        public ReticleStyle Style => reticleStyle;
        public Color Color => crosshairColor;
        public float Size => reticleSize;
        public float Gap => centerGap;
        public bool CenterDotVisible => showCenterDot;
        public Canvas Canvas => _canvas;

        private void Awake() => EnsureCrosshairHierarchy();

        public void EnsureCrosshairHierarchy()
        {
            if (_rootContainer != null) return;
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                GameObject canvasObj = new GameObject("Crosshair_Canvas");
                canvasObj.transform.SetParent(transform, false);
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            Transform existing = _canvas.transform.Find("Crosshair_Reticle");
            if (existing != null) _rootContainer = existing.GetComponent<RectTransform>();
            if (_rootContainer == null)
            {
                GameObject container = new GameObject("Crosshair_Reticle", typeof(RectTransform));
                container.transform.SetParent(_canvas.transform, false);
                _rootContainer = container.GetComponent<RectTransform>();
                _rootContainer.anchorMin = _rootContainer.anchorMax = new Vector2(0.5f, 0.5f);
                _rootContainer.pivot = new Vector2(0.5f, 0.5f);
            }
            RebuildVisual();
        }

        public void SetStyle(ReticleStyle style) { reticleStyle = style; RebuildVisual(); }
        public void SetReticleSize(float value) { reticleSize = Mathf.Clamp(value, 4f, 32f); RebuildVisual(); }
        public void SetCenterGap(float value) { centerGap = Mathf.Clamp(value, 1f, 24f); RebuildVisual(); }
        public void SetCenterDotVisible(bool visible) { showCenterDot = visible; RebuildVisual(); }
        public void SetCrosshairColor(Color color) { crosshairColor = color; RebuildVisual(); }

        public void RebuildVisual()
        {
            if (_rootContainer == null) return;
            for (int i = _rootContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = _rootContainer.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            _rootContainer.sizeDelta = Vector2.one * 80f;
            if (showCenterDot || reticleStyle == ReticleStyle.Dot) CreatePart("Dot", Vector2.zero, Vector2.one * (reticleStyle == ReticleStyle.Dot ? reticleSize : centerDotSize));
            if (reticleStyle == ReticleStyle.Cross) CreateCross(false);
            else if (reticleStyle == ReticleStyle.Brackets) CreateCross(true);
            else if (reticleStyle == ReticleStyle.Circle) CreateCircle();
        }

        private void CreateCross(bool brackets)
        {
            float d = centerGap + reticleSize * 0.5f;
            if (!brackets)
            {
                CreatePart("Tick_Top", new Vector2(0, d), new Vector2(reticleThickness, reticleSize));
                CreatePart("Tick_Bottom", new Vector2(0, -d), new Vector2(reticleThickness, reticleSize));
                CreatePart("Tick_Left", new Vector2(-d, 0), new Vector2(reticleSize, reticleThickness));
                CreatePart("Tick_Right", new Vector2(d, 0), new Vector2(reticleSize, reticleThickness));
                return;
            }
            float arm = reticleSize * 0.62f;
            float edge = centerGap + reticleSize;
            CreatePart("Bracket_TopLeft_H", new Vector2(-edge + arm * .5f, edge), new Vector2(arm, reticleThickness));
            CreatePart("Bracket_TopLeft_V", new Vector2(-edge, edge - arm * .5f), new Vector2(reticleThickness, arm));
            CreatePart("Bracket_TopRight_H", new Vector2(edge - arm * .5f, edge), new Vector2(arm, reticleThickness));
            CreatePart("Bracket_TopRight_V", new Vector2(edge, edge - arm * .5f), new Vector2(reticleThickness, arm));
            CreatePart("Bracket_BottomLeft_H", new Vector2(-edge + arm * .5f, -edge), new Vector2(arm, reticleThickness));
            CreatePart("Bracket_BottomLeft_V", new Vector2(-edge, -edge + arm * .5f), new Vector2(reticleThickness, arm));
            CreatePart("Bracket_BottomRight_H", new Vector2(edge - arm * .5f, -edge), new Vector2(arm, reticleThickness));
            CreatePart("Bracket_BottomRight_V", new Vector2(edge, -edge + arm * .5f), new Vector2(reticleThickness, arm));
        }

        private void CreateCircle()
        {
            GameObject circle = new GameObject("Reticle_Circle", typeof(RectTransform), typeof(Image));
            circle.transform.SetParent(_rootContainer, false);
            RectTransform rt = circle.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.sizeDelta = Vector2.one * (centerGap * 2f + reticleSize * 1.5f);
            Image image = circle.GetComponent<Image>();
            image.sprite = Weapons.Ammo.CylinderUISpriteFactory.CircleRing;
            image.color = crosshairColor;
            image.raycastTarget = false;
        }

        private void CreatePart(string name, Vector2 position, Vector2 size)
        {
            GameObject part = new GameObject(name, typeof(RectTransform), typeof(Image));
            part.transform.SetParent(_rootContainer, false);
            RectTransform rt = part.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            Image image = part.GetComponent<Image>();
            image.color = crosshairColor;
            image.raycastTarget = false;
        }
    }
}
