# RECORDED — Core Mechanics Reference

Status: single source of truth for the Spectrum Gun, color flags, and anomalous physics.
Scope: weapon payload, color mixing, physical reactions, cleanup, and VFX hooks. This file does not authorize script changes by itself.
Future coding sessions that touch the Color System, Spectrum Gun, or physics reactions must read this document first and follow it.

Namespace and types already in the project (do not replace them with a parallel model):

- `Recorded.ColorSystem.ObjectColorState` — `[Flags]` primary colors
- `Recorded.ColorSystem.IColorReceivable` — persistent paint on a target
- `Recorded.ColorSystem.IColorShotImpact` — immediate hit reaction, separate from paint lifetime
- `Recorded.ColorSystem.RGBColorTarget` — flag storage, per-primary timers, visuals, Stasis Lock demotion
- `Recorded.ColorSystem.SpectrumSkillResponder` — primary Red / Green / Blue skill execution
- `Recorded.ColorSystem.ColorMixPhysicsReactor` — Yellow / Magenta / Cyan / White physics, plus VFX `UnityEvent` hooks
- `Recorded.Weapons.SpectrumGunController` — gun; chooses a chamber and delivers a payload
- `Recorded.Weapons.Ammo.AmmoType` — chamber contents (`Empty`, `Red`, `Green`, `Cyan`)

---

## 1. Game Overview

| | |
|---|---|
| Name | RECORDED |
| Genre | Sci-Fi Action-Puzzle, first person |
| Theme | "We R in trouble" — overcoming broken physical laws inside a looping research facility |
| Fantasy | The facility's physics are damaged. The player does not shoot to destroy. They paint objects with physical states, then mix those states until the room's laws do something useful. |

### Core Loop

1. Identify the physical obstacle (gap, blocked path, moving hazard, unreachable surface).
2. Apply one or more primary colors. Each color is a physical state, not damage.
3. Mix colors on the same target. The mix is a new anomalous reaction.
4. Use that reaction to cross, move, hold, launch, or reset the obstacle.
5. If the mix is wrong, fire the third primary. White clears the object so the puzzle can be tried again. Waiting out a timer also returns the object to Neutral Grey. Nothing is left half-painted.

The loop is readable only if three things stay true:

- One shot applies one primary.
- A mix is visible on the object (Yellow, Magenta, Cyan, or a White flash).
- The physical result is a consequence of the flags on the target, never a special-case buried in the gun.

---

## 2. The Spectrum Gun

The player wields the Spectrum Gun. It is the only primary tool in this system. It does not deal traditional damage and it does not decide mix reactions.

The gun does exactly this:

1. Select one loaded chamber.
2. Cast a ray or projectile.
3. Deliver a **Color Payload** to whatever was hit.

A payload is data. Suggested fields, so hologram co-op can reuse the same struct later:

| Field | Role |
|---|---|
| `Primary` | Exactly one of `ObjectColorState.Red`, `Green`, or `Blue` |
| `AmmoType` | Chamber that produced the shot, for VFX and UI |
| `SourceId` | Who fired. Player now; hologram later. Never assume a single gun. |
| `SourcePosition` | World position of the shooter at fire time. Green uses this as the tether anchor. |
| `ShotDirection` | World direction of the shot. Red uses this as the impulse axis. |
| `HitPoint` | World point of impact |
| `HitNormal` | Surface normal at impact |
| `HitCollider` | The collider that was hit |

The gun may play recoil, tracer, and impact VFX. It must not:

- Switch on the target's current mix and run Magenta, Cyan, Yellow, or White itself.
- Accumulate colors on behalf of the target.
- Know whether a second shooter has already painted the same object.
- Spawn mix particles or drive emissive color. Those subscribe to the reactor events in section 8.

### Primaries

| Chamber / design name | Flag | Physical state |
|---|---|---|
| RED — Impulse | `ObjectColorState.Red` (1) | Strong push along `ShotDirection` |
| GREEN — Tether / Pull | `ObjectColorState.Green` (2) | Pull toward `SourcePosition`, and store that position as a tether anchor. Ammo and older comments call this **Bind**. Bind and Tether are the same primary. |
| BLUE — Stasis | `ObjectColorState.Blue` (4) | Freeze the object's physics and time temporarily. Store the pre-freeze velocity so later mixes can spend it. |

