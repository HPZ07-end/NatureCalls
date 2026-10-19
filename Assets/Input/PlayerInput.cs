using UnityEngine;
using UnityEngine.InputSystem;
using NatureCalls.Player;
using NatureCalls.Facility;

namespace NatureCalls.Input
{
    [RequireComponent(typeof(Movement), typeof(Posture))]
    public sealed class PlayerInput : MonoBehaviour
    {
        private InputAction move, look, toggle, interact, release;
        private Movement movement;
        private Posture posture;
        public bool CaptureMouse = true;
        public bool Captured => Cursor.lockState == CursorLockMode.Locked;
        private void Awake()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            toggle = new InputAction("Posture", InputActionType.Button, "<Keyboard>/c");
            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            release = new InputAction("Release", InputActionType.Button, "<Keyboard>/escape");
        }
        private void OnEnable() { move?.Enable(); look?.Enable(); toggle?.Enable(); interact?.Enable(); release?.Enable(); }
        private void OnDisable()
        {
            move?.Disable(); look?.Disable(); toggle?.Disable(); interact?.Disable(); release?.Disable();
            Unlock();
        }
        private void OnDestroy() { move?.Dispose(); look?.Dispose(); toggle?.Dispose(); interact?.Dispose(); release?.Dispose(); }
        public static void Unlock() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnApplicationFocus(bool focused) { if (!focused) Unlock(); }
        private void Update()
        {
            if (!movement.Controlled) return;
            if (release.WasPressedThisFrame()) Unlock();
            if (CaptureMouse && Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                Mouse.current.position.ReadValue().x > 360)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool accept = Application.isFocused && (Captured || !CaptureMouse);
            if (accept)
            {
                movement.Look(look.ReadValue<Vector2>());
                if (toggle.WasPressedThisFrame()) posture.Toggle();
                if (interact.WasPressedThisFrame())
                {
                    foreach (var toilet in FindObjectsByType<Toilet>(FindObjectsSortMode.None))
                        if (toilet.Enter(movement)) break;
                }
            }
            movement.Move(accept ? move.ReadValue<Vector2>() : Vector2.zero, Time.deltaTime);
        }
    }
}
