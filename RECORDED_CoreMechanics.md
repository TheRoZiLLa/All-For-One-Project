# RECORDED — Core Mechanics Reference

Status: design authority for the Spectrum Gun, color flags, and anomalous physics.
Scope: weapon payload, color mixing, and physical reactions. This file does not authorize script changes by itself.
Future coding sessions that touch the Color System, Spectrum Gun, or physics reactions must read this document first and follow it.

Namespace and types already in the project (do not replace them with a parallel model):

- `Recorded.ColorSystem.ObjectColorState` — `[Flags]` primary colors
- `Recorded.ColorSystem.IColorReceivable` — persistent paint on a target
- `Recorded.ColorSystem.IColorShotImpact` — immediate hit reaction, separate from paint lifetime
- `Recorded.ColorSystem.RGBColorTarget` — flag storage, per-primary timers, visuals
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
4. Use that reaction to cross, move, freeze, launch, or reset the obstacle.
5. If the mix is wrong, fire the third primary. White clears the object so the puzzle can be tried again.

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

### Primaries

| Chamber / design name | Flag | Physical state |
|---|---|---|
| RED — Impulse | `ObjectColorState.Red` (1) | Strong push along `ShotDirection` |
| GREEN — Tether / Pull | `ObjectColorState.Green` (2) | Pull toward `SourcePosition`, and store that position as a tether anchor. Ammo and older comments call this **Bind**. Bind and Tether are the same primary. |
| BLUE — Stasis | `ObjectColorState.Blue` (4) | Freeze the object's physics and time temporarily. Store the pre-freeze velocity so later mixes can spend it. |

### Naming hazard — Cyan ammo is not the Cyan reaction

`AmmoType.Cyan` is the **stasis chamber**. The gun maps it to `ObjectColorState.Blue`.

The **Cyan reaction** (anti-gravity suspension) is the mix `Green | Blue`. It is never a chamber, never an ammo type, and never a fourth flag.

| Word | Means |
|---|---|
| Cyan ammo / Cyan chamber | Primary Blue. Stasis. |
| Cyan reaction | `Green \| Blue`. Anti-gravity suspension. |
| Blue | The flag. Stasis. The only legal way to talk about this primary in gameplay code. |

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
Cyan    = Green | Blue           // 6   Anti-Gravity Suspension
White   = Red   | Green | Blue   // 7   System Override / Reset
```

Rules:

- Apply a primary with bitwise OR (`state |= primary`).
- Remove a primary with bitwise AND-NOT (`state &= ~primary`).
- Test a primary with AND (`(state & primary) != 0`).
- Identify a mix by equality on the masked primaries: `(state & (Red | Green | Blue)) == (Red | Blue)` is Magenta. Do not add `Yellow`, `Magenta`, `Cyan`, or `White` enum members.
- A shot carries one primary. OR-ing `Red | Green` from a single payload is illegal.
- Visual color is resolved from the flags (`RGBColorTarget.GetColorForState`). It is not an input to the physics.

### Paint lifetime (already implemented — do not break)

`RGBColorTarget` owns persistence:

- Each primary has its own timer.
- Duration is `RGBColorTarget.ColorDuration` (5 seconds) from the moment that primary is **first** applied.
- A duplicate hit on a primary that is already active does **not** reset, extend, or restart that timer.
- `IColorShotImpact.OnColorShotHit` still runs on every valid hit, including duplicates. Immediate impulse, tether, and stasis entry are hit reactions. Paint expiry is separate.
- When a primary's timer elapses, only that bit is cleared. The other primaries remain. The displayed color and the active reaction must be recomputed from the new flags.
- `ResetColors()` sets the state to `None`, clears every timer, and restores Neutral Grey.

Neutral Grey (`None`) means no physical paint. It is the resting appearance, not a color the gun can shoot.

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
  IColorShotImpact     immediate primary effect for this hit
  IColorReceivable     OR the primary into flags, timers, visuals
  Color physics reactor  watches the flag transition and runs the mix
```

### Responsibilities