### Naming hazard — Cyan ammo is not the Cyan reaction

`AmmoType.Cyan` is the **stasis chamber**. The gun maps it to `ObjectColorState.Blue`.

The **Cyan reaction** (Telekinesis / Gravity Hold) is the mix `Green | Blue`. It is never a chamber, never an ammo type, and never a fourth flag.

| Word | Means |
|---|---|
| Cyan ammo / Cyan chamber | Primary Blue. Stasis. |
| Cyan reaction | `Green \| Blue`. Telekinesis / Gravity Hold. The object follows a point in front of the camera. |
| Blue | The flag. Stasis. The only legal way to talk about this primary in gameplay code. |
| Stasis Lock | A second Blue shot while Cyan is active. Strips Green and freezes the object in mid-air. See section 6. |

---

## 3. Bitwise Color Model

Colors are a `[Flags]` enum. Mixed results are **derived**, never stored as their own bits.

```
None   = 0
Red    = 1 << 0   // 1
Green  = 1 << 1   // 2
Blue   = 1 << 2   // 4

Yellow  = Red   | Green          // 3   Kinetic Snap
Magenta = Red   | Blue           // 5   Time Bomb
Cyan    = Green | Blue           // 6   Telekinesis / Gravity Hold
White   = Red   | Green | Blue   // 7   System Override / Reset
```

Rules:

- Apply a primary with bitwise OR (`state |= primary`).
- Remove a primary with bitwise AND-NOT (`state &= ~primary`).
- Test a primary with AND (`(state & primary) != 0`).
- Identify a mix by equality on the masked primaries: `(state & (Red | Green | Blue)) == (Red | Blue)` is Magenta. Do not add `Yellow`, `Magenta`, `Cyan`, or `White` enum members.
- A shot carries one primary. OR-ing `Red | Green` from a single payload is illegal.
- Visual color is resolved from the flags (`RGBColorTarget.GetColorForState`). It is not an input to the physics.
- The one deliberate demotion is Stasis Lock: Cyan (`Green | Blue`) plus another Blue shot becomes Blue only. That path strips Green. It does not OR a new bit.

### Paint lifetime

`RGBColorTarget` owns persistence:

- Each primary has its own timer.
- Base duration is `RGBColorTarget.ColorDuration` = **10 seconds**, counted from the moment that primary is **first** applied.
- A duplicate hit on a primary that is already active does **not** reset, extend, or restart that timer. A second Blue shot during Stasis Lock does **not** refresh the existing Blue timer.
- `IColorShotImpact.OnColorShotHit` still runs on every valid hit, including duplicates. Immediate impulse, tether, and stasis entry are hit reactions. Paint expiry is separate.
- Neutral Grey (`None`) means no physical paint. It is the resting appearance, not a color the gun can shoot.

### Global cleanup — no lingering primaries

When paint expires, or when a mixed reaction concludes, the object returns all the way to `None` (Neutral Grey). A leftover primary is illegal.

| Event | Required end state |
|---|---|
| A lone primary's 10 second timer elapses | `None` |
| Any ingredient of Yellow, Magenta, or Cyan expires | `None`. The partner primary is cleared in the same step. It must not remain painted. |
| Magenta detonates | `None`. Red and Blue are both consumed. |
| White runs | `None` after the flash. |
| `ResetColors()` | `None` |

`ColorMixPhysicsReactor` performs that full clear on mix break, Magenta detonation, and White. `RGBColorTarget.ResetColors()` is the shared flag clear: every bit off, every timer cleared, visuals back to Neutral Grey.

The only exception is **Stasis Lock** (section 6). Shooting Blue again during Cyan strips Green and leaves standard Blue. That is an override, not an expiry and not a concluded mix. Every other exit from a mix goes to Grey.

Magenta may pause its Red and Blue timers while the fuse is armed, so the bomb is not cut off by the 10 second clock. Detonation then clears everything. Cyan does **not** pause its timers. Telekinesis lasts until the earlier of the two 10 second clocks expires, and that expiry full-clears to Grey. Yellow does not pause timers. The snap is instant; the Yellow paint remains until one ingredient expires, and that expiry full-clears to Grey.

---

## 4. Architecture

Two sides. They meet only through the payload and `IColorReceivable`.

