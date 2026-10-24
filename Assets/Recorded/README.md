# RECORDED movement

## Try it

Open `Assets/Recorded/Scenes/MovementSandbox.unity` on its own and press Play.
WASD moves, mouse looks, Space jumps, Left Shift sprints, Left Ctrl crouches.
Escape releases the cursor; click the Game view to resume. Focus loss releases the cursor.
The sandbox contains walls, 20 cm stairs, a ramp, a ledge and a 1.3 m clearance tunnel.
The original SampleScene, its unsaved objects and original InputSystem_Actions asset are preserved.

## Player setup

Drag `Assets/Recorded/Prefabs/Player.prefab` into a scene with solid floor colliders.
Place the root at foot level, with scale (1, 1, 1) and only yaw rotation.
Use the prefab's base camera, hands-only overlay camera and single AudioListener;
disable the scene's old camera/listener if necessary.
Do not put the player under a scaled or tilted parent.

```text
Player: CharacterController, PlayerInputReader, FirstPersonLook, FirstPersonMotor
  CameraPivot
    MainCamera: Camera, AudioListener
      Viewmodel: FirstPersonViewmodel
        Arms: Animator with REV_Viewmodel controller, original hands and gun
      ViewmodelCamera: URP Overlay, Viewmodel layer only
  Camera Effects: global Volume with PlayerCameraProfile
```

References and capsule dimensions are already assigned in the prefab.
MovementSetup in the Editor folder generated the assets through Unity APIs.
`RECORDED > Create Movement Assets` refuses to overwrite an existing prefab or sandbox.

## Files added

- `Movement/PlayerInputReader.cs`: cloned Input System actions and live input collection.
- `Movement/FirstPersonMotor.cs`: CharacterController movement, crouch, grounding and impulses.
- `Movement/FirstPersonLook.cs`: camera pitch, player yaw, cursor ownership and walking camera motion.
- `Movement/FirstPersonViewmodel.cs`: smooth wall proximity retraction and synchronized hands-camera FOV.
- `Movement/PlayerMovementSettings.cs`: Inspector tuning values and capsule validation.
- `Movement/MovementData.cs`: input command, movement states and read-only snapshot.
- `Editor/MovementSetup.cs`: prefab and sandbox creation through Unity's Editor API.
- `Editor/ViewmodelSetup.cs`: attaches existing arms to the camera and configures the URP camera stack.
- `Tests/PlayMode/MovementTests.cs`: movement and walking-camera regression tests.
- `Tests/PlayMode/ViewmodelTests.cs`: camera attachment and wall retraction checks at 30/60/144 FPS.
- Three assembly definitions isolate runtime, Editor and test code.
- `Input/RecordedInput.inputactions`, `Prefabs/Player.prefab`, `Scenes/MovementSandbox.unity`
  and `Materials/Floor.mat`, `Materials/Obstacles.mat`: playable assets.
- `Settings/PlayerCameraProfile.asset`: a dedicated, mild camera-motion blur override.
- Unity-generated `.meta` files retain all asset references.

## Defaults and tuning

Select Player > FirstPersonMotor > Settings. Values are local to each prefab instance.

| Setting | Default |
|---|---|
| Walk / sprint / crouch | 4.5 / 7 / 2.5 m/s |
| Ground acceleration / deceleration | 18 / 24 m/s² |
| Air control | 0.35 |
| Jump height / gravity | 1.4 m / -22 m/s² |
| Coyote time / jump buffer | 0.12 / 0.12 s |
| Standing / crouching height | 1.8 / 1.1 m |
| Radius / skin width | 0.3 / 0.03 m |
| Crouch transition / eye inset | 0.15 s / 0.15 m |
| Step height / slope limit | 0.3 m / 50° |
| Ground probe / ground stick speed | 0.12 m / 2 m/s |
| Ground / air impulse damping | 5 / 0.8 per second |
| Mouse sensitivity / pitch limit | 0.1 degrees per pixel / ±85° |
| Camera FOV / near clip | 75° / 0.03 m |
| Stair camera smoothing | 0.08 s |
| Bob amount / cadence | 0.0035 m / 1.6 Hz at walk speed |
| Maximum strafe tilt / motion smoothing | 0.7° / 0.10 s |
| Motion blur intensity / clamp | 0.12 / 0.025 |

