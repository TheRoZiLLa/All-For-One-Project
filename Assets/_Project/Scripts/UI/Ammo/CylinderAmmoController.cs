using System;
using UnityEngine;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Core data controller for the 6-chamber revolver cylinder.
    /// Stores ammunition states independently of UI visual presentation or input bindings.
    /// Supports future ammo pickup loading and standalone test reload.
    /// </summary>
    public class CylinderAmmoController : MonoBehaviour, ICylinderAmmoProvider
    {
        public const int CylinderCapacity = 6;

        [Header("Initial State")]
        [Tooltip("If true, automatically loads 6 random rounds on start.")]
        [SerializeField] private bool autoLoadOnStart = true;

        // The 6 chambers of the revolver cylinder
        [SerializeField] private AmmoType[] chambers = new AmmoType[CylinderCapacity];

        // The chamber index (0..5) currently aligned with the 12 o'clock firing position
        [SerializeField] private int activeChamberIndex = 0;

        public int ChamberCount => CylinderCapacity;
        public int ActiveChamberIndex => activeChamberIndex;
        public AmmoType ActiveAmmoType => chambers[activeChamberIndex];
        public bool HasActiveRound => chambers[activeChamberIndex] != AmmoType.Empty;

        public event Action<int, AmmoType> OnChamberStateChanged;
        public event Action<int> OnActiveChamberChanged;
        public event Action OnCylinderReloaded;

        private void Awake()
        {
            if (chambers == null || chambers.Length != CylinderCapacity)
            {
                chambers = new AmmoType[CylinderCapacity];
            }
        }

        private void Start()
        {
            if (autoLoadOnStart)
            {
                TestReloadRandom();
            }
        }

        /// <summary>
        /// Returns the ammunition type at a specified chamber index (0..5).
        /// </summary>
        public AmmoType GetChamberAmmo(int chamberIndex)
        {
            if (chamberIndex < 0 || chamberIndex >= CylinderCapacity) return AmmoType.Empty;
            return chambers[chamberIndex];
        }

        /// <summary>
        /// Attempts to consume the round at the 12 o'clock firing position.
        /// If loaded, marks chamber as Empty and returns true.
        /// If empty, returns false without modifying data.
        /// </summary>
        public bool TryConsumeActiveRound(out AmmoType consumedType)
        {
            consumedType = chambers[activeChamberIndex];

            if (consumedType == AmmoType.Empty)
            {
                return false;
            }

            // Consume round
            chambers[activeChamberIndex] = AmmoType.Empty;
            OnChamberStateChanged?.Invoke(activeChamberIndex, AmmoType.Empty);
            return true;
        }

        /// <summary>
        /// Updates the active chamber index aligned with the 12 o'clock indicator.
        /// Called by the rotation controller when rotating the cylinder.
        /// </summary>
        public void SetActiveChamber(int index)
        {
            int normalized = (index % CylinderCapacity + CylinderCapacity) % CylinderCapacity;
            if (activeChamberIndex != normalized)
            {
                activeChamberIndex = normalized;
                OnActiveChamberChanged?.Invoke(activeChamberIndex);
            }
        }

        /// <summary>
        /// Loads a specific chamber with an ammo type (for future ammo pickup systems).
        /// </summary>
        public void LoadChamber(int chamberIndex, AmmoType ammoType)
        {
            if (chamberIndex < 0 || chamberIndex >= CylinderCapacity) return;

            chambers[chamberIndex] = ammoType;
            OnChamberStateChanged?.Invoke(chamberIndex, ammoType);
        }

        /// <summary>
        /// Test-only reload: Fills all 6 chambers with a randomized distribution of RED, GREEN, and CYAN.
        /// Both color counts and chamber positions are randomized naturally.
        /// </summary>
        public void TestReloadRandom()
        {
            // Available color choices for random loading (Red, Green, Cyan)
            AmmoType[] colorChoices = new AmmoType[] { AmmoType.Red, AmmoType.Green, AmmoType.Cyan };

            for (int i = 0; i < CylinderCapacity; i++)
            {
                chambers[i] = colorChoices[UnityEngine.Random.Range(0, colorChoices.Length)];
                OnChamberStateChanged?.Invoke(i, chambers[i]);
            }

            OnCylinderReloaded?.Invoke();
        }
    }
}
