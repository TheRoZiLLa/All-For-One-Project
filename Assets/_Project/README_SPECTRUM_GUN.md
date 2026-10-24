# RECORDED — Spectrum Gun & RGB Color System (Complete Overhaul)

## 1. System Overview
The **Spectrum Gun** is an 8-chamber sci-fi revolver that fires three elemental/color ammunition types (**RED**, **GREEN**, and **CYAN**) and combines **8-state additive RGB color mixing** with **3 distinct physical gameplay skills**:

1. **RED (`AmmoType.Red` -> `ObjectColorState.Red`) — Impulse**
   - Applies an immediate directional physics push (`ForceMode.Impulse`) + upward lift along the shot trajectory on impact via `IColorShotImpact` (`SpectrumSkillResponder`).
   - Triggers on every Red hit without resetting or extending the 10-second Red color state timer.
2. **GREEN (`AmmoType.Green` -> `ObjectColorState.Green`) — Bind**
   - A lone Green target waits; two Green targets within range share one walkable energy tether. Re-shooting an endpoint reels its partner toward it.
   - The link follows the Green state and cleans up when that state ends. It does not attract unpainted objects or create SpringJoints.
3. **CYAN (`AmmoType.Cyan` -> `ObjectColorState.Blue`) — Stasis**
   - Primary Blue snapshots linear/angular velocity and holds the body kinematic with gravity off; reset or expiry restores the snapshot.
   - Cyan ammo is primary Blue. The Green + Blue mix is a separate zero-gravity suspension; another Blue shot converts it to full stasis.

`ColorMixPhysicsReactor` is attached to Dev GameTest targets and added by the bootstrap when targets are generated. It handles Yellow, Magenta, Cyan and White reactions from the new origin changes, independently of the held gun and HUD.

---

## 2. Controls & Keybindings
- **Left Mouse Button (`LMB`)**: Fire the active chamber at the **12 o'clock** top firing position (aligned with the revolver's barrel).
  - If the 12 o'clock chamber is loaded: consumes the round, fires a barrel-tip raycast/bolt/tracer, applies the skill & RGB color state, and auto-advances the cylinder counter-clockwise (`+45°` on Z from the shooter's rear view, bringing the next clockwise chamber into 12 o'clock like a real revolver).
  - If the 12 o'clock chamber is empty: plays a dry-fire click and does **not** advance the cylinder.
- **`Q` / Mouse Scroll Up**: Select the previous chamber with a 45-degree step.
- **`E` / Mouse Scroll Down**: Select the next chamber with a 45-degree step. Inside a security kiosk trigger, E unlocks that kiosk without rotating the gun; scroll remains available. Disabling and re-enabling the cylinder finishes its selected index and releases its firing lock.
- **`R` (Reload)**: Starts the 2.5 s weapon reload animation, refills eight randomized **RED**, **GREEN**, and **CYAN** rounds at the clip's loading phase (1.65 s at normal speed), and unlocks firing after closing. Animator speed changes and pauses also affect loading. Both the AmmoTestInput route and the weapon's fallback route use TriggerReload; repeated R cannot queue another reload. Without an active weapon, the standalone ammo prototype still refills immediately.

---

## 3. Architecture & Key Scripts
The saved `Dev GameTest` scene uses the original hand-mounted model at
`Player/CameraPivot/MainCamera/Viewmodel/Arms/upper_arm.R/forearm.R/hand.R/REV`.
`RaycastWeapon`, `WeaponImpactFeedback` and `SpectrumGunController` are on REV itself;
the separate camera-child SpectrumGun instance has been removed. The existing hand/bone poses,
meshes and materials are preserved. A native Animator on Arms drives the right shoulder,
forearm and wrist together; the gun stays attached to the authored hand grip.

`Assets/_Project/Animations/Weapons/REV_Viewmodel.controller` has Ready, Fire and Reload states,
driven by the Fire trigger only after a successful shot. REV_Ready.anim holds the rest pose;
REV_Fire.anim supplies a 0.42 s shot with fast 25 ms setback, staggered barrel rise, damped
follow-through and a small settling overshoot. The maximum authored setback is 5.5 cm.
An interruptible 25 ms blend supports repeated shots during recovery. Curves use clamped
smooth tangents and quaternion continuity; both states disable Write Defaults and root motion.
The camera and viewmodel wall-retraction root are outside the animated bindings. There are
no animation events driving ammo or hit detection. Change Ready and Fire together if editing
the rig's rest pose later. The old empty Animator on upper_arm.R was removed to avoid competing
bone animation. Existing glow1–glow10 and the authored ChamberGlow range/intensity are preserved.
ChamberGlow and glow1–glow10 now follow the active chamber's RED/GREEN/CYAN color together,
including manual rotation, auto-advance and reload. An empty active chamber disables these lights;
loading a round enables them again. Standalone color-mode changes also update all eleven lights.
The controller resolves the lights once and changes only color/enabled, preserving authored
intensity, range and placement. MuzzlePoint remains separate so a fired flash keeps its own color.

