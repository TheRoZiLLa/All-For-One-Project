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
Use one local player camera and AudioListener; disable the scene's old camera/listener if necessary.
Do not put the player under a scaled or tilted parent.

```text
Player: CharacterController, PlayerInputReader, FirstPersonLook, FirstPersonMotor
  CameraPivot
    MainCamera: Camera, AudioListener
```

References and capsule dimensions are already assigned in the prefab.
MovementSetup in the Editor folder generated the assets through Unity APIs.
`RECORDED > Create Movement Assets` refuses to overwrite an existing prefab or sandbox.

## Files added

- `Movement/PlayerInputReader.cs`: cloned Input System actions and live input collection.
- `Movement/FirstPersonMotor.cs`: CharacterController movement, crouch, grounding and impulses.
- `Movement/FirstPersonLook.cs`: camera pitch, player yaw, cursor ownership and walking camera motion.
- `Movement/PlayerMovementSettings.cs`: Inspector tuning values and capsule validation.
- `Movement/MovementData.cs`: input command, movement states and read-only snapshot.
- `Editor/MovementSetup.cs`: prefab and sandbox creation through Unity's Editor API.
- `Tests/PlayMode/MovementTests.cs`: movement and walking-camera regression tests.
- Three assembly definitions isolate runtime, Editor and test code.
- `Input/RecordedInput.inputactions`, `Prefabs/Player.prefab`, `Scenes/MovementSandbox.unity`
  and `Materials/Floor.mat`, `Materials/Obstacles.mat`: playable assets.
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
| Walking bob amount / stride length | 0.012 / 0.75 m |
| Stair camera smoothing | 0.08 s |

Increase acceleration for faster turns; increase deceleration for faster stops.
Lower gravity magnitude for a slower jump. Jump height remains independent of gravity.
Reduce impulse damping for longer knockback. Zero damping preserves external velocity until collision.
Edit capsule dimensions before entering Play; initialization applies dimensions to CharacterController.
CameraPivot height follows the collision capsule throughout crouch transitions.
The camera softens vertical stair movement without delaying mouse look or moving the collision capsule.
The Player prefab enables subtle walking bob, driven by controlled speed and actual horizontal movement.
Bob fades out when idle or airborne, is reduced while crouching, and is not driven by knockback alone.
Disable `FirstPersonLook > Walking Motion` to remove bob; set `Step Smoothing` to zero to remove stair smoothing.
Walking Motion stays off by default on newly added components and is enabled in the provided prefab.
Camera offsets stay below the capsule top. No roll or automatic FOV changes are applied.

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

On 2026-10-23, Unity Test Runner passed all 24 PlayMode cases on Unity 6000.3.27f1.
The full six-tread sandbox staircase was also checked directly through MCP with the saved Player prefab:
walking, sprinting and crouching all reached the 1.2 m top and cleared the flight at 30, 60 and 144 FPS.
The previous stair failure came from the ground probe missing rounded stair edges and input speed
being erased on contact with a riser. A grounded-only ray fallback now confirms the floor under the
capsule, and CharacterController resolves input collisions without resetting the requested movement speed.
Camera tests cover small walking bob, return to rest, knockback without bob, headroom and stair smoothing.
Two pre-existing Package Manager `ScriptableSingleton already exists` errors remain in the Console;
no movement errors were logged during these checks.

`Tests/PlayMode/MovementTests.cs` contains assertions for movement normalization, turning and stopping,
sprint/crouch priority, jump apex and double-jump rejection, coyote time, jump buffering,
ceiling clearance, combined impulses, wall/ceiling collision, 30/60/144 FPS damping,
stairs/slopes, pitch and snapshot data, and input-map ownership.
Run this assembly from Unity Test Runner's PlayMode tab after saving your scene.

The CharacterController supports static environment collisions and external velocity.
Rigidbody pushing, moving-platform attachment, networking, weapons and recording/replay are outside this foundation.
Mouse comfort, actual hardware controls and collision behavior in the final level still need hands-on playtesting.
