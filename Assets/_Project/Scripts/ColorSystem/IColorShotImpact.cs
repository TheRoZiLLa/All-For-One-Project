using UnityEngine;
using Recorded.Weapons.Ammo;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Interface for objects that react to the immediate physical/tactical impact of a Spectrum Gun shot.
    /// Decoupled from IColorReceivable (which manages the 5-second persistent RGB color flags).
    /// Duplicate hits do NOT reset the 5-second color timer, but OnColorShotHit is invoked on every valid shot.
    /// </summary>
    public interface IColorShotImpact
    {
        void OnColorShotHit(AmmoType firedAmmo, ObjectColorState mappedColorFlag, RaycastHit hitInfo, Vector3 shotDirection);
    }
}