```
Source (player gun, later a hologram)
  casts ray / projectile
  builds Color Payload
        |
        v
Target
  IColorShotImpact / SpectrumSkillResponder
        immediate primary effect for this hit
  IColorReceivable / RGBColorTarget
        OR the primary into flags, 10s timers, visuals
        Stasis Lock demotion when Blue hits active Cyan
  ColorMixPhysicsReactor
        watches the flag transition and runs the mix
        raises UnityEvents for Lighting & VFX
```

### Responsibilities

| Owner | Owns | Must not own |
|---|---|---|
| Gun / hologram | Aim, chamber, payload, shooter VFX | Mix math, rigidbody reactions, clearing another source's colors, mix particles |
| `IColorReceivable` / `RGBColorTarget` | Flags, 10 second per-primary timers, duplicate-hit rule, MaterialPropertyBlock color, `OnColorStateChanged`, `ResetColors`, Stasis Lock flag strip | Force magnitudes, telekinesis follow, explosions, particle systems |
| `SpectrumSkillResponder` (`IColorShotImpact`) | One-shot primary response (push, pull, enter stasis) and stored Red direction / Green anchor / stasis snapshot | Deciding Yellow / Magenta / Cyan / White |
| `ColorMixPhysicsReactor` | Detect flag **transitions**, run mix reactions, full-clear to `None`, cancel holds, raise VFX events | Reading the gun, raycasting for damage, spawning particles, setting emissive materials |

The reactor subscribes to `OnColorStateChanged`. It stores the previous flags and reacts to the edge, not to the level. Staying on Magenta must not re-arm the bomb. A duplicate Red hit while Magenta is already active must not restart the fuse.

Lighting and VFX subscribe to the reactor's `UnityEvent` hooks. They do not read rigidbody code and the reactor does not reference particle systems.

### Multi-source rule

Any number of sources may call `ApplyColor` on the same target during the same paint window. The target ORs flags. It does not keep a separate color per source inside the enum.

Source identity lives beside the flags, on the target, as reaction inputs:

- Last Red `ShotDirection` and `SourceId`
- Last Green `SourcePosition` (tether anchor) and `SourceId`
- Stasis snapshot (linear and angular velocity) taken at the moment Blue freezes the body

If the player paints Red and a hologram paints Green before either timer expires, the target becomes Yellow and the snap uses both sources' data. No source merges payloads before they arrive.

Cyan telekinesis follows **the player camera** (`Camera.main`), 3–5 units along its forward vector. A hologram source may paint the Green or Blue bit, but the hold point is the live player view until a hologram camera is an explicit design change. Do not invent a second follow target inside the reactor.

---

## 5. Primary Reactions

Primaries resolve on impact, even when a mix will also start this frame. Mix rules below say when a primary's live force is redirected into storage instead of the rigidbody.

### Red — Impulse

- Apply a strong `ForceMode.Impulse` (or equivalent) along `ShotDirection`, at `HitPoint` when a rigidbody is present.
- Write `ShotDirection` into the target's stored Red vector even if the body cannot be pushed (kinematic, already in stasis).
- If the body is already in stasis, do **not** push it now. Add the impulse into the stored kinetic vector. That stored energy is what Magenta later releases.

### Green — Tether / Pull

- Apply a pull impulse toward `SourcePosition`, unless the body is in stasis.
- Always store `SourcePosition` as the tether anchor for a later Yellow snap.
- If the body is in stasis, skip the live pull and keep the anchor. Yellow will use the anchor when the mix is evaluated.
- A persistent joint is optional presentation. The design requirement is the anchor plus a pull. Do not require a joint for the mix to be valid.

### Blue — Stasis

- On the transitioning edge into Blue (the bit was off, now on): snapshot linear and angular velocity, then freeze the body.
- Freeze means physics and time stop for this object: velocity cleared, and the body held in place. Sleeping the rigidbody or making it kinematic are both acceptable as long as the snapshot can be restored.
- While the Blue bit is on by itself, the object stays where it was frozen. It does not follow the camera. Camera follow is Cyan only.
- Re-hitting Blue while it is already the only active primary does not take a new snapshot and does not refresh the 10 second timer.
- Re-hitting Blue while the object is Cyan is not a duplicate Blue hit. It is Stasis Lock (section 6).
- When a lone Blue timer elapses, clear the object all the way to `None`. Do not leave a frozen body with no flag. Unfreeze as part of the clear.