Increase acceleration for faster turns; increase deceleration for faster stops.
Lower gravity magnitude for a slower jump. Jump height remains independent of gravity.
Reduce impulse damping for longer knockback. Zero damping preserves external velocity until collision.
Edit capsule dimensions before entering Play; initialization applies dimensions to CharacterController.
CameraPivot height follows the collision capsule throughout crouch transitions.
The camera softens vertical stair movement without delaying mouse look or moving the collision capsule.
The Player prefab enables a small, damped walking bob and a gentle roll when strafing.
Cadence stays between 0.65 and 1.35 times the base rate; crouching halves the bob amount.
Bob and roll ease back to rest when stopped or airborne, and do not respond to knockback alone.
`FirstPersonLook > Gentle Camera Motion` exposes separate Enable Bob and Enable Tilt switches,
amplitude, frequency, maximum tilt and smoothing time. Newly added components leave the switches off.
Set `FirstPersonLook > Step Smoothing` to zero to remove stair smoothing.
Camera offsets stay below the capsule top. Mouse yaw/pitch stay immediate; no FOV pumping is applied.

The prefab's `Camera Effects` child is a global Volume on Default layer with priority 10.
It uses `Assets/Recorded/Settings/PlayerCameraProfile.asset`, containing only Motion Blur:
CameraOnly mode, Medium quality, intensity 0.12, clamp 0.025. Its parameters override the defaults.
MainCamera has URP post-processing enabled and includes Default in its Volume Layer Mask.
View this effect in Game view. Lower Intensity or disable the Motion Blur override to remove blur.
Other lighting and color profiles are preserved. `MovementSetup.ConfigureCameraEffects` creates
the same setup when generating a fresh Player prefab.

The saved Player prefab includes the existing Arms rig and gun under MainCamera, preserving their pose.
Parenting makes hands follow pitch, yaw, bob and roll immediately. ViewmodelCamera follows the same
transform and FOV, clears world depth and draws only the Viewmodel layer; MainCamera excludes that layer.
This keeps hands visible against nearby walls. Hands do not cast/receive shadows; the overlay disables
post-processing, shadows, depth/color texture requests and automatic Volume updates.
At `MainCamera > Viewmodel > FirstPersonViewmodel`, wall detection starts at 0.8 m and smoothly
pulls back up to 0.08 m / lowers the gun by 10 degrees, with a 0.08 s response. It ignores player colliders.
The same holder adds 0.012 m walking sway, a small movement lean and up to 1.5 degrees of turn lag,
smoothed over 0.10 s. Weapon steps share the camera gait phase even when camera bob is disabled.
Motion fades on stopping or going airborne, halves while crouched, and follows actual travel so pushing
against a wall or knockback alone cannot create footsteps. The near-wall pose keeps priority; gait and
turn sway drop to 20% during Reload. Both arms and ReloadPickup share this transform, preserving grip.
At rest the holder breathes at 0.22 Hz with a 0.0025 m amplitude. Walking fades this out; crouching reduces it.
Jumping adds up to 0.022 m of vertical inertia: the weapon trails takeoff, lifts during descent, then dips
and lightly rebounds on landing. Landing strength follows the last downward speed and stays capped for
long falls. Airborne/idle motion uses the same smoothing, reload reduction and wall priority as walking;
it moves both arms together without changing the camera's aim. Idle Amount and Jump Amount are adjustable
on FirstPersonViewmodel. Pauses freeze the motion; disabling it restores the authored pose.
This is a visual viewmodel effect. The saved prefab now has the Spectrum weapon system on the held REV;
its raycasts check world geometry between the camera, muzzle and target. See
`Assets/_Project/README_SPECTRUM_GUN.md` for ammo, effects and verification details.
Replacement hands must also use the Viewmodel layer.
The Arms Animator animates only the right arm bones for firing; it leaves the camera and
FirstPersonViewmodel wall-retraction transform untouched. The saved rest pose and authored
gun glow lights are retained.

