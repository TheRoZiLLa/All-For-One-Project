using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Recorded.Tests
{
    public sealed class MovementTests
    {
        private GameObject world, player;
        private FirstPersonMotor motor;
        private CharacterController capsule;
        private Transform pivot;
        private readonly Vector3 origin = new Vector3(10000f, 0f, 10000f);
        private CursorLockMode oldCursor;
        private bool oldVisible;

        [SetUp]
        public void SetUp()
        {
            oldCursor = Cursor.lockState;
            oldVisible = Cursor.visible;
            world = new GameObject("Movement test fixtures");
            Box("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(40f, 0.5f, 40f));
            player = new GameObject("Test player");
            player.SetActive(false);
            player.transform.position = origin;
            capsule = player.AddComponent<CharacterController>();
            pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            var view = new GameObject("View", typeof(Camera));
            view.transform.SetParent(pivot, false);
            view.GetComponent<Camera>().nearClipPlane = 0.03f;
            var look = player.AddComponent<FirstPersonLook>();
            SetField(look, "cameraPivot", pivot);
            look.enabled = false;
            motor = player.AddComponent<FirstPersonMotor>();
            motor.enabled = false;
            SetField(motor, "cameraPivot", pivot);
            player.SetActive(true);
            Physics.SyncTransforms();
            Tick(default, 0.1f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(world);
            Cursor.lockState = oldCursor;
            Cursor.visible = oldVisible;
        }

        [Test]
        public void Move_NormalizesDiagonal_AcceleratesTurnsAndStops()
        {
            Tick(new MovementInput(Vector2.one), 1f);
            Assert.That(new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude,
                Is.EqualTo(motor.Settings.WalkSpeed).Within(0.15f));
            Tick(new MovementInput(Vector2.left), 0.5f);
            Assert.That(motor.Velocity.x, Is.LessThan(-motor.Settings.WalkSpeed * 0.9f));
            Tick(default, 0.4f);
            Assert.That(new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude, Is.LessThan(0.1f));
            Assert.That(motor.IsGrounded, Is.True);
        }

        [Test]
        public void Sprint_WorksBackward_AndCrouchOverridesIt()
        {
            Tick(new MovementInput(Vector2.down, sprintHeld: true), 1f);
            Assert.That(motor.Velocity.z, Is.EqualTo(-motor.Settings.SprintSpeed).Within(0.15f));
            Tick(new MovementInput(Vector2.down, sprintHeld: true, crouchHeld: true), 0.5f);
            Assert.That(motor.IsSprinting, Is.False);
            Assert.That(motor.Velocity.z, Is.EqualTo(-motor.Settings.CrouchSpeed).Within(0.15f));
        }

        [Test]
        public void Jump_ReachesConfiguredHeight_RejectsDoubleJump_AndLands()
        {
            float start = player.transform.position.y;
            motor.Simulate(new MovementInput(Vector2.zero, jumpPressed: true), 1f / 60f);
            Assert.That(motor.IsGrounded, Is.False);
            float apex = player.transform.position.y;
            for (int i = 0; i < 90; i++)
            {
                motor.Simulate(new MovementInput(Vector2.zero, jumpPressed: i == 8), 1f / 60f);
                apex = Mathf.Max(apex, player.transform.position.y);
            }
            Assert.That(apex - start, Is.EqualTo(1.4f).Within(0.08f));
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(player.transform.position.y, Is.EqualTo(start).Within(0.05f));
        }

        [Test]
        public void Jump_CoyoteAndBuffer_WorkWithoutGrantingAnotherAirJump()
        {
            Object.DestroyImmediate(world.transform.Find("Ground").gameObject);
            Physics.SyncTransforms();
            Tick(default, 0.05f);
            motor.Simulate(new MovementInput(Vector2.zero, jumpPressed: true), 1f / 60f);
            Assert.That(motor.Velocity.y, Is.GreaterThan(6f));
            Tick(default, 0.2f);
            float before = motor.Velocity.y;
            motor.Simulate(new MovementInput(Vector2.zero, jumpPressed: true), 1f / 60f);
            Assert.That(motor.Velocity.y, Is.LessThan(before));

            Box("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(40f, 0.5f, 40f));
            Teleport(new Vector3(0f, 0.25f, 0f));
            SetField(motor, "verticalVelocity", -3f);
            SetField(motor, "lastGrounded", float.NegativeInfinity);
            motor.Simulate(new MovementInput(Vector2.zero, jumpPressed: true), 1f / 60f);
            Tick(default, 0.09f);
            Assert.That(motor.Velocity.y, Is.GreaterThan(4f), "Buffered jump should fire on landing.");
        }

        [Test]
        public void Crouch_AlignsCameraAndCapsule_BlocksStanding_ThenRestoresHeight()
        {
            Tick(new MovementInput(Vector2.zero, crouchHeld: true), 0.3f);
            Assert.That(capsule.height, Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(pivot.localPosition.y, Is.EqualTo(0.95f).Within(0.001f));
            Assert.That(capsule.center.y - capsule.height * 0.5f, Is.EqualTo(0f).Within(0.001f));
            var ceiling = Box("Ceiling", new Vector3(0f, 1.55f, 0f), new Vector3(4f, 0.5f, 4f));
            Physics.SyncTransforms();
            Tick(default, 0.4f);
            Assert.That(motor.IsCrouching, Is.True);
            Assert.That(capsule.height, Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(motor.State, Is.EqualTo(MovementState.Crouching));
            Object.DestroyImmediate(ceiling);
            Physics.SyncTransforms();
            motor.Simulate(default, 1f / 60f);
            Assert.That(capsule.height, Is.GreaterThan(1.1f).And.LessThan(1.8f));
            Tick(default, 0.3f);
            Assert.That(capsule.height, Is.EqualTo(1.8f).Within(0.001f));
        }

        [Test]
        public void Impulses_Combine_KeepControl_AndStopAtWallsAndCeilings()
        {
            motor.ApplyImpulse(new Vector3(6f, 0f, 0f));
            motor.ApplyImpulse(new Vector3(4f, 0f, 0f));
            Assert.That(motor.ExternalVelocity.x, Is.EqualTo(10f));
            motor.Simulate(new MovementInput(Vector2.up), 1f / 60f);
            Assert.That(motor.Velocity.x, Is.GreaterThan(5f));
            Assert.That(motor.Velocity.z, Is.GreaterThan(0f));
            Tick(new MovementInput(Vector2.up), 0.4f);
            Assert.That(motor.ExternalVelocity.x, Is.LessThan(2f));
            Box("Wall", new Vector3(3f, 2f, 0f), new Vector3(0.5f, 4f, 10f));
            Box("Ceiling", new Vector3(0f, 2.5f, 0f), new Vector3(10f, 0.5f, 10f));
            Physics.SyncTransforms();
            motor.ApplyImpulse(new Vector3(50f, 20f, 0f));
            Tick(default, 0.25f);
            Assert.That(player.transform.position.x - origin.x, Is.LessThan(2.5f));
            Assert.That(player.transform.position.y + capsule.height, Is.LessThan(2.35f));
            Tick(default, 1f);
            Assert.That(motor.IsGrounded, Is.True);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void ImpulseDecay_IsIndependentOfFrameRate(int fps)
        {
            motor.ApplyImpulse(new Vector3(10f, 0f, 0f));
            for (int i = 0; i < fps; i++) motor.Simulate(default, 1f / fps);
            Assert.That(motor.ExternalVelocity.x, Is.EqualTo(10f * Mathf.Exp(-5f)).Within(0.001f));
            Assert.That(player.transform.position.x - origin.x, Is.EqualTo(2f * (1f - Mathf.Exp(-5f))).Within(0.05f));
        }

        [Test]
        public void StairsAndSlope_AreTraversable_AndSteepSlopeDoesNotGround()
        {
            for (int i = 0; i < 5; i++)
                Box("Step", new Vector3(0f, (i + 1) * 0.1f, 1f + i * 0.5f),
                    new Vector3(3f, (i + 1) * 0.2f, 0.5f));
            Physics.SyncTransforms();
            Tick(new MovementInput(Vector2.up), 3f / motor.Settings.WalkSpeed);
            Assert.That(player.transform.position.y, Is.GreaterThan(0.55f));
            Assert.That(motor.IsGrounded, Is.True);
            var ramp = Box("Slope", new Vector3(8f, 0.7f, 0f), new Vector3(4f, 0.3f, 6f));
            ramp.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            Teleport(new Vector3(8f, 0.4f, -2f));
            SetField(motor, "movementVelocity", Vector3.zero);
            Tick(new MovementInput(Vector2.up), 0.8f);
            Assert.That(player.transform.position.y, Is.GreaterThan(0.7f));
            Assert.That(motor.IsGrounded, Is.True);
            Tick(new MovementInput(Vector2.down), 0.5f);
            Assert.That(motor.IsGrounded, Is.True, "Downhill movement should stay grounded.");
            ramp.transform.rotation = Quaternion.Euler(-70f, 0f, 0f);
            Teleport(new Vector3(8f, 0f, -3.2f));
            Tick(new MovementInput(Vector2.up), 0.8f);
            Assert.That(player.transform.position.y, Is.LessThan(0.4f), "A steep slope must not be climbed.");
        }

        [Test]
        public void Look_ClampsPitch_RotatesYaw_AndSnapshotExposesPose()
        {
            var look = player.GetComponent<FirstPersonLook>();
            look.ApplyLook(new Vector2(900f, 10000f));
            Assert.That(look.Pitch, Is.EqualTo(-85f));
            Assert.That(Mathf.DeltaAngle(0f, player.transform.eulerAngles.y), Is.EqualTo(90f).Within(0.01f));
            look.ApplyLook(new Vector2(0f, -10000f));
            Assert.That(look.Pitch, Is.EqualTo(85f));
            var snapshot = motor.Snapshot;
            Assert.That(snapshot.CameraPitch, Is.EqualTo(85f));
            Assert.That(snapshot.Position, Is.EqualTo(player.transform.position));
            Assert.That(snapshot.Rotation, Is.EqualTo(player.transform.rotation));
            Assert.That(snapshot.IsGrounded, Is.EqualTo(motor.IsGrounded));
            Assert.That(snapshot.State, Is.EqualTo(motor.State));
            Assert.That(snapshot.Velocity, Is.EqualTo(motor.Velocity));
        }

        [TestCase(30, 0)]
        [TestCase(60, 0)]
        [TestCase(144, 0)]
        [TestCase(30, 1)]
        [TestCase(60, 1)]
        [TestCase(144, 1)]
        [TestCase(30, 2)]
        [TestCase(60, 2)]
        [TestCase(144, 2)]
        public void Stairs_CompleteSandboxFlight_UpAndDown(int fps, int mode)
        {
            for (int i = 0; i < 6; i++)
                Box("Stair regression", new Vector3(5f, (i + 1) * 0.1f, 4f + i * 0.5f),
                    new Vector3(3f, (i + 1) * 0.2f, 0.5f));
            Teleport(new Vector3(5f, 0.03f, 2.8f));
            float highest = 0f;
            for (int i = 0; i < fps * 2; i++)
            {
                motor.Simulate(new MovementInput(Vector2.up, sprintHeld: mode == 1, crouchHeld: mode == 2), 1f / fps);
                highest = Mathf.Max(highest, player.transform.position.y);
            }
            Assert.That(highest, Is.GreaterThan(1.15f), "Must reach the sixth tread, not only the first step.");
            Assert.That(player.transform.position.z - origin.z, Is.GreaterThan(6.9f));
            Teleport(new Vector3(5f, 1.23f, 6.4f));
            SetField(motor, "movementVelocity", Vector3.zero);
            SetField(motor, "verticalVelocity", 0f);
            for (int i = 0; i < fps * 2; i++)
                motor.Simulate(new MovementInput(Vector2.down, sprintHeld: mode == 1, crouchHeld: mode == 2), 1f / fps);
            Assert.That(player.transform.position.z - origin.z, Is.LessThan(3.5f));
            Assert.That(motor.IsGrounded, Is.True);
        }

        [Test]
        public void Step_DoesNotClimbTallWall_AndCrouchStillFitsUnderCeiling()
        {
            var wall = Box("Tall step", new Vector3(0f, 0.5f, 1f), new Vector3(3f, 1f, 1f));
            Physics.SyncTransforms();
            Tick(new MovementInput(Vector2.up), 0.6f);
            Assert.That(player.transform.position.y, Is.LessThan(0.1f));
            Object.DestroyImmediate(wall);
            Teleport(Vector3.zero);
            Box("Low step", new Vector3(0f, 0.1f, 1f), new Vector3(3f, 0.2f, 1f));
            Box("Low ceiling", new Vector3(0f, 1.6f, 1f), new Vector3(3f, 0.2f, 1f));
            Physics.SyncTransforms();
            float highest = 0f;
            for (int i = 0; i < 42; i++)
            {
                motor.Simulate(new MovementInput(Vector2.up, crouchHeld: true), 1f / 60f);
                highest = Mathf.Max(highest, player.transform.position.y);
            }
            Assert.That(highest, Is.GreaterThan(0.18f));
            Assert.That(motor.IsCrouching, Is.True);
        }

        [Test]
        public void CameraMotion_RemainsStillWhileWalking_IdleAndKnockback()
        {
            var look = player.GetComponent<FirstPersonLook>();
            var view = player.GetComponentInChildren<Camera>().transform;
            var updateView = typeof(FirstPersonLook).GetMethod("UpdateCameraMotion", BindingFlags.NonPublic | BindingFlags.Instance);
            float maximumOffset = 0f;
            for (int i = 0; i < 60; i++)
            {
                motor.Simulate(new MovementInput(Vector2.up), 1f / 60f);
                updateView.Invoke(look, new object[] { 1f / 60f });
                maximumOffset = Mathf.Max(maximumOffset, view.localPosition.magnitude);
                Assert.That(view.position.y - player.transform.position.y + 0.03f,
                    Is.LessThan(capsule.height - motor.Settings.SkinWidth));
            }
            Assert.That(maximumOffset, Is.LessThan(0.002f));
            for (int i = 0; i < 60; i++)
            {
                motor.Simulate(default, 1f / 60f);
                updateView.Invoke(look, new object[] { 1f / 60f });
            }
            Assert.That(view.localPosition.magnitude, Is.LessThan(0.002f));
            motor.ApplyImpulse(Vector3.right * 8f);
            for (int i = 0; i < 30; i++)
            {
                motor.Simulate(default, 1f / 60f);
                updateView.Invoke(look, new object[] { 1f / 60f });
                Assert.That(view.localPosition.magnitude, Is.LessThan(0.002f));
            }
            Assert.That(look.Pitch, Is.EqualTo(0f));
        }

        [Test]
        public void CameraMotion_SoftensStairs_WithoutChangingCapsuleOrLook()
        {
            for (int i = 0; i < 6; i++)
                Box("Camera stair", new Vector3(0f, (i + 1) * 0.1f, 1f + i * 0.5f),
                    new Vector3(3f, (i + 1) * 0.2f, 0.5f));
            Physics.SyncTransforms();
            var look = player.GetComponent<FirstPersonLook>();
            var view = player.GetComponentInChildren<Camera>().transform;
            var updateView = typeof(FirstPersonLook).GetMethod("UpdateCameraMotion", BindingFlags.NonPublic | BindingFlags.Instance);
            updateView.Invoke(look, new object[] { 1f / 60f });
            float largestRootRise = 0f, largestViewRise = 0f;
            for (int i = 0; i < 65; i++)
            {
                float rootBefore = player.transform.position.y, viewBefore = view.position.y;
                motor.Simulate(new MovementInput(Vector2.up), 1f / 60f);
                updateView.Invoke(look, new object[] { 1f / 60f });
                largestRootRise = Mathf.Max(largestRootRise, player.transform.position.y - rootBefore);
                largestViewRise = Mathf.Max(largestViewRise, view.position.y - viewBefore);
            }
            Assert.That(largestRootRise, Is.GreaterThan(0.05f));
            Assert.That(largestViewRise, Is.LessThan(largestRootRise * 0.9f));
            Assert.That(capsule.height, Is.EqualTo(motor.Settings.StandingHeight));
            Assert.That(look.Pitch, Is.EqualTo(0f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void CameraMotion_GentleBobAndTilt_AreBoundedAndSettle(int fps)
        {
            var look = player.GetComponent<FirstPersonLook>();
            var view = player.GetComponentInChildren<Camera>().transform;
            var updateView = typeof(FirstPersonLook).GetMethod("UpdateCameraMotion", BindingFlags.NonPublic | BindingFlags.Instance);
            SetField(look, "enableBob", true);
            SetField(look, "enableTilt", true);
            float dt = 1f / fps, peakOffset = 0f, peakRoll = 0f;
            Vector3 previous = view.localPosition;
            float previousRoll = 0f;
            for (int i = 0; i < fps * 2; i++)
            {
                motor.Simulate(new MovementInput(i < fps ? Vector2.right : Vector2.left), dt);
                updateView.Invoke(look, new object[] { dt });
                float currentRoll = Mathf.DeltaAngle(0f, view.localEulerAngles.z);
                peakOffset = Mathf.Max(peakOffset, view.localPosition.magnitude);
                peakRoll = Mathf.Max(peakRoll, Mathf.Abs(currentRoll));
                Assert.That(Vector3.Distance(previous, view.localPosition) / dt, Is.LessThan(0.07f), "Bob must not snap.");
                Assert.That(Mathf.Abs(currentRoll - previousRoll) / dt, Is.LessThan(35f), "Tilt must not snap when reversing.");
                Assert.That(Mathf.Abs(currentRoll), Is.LessThanOrEqualTo(1.52f));
                Assert.That(view.position.y - player.transform.position.y + 0.03f,
                    Is.LessThan(capsule.height - motor.Settings.SkinWidth));
                previous = view.localPosition;
                previousRoll = currentRoll;
            }
            Assert.That(peakOffset, Is.GreaterThan(0.0007f).And.LessThan(0.009f));
            Assert.That(peakRoll, Is.GreaterThan(1f));
            look.ApplyLook(new Vector2(20f, 10f));
            Assert.That(look.Pitch, Is.EqualTo(-1f).Within(0.001f), "Mouse look stays immediate.");
            for (int i = 0; i < fps * 2; i++)
            {
                motor.Simulate(default, dt);
                updateView.Invoke(look, new object[] { dt });
            }
            Assert.That(view.localPosition.magnitude, Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, view.localEulerAngles.z)), Is.LessThan(0.005f));
            motor.ApplyImpulse(Vector3.right * 8f);
            for (int i = 0; i < fps / 2; i++)
            {
                motor.Simulate(default, dt);
                updateView.Invoke(look, new object[] { dt });
                Assert.That(view.localPosition.magnitude, Is.LessThan(0.0001f), "Knockback alone must not add footsteps.");
            }
            SetField(look, "enableBob", false);
            SetField(look, "enableTilt", false);
            for (int i = 0; i < fps; i++)
            {
                motor.Simulate(new MovementInput(Vector2.right), dt);
                updateView.Invoke(look, new object[] { dt });
            }
            Assert.That(view.localPosition.magnitude, Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, view.localEulerAngles.z)), Is.LessThan(0.005f));
        }

        [Test]
        public void InputReader_UsesActionAsset_AndDoesNotDisableSharedActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Player");
            map.AddAction("Move", InputActionType.Value)
                .AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w");
            map.AddAction("Look", InputActionType.Value, "<Pointer>/delta");
            map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            map.AddAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            map.Enable();
            var readerObject = new GameObject("Test input reader");
            readerObject.transform.SetParent(world.transform);
            readerObject.SetActive(false);
            var reader = readerObject.AddComponent<PlayerInputReader>();
            SetField(reader, "inputActions", asset);
            readerObject.SetActive(true);
            var originalSettings = InputSystem.settings;
            var previousBackground = originalSettings.backgroundBehavior;
            originalSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var previousEditorInput = originalSettings.editorInputBehaviorInPlayMode;
            originalSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.That(reader.enabled, Is.True);
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(
                    Key.W, Key.Space, Key.LeftShift, Key.LeftCtrl));
                InputSystem.Update();
                var runtime = (InputActionAsset)typeof(PlayerInputReader)
                    .GetField("runtimeActions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reader);
                var runtimeMap = runtime.FindActionMap("Player");
                Assert.That(runtimeMap.FindAction("Move").ReadValue<Vector2>().y, Is.EqualTo(1f));
                Assert.That(runtimeMap.FindAction("Jump").IsPressed(), Is.True);
                Assert.That(runtimeMap.FindAction("Sprint").IsPressed(), Is.True);
                Assert.That(runtimeMap.FindAction("Crouch").IsPressed(), Is.True);
                reader.enabled = false;
                Assert.That(map.enabled, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                originalSettings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
                originalSettings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
                map.Disable();
                Object.DestroyImmediate(asset);
            }
        }

        private void Tick(MovementInput command, float duration)
        {
            int frames = Mathf.CeilToInt(duration * 60f);
            for (int i = 0; i < frames; i++) motor.Simulate(command, duration / frames);
        }

        private GameObject Box(string name, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(world.transform);
            box.transform.position = origin + position;
            box.transform.localScale = scale;
            return box;
        }

        private void Teleport(Vector3 position)
        {
            capsule.enabled = false;
            player.transform.position = origin + position;
            capsule.enabled = true;
            Physics.SyncTransforms();
        }

        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