---

## 6. Mix Reactions

A mix is evaluated after the primary bit is applied, on the transition into that exact flag set. White outranks every other mix. If the resulting flags are `Red | Green | Blue`, run White and do not also run Yellow, Magenta, or Cyan.

### Priority

1. White (`7`) — cancel everything, burst, clear to `None`.
2. Stasis Lock — only when the shot is Blue and the current flags are already exactly Cyan. This runs before a normal Blue apply. It never produces White.
3. Otherwise the exact pair that just became complete:
   - Magenta (`5`) Time Bomb
   - Cyan (`6`) Telekinesis / Gravity Hold
   - Yellow (`3`) Kinetic Snap
4. A lone primary uses section 5 only.

Leaving a mix full-clears to `None`, except Stasis Lock, which demotes Cyan to lone Blue.

### Red + Blue = Magenta — Time Bomb

Fantasy: the object is frozen and loaded. A short fuse later it dumps everything it stored as a kinetic blast.

Entry (edge into `Red | Blue`):

- Cancel any live Yellow or Cyan hold. Those pairs cannot coexist with Magenta. End telekinesis before the freeze.
- Ensure stasis is on. If Blue was just applied, the stasis snapshot from section 5 is the stored energy. If the body was already frozen, keep that snapshot; do not overwrite it with zero.
- If Red arrives during stasis, fold the Red impulse into the stored kinetic vector instead of moving the body.
- Hold the object in place for the fuse. It does not follow the camera.
- Fuse length is a designer tunable, default **2.5 seconds**, legal range **2.0–3.0 seconds**. The fuse is a reaction timer, not a new color flag.
- While the fuse runs, pause the Red and Blue paint timers so the 10 second clock cannot expire the bomb. Green is not present.
- Raise `OnMagentaTriggered` for VFX. Do not play the explosion VFX here.

Detonation:

- Unfreeze the body.
- Release a radial kinetic blast at the object's position (affects other dynamic bodies in radius; does not use the gun).
- Add the stored velocity, scaled by a blast multiplier, back onto this body so the charged object itself is thrown.
- Raise `OnMagentaDetonated` for the Lighting & VFX pipeline.
- Consume the mix: clear Red and Blue together. Persistent state is `None`. Do not leave Red or Blue painted after the blast. Do not zero the blast velocity as part of that clear; the throw is the point of the reaction.

Cancel:

- Incoming Green completes White. Cancel the fuse, do not detonate, run White.
- `ResetColors()` from any other legal caller also cancels the fuse without detonating.
- If a fuse is cancelled without detonating, still end on `None` when the cancel path is a full clear (White). Do not drop back to a leftover Red or Blue.

### Green + Blue = Cyan — Telekinesis / Gravity Hold

Fantasy: the object leaves the floor and hangs in the player's aim. It is a carried hold, not a frozen platform.

Cyan is **not** anti-gravity suspension and it is **not** a mid-air platform locked at the impact point. Those behaviors are retired.

Entry (edge into `Green | Blue`):

- Cancel any Magenta fuse without detonating.
- If `Rigidbody.mass` is above `maxTelekinesisMass` (default **50**), Cyan does not lift. The mix collapses immediately to Blue Stasis: Green is stripped, the body freezes where it is, and `OnCyanTriggered` does not fire.
- If Blue stasis currently has the body frozen, release that freeze so the body can move. Telekinesis cannot track the camera while kinematic.
- Disable gravity.
- Ignore standard drag: set linear damping and angular damping to 0 while the hold is active.
- Do not pause the Green or Blue 10 second timers.
- Raise `OnCyanTriggered` once, for VFX. Do not hardcode particles or emissive changes in the reactor.

Every physics step while Cyan is active:

- Sample a hold point **3–5 units** directly in front of the player camera, along `Camera.main` forward. Default distance is **4**. Clamp any tuned distance into that 3–5 range.
- If geometry sits between the camera and that point, shorten the hold so the object stops short of the surface instead of pushing through it. Ignore the held object itself and the player.
- Smoothly drive the rigidbody toward that point (damped velocity follow, speed-capped). Bleed angular velocity so the object settles instead of tumbling.
- Keep gravity off and damping at 0 for the whole hold. Re-apply those overrides if another system flips them during the hold.

Exit:

- Ingredient timer expiry, White, Magenta, or Yellow: stop tracking immediately and full-clear paint to `None` (Magenta and Yellow replace the state only as their own reactions; White and expiry end on Grey).
- Restore gravity and the pre-hold damping when the hold ends, unless Stasis Lock says otherwise.
- Do not leave the body ignoring gravity after the Cyan flags are gone.

### Stasis Lock — Cyan, then Blue

Fantasy: the player is carrying the object and chooses to pin it. A second Blue shot lets go of the camera and freezes the object dead in the air.

Trigger: the object is exactly Cyan (`Green | Blue`) and the new payload is Blue. This is checked before the normal additive apply. It is the only case where a Blue shot removes a flag.

Behavior, in order:

1. Strip the Green flag immediately. Clear the Green timer. Do not refresh the existing Blue timer.
2. Flags are now exactly Blue. The visible color is primary Blue, not Cyan.
3. Stop telekinesis the same frame. The object no longer samples the camera point.
4. Zero linear and angular velocity.
5. Enter standard Blue stasis: the body freezes in mid-air where it was when the second Blue shot landed. It does not fall, and it does not keep floating toward the crosshair.
6. Do not full-clear to `None`. Lone Blue from this override is legal. When that Blue timer later elapses, cleanup rules apply and the object becomes Grey.
7. Do not raise `OnCyanTriggered` again. The hold is over. Stasis presentation uses the primary Blue path, not a new mix event.

Red onto Cyan is not Stasis Lock. Red completes White.

### Red + Green = Yellow — Kinetic Snap

Fantasy: a violent push and pull in one reaction. Puzzle use is a slingshot across a gap. Later, a hologram can be the second hand on that slingshot, including launching the player.

Entry (edge into `Red | Green`):

- End Cyan telekinesis if it was active. Release stasis if the body was frozen, without restoring the old snapshot into the snap.
- Raise `OnYellowTriggered` for VFX before the impulse.
- Read the stored Red `ShotDirection` and the stored Green tether anchor. If one of them is missing, the snap does nothing physical and stays painted Yellow until a timer drops a bit. Do not invent a direction.
- One impulse, once:

```
pullDirection = normalize(greenAnchor - bodyPosition)
snapDirection = normalize(redDirection * pushWeight + pullDirection * pullWeight)
velocity change = snapDirection * snapImpulse
```

- `pushWeight`, `pullWeight`, and `snapImpulse` are designer tunables. Default weights are equal. The result must be able to cross a gameplay gap; the number belongs in data, not in the gun.
- The body is not left in stasis. Yellow does not freeze time and does not follow the camera.
- Paint timers are **not** paused. Yellow is instant. Red and Green keep their 10 second clocks (or whatever remains). The snap does not repeat when a duplicate hit fails to refresh a timer, and it does not repeat on later `OnColorStateChanged` calls that are still Yellow.
- When either ingredient expires, full-clear to `None`. Do not leave the other primary painted.
- Re-arm only if the pair was cleared and later both bits become present again on a new edge.

### Red + Green + Blue = White — System Override / Reset

Fantasy: white noise. The facility locally reboots the object. Every paint, hold, fuse, and anomaly on it is gone.

Entry (edge into `Red | Green | Blue`, including a third primary landing on any pair):

- This reaction is immediate. There is no hold and no fuse.
- Raise `OnWhiteTriggered` for the white-noise flash. Particles and emissive response live on that event.
- Cancel Magenta without detonating.
- Cancel Cyan telekinesis immediately. Stop camera tracking. Restore gravity. Do not leave damping at 0.
- Cancel any pending Yellow re-arm data.
- Clear stored Red direction, Green anchor, and stasis snapshot.
- Remove joints or other anomaly components this system added.
- Zero residual velocity from this system. Do not add a blast.
- Call the same clear as `ResetColors()`: flags `None`, all paint timers cleared, visual Neutral Grey. No primary may remain.

Event order for listeners:

1. State becomes White long enough for `OnColorStateChanged` to report `Red | Green | Blue` (the override signal).
2. The reactor raises `OnWhiteTriggered` and handles White.
3. State becomes `None` and `OnColorStateChanged` fires again.

Listeners must treat White as a command to reset, then trust `None` as the resting state. They must not start Magenta, Cyan, or Yellow from the intermediate White value. VFX may flash white on step 2; the persistent material is Neutral Grey after step 3.