| Owner | Owns | Must not own |
|---|---|---|
| Gun / hologram | Aim, chamber, payload, shooter VFX | Mix math, rigidbody reactions, clearing another source's colors |
| `IColorReceivable` / `RGBColorTarget` | Flags, per-primary timers, duplicate-hit rule, MaterialPropertyBlock color, `OnColorStateChanged`, `ResetColors` | Force magnitudes, joints, explosions |
| `IColorShotImpact` | One-shot response to this hit (push, pull, enter stasis) | Deciding Yellow / Magenta / Cyan / White |
| Physics reactor (future component on the target) | Detect flag **transitions**, run mix reactions, cancel them, restore physics on White or expiry | Reading the gun, raycasting, ammo UI |

The reactor subscribes to `OnColorStateChanged`. It stores the previous flags and reacts to the edge, not to the level. Staying on Magenta must not re-arm the bomb. A duplicate Red hit while Magenta is already active must not restart the fuse.

### Multi-source rule

Any number of sources may call `ApplyColor` on the same target during the same paint window. The target ORs flags. It does not keep a separate color per source inside the enum.

Source identity lives beside the flags, on the target, as reaction inputs:

- Last Red `ShotDirection` and `SourceId`
- Last Green `SourcePosition` (tether anchor) and `SourceId`
- Stasis snapshot (linear and angular velocity) taken at the moment Blue freezes the body

If the player paints Red and a hologram paints Green before either timer expires, the target becomes Yellow and the snap uses both sources' data. No source merges payloads before they arrive.

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
- Freeze means physics and time stop for this object: velocity cleared, gravity disabled for the hold, and the body held in place. Sleeping the rigidbody or making it kinematic are both acceptable as long as the snapshot can be restored.
- While the Blue bit is on, gameplay time for that object does not advance its own motion. Paint timers on the target still run unless a mix rule below pauses them.
- On the edge off Blue, if no mix consumed the snapshot: restore gravity, unfreeze, and restore the snapshotted velocity.
- Re-hitting Blue while it is already active does not take a new snapshot and does not refresh the 5 second timer.

---

## 6. Mix Reactions

A mix is evaluated after the primary bit is applied, on the transition into that exact flag set. White outranks every other mix. If the resulting flags are `Red | Green | Blue`, run White and do not also run Yellow, Magenta, or Cyan.

### Priority

1. White (`7`) — cancel everything, burst, clear.
2. Otherwise the exact pair that just became complete:
   - Magenta (`5`) Time Bomb
   - Cyan (`6`) Anti-Gravity Suspension
   - Yellow (`3`) Kinetic Snap
3. A lone primary uses section 5 only.

Losing a bit (timer expiry) ends the mix that required it. It does not start a different mix unless the remaining flags are themselves a defined pair. With only three primaries, dropping one bit from a pair leaves a single primary, and dropping one bit from White is impossible because White clears all three immediately.

### Red + Blue = Magenta — Time Bomb

Fantasy: the object is frozen and loaded. A short fuse later it dumps everything it stored as a kinetic blast.

Entry (edge into `Red | Blue`):

- Cancel any live Yellow or Cyan hold. Those pairs cannot coexist with Magenta.
- Ensure stasis is on. If Blue was just applied, the stasis snapshot from section 5 is the stored energy. If the body was already frozen, keep that snapshot; do not overwrite it with zero.
- If Red arrives during stasis, fold the Red impulse into the stored kinetic vector instead of moving the body.
- Hold the object in place for the fuse.
- Fuse length is a designer tunable, default **2.5 seconds**, legal range **2.0–3.0 seconds**. The fuse is a reaction timer, not a new color flag.
- While the fuse runs, pause the Red and Blue paint timers so the bomb cannot expire out from under itself. Green is not present.

Detonation:

- Unfreeze the body.
- Release a radial kinetic blast at the object's position (affects other dynamic bodies in radius; does not use the gun).
- Add the stored velocity, scaled by a blast multiplier, back onto this body so the charged object itself is thrown.
- Consume the mix: clear Red and Blue, clear the snapshot, clear the fuse, unpause nothing (timers are cleared).
- Visual: Magenta for the fuse, then the blast. Persistent state after detonation is `None` unless a future hit has already reapplied a color (it should not, mid-detonation).

Cancel:

- Incoming Green completes White. Cancel the fuse, do not detonate, run White.
- `ResetColors()` from any other legal caller also cancels the fuse without detonating.

### Green + Blue = Cyan — Anti-Gravity Suspension

Fantasy: the object is frozen in time and has no gravity. It stays where the mix completed. Used as a mid-air platform.

Entry (edge into `Green | Blue`):