REV_Reload.anim uses a 2.5 s pickup, alignment, insertion and stow sequence. Its Barrel now uses the saved
Player prefab's seated position, a short 55-degree hinge arc (about 9.2 cm center travel at
the current rig scale), and a full spin around the mesh center that finishes before cartridges approach. Position compensates for
the mesh's off-center origin so spinning cannot make the cylinder orbit or float away.
Ready and Fire explicitly restore the same seated Barrel pose. Update all three clips if
moving the cylinder seat later. `HeldRevolverVerificationTests.VerifyReloadBarrelInPlayMode()`
checks hinge radius, bounded travel, smooth steps and final/interrupt recovery at 30/60/144 FPS.

The existing CylinderAmmoHUD reads the actual Barrel transform in LateUpdate during reload.
Its inner cylinder swings out with the 3D mesh and follows the axial spin separately from the
crane swing. The firing bracket dims while unlocked. Cartridge visuals show empty chambers
a fade out during ejection and fade in after the seated refill, without translating or scaling the cartridge images, then
the inner cylinder closes into its original housing. Reload presentation does not alter
ammo data or its active index. CylinderRotationController composes the reload rotation with
normal Q/E/shot rotation so there is only one writer for the UI rotation. Pause, completion
and cancellation restore the correct presentation and chamber orientation.
Chamber and bracket colors blend over a short interval while logical ammo changes immediately.
The cylinder uses a 0.16 s eased step without overshoot.
The held Barrel mesh follows the same Q/E, scroll and firing angle around its +X axis. Position compensates for the off-center mesh pivot, and the index composes after Animator without accumulating during pauses. Reload hand/loader contact uses the original animation frame; the HUD subtracts the additive index when reading reload spin. Empty and replacement HUD rounds fade in place; the firing marker also changes color without pulsing its size.

`RevolverReloadPresentation` adds eight brass cartridge visuals and a speedloader to the held REV.
They enter the mesh +X bores from behind during normalized reload time 0.40 to 0.66;
ammo commits only at the seated pose. The carrier twists to release, withdraws, and returns to the pouch with the left hand. Movement follows
Animator progress, including speed changes/pauses, and hides on cancellation or completion.
The loader is prewarmed once per weapon and stays present below view before pickup, so it never switches on in the visible frame. Its eight rounds have case rims, separate copper noses and primers, with shared materials reused between reloads.
Its bore layout is calibrated for the current REV cylinder mesh; recalibrate if that asset changes.

REV_Reload.anim also drives the existing left shoulder, elbow, wrist and fifteen finger bones.
The thumb and index fingertip pads pinch the release knob; the remaining fingers fold around the grip.
ReloadGrip.L records that contact frame. ReloadPickup stays below view under Viewmodel, so wall retraction moves both anchors together.
The hand closes at the pouch (0.08-0.18), carries and aligns (0.18-0.40), inserts (0.40-0.66),
twists/releases (0.66-0.72), withdraws (0.72-0.78), then stows below view (0.78-0.93).
Object ownership changes only at matching poses; the hand opens after returning the empty loader. Primary motion uses baked local transform curves sampled at 50 FPS; a small two-bone contact correction
keeps the fingertip pads aligned during insertion without adding another Animator. REV_Ready and REV_Fire restore the
authored left pose. The grip is calibrated for this rig and loader geometry; rebake when changing either.
The left arm uses a consistent bend plane and bind-pose bone frames, avoiding forearm roll flips on the return path. The pickup anchor sits below view close to the cylinder to shorten the stow motion. Verification bounds elbow and wrist rotation steps as well as wrist travel at 30/60/144 FPS.

`RevolverWeaponBuilder.TryConfigureHeldRevolver` recognizes that hierarchy before the bootstrap
or Editor setup can create a second gun. Its MuzzlePoint is calibrated to the front bore in the
Body mesh (+X in mesh space); the Barrel mesh is the cylinder. MuzzlePoint cancels the rig's
inherited scale so the muzzle flare stays small. ChamberGlow follows the active ammo color.
The Player prefab stores camera/muzzle/light references; the Dev scene binds its existing
CylinderAmmoHUD_System. Other scenes resolve their own cylinder system, or retain the existing
standalone unlimited-ammo fallback when none is present. Reloaded colors still use the prototype random distribution.

