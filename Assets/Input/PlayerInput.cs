using UnityEngine;
using UnityEngine.InputSystem;
using NatureCalls.Player;
using NatureCalls.Facility;

namespace NatureCalls.Input
{
    [RequireComponent(typeof(Movement), typeof(Posture))]
    public sealed class PlayerInput : MonoBehaviour
    {
        private InputAction move, look, jump, toggle, interact, throwRoll, release;
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
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            toggle = new InputAction("Posture", InputActionType.Button, "<Keyboard>/c");
            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            throwRoll = new InputAction("Throw Paper Roll", InputActionType.Button, "<Keyboard>/r");
            release = new InputAction("Release", InputActionType.Button, "<Keyboard>/escape");
        }
        private void OnEnable() { move?.Enable(); look?.Enable(); jump?.Enable(); toggle?.Enable(); interact?.Enable(); throwRoll?.Enable(); release?.Enable(); }
        private void OnDisable()
        {
            move?.Disable(); look?.Disable(); jump?.Disable(); toggle?.Disable(); interact?.Disable(); throwRoll?.Disable(); release?.Disable();
            Unlock();
        }
        private void OnDestroy() { move?.Dispose(); look?.Dispose(); jump?.Dispose(); toggle?.Dispose(); interact?.Dispose(); throwRoll?.Dispose(); release?.Dispose(); }
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
                if (jump.WasPressedThisFrame()) movement.Jump();
                if (toggle.WasPressedThisFrame()) posture.Toggle();
                if (interact.WasPressedThisFrame())
                {
                    if (!TryPickupNearestPaperRoll() && !TryInteractNearestStallDoor())
                        foreach (var toilet in FindObjectsByType<Toilet>(FindObjectsSortMode.None))
                            if (toilet.Enter(movement)) break;
                }
                if (throwRoll.WasPressedThisFrame()) TryThrowCarriedPaperRoll();
            }
            movement.Move(accept ? move.ReadValue<Vector2>() : Vector2.zero, Time.deltaTime);
        }

        private bool TryPickupNearestPaperRoll()
        {
            PaperRoll nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var roll in FindObjectsByType<PaperRoll>(FindObjectsSortMode.None))
            {
                if (!roll.CanPickup(movement)) continue;
                var distance = Vector3.SqrMagnitude(roll.transform.position - movement.transform.position);
                if (distance >= nearestDistance) continue;
                nearest = roll;
                nearestDistance = distance;
            }
            return nearest != null && nearest.Pickup(movement);
        }

        private bool TryInteractNearestStallDoor()
        {
            StallDoor nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var door in FindObjectsByType<StallDoor>(FindObjectsSortMode.None))
            {
                if (!door.CanInteract(movement)) continue;
                var distance = Vector3.SqrMagnitude(door.DoorColliderPivot.position - movement.transform.position);
                if (distance >= nearestDistance) continue;
                nearest = door;
                nearestDistance = distance;
            }
            return nearest != null && nearest.Interact(movement);
        }

        private bool TryThrowCarriedPaperRoll()
        {
            var carried = PaperRoll.FindCarriedBy(movement);
            return carried != null && carried.Throw(movement, movement.transform.forward);
        }
    }
}
