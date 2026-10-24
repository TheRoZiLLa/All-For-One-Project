using System;
using UnityEngine;
using UnityEngine.UI;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Renders the 2D six-chamber revolver ammunition HUD at the bottom-left of the screen.
    /// Displays the rear face of the cylinder viewed from the shooter's perspective.
    /// Features industrial brushed gunmetal framing, recessed fluted chambers, brass/metallic
    /// cartridge primers, and a precision 12 o'clock firing chamber bracket & chevron indicator.
    /// </summary>
    public class CylinderAmmoHUD : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CylinderAmmoController ammoController;
        [SerializeField] private CylinderRotationController rotationController;

        [Header("Layout & Positioning")]
        [Tooltip("Offset in pixels from the bottom-left corner of the screen.")]
        [SerializeField] private Vector2 screenOffset = new Vector2(115f, 115f);

        [Tooltip("Diameter of the cylinder HUD in pixels.")]
        [SerializeField] private float hudDiameter = 176f;

        [Header("Color Palette (Rear Cartridge Bases)")]
        [SerializeField] private Color redColor = new Color(1f, 0.20f, 0.20f, 1f);     // Impulse Red
        [SerializeField] private Color greenColor = new Color(0.18f, 0.96f, 0.38f, 1f); // Bind Green
        [SerializeField] private Color cyanColor = new Color(0.18f, 0.90f, 1f, 1f);     // Stasis Cyan (Light Blue)

        [Header("Industrial Gunmetal Finishes")]
        [SerializeField] private Color outerFrameColor = new Color(0.09f, 0.10f, 0.13f, 0.96f);
        [SerializeField] private Color outerRimColor = new Color(0.32f, 0.37f, 0.44f, 1f);
        [SerializeField] private Color innerCylinderColor = new Color(0.15f, 0.17f, 0.21f, 1f);
        [SerializeField] private Color chamberRimColor = new Color(0.36f, 0.41f, 0.48f, 1f);
        [SerializeField] private Color emptyHollowColor = new Color(0.04f, 0.05f, 0.06f, 1f);
        [SerializeField] private Color cartridgeBaseColor = new Color(0.46f, 0.50f, 0.56f, 1f);
        [SerializeField] private Color indicatorColor = new Color(0.22f, 0.92f, 1f, 1f);

        // Visual references for the 6 chambers
        private struct ChamberUI
        {
            public GameObject root;
            public Image rimImage;
            public Image emptyHollowImage;
            public Image cartridgeBaseImage;
            public Image colorCapImage;
            public Image primerGlowImage;
        }

        private ChamberUI[] _chamberVisuals = new ChamberUI[CylinderAmmoController.CylinderCapacity];
        private RectTransform _rootContainer;
        private RectTransform _innerCylinderRect;
        private Image _activeBracketRingImage;
        private Image _activeChevronImage;
        private float _indicatorPulse;

        public RectTransform InnerCylinderRect => _innerCylinderRect;

        private void Awake()
        {
            ResolveControllers();
            BuildHUDHierarchy();
        }

        private void OnEnable()
        {
            ResolveControllers();
            if (ammoController != null)
            {
                ammoController.OnChamberStateChanged -= HandleChamberStateChanged;
                ammoController.OnCylinderReloaded -= RefreshAllChambers;
                ammoController.OnActiveChamberChanged -= HandleActiveChamberChanged;

                ammoController.OnChamberStateChanged += HandleChamberStateChanged;
                ammoController.OnCylinderReloaded += RefreshAllChambers;
                ammoController.OnActiveChamberChanged += HandleActiveChamberChanged;
            }
        }

        private void OnDisable()
        {
            if (ammoController != null)
            {
                ammoController.OnChamberStateChanged -= HandleChamberStateChanged;
                ammoController.OnCylinderReloaded -= RefreshAllChambers;
                ammoController.OnActiveChamberChanged -= HandleActiveChamberChanged;
            }
        }

        private void Start()
        {
            ResolveControllers();
            BuildHUDHierarchy();
            RefreshAllChambers();
        }

        private void Update()
        {
            // A restrained unscaled pulse improves scanability without moving the fixed 12 o'clock marker.
            if (_activeBracketRingImage == null) return;
            _indicatorPulse = Mathf.Lerp(_indicatorPulse, 0f, Time.unscaledDeltaTime * 7f);
            float breathe = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.035f + _indicatorPulse * 0.10f;
            _activeBracketRingImage.rectTransform.localScale = Vector3.one * breathe;
            if (_activeChevronImage != null) _activeChevronImage.rectTransform.localScale = Vector3.one * (1f + _indicatorPulse * 0.08f);
        }

        private void ResolveControllers()
        {
            if (ammoController == null)
            {
                ammoController = GetComponent<CylinderAmmoController>();
                if (ammoController == null) ammoController = FindFirstObjectByType<CylinderAmmoController>();
            }

            if (rotationController == null)
            {
                rotationController = GetComponent<CylinderRotationController>();
                if (rotationController == null) rotationController = FindFirstObjectByType<CylinderRotationController>();
            }
        }

        /// <summary>
        /// Constructs the layered uGUI hierarchy:
        /// 1. Stationary Outer Housing & Precision 12 o'clock Firing Chamber Bracket.
        /// 2. Rotating Inner Cylinder with 6 Chamber slots, Fluted Notches, and Center Ratchet Hub.
        /// </summary>
        public void BuildHUDHierarchy()
        {
            ResolveControllers();

            // Always ensure rotationController is bound even if hierarchy was already constructed
            if (_rootContainer != null && _innerCylinderRect != null && _chamberVisuals != null &&
                _chamberVisuals.Length == CylinderAmmoController.CylinderCapacity && _chamberVisuals[0].root != null &&
                _rootContainer.Find("Stationary_12oclock_FiringIndicator") != null)
            {
                if (rotationController != null)
                {
                    rotationController.SetRotatingInnerCylinder(_innerCylinderRect);
                }
                return;
            }

            // 1. Clean up any pre-serialized Edit-Mode Canvas children so we never have duplicate/stale UI overlays
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                for (int c = transform.childCount - 1; c >= 0; c--)
                {
                    Transform child = transform.GetChild(c);
                    if (child.name.StartsWith("CylinderAmmo_Canvas"))
                    {
                        child.name = "CylinderAmmo_Canvas_Stale";
                        child.gameObject.SetActive(false);
                        if (Application.isPlaying)
                        {
                            child.SetParent(null, false);
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

            // 3. Stationary Outer Gunmetal Housing
            BuildStationaryOuterHousing(_rootContainer);

            // 4. Rotating Inner Cylinder
            BuildRotatingInnerCylinder(_rootContainer);

            // 5. Stationary 12 O'Clock Firing Chamber Bracket & Indicator (Frames the 12 o'clock active chamber cleanly)
            BuildStationaryFiringIndicator(_rootContainer);

            // 6. Connect rotating rect to rotation controller immediately
            if (rotationController != null && _innerCylinderRect != null)
            {
                rotationController.SetRotatingInnerCylinder(_innerCylinderRect);
            }

            RefreshAllChambers();
        }

        private void BuildStationaryOuterHousing(RectTransform parent)
        {
            GameObject frameObj = new GameObject("Stationary_OuterHousing");
            frameObj.transform.SetParent(parent, false);

            RectTransform rt = frameObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // Outer dark armored bezel disc
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

            // Inner precision bezel ring
            GameObject innerBezelObj = new GameObject("OuterHousing_InnerBezel");
            innerBezelObj.transform.SetParent(frameObj.transform, false);
            RectTransform innerBezelRt = innerBezelObj.AddComponent<RectTransform>();
            innerBezelRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerBezelRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerBezelRt.sizeDelta = new Vector2(hudDiameter * 0.88f, hudDiameter * 0.88f);

            Image innerBezelImg = innerBezelObj.AddComponent<Image>();
            innerBezelImg.sprite = CylinderUISpriteFactory.CircleRing;
            innerBezelImg.color = new Color(0.20f, 0.23f, 0.28f, 0.9f);
            innerBezelImg.raycastTarget = false;
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
            _innerCylinderRect.sizeDelta = new Vector2(hudDiameter * 0.84f, hudDiameter * 0.84f);

            // Inner cylinder machined body
            Image innerBg = innerObj.AddComponent<Image>();
            innerBg.sprite = CylinderUISpriteFactory.CircleFilled;
            innerBg.color = innerCylinderColor;
            innerBg.raycastTarget = false;

            // Build 6 decorative exterior flutes between chambers (at 30°, 90°, 150°, 210°, 270°, 330°)
            // so rotation is visually unmistakable even when all chambers hold the same ammo color!
            float fluteRadius = hudDiameter * 0.37f;
            float fluteSize = hudDiameter * 0.11f;
            for (int f = 0; f < CylinderAmmoController.CylinderCapacity; f++)
            {
                float fluteAngleDeg = 30f + (f * 60f);
                float fluteRad = fluteAngleDeg * Mathf.Deg2Rad;
                Vector2 flutePos = new Vector2(Mathf.Sin(fluteRad) * fluteRadius, Mathf.Cos(fluteRad) * fluteRadius);

                GameObject fluteObj = new GameObject($"Cylinder_Flute_{f}");
                fluteObj.transform.SetParent(_innerCylinderRect, false);
                RectTransform fluteRt = fluteObj.AddComponent<RectTransform>();
                fluteRt.anchorMin = new Vector2(0.5f, 0.5f);
                fluteRt.anchorMax = new Vector2(0.5f, 0.5f);
                fluteRt.anchoredPosition = flutePos;
                fluteRt.sizeDelta = new Vector2(fluteSize, fluteSize);

                Image fluteImg = fluteObj.AddComponent<Image>();
                fluteImg.sprite = CylinderUISpriteFactory.CircleFilled;
                fluteImg.color = outerFrameColor;
                fluteImg.raycastTarget = false;
            }

            // Build the 6 Chambers around the cylinder
            // Positioned at radius 0.255 * hudDiameter so the top chamber sits cleanly inside the housing
            // with plenty of room for the 12 o'clock active firing bracket and chevron above it.
            float chamberRadius = hudDiameter * 0.255f;
            float chamberSize = hudDiameter * 0.225f;

            // 6 Chambers: Chamber 0 starts at 0 deg (12 o'clock top, aligned with the revolver barrel)
            // Subsequent chambers are indexed clockwise from the shooter's rear view:
            // Index 0 = 0° (12 o'clock), 1 = 60° (2 o'clock), 2 = 120° (4 o'clock), 3 = 180° (12 o'clock), 4 = 240° (8 o'clock), 5 = 300° (10 o'clock)
            for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++)
            {
                float angleDeg = i * 60f;
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Sin(rad) * chamberRadius, Mathf.Cos(rad) * chamberRadius);

                _chamberVisuals[i] = CreateChamberVisual(_innerCylinderRect, i, pos, chamberSize);
            }

            // Center Mechanical Ratchet Hub
            GameObject hubObj = new GameObject("CenterHub");
            hubObj.transform.SetParent(_innerCylinderRect, false);
            RectTransform hubRt = hubObj.AddComponent<RectTransform>();
            hubRt.anchorMin = new Vector2(0.5f, 0.5f);
            hubRt.anchorMax = new Vector2(0.5f, 0.5f);
            hubRt.anchoredPosition = Vector2.zero;
            hubRt.sizeDelta = new Vector2(hudDiameter * 0.20f, hudDiameter * 0.20f);

            Image hubImg = hubObj.AddComponent<Image>();
            hubImg.sprite = CylinderUISpriteFactory.CircleFilled;
            hubImg.color = new Color(0.24f, 0.27f, 0.33f, 1f);
            hubImg.raycastTarget = false;

            // Ratchet Star / Spokes on Center Hub so rotation is also visible at the center axis
            for (int s = 0; s < 3; s++)
            {
                GameObject spokeObj = new GameObject($"Hub_Spoke_{s}");
                spokeObj.transform.SetParent(hubObj.transform, false);
                RectTransform spokeRt = spokeObj.AddComponent<RectTransform>();
                spokeRt.anchorMin = new Vector2(0.5f, 0.5f);
                spokeRt.anchorMax = new Vector2(0.5f, 0.5f);
                spokeRt.sizeDelta = new Vector2(hudDiameter * 0.16f, 3f);
                spokeRt.localRotation = Quaternion.Euler(0f, 0f, s * 60f);

                Image spokeImg = spokeObj.AddComponent<Image>();
                spokeImg.color = new Color(0.14f, 0.16f, 0.20f, 0.95f);
                spokeImg.raycastTarget = false;
            }

            // Center Bolt Pin
            GameObject pinObj = new GameObject("Hub_Pin");
            pinObj.transform.SetParent(hubObj.transform, false);
            RectTransform pinRt = pinObj.AddComponent<RectTransform>();
            pinRt.sizeDelta = new Vector2(hudDiameter * 0.08f, hudDiameter * 0.08f);
            Image pinImg = pinObj.AddComponent<Image>();
            pinImg.sprite = CylinderUISpriteFactory.CircleFilled;
            pinImg.color = new Color(0.48f, 0.53f, 0.60f, 1f);
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
            chamber.rimImage = rim;

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

            // 3. Cartridge Base Rim (Rear view of metallic casing)
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

            // 4. Outer Color Glow Ring inside Cartridge
            GameObject glowRingObj = new GameObject("Primer_GlowRing");
            glowRingObj.transform.SetParent(baseObj.transform, false);
            RectTransform glowRt = glowRingObj.AddComponent<RectTransform>();
            glowRt.anchorMin = new Vector2(0.5f, 0.5f);
            glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.sizeDelta = new Vector2(size * 0.72f, size * 0.72f);

            chamber.primerGlowImage = glowRingObj.AddComponent<Image>();
            chamber.primerGlowImage.sprite = CylinderUISpriteFactory.CircleRing;
            chamber.primerGlowImage.color = redColor;
            chamber.primerGlowImage.raycastTarget = false;

            // 5. Cartridge Primer / Color Core Cap (RED, GREEN, CYAN)
            GameObject colorCapObj = new GameObject("Color_CoreCap");
            colorCapObj.transform.SetParent(baseObj.transform, false);
            RectTransform colorRt = colorCapObj.AddComponent<RectTransform>();
            colorRt.anchorMin = new Vector2(0.5f, 0.5f);
            colorRt.anchorMax = new Vector2(0.5f, 0.5f);
            colorRt.sizeDelta = new Vector2(size * 0.54f, size * 0.54f);

            chamber.colorCapImage = colorCapObj.AddComponent<Image>();
            chamber.colorCapImage.sprite = CylinderUISpriteFactory.CircleFilled;
            chamber.colorCapImage.color = redColor;
            chamber.colorCapImage.raycastTarget = false;

            return chamber;
        }

        private void BuildStationaryFiringIndicator(RectTransform parent)
        {
            float chamberRadius = hudDiameter * 0.255f;
            float bracketSize = hudDiameter * 0.265f;

            // 1. Stationary 12 O'Clock Active Chamber Reticle Ring (frames the top chamber at (0, +chamberRadius))
            GameObject bracketObj = new GameObject("Stationary_12oclock_ChamberBracket");
            bracketObj.transform.SetParent(parent, false);

            RectTransform bracketRt = bracketObj.AddComponent<RectTransform>();
            bracketRt.anchorMin = new Vector2(0.5f, 0.5f);
            bracketRt.anchorMax = new Vector2(0.5f, 0.5f);
            bracketRt.pivot = new Vector2(0.5f, 0.5f);
            bracketRt.anchoredPosition = new Vector2(0f, chamberRadius);
            bracketRt.sizeDelta = new Vector2(bracketSize, bracketSize);

            _activeBracketRingImage = bracketObj.AddComponent<Image>();
            _activeBracketRingImage.sprite = CylinderUISpriteFactory.CircleRing;
            _activeBracketRingImage.color = indicatorColor;
            _activeBracketRingImage.raycastTarget = false;

            // 2. Fixed Firing Position Chevron at 12 O'Clock (sits cleanly on the upper bezel rim pointing down into the bracket)
            GameObject indicatorObj = new GameObject("Stationary_12oclock_FiringIndicator");
            indicatorObj.transform.SetParent(parent, false);

            RectTransform indRt = indicatorObj.AddComponent<RectTransform>();
            indRt.anchorMin = new Vector2(0.5f, 0.5f);
            indRt.anchorMax = new Vector2(0.5f, 0.5f);
            indRt.pivot = new Vector2(0.5f, 0.5f);
            // Placed cleanly between the top of the 12 o'clock chamber and the outer housing border
            indRt.anchoredPosition = new Vector2(0f, hudDiameter * 0.435f);
            indRt.sizeDelta = new Vector2(hudDiameter * 0.095f, hudDiameter * 0.085f);
            indRt.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _activeChevronImage = indicatorObj.AddComponent<Image>();
            _activeChevronImage.sprite = CylinderUISpriteFactory.IndicatorNotch;
            _activeChevronImage.color = indicatorColor;
            _activeChevronImage.raycastTarget = false;
        }

        private void HandleChamberStateChanged(int chamberIndex, AmmoType newAmmo)
        {
            UpdateChamberVisual(chamberIndex, newAmmo);
            UpdateActiveIndicatorTint();
        }

        private void HandleActiveChamberChanged(int activeIndex)
        {
            _indicatorPulse = 1f;
            UpdateActiveIndicatorTint();
        }

        public void RefreshAllChambers()
        {
            if (ammoController == null) return;

            for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++)
            {
                UpdateChamberVisual(i, ammoController.GetChamberAmmo(i));
            }
            UpdateActiveIndicatorTint();
        }

        private void UpdateActiveIndicatorTint()
        {
            if (ammoController == null) return;

            AmmoType activeAmmo = ammoController.ActiveAmmoType;
            Color tint = activeAmmo == AmmoType.Empty
                ? new Color(0.45f, 0.50f, 0.56f, 0.65f)
                : GetAmmoColor(activeAmmo);

            if (_activeBracketRingImage != null)
            {
                _activeBracketRingImage.color = tint;
            }
            if (_activeChevronImage != null)
            {
                _activeChevronImage.color = tint;
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
                if (chamber.primerGlowImage != null)
                {
                    chamber.primerGlowImage.color = new Color(capColor.r, capColor.g, capColor.b, 0.65f);
                }
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
