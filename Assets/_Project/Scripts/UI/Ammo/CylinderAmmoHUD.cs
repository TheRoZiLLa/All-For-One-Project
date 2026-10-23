using System;
using UnityEngine;
using UnityEngine.UI;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Renders the 2D six-chamber revolver ammunition HUD at the bottom-left of the screen.
    /// Displays the rear face of the cylinder viewed from the shooter's perspective.
    /// Maintains separation between stationary outer frame and rotating inner cylinder.
    /// </summary>
    public class CylinderAmmoHUD : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CylinderAmmoController ammoController;
        [SerializeField] private CylinderRotationController rotationController;

        [Header("Layout & Positioning")]
        [Tooltip("Offset in pixels from the bottom-left corner of the screen.")]
        [SerializeField] private Vector2 screenOffset = new Vector2(100f, 100f);

        [Tooltip("Diameter of the cylinder HUD in pixels.")]
        [SerializeField] private float hudDiameter = 160f;

        [Header("Color Palette (Rear Cartridge Bases)")]
        [SerializeField] private Color redColor = new Color(1f, 0.18f, 0.18f, 1f);     // Impulse Red
        [SerializeField] private Color greenColor = new Color(0.18f, 0.95f, 0.35f, 1f); // Bind Green
        [SerializeField] private Color cyanColor = new Color(0.18f, 0.88f, 1f, 1f);     // Stasis Cyan (Light Blue)

        [Header("Metallic Finishes")]
        [SerializeField] private Color outerFrameColor = new Color(0.12f, 0.13f, 0.16f, 1f);
        [SerializeField] private Color outerRimColor = new Color(0.28f, 0.32f, 0.38f, 1f);
        [SerializeField] private Color innerCylinderColor = new Color(0.15f, 0.16f, 0.19f, 1f);
        [SerializeField] private Color chamberRimColor = new Color(0.32f, 0.36f, 0.42f, 1f);
        [SerializeField] private Color emptyHollowColor = new Color(0.06f, 0.07f, 0.08f, 1f);
        [SerializeField] private Color cartridgeBaseColor = new Color(0.40f, 0.43f, 0.48f, 1f);
        [SerializeField] private Color indicatorColor = new Color(0.22f, 0.85f, 1f, 0.95f);

        // Visual references for the 6 chambers
        private struct ChamberUI
        {
            public GameObject root;
            public Image emptyHollowImage;
            public Image cartridgeBaseImage;
            public Image colorCapImage;
        }

        private ChamberUI[] _chamberVisuals = new ChamberUI[CylinderAmmoController.CylinderCapacity];
        private RectTransform _rootContainer;
        private RectTransform _innerCylinderRect;

        private void Awake()
        {
            if (ammoController == null)
            {
                ammoController = GetComponent<CylinderAmmoController>();
            }

            if (rotationController == null)
            {
                rotationController = GetComponent<CylinderRotationController>();
            }

            BuildHUDHierarchy();
        }

        private void OnEnable()
        {
            if (ammoController != null)
            {
                ammoController.OnChamberStateChanged += HandleChamberStateChanged;
                ammoController.OnCylinderReloaded += RefreshAllChambers;
            }
        }

        private void OnDisable()
        {
            if (ammoController != null)
            {
                ammoController.OnChamberStateChanged -= HandleChamberStateChanged;
                ammoController.OnCylinderReloaded -= RefreshAllChambers;
            }
        }

        private void Start()
        {
            RefreshAllChambers();
        }

        /// <summary>
        /// Constructs the layered uGUI hierarchy:
        /// 1. Stationary Outer Housing & 6 o'clock Firing Indicator.
        /// 2. Rotating Inner Cylinder with 6 Chamber slots and Center Hub.
        /// </summary>
        public void BuildHUDHierarchy()
        {
            if (_rootContainer != null && _chamberVisuals != null && _chamberVisuals.Length == CylinderAmmoController.CylinderCapacity && _chamberVisuals[0].root != null)
            {
                return; // Already built and valid in this session
            }

            // 1. Ensure fresh Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                for (int c = transform.childCount - 1; c >= 0; c--)
                {
                    Transform child = transform.GetChild(c);
                    if (child.name == "CylinderAmmo_Canvas")
                    {
                        if (Application.isPlaying)
                        {
                            child.gameObject.SetActive(false);
                            Destroy(child.gameObject);
                        }
                        else
                        {
                            DestroyImmediate(child.gameObject);
                        }
                    }
                }

                GameObject canvasObj = new GameObject("CylinderAmmo_Canvas");
                canvasObj.transform.SetParent(transform, false);

                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 2. HUD Root Container (Anchored Bottom-Left)
            GameObject rootObj = new GameObject("CylinderHUD_Root");
            rootObj.transform.SetParent(canvas.transform, false);

            _rootContainer = rootObj.AddComponent<RectTransform>();
            _rootContainer.anchorMin = Vector2.zero; // Bottom-Left
            _rootContainer.anchorMax = Vector2.zero;
            _rootContainer.pivot = new Vector2(0.5f, 0.5f);
            _rootContainer.anchoredPosition = screenOffset;
            _rootContainer.sizeDelta = new Vector2(hudDiameter, hudDiameter);

            // 3. Stationary Outer Housing
            BuildStationaryOuterHousing(_rootContainer);

            // 4. Rotating Inner Cylinder
            BuildRotatingInnerCylinder(_rootContainer);

            // 5. Stationary 6 O'Clock Firing Indicator (Renders on top of inner cylinder pointing UP into bottom chamber)
            BuildStationaryFiringIndicator(_rootContainer);

            // Connect rotating rect to rotation controller
            if (rotationController != null && _innerCylinderRect != null)
            {
                rotationController.SetRotatingInnerCylinder(_innerCylinderRect);
            }
        }

        private void BuildStationaryOuterHousing(RectTransform parent)
        {
            GameObject frameObj = new GameObject("Stationary_OuterHousing");
            frameObj.transform.SetParent(parent, false);

            RectTransform rt = frameObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // Outer dark bezel disc
            Image bg = frameObj.AddComponent<Image>();
            bg.sprite = CylinderUISpriteFactory.CircleFilled;
            bg.color = outerFrameColor;
            bg.raycastTarget = false;

            // Outer brushed steel ring
            GameObject ringObj = new GameObject("OuterHousing_SteelRim");
            ringObj.transform.SetParent(frameObj.transform, false);
            RectTransform ringRt = ringObj.AddComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.sizeDelta = Vector2.zero;

            Image ring = ringObj.AddComponent<Image>();
            ring.sprite = CylinderUISpriteFactory.CircleRing;
            ring.color = outerRimColor;
            ring.raycastTarget = false;
        }

        private void BuildRotatingInnerCylinder(RectTransform parent)
        {
            GameObject innerObj = new GameObject("Rotating_InnerCylinder");
            innerObj.transform.SetParent(parent, false);

            _innerCylinderRect = innerObj.AddComponent<RectTransform>();
            _innerCylinderRect.anchorMin = new Vector2(0.5f, 0.5f);
            _innerCylinderRect.anchorMax = new Vector2(0.5f, 0.5f);
            _innerCylinderRect.pivot = new Vector2(0.5f, 0.5f);
            _innerCylinderRect.anchoredPosition = Vector2.zero;
            _innerCylinderRect.sizeDelta = new Vector2(hudDiameter * 0.92f, hudDiameter * 0.92f);

            // Inner cylinder background
            Image innerBg = innerObj.AddComponent<Image>();
            innerBg.sprite = CylinderUISpriteFactory.CircleFilled;
            innerBg.color = innerCylinderColor;
            innerBg.raycastTarget = false;

            // Build the 6 Chambers around the cylinder
            float chamberRadius = hudDiameter * 0.28f; // Distance from center to chamber centers
            float chamberSize = hudDiameter * 0.24f;   // Diameter of each chamber

            // 6 Chambers: Chamber 0 starts at 180 deg (6 o'clock bottom)
            // Subsequent chambers: Index 1 = 120° (4 o'clock), 2 = 60° (2 o'clock), 3 = 0° (12 o'clock), 4 = 300° (10 o'clock), 5 = 240° (8 o'clock)
            for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++)
            {
                float angleDeg = 180f - (i * 60f);
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Sin(rad) * chamberRadius, Mathf.Cos(rad) * chamberRadius);

                _chamberVisuals[i] = CreateChamberVisual(_innerCylinderRect, i, pos, chamberSize);
            }

            // Center Mechanical Hub
            GameObject hubObj = new GameObject("CenterHub");
            hubObj.transform.SetParent(_innerCylinderRect, false);
            RectTransform hubRt = hubObj.AddComponent<RectTransform>();
            hubRt.anchorMin = new Vector2(0.5f, 0.5f);
            hubRt.anchorMax = new Vector2(0.5f, 0.5f);
            hubRt.anchoredPosition = Vector2.zero;
            hubRt.sizeDelta = new Vector2(hudDiameter * 0.20f, hudDiameter * 0.20f);

            Image hubImg = hubObj.AddComponent<Image>();
            hubImg.sprite = CylinderUISpriteFactory.CircleFilled;
            hubImg.color = new Color(0.25f, 0.28f, 0.33f, 1f);
            hubImg.raycastTarget = false;

            // Center Bolt Pin
            GameObject pinObj = new GameObject("Hub_Pin");
            pinObj.transform.SetParent(hubObj.transform, false);
            RectTransform pinRt = pinObj.AddComponent<RectTransform>();
            pinRt.sizeDelta = new Vector2(hudDiameter * 0.08f, hudDiameter * 0.08f);
            Image pinImg = pinObj.AddComponent<Image>();
            pinImg.sprite = CylinderUISpriteFactory.CircleFilled;
            pinImg.color = new Color(0.42f, 0.46f, 0.52f, 1f);
            pinImg.raycastTarget = false;
        }

        private ChamberUI CreateChamberVisual(RectTransform parent, int index, Vector2 position, float size)
        {
            ChamberUI chamber = new ChamberUI();

            GameObject chamberRoot = new GameObject($"Chamber_{index}");
            chamberRoot.transform.SetParent(parent, false);

            RectTransform rt = chamberRoot.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);

            chamber.root = chamberRoot;

            // 1. Chamber Metallic Rim (always visible)
            Image rim = chamberRoot.AddComponent<Image>();
            rim.sprite = CylinderUISpriteFactory.CircleRing;
            rim.color = chamberRimColor;
            rim.raycastTarget = false;

            // 2. Empty Hollow Recess (visible when empty)
            GameObject emptyObj = new GameObject("Empty_Hollow");
            emptyObj.transform.SetParent(chamberRoot.transform, false);
            RectTransform emptyRt = emptyObj.AddComponent<RectTransform>();
            emptyRt.anchorMin = Vector2.zero;
            emptyRt.anchorMax = Vector2.one;
            emptyRt.sizeDelta = new Vector2(-4f, -4f);

            chamber.emptyHollowImage = emptyObj.AddComponent<Image>();
            chamber.emptyHollowImage.sprite = CylinderUISpriteFactory.CircleFilled;
            chamber.emptyHollowImage.color = emptyHollowColor;
            chamber.emptyHollowImage.raycastTarget = false;

            // 3. Cartridge Base Rim (Rear view of brass case)
            GameObject baseObj = new GameObject("Cartridge_Base");
            baseObj.transform.SetParent(chamberRoot.transform, false);
            RectTransform baseRt = baseObj.AddComponent<RectTransform>();
            baseRt.anchorMin = Vector2.zero;
            baseRt.anchorMax = Vector2.one;
            baseRt.sizeDelta = new Vector2(-4f, -4f);

            chamber.cartridgeBaseImage = baseObj.AddComponent<Image>();
            chamber.cartridgeBaseImage.sprite = CylinderUISpriteFactory.CircleFilled;
            chamber.cartridgeBaseImage.color = cartridgeBaseColor;
            chamber.cartridgeBaseImage.raycastTarget = false;

            // 4. Cartridge Primer / Color Core Cap (RED, GREEN, CYAN)
            GameObject colorCapObj = new GameObject("Color_CoreCap");
            colorCapObj.transform.SetParent(baseObj.transform, false);
            RectTransform colorRt = colorCapObj.AddComponent<RectTransform>();
            colorRt.anchorMin = new Vector2(0.5f, 0.5f);
            colorRt.anchorMax = new Vector2(0.5f, 0.5f);
            colorRt.sizeDelta = new Vector2(size * 0.52f, size * 0.52f);

            chamber.colorCapImage = colorCapObj.AddComponent<Image>();
            chamber.colorCapImage.sprite = CylinderUISpriteFactory.CircleFilled;
            chamber.colorCapImage.color = redColor;
            chamber.colorCapImage.raycastTarget = false;

            return chamber;
        }

        private void BuildStationaryFiringIndicator(RectTransform parent)
        {
            // Fixed Firing Position Indicator at 6 O'Clock (Bottom)
            // Points upward toward the 6 o'clock active chamber
            GameObject indicatorObj = new GameObject("Stationary_6oclock_FiringIndicator");
            indicatorObj.transform.SetParent(parent, false);

            RectTransform indRt = indicatorObj.AddComponent<RectTransform>();
            indRt.anchorMin = new Vector2(0.5f, 0f);
            indRt.anchorMax = new Vector2(0.5f, 0f);
            indRt.pivot = new Vector2(0.5f, 0.5f);
            // Placed at the bottom edge, pointing up into the 6 o'clock chamber
            indRt.anchoredPosition = new Vector2(0f, hudDiameter * 0.065f);
            indRt.sizeDelta = new Vector2(hudDiameter * 0.12f, hudDiameter * 0.12f);
            indRt.localRotation = Quaternion.identity;

            Image indImg = indicatorObj.AddComponent<Image>();
            indImg.sprite = CylinderUISpriteFactory.IndicatorNotch;
            indImg.color = indicatorColor;
            indImg.raycastTarget = false;
        }

        private void HandleChamberStateChanged(int chamberIndex, AmmoType newAmmo)
        {
            UpdateChamberVisual(chamberIndex, newAmmo);
        }

        public void RefreshAllChambers()
        {
            if (ammoController == null) return;

            for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++)
            {
                UpdateChamberVisual(i, ammoController.GetChamberAmmo(i));
            }
        }

        private void UpdateChamberVisual(int index, AmmoType ammo)
        {
            if (index < 0 || index >= _chamberVisuals.Length) return;

            ChamberUI chamber = _chamberVisuals[index];
            if (chamber.root == null) return;

            if (ammo == AmmoType.Empty)
            {
                // Show dark hollow recess, hide cartridge base and color
                chamber.emptyHollowImage.gameObject.SetActive(true);
                chamber.cartridgeBaseImage.gameObject.SetActive(false);
            }
            else
            {
                // Show cartridge base and set color cap
                chamber.emptyHollowImage.gameObject.SetActive(false);
                chamber.cartridgeBaseImage.gameObject.SetActive(true);

                Color capColor = GetAmmoColor(ammo);
                chamber.colorCapImage.color = capColor;
            }
        }

        public Color GetAmmoColor(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Red: return redColor;
                case AmmoType.Green: return greenColor;
                case AmmoType.Cyan: return cyanColor;
                default: return emptyHollowColor;
            }
        }
    }
}
