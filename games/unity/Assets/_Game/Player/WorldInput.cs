using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Cafe
{
    // One action map is shared by walking, interaction and menus. Keyboard/mouse first, gamepad where practical.
    public sealed class WorldInput : MonoBehaviour
    {
        private InputActionMap actions;
        private InputAction move, look, padLook, orbit, zoom, interact, talk, cancel, run, walk, jump, menu, crouch, inventory, journal, primary;
        [Tooltip("Rotate the camera with the mouse without holding the right button (cursor is locked while exploring).")]
        public bool AlwaysLook;
        /// Set by UI while a text field has focus or a full-screen menu is open, so typing never moves the avatar.
        public bool Suspended;
        private bool walkToggle;

        public Vector2 Movement => Suspended ? Vector2.zero : move.ReadValue<Vector2>();
        public Vector2 Look
        {
            get
            {
                if (Suspended) return Vector2.zero;
                var pad = padLook.ReadValue<Vector2>() * 12f;
                return (AlwaysLook || orbit.IsPressed() ? look.ReadValue<Vector2>() : Vector2.zero) + pad;
            }
        }
        public float Zoom => Suspended ? 0 : zoom.ReadValue<Vector2>().y / 120f;
        /// E: use objects, sit, fish. Older districts also accept F so either key starts a conversation there.
        public bool Interact => !Suspended && interact.WasPressedThisFrame();
        /// F: talk to the nearest NPC.
        public bool Talk => !Suspended && talk.WasPressedThisFrame();
        public bool Cancel => cancel.WasPressedThisFrame();
        /// Shift: sprint.
        public bool Run => !Suspended && run.IsPressed();
        public bool Sprint => Run;
        /// Ctrl held, or Caps Lock toggled: walk instead of the default jog.
        public bool Walk => !Suspended && (walk.IsPressed() || walkToggle);
        public bool Jump => !Suspended && jump.WasPressedThisFrame();
        public bool Menu => menu.WasPressedThisFrame();
        public bool Crouch => !Suspended && crouch.WasPressedThisFrame();
        public bool Inventory => inventory.WasPressedThisFrame();
        public bool Journal => journal.WasPressedThisFrame();
        /// Left mouse: reel in while fishing, use objects. Held state.
        public bool Primary => !Suspended && primary.IsPressed();
        public bool PrimaryPressed => !Suspended && primary.WasPressedThisFrame();

        private void Awake()
        {
            actions = new InputActionMap("World");
            move = actions.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            look = actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            padLook = actions.AddAction("PadLook", InputActionType.Value, "<Gamepad>/rightStick");
            orbit = actions.AddAction("Orbit", InputActionType.Button, "<Mouse>/rightButton");
            zoom = actions.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
            interact = actions.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonWest");
            talk = actions.AddAction("Talk", InputActionType.Button, "<Keyboard>/f");
            talk.AddBinding("<Gamepad>/buttonNorth");
            cancel = actions.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/start");
            run = actions.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            run.AddBinding("<Keyboard>/rightShift");
            run.AddBinding("<Gamepad>/leftStickPress");
            walk = actions.AddAction("Walk", InputActionType.Button, "<Keyboard>/leftCtrl");
            jump = actions.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");
            menu = actions.AddAction("Map", InputActionType.Button, "<Keyboard>/m");
            menu.AddBinding("<Gamepad>/select");
            crouch = actions.AddAction("Crouch", InputActionType.Button, "<Keyboard>/c");
            crouch.AddBinding("<Gamepad>/buttonEast");
            inventory = actions.AddAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
            journal = actions.AddAction("Journal", InputActionType.Button, "<Keyboard>/j");
            primary = actions.AddAction("Primary", InputActionType.Button, "<Mouse>/leftButton");
            primary.AddBinding("<Gamepad>/rightTrigger");
            var capsLock = actions.AddAction("WalkToggle", InputActionType.Button, "<Keyboard>/capsLock");
            capsLock.performed += _ => walkToggle = !walkToggle;
        }
        private void OnEnable() { actions?.Enable(); }
        private void OnDisable() { actions?.Disable(); }
        private void OnDestroy() { actions?.Dispose(); }
        public void Clear()
        {
            // A key released inside a browser text field must not keep the avatar walking.
            if (Keyboard.current != null) InputSystem.ResetDevice(Keyboard.current);
        }
    }
}
