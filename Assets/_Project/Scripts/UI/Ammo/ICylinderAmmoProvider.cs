using System;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Clean integration interface separating the cylinder ammunition system from weapon controller and pickup logic.
    /// Future weapon controllers query and consume rounds through this contract.
    /// </summary>
    public interface ICylinderAmmoProvider
    {
        /// <summary>Number of chambers in the cylinder (6).</summary>
        int ChamberCount { get; }

        /// <summary>Index (0..5) of the chamber currently aligned with the fixed 12 o'clock firing position.</summary>
        int ActiveChamberIndex { get; }

        /// <summary>The ammunition type present in the active 12 o'clock chamber.</summary>
        AmmoType ActiveAmmoType { get; }

        /// <summary>True if the active 12 o'clock chamber is not Empty.</summary>
        bool HasActiveRound { get; }

        /// <summary>Returns the ammunition type at a specific chamber index (0..5).</summary>
        AmmoType GetChamberAmmo(int chamberIndex);

        /// <summary>
        /// Attempts to consume the round currently aligned at the 12 o'clock firing position.
        /// If successful, the chamber transitions to Empty, returns true, and outputs the consumed AmmoType.
        /// If the chamber is Empty, returns false.
        /// </summary>
        bool TryConsumeActiveRound(out AmmoType consumedType);

        /// <summary>Loads a specific chamber with an ammo type (for future pickup systems).</summary>
        void LoadChamber(int chamberIndex, AmmoType ammoType);

        /// <summary>Event raised whenever a chamber's state changes (chamberIndex, newAmmoType).</summary>
        event Action<int, AmmoType> OnChamberStateChanged;

        /// <summary>Event raised whenever the active chamber index changes (newActiveIndex).</summary>
        event Action<int> OnActiveChamberChanged;

        /// <summary>Event raised when the cylinder has been reloaded.</summary>
        event Action OnCylinderReloaded;
    }
}
