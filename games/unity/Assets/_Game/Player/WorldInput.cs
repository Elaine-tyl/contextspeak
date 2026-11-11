using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Cafe
{
    // One action map is shared by walking and nearby-NPC interaction.
    public sealed class WorldInput : MonoBehaviour
    {
        private InputActionMap actions;
        private InputAction move, look, orbit, zoom, interact, cancel;
        public Vector2 Movement => move.ReadValue<Vector2>();
        public Vector2 Look => orbit.IsPressed() ? look.ReadValue<Vector2>() : Vector2.zero;
        public float Zoom => zoom.ReadValue<Vector2>().y / 120f;
        public bool Interact => interact.WasPressedThisFrame();
        public bool Cancel => cancel.WasPressedThisFrame();
        private void Awake()
        {
            actions = new InputActionMap("World");
            move = actions.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            look = actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            orbit = actions.AddAction("Orbit", InputActionType.Button, "<Mouse>/rightButton");
            zoom = actions.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
            interact = actions.AddAction("Talk", InputActionType.Button, "<Keyboard>/e");
            cancel = actions.AddAction("Explore", InputActionType.Button, "<Keyboard>/escape");
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