- Snapshot velocity if Blue is just now freezing the body, then zero it.
- Record the world position and rotation at the moment of the mix. That pose is the hold. "Where it was hit" means this pose, not a point that follows the gun.
- Disable gravity. Freeze position and rotation.
- Do not pull toward the green source while the hold is active. The tether anchor is still stored, but the live pull is suppressed until the hold ends.
- Pause Green and Blue paint timers while the hold is active so a platform does not fall at an arbitrary leftover of the 5 second window. The hold lasts until it is cancelled or overridden, not until a hidden timer the player cannot see. If a later tuning pass wants a maximum hold, add an explicit `cyanHoldDuration`. Do not reuse `ColorDuration` silently for that.

Exit (White, `ResetColors`, or a future explicit release):

- Restore gravity.
- Unfreeze.
- Do not restore the pre-stasis velocity. A platform that resumes its old speed is a hazard, not a puzzle tool. Leave linear and angular velocity at zero, then let gravity take over.
- Clear the hold pose.

Cancel:

- Incoming Red completes White. Run White. Do not fall back into a lone primary first.
- There is no automatic conversion from Cyan into Magenta or Yellow without passing through White, because the third primary is White.

### Red + Green = Yellow — Kinetic Snap

Fantasy: a violent push and pull in one reaction. Puzzle use is a slingshot across a gap. Later, a hologram can be the second hand on that slingshot, including launching the player.

Entry (edge into `Red | Green`):

- Read the stored Red `ShotDirection` and the stored Green tether anchor. If one of them is missing, the snap does nothing physical and stays painted Yellow until a timer drops a bit. Do not invent a direction.
- One impulse, once:

```
pullDirection = normalize(greenAnchor - bodyPosition)
snapDirection = normalize(redDirection * pushWeight + pullDirection * pullWeight)
velocity change = snapDirection * snapImpulse
```

- `pushWeight`, `pullWeight`, and `snapImpulse` are designer tunables. Default weights are equal. The result must be able to cross a gameplay gap; the number belongs in data, not in the gun.
- The body is not left in stasis. Yellow does not freeze time.
- Paint timers are **not** paused. Yellow is instant. Red and Green keep their 5 second clocks (or whatever remains). The snap does not repeat when a duplicate hit fails to refresh a timer, and it does not repeat on later `OnColorStateChanged` calls that are still Yellow.
- Re-arm only if the pair is broken (a bit expires or White clears) and later both bits become present again on a new edge.

Stasis interaction:

- Yellow does not include Blue. If the body was somehow frozen, the snap unfreezes it first, then applies the impulse. The intended way to be frozen and loaded is Magenta, not Yellow.

Future hologram:

- The snapped body may be a prop or, later, a body the player is standing on or a hologram-linked proxy. The reactor applies the impulse to the target's rigidbody. It does not check "is this the player."
- Two sources are already supported if each wrote its own payload field (Red direction from one `SourceId`, Green anchor from another).

### Red + Green + Blue = White — System Override / Reset

Fantasy: white noise. The facility locally reboots the object. Every paint, hold, fuse, and anomaly on it is gone.

Entry (edge into `Red | Green | Blue`, including a third primary landing on any pair):

- This reaction is immediate. There is no hold and no fuse.
- Cancel Magenta without detonating.
- Cancel Cyan without leaving the body in the air under a leftover freeze. Restore gravity and unfreeze as part of the reset.
- Cancel any pending Yellow re-arm data.
- Clear stored Red direction, Green anchor, and stasis snapshot.
- Remove joints or other anomaly components this system added.
- Zero residual reaction velocity from this system. Do not add a blast.
- Call the same clear as `ResetColors()`: flags `None`, all paint timers cleared, visual Neutral Grey.
- Play a White flash as VFX. White is not a persistent painted state. `currentState` must not remain `Red | Green | Blue` after the reaction.

Event order for listeners:

1. State becomes White long enough for `OnColorStateChanged` to report `Red | Green | Blue` (the flash / override signal).
2. The reactor handles White.
3. State becomes `None` and `OnColorStateChanged` fires again.

Listeners must treat White as a command to reset, then trust `None` as the resting state. They must not start Magenta, Cyan, or Yellow from the intermediate White value.

---

## 7. Transition Table

"Edge" means the masked flags changed. Duplicate hits that do not change flags do not appear here. Immediate `OnColorShotHit` still runs for those hits.

