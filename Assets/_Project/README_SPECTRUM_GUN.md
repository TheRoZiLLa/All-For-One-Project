# RECORDED — Spectrum Gun & RGB Color System (Complete Overhaul)

## 1. System Overview
The **Spectrum Gun** is a 6-chamber sci-fi revolver that fires three elemental/color ammunition types (**RED**, **GREEN**, and **CYAN**) and combines **8-state additive RGB color mixing** with **3 distinct physical gameplay skills**:

1. **RED (`AmmoType.Red` -> `ObjectColorState.Red`) — Impulse**
   - Applies an immediate directional physics push (`ForceMode.Impulse`) + upward lift along the shot trajectory on impact via `IColorShotImpact` (`SpectrumSkillResponder`).
   - Triggers on every Red hit without resetting or extending the 5-second Red color state timer.
2. **GREEN (`AmmoType.Green` -> `ObjectColorState.Green`) — Bind**
   - Creates a physical tether (`SpringJoint` + glowing green `LineRenderer` energy cord) anchoring the hit object to the impact point in world space.
   - Runs on an independent, Inspector-configurable `greenBindDuration` (default `5.0s`), prevents duplicate joints on re-hit, and cleanly destroys the joint when expired.
3. **CYAN (`AmmoType.Cyan` -> `ObjectColorState.Blue`) — Stasis**
   - Arrests linear/angular velocity (`stasisInitialVelocityScale`), suspends gravity, and applies heavy stasis damping (`stasisLinearDamping`) or full freeze (`fullFreezeOnStasis`).
   - Runs on an independent, Inspector-configurable `cyanStasisDuration` (default `5.0s`) and cleanly restores original `Rigidbody` properties when expired.

---

## 2. Controls & Keybindings
- **Left Mouse Button (`LMB`)**: Fire the active chamber at the **6 o'clock** bottom firing position.
  - If the 6 o'clock chamber is loaded: consumes the round, fires a barrel-tip raycast/bolt/tracer, applies the skill & RGB color state, and auto-advances the cylinder by `60°`.
  - If the 6 o'clock chamber is empty: plays a dry-fire click and does **not** advance the cylinder.
- **`Q` / Mouse Scroll Up**: Manually rotate the cylinder left (`+60°`, brings the 4 o'clock chamber into the 6 o'clock firing bracket).
- **`E` / Mouse Scroll Down**: Manually rotate the cylinder right (`-60°`, brings the 8 o'clock chamber into the 6 o'clock firing bracket).
- **`R` (Test Reload)**: Fills all 6 chambers with a randomized distribution of **RED**, **GREEN**, and **CYAN** rounds (`AmmoTestInput`).

---

## 3. Architecture & Key Scripts
- **`Assets/_Project/Scripts/Weapons/`**
  - `SpectrumGunController.cs`: Master weapon controller, input handling, recoil animation, 6 o'clock chamber sync, and firing logic.
  - `RaycastWeapon.cs`: Center-screen aim raycast with automatic barrel-tip `MuzzlePoint` resolution and close-range barrel obstruction checks.
  - `WeaponImpactFeedback.cs`: Pooled muzzle flash flare, high-speed energy bolt projectile, tapering beam tracer from `MuzzlePoint`, and surface impact flashes.
  - `RevolverWeaponBuilder.cs`: Builds `REV.fbx` with exact upper-barrel-bore `MuzzlePoint` calculation and ergonomic first-person viewmodel placement `(0.14, -0.11, 0.25)`.
- **`Assets/_Project/Scripts/UI/Ammo/`**
  - `CylinderAmmoController.cs`: 6-chamber data model and events (`OnChamberStateChanged`, `OnActiveChamberChanged`, `OnCylinderReloaded`).
  - `CylinderRotationController.cs`: Immediate logical chamber advancement + smooth `60°` mechanical ratchet rotation on `Rotating_InnerCylinder`.
  - `CylinderAmmoHUD.cs` & `CylinderUISpriteFactory.cs`: Industrial gunmetal 6-chamber HUD with exterior cylinder flutes, ratchet hub spokes, and a stationary **6 o'clock** active chamber reticle bracket & chevron indicator.
- **`Assets/_Project/Scripts/ColorSystem/`**
  - `RGBColorTarget.cs`: 8-state additive RGB state machine (`None`, `Red`, `Green`, `Blue`, `Yellow`, `Magenta`, `Cyan`, `White`) with independent 5-second timers per primary color and `MaterialPropertyBlock` rendering.
  - `SpectrumSkillResponder.cs`: Physical weapon skills (**RED Impulse**, **GREEN Bind**, **CYAN Stasis**) with independent skill durations and safe `Rigidbody` state restoration.
  - `ColorPuzzleCondition.cs`: Inspector-friendly puzzle condition component with `UnityEvent` callbacks (`onConditionMet`, `onConditionLost`) when watched targets match a required `ObjectColorState`.
- **`Assets/_Project/Scripts/Testing/Editor/`**
  - `RGBColorSystemVerificationTests.cs`: 28-case automated verification suite (`RECORDED -> Run Automated RGB & Cylinder Verification (28 Cases)`).