Hands and muzzle flare use the Viewmodel overlay. Tracers, bolts and impacts use a separate
unit-scale world-effects root on Default layer, which is cleaned up with the weapon. Advancing
the cylinder changes the chamber indicator without recoloring a flash/bolt already fired.
Camera-to-muzzle and muzzle-to-target obstruction checks prevent a muzzle beyond cover from
shooting through it. Hit detection remains instant raycast; the moving bolt is visual feedback.
Shot feedback uses an additive particle material referenced by the saved weapon so its shader is included
in builds. A six-blade muzzle crown collapses over the flash; a bright core and thin energy helix fade
independently of subsequent chamber colors. Impacts combine a short star burst, two hollow shock rings
and fourteen stretched world-space sparks. Shadow-free hit lights decay quickly, and eight prewarmed
impact objects share their spark/star material. Pool reuse creates no per-shot materials; disabling the
weapon clears particles and restores muzzle-light intensity. The target's mixed surface remains visible
through the hollow rings. VerifyImpactPresentationInPlayMode checks rendering, tint capture and cleanup.

- **`Assets/_Project/Scripts/Weapons/`**
  - `SpectrumGunController.cs`: Master weapon controller, input handling, 12 o'clock chamber sync, firing logic and successful-shot Animator trigger; the legacy standalone model retains its recoil.
  - `RaycastWeapon.cs`: Center-screen aim raycast with automatic barrel-tip `MuzzlePoint` resolution and close-range barrel obstruction checks.
  - `WeaponImpactFeedback.cs`: Pooled muzzle flash flare, high-speed energy bolt projectile, tapering beam tracer from `MuzzlePoint`, and surface impact flashes.
  - `RevolverWeaponBuilder.cs`: Builds `REV.fbx` with exact upper-barrel-bore `MuzzlePoint` calculation and ergonomic first-person viewmodel placement `(0.14, -0.11, 0.25)`.
- **`Assets/_Project/Scripts/UI/Ammo/`**
  - `CylinderAmmoController.cs`: 8-chamber data model and events (`OnChamberStateChanged`, `OnActiveChamberChanged`, `OnCylinderReloaded`).
  - `CylinderRotationController.cs`: Immediate logical chamber advancement + smooth `45°` mechanical ratchet rotation on `Rotating_InnerCylinder`.
  - `CylinderAmmoHUD.cs` & `CylinderUISpriteFactory.cs`: Industrial gunmetal 8-chamber HUD with exterior cylinder flutes, ratchet hub spokes, and a stationary **12 o'clock** active chamber reticle bracket & chevron indicator.
- **`Assets/_Project/Scripts/ColorSystem/`**
  - `RGBColorTarget.cs`: 8-state additive RGB state machine (`None`, `Red`, `Green`, `Blue`, `Yellow`, `Magenta`, `Cyan`, `White`) with independent 10-second timers per primary color and `MaterialPropertyBlock` rendering.
  - `SpectrumSkillResponder.cs`: Physical weapon skills (**RED Impulse**, **GREEN Bind**, **CYAN Stasis**) with independent skill durations and safe `Rigidbody` state restoration.
  - `ColorPuzzleCondition.cs`: Inspector-friendly puzzle condition component with `UnityEvent` callbacks (`onConditionMet`, `onConditionLost`) when watched targets match a required `ObjectColorState`.
- **`Assets/_Project/Scripts/Testing/Editor/`**
  - `RGBColorSystemVerificationTests.cs`: 28-case automated verification suite (`RECORDED -> Run Automated RGB & Cylinder Verification (28 Cases)`).
  - `HeldRevolverVerificationTests.VerifyReloadInputInPlayMode(...)`: coroutine verification using Input System R-key events through both handlers, with delayed refill, duplicate-input/fire rejection and cancellation/retry checks.
  - `HeldRevolverVerificationTests.VerifyReloadHudInPlayMode(...)`: checks opening/ejection, paused animation without premature refill, inserted rounds, closing/reset, cancellation and subsequent manual/shot chamber indexing.
  - `HeldRevolverVerificationTests.VerifyInPlayMode()`: assert-based fixture checks for preserved grip/idempotent setup, eight RGB shots and auto-advance, cooldown/dry fire, stable shot colors, world-space effects/cleanup, and camera-to-muzzle cover. Run in Play Mode through Editor scripting. A live Dev scene shot was also checked for RED impact, one consumed round, GREEN next chamber and an unchanged grip.
  - `HeldRevolverVerificationTests.VerifyShotAnimationInPlayMode()`: eight-shot re-trigger and recovery checks at 30/60/144 FPS, dry-fire rejection, bounded pose changes, unchanged grip/camera/glow, independent wall retraction and Animator reset. Both verification methods passed with the saved Player prefab; the native firing motion was also captured from Game view. A standalone build has not been tested.
  - `HeldRevolverVerificationTests.VerifyGlowColorsInPlayMode()`: all eleven lights checked for RED/GREEN/CYAN/empty, auto-advance, reload and standalone mode; verifies preserved intensity/range/placement and independent muzzle-flash color.
