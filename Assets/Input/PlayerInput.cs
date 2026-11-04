using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using NatureCalls.Core;
using NatureCalls.Player;
using NatureCalls.Facility;
using NatureCalls.Map;
using Avatar = NatureCalls.Network.Avatar;

namespace NatureCalls.Input
{
    [RequireComponent(typeof(Movement), typeof(Posture))]
    public sealed class PlayerInput : MonoBehaviour
    {
        private InputAction move, look, jump, toggle, interact, throwRoll, release;
        private Movement movement;
        private Posture posture;
        private Avatar avatar;
        private PlayerNeeds needs;
        // A press of C toilets for a fixed time. Pending until the squat begins (online the host grants the seat).
        private float toiletingRequested = -1;
        private float toiletingEnds = -1;
        public bool IsToileting => toiletingEnds >= 0;
        public bool CaptureMouse = true;
        public bool Captured => Cursor.lockState == CursorLockMode.Locked;
        private void Awake()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            avatar = GetComponent<Avatar>();
            needs = GetComponent<PlayerNeeds>();
            posture.Changed += PostureChanged;
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
        private void PostureChanged(PostureState state)
        {
            if (state == PostureState.Squatting && toiletingRequested >= 0)
            {
                toiletingRequested = -1;
                toiletingEnds = Time.time + NeedsRules.ToiletingSeconds;
            }
            else if (state == PostureState.Standing) toiletingEnds = -1;
        }
        private void OnDestroy() { if (posture != null) posture.Changed -= PostureChanged; move?.Dispose(); look?.Dispose(); jump?.Dispose(); toggle?.Dispose(); interact?.Dispose(); throwRoll?.Dispose(); release?.Dispose(); }
        public static void Unlock() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnApplicationFocus(bool focused) { if (!focused) Unlock(); }
        private void Update()
        {
            if (toiletingRequested >= 0 && Time.time > toiletingRequested + 2) toiletingRequested = -1; // seat refused
            if (toiletingEnds >= 0 && Time.time >= toiletingEnds) FinishToileting();
            if (!movement.Controlled) return;
            if (release.WasPressedThisFrame()) Unlock();
            if (CaptureMouse && Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !IsPointerOverUi())
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool accept = Application.isFocused && (Captured || !CaptureMouse);
            if (accept)
            {
                movement.Look(look.ReadValue<Vector2>());
                if (jump.WasPressedThisFrame()) movement.Jump();
                if (toggle.WasPressedThisFrame()) TryToilet();
                if (interact.WasPressedThisFrame()) TryInteract();
                if (throwRoll.WasPressedThisFrame()) TryThrowCarriedPaperRoll();
            }
            movement.Move(accept ? move.ReadValue<Vector2>() : Vector2.zero, Time.deltaTime);
        }

        private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>
        /// C: all toileting, one press. Inside a stall the player sits on that stall's toilet (if it is free);
        /// anywhere else the player squats where they stand. After <see cref="NeedsRules.ToiletingSeconds"/> the
        /// player stands up by themselves; C cannot cut it short.
        /// </summary>
        public bool TryToilet()
        {
            if (posture.IsSquatting || toiletingRequested >= 0) return false;
            var stall = StallDoor.Containing(movement);
            toiletingRequested = Time.time;
            bool started;
            if (stall == null || stall.Toilet == null) { posture.Set(PostureState.Squatting); started = true; }
            else started = avatar != null && avatar.IsSpawned ? avatar.RequestSeat(stall.Toilet) : stall.Toilet.Seat(movement);
            if (!started) toiletingRequested = -1;
            Debug.Log($"TOILET_C|player={name}|stall={(stall != null ? stall.name : "none")}|occupied={(stall != null && stall.Toilet != null && stall.Toilet.IsOccupied)}|started={started}");
            return started;
        }

        // Time is up: stand up, then report where the toileting happened.
        private void FinishToileting()
        {
            toiletingEnds = -1;
            bool insideStall = Toilet.SeatedOn(movement) != null;
            var position = movement.transform.position;
            StandUp();
            if (avatar != null && avatar.IsSpawned) { avatar.ReportToileting(insideStall, position); return; }
            if (needs != null) needs.CompleteToileting(insideStall);
            if (!insideStall && needs != null && needs.Ticking) FindFirstObjectByType<Playground>()?.Drop(position);
        }

        private void StandUp()
        {
            var toilet = Toilet.SeatedOn(movement);
            posture.Set(PostureState.Standing);
            if (toilet == null) return;
            if (avatar != null && avatar.IsSpawned) avatar.ReleaseSeat(toilet);
            else toilet.Release(movement);
        }

        /// <summary>E: paper rolls, the mirror, stall doors and the Lobby door. Works while seated, so a seated player can lock the door.</summary>
        public bool TryInteract()
        {
            if (TryPickupNearestPaperRoll()) return true;
            foreach (var mirror in FindObjectsByType<DressingMirror>(FindObjectsSortMode.None))
                if (mirror.Interact(movement)) return true;
            if (TryInteractNearestStallDoor()) return true;
            foreach (var door in FindObjectsByType<LobbyDoor>(FindObjectsSortMode.None))
                if (door.Interact(movement)) return true;
            return false;
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
            return nearest != null && (avatar != null && avatar.IsSpawned
                ? avatar.RequestPickup(nearest) : nearest.Pickup(movement));
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
            return nearest != null && (avatar != null && avatar.IsSpawned
                ? avatar.RequestDoor(nearest) : nearest.Interact(movement));
        }

        private bool TryThrowCarriedPaperRoll()
        {
            var carried = PaperRoll.FindCarriedBy(movement);
            return carried != null && (avatar != null && avatar.IsSpawned
                ? avatar.RequestThrow(carried, movement.transform.forward) : carried.Throw(movement, movement.transform.forward));
        }
    }
}
