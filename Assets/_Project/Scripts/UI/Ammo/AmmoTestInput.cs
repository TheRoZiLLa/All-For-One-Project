using UnityEngine;
using UnityEngine.InputSystem;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Temporary test input component for the Cylinder Ammo prototype.
    /// Handles:
    /// - 'R': Unlimited test reload with randomized RED, GREEN, and CYAN distribution.
    /// - 'Q' / 'E' or Mouse Scroll: Manual rotation of the cylinder (when standalone without SpectrumGunController).
    /// - Fire (Left Click / Space): Consumes the 12 o'clock round and auto-advances the cylinder (when standalone).
    /// 
    /// SEPARATION NOTICE:
    /// This component is completely decoupled from the core ammunition data model.
    /// Disabling or removing this script does not affect chamber data, rotation logic, or HUD visuals.
    /// In the final game, ammo pickups and the real weapon controller replace this component.
    /// </summary>
    public class AmmoTestInput : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CylinderAmmoController ammoController;
        [SerializeField] private CylinderRotationController rotationController;

        [Header("Test Firing Controls")]
        [Tooltip("If true, pressing left click or space triggers test firing and auto-advancement when no SpectrumGunController is present.")]
        [SerializeField] private bool enableTestFiring = true;

        private SpectrumGunController _cachedGunController;

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
        }

        private void Start()
        {
            _cachedGunController = FindFirstObjectByType<SpectrumGunController>();
        }

        private void Update()
        {
            // Always allow R test reload through AmmoTestInput (the dedicated temporary reload component)
            HandleReloadInput();

            if (_cachedGunController == null)
            {
                _cachedGunController = FindFirstObjectByType<SpectrumGunController>();
            }

            // If SpectrumGunController is active in the scene, let it handle Q/E/Scroll rotation and weapon firing
            if (_cachedGunController != null && _cachedGunController.isActiveAndEnabled)
            {
                return;
            }

            HandleRotationInput();

            if (enableTestFiring)
            {
                HandleTestFireInput();
            }
        }

        private void HandleRotationInput()
        {
            if (rotationController == null || rotationController.IsRotating) return;

            // 1. Keyboard Q / E
            if (Keyboard.current != null)
            {
                // Q = Rotate Left
                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    rotationController.RotateLeft();
                    return;
                }
                // E = Rotate Right
                else if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    rotationController.RotateRight();
                    return;
                }
            }

            // 2. Mouse Scroll Wheel
            if (Mouse.current != null)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (scrollY > 0.1f)
                {
                    rotationController.RotateLeft();
                }
                else if (scrollY < -0.1f)
                {
                    rotationController.RotateRight();
                }
            }
        }

        private void HandleReloadInput()
        {
            if (ammoController == null) return;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ammoController.TestReloadRandom();
                Debug.Log("<color=#FFE84D><b>[AmmoTestInput]</b> Test Reload (R): Filled all 6 chambers with randomized RED, GREEN, CYAN distribution.</color>");
            }
        }

        private void HandleTestFireInput()
        {
            if (ammoController == null || rotationController == null) return;
            if (rotationController.IsRotating) return; // Prevent firing while cylinder is mid-rotation

            bool firePressed = false;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                firePressed = true;
            }
            else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                firePressed = true;
            }

            if (!firePressed) return;

            // Attempt to fire the round at the 12 o'clock firing position
            if (ammoController.HasActiveRound)
            {
                if (ammoController.TryConsumeActiveRound(out AmmoType firedType))
                {
                    Color logColor = GetLogColor(firedType);
                    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(logColor)}><b>[AmmoTestInput]</b> Fired <b>{firedType.ToString().ToUpper()}</b> round from 12 o'clock chamber! Auto-advancing cylinder...</color>");

                    // Automatically advance the cylinder to the next chamber following a successful shot
                    rotationController.AutoAdvance();
                }
            }
            else
            {
                // Dry fire: Chamber at 12 o'clock is empty
                Debug.Log("<color=#888888><b>[AmmoTestInput]</b> *CLICK* (Dry Fire) - 12 o'clock chamber is EMPTY. No shot fired, cylinder does not advance.</color>");
            }
        }

        private Color GetLogColor(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Red: return new Color(1f, 0.2f, 0.2f);
                case AmmoType.Green: return new Color(0.2f, 1f, 0.3f);
                case AmmoType.Cyan: return new Color(0.2f, 0.9f, 1f);
                default: return Color.gray;
            }
        }
    }
}