---

## 7. Transition Table

"Edge" means the masked flags changed. Duplicate hits that do not change flags do not appear here. Immediate `OnColorShotHit` still runs for those hits.

| From | To | What happens |
|---|---|---|
| Any without Red | gains Red only | Paint Red for 10 seconds. Live impulse, or store it if already in stasis. |
| Any without Green | gains Green only | Paint Green for 10 seconds. Live pull unless in stasis. Store anchor. |
| Any without Blue | gains Blue only | Paint Blue for 10 seconds. Snapshot velocity. Enter stasis. |
| Has Red, no Blue | gains Blue | Enter Magenta. Arm fuse. Pause Red and Blue timers. `OnMagentaTriggered`. |
| Has Blue, no Red | gains Red | Enter Magenta. Fold Red into stored energy. Arm fuse. Pause Red and Blue timers. `OnMagentaTriggered`. |
| Has Green, no Blue | gains Blue | Enter Cyan telekinesis if mass ≤ 50. Heavier bodies collapse to Blue Stasis instead. |
| Has Blue, no Green | gains Green | Enter Cyan telekinesis if mass ≤ 50, after releasing stasis. Heavier bodies stay Blue Stasis. |
| Cyan (`Green \| Blue`) | another Blue shot | Stasis Lock. Strip Green. Keep Blue and its current timer. Stop tracking. Freeze in mid-air. |
| Has Red, no Green | gains Green | Enter Yellow. One snap. Timers keep running. `OnYellowTriggered`. |
| Has Green, no Red | gains Red | Enter Yellow. One snap. Timers keep running. `OnYellowTriggered`. |
| Any pair | gains the missing primary | White. `OnWhiteTriggered`. Cancel the pair. Clear to `None`. |
| Magenta | fuse completes | `OnMagentaDetonated`. Blast. Clear to `None`. |
| Yellow, Magenta, or Cyan | an ingredient timer elapses | Full clear to `None`. No leftover primary. |
| Lone primary | its 10 second timer elapses | `None`. |
| Any | `ResetColors()` | Same physical cleanup as White, without requiring the third color. No blast. |

---

## 8. VFX and Event Architecture

`ColorMixPhysicsReactor` is the only place mix reactions are decided. It exposes standard `UnityEvent` hooks so Lighting and VFX can react without a reference from physics code to particles, lights, or emissive materials.

| Event | When it fires |
|---|---|
| `OnMagentaTriggered` | Edge into Magenta, as the fuse is armed |
| `OnMagentaDetonated` | The fuse completes and the kinetic blast is released, before the clear to `None` |
| `OnCyanTriggered` | Edge into Cyan, as telekinesis starts |
| `OnYellowTriggered` | Edge into Yellow, as the snap impulse is applied |
| `OnWhiteTriggered` | Edge into White, as the reset begins |

Rules:

- The reactor invokes the event. It does not spawn a particle system, set a light, or write an emission color.
- VFX components on the same object (or wired in the inspector) subscribe to these events.
- Stasis Lock does not fire `OnCyanTriggered` and does not have its own mix event. It is a return to primary Blue.
- A full clear to `None` does not fire a mix event by itself. White and Magenta already fired their events before the clear.
- Do not also drive the same particle from `OnColorStateChanged`. The color target reports flags. The reactor reports reactions. Double-firing makes the flash play twice.

Material color on the object (the RGB paint) stays on `RGBColorTarget` via `MaterialPropertyBlock`. That is state display, not the Lighting & VFX pipeline.

---

## 9. Phase Recorder (Hologram Co-op) — Constraints, Not a Design

The Phase Recorder is not implemented by this document. The color system must already be safe for it:

- Sources are interchangeable. The player gun is one `SourceId`. A hologram is another. Neither is a singleton inside the target.
- Simultaneous paints are just two `ApplyColor` calls. The bitwise OR is the merge.
- Yellow's slingshot is the first reaction that **needs** both source records. Magenta and Cyan must also ignore who painted them.
- Cyan's follow point is the player camera, not the source that applied Green or Blue.
- White from either source clears colors applied by both. There is no per-source undo inside the enum.
- Reaction code stays on the target so a hologram shot and a player shot cannot each run half of a bomb.
- Stasis Lock is "Blue payload while flags are Cyan," regardless of which source fired the second Blue.