| From | To | What happens |
|---|---|---|
| Any without Red | gains Red only | Paint Red. Live impulse, or store it if already in stasis. |
| Any without Green | gains Green only | Paint Green. Live pull unless in stasis. Store anchor. |
| Any without Blue | gains Blue only | Paint Blue. Snapshot velocity. Enter stasis. |
| Has Red, no Blue | gains Blue | Enter Magenta. Arm fuse. Pause Red and Blue timers. |
| Has Blue, no Red | gains Red | Enter Magenta. Fold Red into stored energy. Arm fuse. Pause Red and Blue timers. |
| Has Green, no Blue | gains Blue | Enter Cyan. Lock pose, gravity off, freeze. Pause Green and Blue timers. |
| Has Blue, no Green | gains Green | Enter Cyan. Same hold. Suppress live pull. |
| Has Red, no Green | gains Green | Enter Yellow. One snap. Timers keep running. |
| Has Green, no Red | gains Red | Enter Yellow. One snap. Timers keep running. |
| Any pair | gains the missing primary | White. Cancel the pair reaction. Clear to `None`. |
| Magenta | fuse completes | Blast. Clear Red and Blue. State `None`. |
| Magenta / Cyan / Yellow | a required bit is cleared without White | End that reaction using its exit rule. Remaining lone primary follows section 5. |
| Any | `ResetColors()` | Same physical cleanup as White, without requiring the third color. No blast. |

---

## 8. Phase Recorder (Hologram Co-op) — Constraints, Not a Design

The Phase Recorder is not implemented by this document. The color system must already be safe for it:

- Sources are interchangeable. The player gun is one `SourceId`. A hologram is another. Neither is a singleton inside the target.
- Simultaneous paints are just two `ApplyColor` calls. The bitwise OR is the merge.
- Yellow's slingshot is the first reaction that **needs** both source records. Magenta and Cyan must also ignore who painted them.
- White from either source clears colors applied by both. There is no per-source undo inside the enum.
- Reaction code stays on the target so a hologram shot and a player shot cannot each run half of a bomb.

Do not add hologram recording, playback, or player-launch special cases while implementing the first physics reactor. Do leave `SourceId`, `SourcePosition`, and `ShotDirection` on the payload so those cases do not require a gun rewrite.

---

## 9. Implementation Notes for Later Sessions

Follow this order when the Color System physics work actually starts. This document does not do that work.

1. Read this file and the current `Recorded.ColorSystem` types before editing.
2. Keep the gun as a payload sender. Extend the payload; do not move mix logic into `SpectrumGunController`.
3. Keep `ObjectColorState` as three bits. Derive Yellow, Magenta, Cyan, and White.
4. Keep the 5 second duplicate-hit paint rules on `RGBColorTarget` for lone primaries.
5. Put mix detection, fuse, suspension, snap, and White cleanup in a target-side reactor that listens to `OnColorStateChanged`.
6. Treat `AmmoType.Cyan` as Blue stasis everywhere. Never map that chamber to `Green | Blue`.
7. Pause paint timers only for Magenta's fuse and Cyan's hold, as specified. Do not pause them for Yellow or for duplicate hits.
8. White and `ResetColors()` share one physical cleanup path.
9. Tune impulse, snap, blast radius, blast multiplier, and fuse length (2–3 seconds, default 2.5) in serialized data on the reactor.
10. When a hologram source is added, it must call the same `IColorReceivable` / `IColorShotImpact` path with its own `SourceId`.

### Out of scope for the color physics pass

- Traditional damage, health, or hit points on the gun
- New enum members for mixed colors
- Phase Recorder recording and playback
- Player locomotion, other than "the snap target is whoever owns the rigidbody"
- Puzzle-specific scripting (which prop is the door, which gap is the slingshot)

---

## 10. One-Page Contract

- The gun shoots a physical state: Red push, Green pull, Blue freeze.
- The target stores those states as bits 1, 2, and 4.
- Mixes are computed, not stored: Yellow slingshot, Magenta time bomb, Cyan floating hold, White reset.
- Cyan the ammo is Blue. Cyan the reaction is Green plus Blue.
- Duplicate paint does not refresh a timer. Duplicate state does not re-fire a mix.
- White wipes the object back to Neutral Grey and cancels every anomaly, including a bomb that has not exploded yet.
- Any future shooter, including a hologram, is just another payload.
