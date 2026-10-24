using UnityEngine;
using UnityEngine.InputSystem;

namespace Recorded
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        private InputActionAsset runtimeActions;
        private InputActionMap map;
        private InputAction move, look, jump, sprint, crouch;

        public MovementInput Current { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool EscapePressed { get; private set; }
        public bool ResumePressed { get; private set; }

        private void OnEnable()
        {
            if (runtimeActions == null)
            {
                if (inputActions == null)
                {
                    Debug.LogError("Assign an Input Actions asset to PlayerInputReader.", this);
                    enabled = false;
                    return;
                }
                // Instantiate() leaves cloned maps pointing at the shared asset, so Enable()
                // asserts "Map must be contained in state". FromJson builds a private copy.
                runtimeActions = InputActionAsset.FromJson(inputActions.ToJson());
                runtimeActions.hideFlags = HideFlags.HideAndDontSave;
                map = runtimeActions.FindActionMap(actionMapName, false);
                move = map?.FindAction("Move");
                look = map?.FindAction("Look");
                jump = map?.FindAction("Jump");
                sprint = map?.FindAction("Sprint");
                crouch = map?.FindAction("Crouch");
                if (move == null || look == null || jump == null || sprint == null || crouch == null)
                {
                    Debug.LogError("Player map needs Move, Look, Jump, Sprint and Crouch actions.", this);
                    Destroy(runtimeActions);
                    runtimeActions = null;
                    enabled = false;
                    return;
                }
            }
            map.Enable();
        }

        private void Update()
        {
            EscapePressed = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            ResumePressed = Mouse.current?.leftButton.wasPressedThisFrame == true;
            bool active = Cursor.lockState == CursorLockMode.Locked && Application.isFocused;
            Current = active ? new MovementInput(move.ReadValue<Vector2>(), jump.WasPressedThisFrame(),
                sprint.IsPressed(), crouch.IsPressed()) : default;
            LookDelta = active ? look.ReadValue<Vector2>() : Vector2.zero;
        }

        private void OnDisable()
        {
            map?.Disable();
            Current = default;
            LookDelta = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }
    }
}
