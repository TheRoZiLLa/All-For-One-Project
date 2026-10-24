# RECORDED — Enemy kill sheet

Scene: `Assets/_Project/Scenes/Dev GameTest.unity`
Shared rule for every enemy. A normal color hit does not kill.

The body dies only when all of these are true:

1. It is the third shot in the current sequence.
2. That shot is Red.
3. The body was already a mix before that Red (two primaries on it).

The Red knockback plays first. Death is 0.35s later, or sooner if the body hits a wall. The floor and the player do not count as a wall. The shot counter clears when the paint returns to none. After death the enemy respawns at its start pose in about 2.5s.

A Red on shot 1 or shot 2 never kills. Magenta's bomb detonates after 2.5s and then clears all paint, so a Red fired after the boom is a new shot 1 and does not kill. The killing Red has to land while the mix is still up.

Colors: Red = Impulse, Green = Tether, Blue = Stasis, Yellow = Red+Green snap, Magenta = Red+Blue time bomb, Cyan = Green+Blue telekinesis.

| Enemy | Scene object | How to kill | Wired in this build |
|---|---|---|---|
| The Blaster | `Enemy_Surveyor` | Any mix, then Red | Yes |
| The Bulwark | `Enemy_Bulwark` | Yellow (shield snaps off), then Red | Yes. Any two-color mix drops the plate. Yellow pops it harder. |
| The Drone | `Enemy_Drone` | Green tether, Red snap, Red. Or Blue drop, Red, Red | Kill rule yes. It hovers until Blue or Green pauses it, then gravity and the tether can move it. |
| The Demolitionist | `Enemy_Demolitionist` | Red, Blue, Red during the Magenta fuse | Yes. Only Magenta (Red+Blue, no Green) may finish it. Mass 80. |
| The Stalker | `Enemy_Stalker` | Green, Red, Red while it is latched on the hologram | Kill rule yes, but the hologram latch is not enforced yet. Any mix then Red kills it. |
| The Glint | `Enemy_Glint` | Green, Blue, Red while Cyan is still holding it | Yes. Only Cyan (Green+Blue, no Red) may finish it. |

## The Blaster

Ranged. Stands off and shoots a bolt. It does not rush unless it is outside about 7.5 m, then it closes and fires.

Kill: build any mix, then Red.

- Green, Blue, Red. Cyan, then the finisher.
- Green, Red, Red. The second shot is the Yellow snap and is not a kill. The third Red finishes.

## The Bulwark

Walks in behind a front plate. The plate takes the hit and throws that color back while it is still up.

Kill:

1. Green. The plate tints and stays up.
2. Red. Yellow. The plate snaps off. Not a kill.
3. Red. Finisher.

In this build any second primary drops the plate, not only Yellow. The kill is still the third shot, and it must be Red on a mix.

## The Drone

Flies and strafes. It does not land on its own. Mass is about 3, so a tether can reel it.

Kill, tether line:

1. Green on the Drone while another Green object is in tether range (`EnemyLane_Anchor` is the post in this scene). The reel plants it.
2. Red. Yellow snap into the floor. Not a kill.
3. Red. Finisher.

Kill, drop line:

1. Blue. Stasis cuts the hover and it falls.
2. Red. Not Green. Green here becomes Cyan and lifts it again.
3. Red, while that mix is still up. Finisher.

## The Demolitionist

Slow armored walker, mass 80. Red impulse does not move it in any useful way. Cyan is over the lift cap of 50, so it collapses to Blue stasis and the plates stay shut. A third Red on Yellow or Cyan does not kill it.

Kill:

1. Red. Paint only. The shove is ignored.
2. Blue. Magenta arms. Fuse is 2.5s. The body is held in stasis.
3. Red during that fuse, before the boom clears the paint. Finisher. The explosion still plays. A Red after the clear does not kill.

## The Stalker

Fast and thin. It weaves toward the player, or toward a hologram if one is active.

Target kill, for Gamma:

1. It must be latched on the Phase Recorder hologram. Shots on the roaming body should not count.
2. Green. Tether the latch so it cannot bail.
3. Red. Yellow snap off the hologram. Not a kill.
4. Red. Finisher.

This build does not lock shots to the latch yet. Any mix, then Red, kills it.

## The Glint

Small body, hovers, mass about 2. The intended read is that the core only turns toward the player while Cyan is holding it.

Kill:

1. Green. Paint only. Not a kill.
2. Blue. Cyan. It is in the telekinesis hold.
3. Red while Cyan is still up. The hold breaks into the impulse, then the finisher. A wall hit in that window kills immediately.

A third Red on Yellow does not kill it. A second Blue during Cyan strips it back to plain stasis and closes the finisher path.