Edit `Assets/Recorded/Input/RecordedInput.inputactions` to change bindings.
It is a dedicated copy of the project's existing actions, with crouch changed to Left Ctrl.
The reader clones this asset at runtime so enabling one player cannot disable another player's maps.
Keyboard and mouse are the supported target; gamepad-specific look tuning is not implemented.

## Integration

`FirstPersonMotor.ApplyImpulse(Vector3)` adds world-space velocity in m/s, independently of movement input.
Successive calls add together. Collision clips blocked external velocity and gravity handles falling.

```csharp
motor.ApplyImpulse(pushDirection.normalized * 8f + Vector3.up * 3f);
```

Read `motor.Snapshot` in LateUpdate, after look and movement, for world position, rotation,
camera pitch, actual displacement velocity, MovementState and IsGrounded.
State is Airborne while in the air, otherwise Crouching or Grounded.
`IsCrouching`, `IsSprinting` and `ExternalVelocity` are also available independently.

For another input source, disable PlayerInputReader and FirstPersonMotor's automatic Update,
then call `motor.Simulate(new MovementInput(move, jumpPressed, sprintHeld, crouchHeld), deltaTime)`
once per frame. Call `look.ApplyLook(mouseDelta)` before movement, with live look disabled.
JumpPressed is an edge, not a held button. Simulate splits movement into collision steps of at most 1/60 s.
This API separates input from execution; it does not promise deterministic physics replay.

## Checks and limits

On 2026-10-24, Unity Test Runner passed all 31 PlayMode cases on Unity 6000.3.27f1.
The full six-tread sandbox staircase was also checked directly through MCP with the saved Player prefab:
walking, sprinting and crouching all reached the 1.2 m top and cleared the flight at 30, 60 and 144 FPS.
The previous stair failure came from the ground probe missing rounded stair edges and input speed
being erased on contact with a riser. A grounded-only ray fallback now confirms the floor under the
capsule, and CharacterController resolves input collisions without resetting the requested movement speed.
Camera tests cover disabled effects, bounded and damped bob/tilt at 30/60/144 FPS,
direction reversals, return to rest, knockback without footsteps, immediate mouse look,
headroom and stair smoothing.
The saved Player prefab was also checked in Play Mode: its camera resolved the dedicated
Motion Blur override in the Volume stack at intensity 0.12 / clamp 0.025, and rendered a Game view capture.
The hands camera stack, preserved rig pose, single AudioListener and near-wall retraction were checked
in the saved scene/prefab. An actual Game view capture against a wall showed the hands and gun intact.
Viewmodel tests cover immediate pitch/yaw/roll/FOV synchronization, smooth bounded retraction,
own-collider exclusion, return to rest and disabling the component at 30/60/144 FPS.

Editor timing samples used the same sandbox spawn and focused Game view, 20 warm-up frames and
150 measured frames. Before the viewmodel change: median 7.88 ms, p95 9.61 ms (~127 FPS).
With the final overlay setup: median 8.81 ms, p95 10.54 ms (~114 FPS). The overlay adds about 0.93 ms;
this change fixes rendering, and is not an overall FPS improvement. An intermediate overlay sample
was 9.36 ms before skipping unused Volume updates; short Editor samples include normal variation.
Turning blur off in a separate sample gave 7.74 ms, so it was not a large cost in this scene.
An unfocused Editor sample was about 100 ms (~10 FPS); severe foreground runtime lag was not
reproduced. GPU timing was unavailable, and no standalone build or final-level benchmark was run.

`Tests/PlayMode/MovementTests.cs` contains assertions for movement normalization, turning and stopping,
sprint/crouch priority, jump apex and double-jump rejection, coyote time, jump buffering,
ceiling clearance, combined impulses, wall/ceiling collision, 30/60/144 FPS damping,
stairs/slopes, pitch and snapshot data, and input-map ownership.
Run this assembly from Unity Test Runner's PlayMode tab after saving your scene.

The CharacterController supports static environment collisions and external velocity.
Rigidbody pushing, moving-platform attachment, networking and recording/replay are outside this foundation.
Weapon gameplay is provided separately by the Spectrum scripts under `Assets/_Project/Scripts/Weapons`.
Mouse comfort, actual hardware controls and collision behavior in the final level still need hands-on playtesting.
