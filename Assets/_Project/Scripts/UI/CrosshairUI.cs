using UnityEngine;
using UnityEngine.UI;

namespace Recorded.UI
{
    /// <summary>
    /// Generates and manages a crisp, resolution-independent First-Person crosshair at the screen center.
    /// Can automatically build its UI hierarchy if none is assigned in the Inspector.
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("Crosshair Appearance")]
        [SerializeField] private Color crosshairColor = new Color(0.9f, 0.95f, 1f, 0.85f);
        [SerializeField] private float reticleSize = 10f;     // Length of each tick
        [SerializeField] private float reticleThickness = 2f; // Thickness of each tick
        [SerializeField] private float centerGap = 5f;        // Distance from center dot to ticks
        [SerializeField] private bool showCenterDot = true;
        [SerializeField] private float centerDotSize = 3f;

        private RectTransform _rootContainer;

        private void Awake()
        {
            EnsureCrosshairHierarchy();
        }

        private void EnsureCrosshairHierarchy()
        {
            // Check if Canvas exists on parent or in scene
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Crosshair_Canvas");
                canvasObj.transform.SetParent(transform, false);

                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // Render above world

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create container anchored exactly in screen center
            GameObject container = new GameObject("Crosshair_Reticle");
            container.transform.SetParent(canvas.transform, false);

            _rootContainer = container.AddComponent<RectTransform>();
            _rootContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _rootContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _rootContainer.pivot = new Vector2(0.5f, 0.5f);
            _rootContainer.anchoredPosition = Vector2.zero;
            _rootContainer.sizeDelta = new Vector2(64, 64);

            // Center Dot
            if (showCenterDot)
            {
                CreateTick("Dot", Vector2.zero, new Vector2(centerDotSize, centerDotSize));
            }

            // Top Tick
            CreateTick("Tick_Top", new Vector2(0, centerGap + (reticleSize * 0.5f)), new Vector2(reticleThickness, reticleSize));
            // Bottom Tick
            CreateTick("Tick_Bottom", new Vector2(0, -(centerGap + (reticleSize * 0.5f))), new Vector2(reticleThickness, reticleSize));
            // Left Tick
            CreateTick("Tick_Left", new Vector2(-(centerGap + (reticleSize * 0.5f)), 0), new Vector2(reticleSize, reticleThickness));
            // Right Tick
            CreateTick("Tick_Right", new Vector2(centerGap + (reticleSize * 0.5f), 0), new Vector2(reticleSize, reticleThickness));
        }

        private void CreateTick(string name, Vector2 position, Vector2 size)
        {
            GameObject tickObj = new GameObject(name);
            tickObj.transform.SetParent(_rootContainer, false);

            RectTransform rt = tickObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            Image img = tickObj.AddComponent<Image>();
            img.color = crosshairColor;
            img.raycastTarget = false; // Don't block raycasts/clicks
        }

        public void SetCrosshairColor(Color color)
        {
            crosshairColor = color;
            if (_rootContainer == null) return;

            Image[] images = _rootContainer.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                img.color = color;
            }
        }
    }
}