Do not add hologram recording, playback, or player-launch special cases while working on these reactions. Do leave `SourceId`, `SourcePosition`, and `ShotDirection` on the payload so those cases do not require a gun rewrite.

---

## 10. Implementation Notes for Later Sessions

Follow this order when Color System work continues. This document does not do that work.

1. Read this file and the current `Recorded.ColorSystem` types before editing.
2. Keep the gun as a payload sender. Do not move mix logic into `SpectrumGunController`.
3. Keep `ObjectColorState` as three bits. Derive Yellow, Magenta, Cyan, and White.
4. Keep the **10 second** duplicate-hit paint rules on `RGBColorTarget`.
5. Keep mix detection, the Magenta fuse, Cyan telekinesis, Yellow snap, Stasis Lock handling, and White cleanup in `ColorMixPhysicsReactor`.
6. Any expiry or concluded mix ends on `None`, except Stasis Lock, which leaves Blue.
7. Cyan follows a point 3–5 units in front of the player camera with gravity off and drag ignored. Do not restore the old static anti-gravity platform.
8. Treat `AmmoType.Cyan` as Blue stasis everywhere. Never map that chamber to `Green | Blue`.
9. Pause paint timers only for Magenta's fuse. Do not pause them for Cyan, Yellow, or duplicate hits.
10. White and `ResetColors()` share one physical cleanup path.
11. New lights and particles subscribe to the reactor `UnityEvent`s in section 8. Do not add visual calls inside the physics methods.
12. Tune impulse, snap, blast radius, blast multiplier, fuse length (2–3 seconds, default 2.5), and Cyan follow distance (3–5, default 4) in serialized data on the reactor.
13. When a hologram source is added, it must call the same `IColorReceivable` / `IColorShotImpact` path with its own `SourceId`.

### Out of scope unless a later brief says otherwise

- Traditional damage, health, or hit points on the gun
- New enum members for mixed colors
- Phase Recorder recording and playback
- Player locomotion, other than "the snap target is whoever owns the rigidbody"
- Puzzle-specific scripting (which prop is the door, which gap is the slingshot)
- Hardcoded particle or emissive changes inside `ColorMixPhysicsReactor`

---

## 11. One-Page Contract

- The gun shoots a physical state: Red push, Green pull, Blue freeze.
- The target stores those states as bits 1, 2, and 4. Each new primary lasts **10 seconds**. A duplicate hit does not refresh that clock.
- Mixes are computed, not stored: Yellow slingshot, Magenta time bomb, Cyan telekinesis, White reset.
- Cyan the ammo is Blue. Cyan the reaction is Green plus Blue: gravity off, drag ignored, smooth follow 3–5 units in front of the player camera.
- A second Blue shot during Cyan is Stasis Lock: strip Green, stop tracking, freeze in mid-air as Blue. That Blue timer is not refreshed.
- When a color expires, or when Magenta detonates or White resets, the object goes fully to `None` (Grey). Lingering primaries are not allowed. Stasis Lock is the only exception.
- Duplicate state does not re-fire a mix.
- `ColorMixPhysicsReactor` raises `OnMagentaTriggered`, `OnMagentaDetonated`, `OnCyanTriggered`, `OnYellowTriggered`, and `OnWhiteTriggered`. VFX listens. Physics does not spawn effects.
- Any future shooter, including a hologram, is just another payload.

---

## 12. Systemic Physics and Mass Limits

Every non-structural prop can take a color. Walls, floors, and seals cannot.

- The gun looks up `IColorTarget`. It does not call `GetComponent` for `RGBColorTarget`.
- `IColorTarget` extends `IColorReceivable`. Paint, timers, and mixes stay on that contract.
- A shot still hits Default geometry, so a wall blocks the beam. The color payload is delivered only when the collider is on the layer `ColorInteractable`.
- Red push is `ForceMode.Impulse`. Unity divides that impulse by mass, so a light prop flies and a heavy prop barely moves. Do not switch Red to Acceleration.
- Cyan telekinesis refuses mass above 50 and becomes Blue Stasis instead of a camera follow.
- `PuzzleItemSpawner` watches world Y. Puzzle-critical bodies (`IColorTarget.IsPuzzleCritical`) return to the pose they had on load, clear their paint, and stop. Other props are destroyed. Default fall line is Y = -8.
