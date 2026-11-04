using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    public enum StallDoorState
    {
        Open,
        Closed,
        Locked
    }

    [DisallowMultipleComponent]
    public sealed class StallDoor : MonoBehaviour
    {
        [Min(.1f)] public float InteractionDistance = 1.5f;
        [Range(70, 120)] public float OpenAngle = 95f;
        public Transform DoorLeaf;
        public Transform DoorColliderPivot;
        [SerializeField] private StallDoorState initialState = StallDoorState.Closed;
        public StallDoorState State { get; private set; }

        private Quaternion leafClosedRotation;
        private Quaternion colliderClosedRotation;
        // The stall's footprint in its own space, measured with the door closed.
        private Bounds footprint;

        /// <summary>The stall's toilet, or null.</summary>
        public Toilet Toilet { get; private set; }

        private void Awake()
        {
            if (DoorLeaf == null || DoorColliderPivot == null)
            {
                enabled = false;
                Debug.LogError($"STALL_DOOR_INVALID|stall={name}|leaf={DoorLeaf != null}|colliderPivot={DoorColliderPivot != null}");
                return;
            }
            leafClosedRotation = DoorLeaf.localRotation;
            colliderClosedRotation = DoorColliderPivot.localRotation;
            Toilet = GetComponentInChildren<Toilet>(true);
            footprint = LocalFootprint();
            SetState(initialState);
        }

        public bool CanInteract(Movement player) => enabled && player != null &&
            Vector3.Distance(player.transform.position, DoorColliderPivot.position) <= InteractionDistance;

        public bool IsInside(Movement player)
        {
            if (player == null || DoorLeaf == null) return false;
            var playerLocal = transform.InverseTransformPoint(player.transform.position);
            var doorLocal = transform.InverseTransformPoint(DoorLeaf.position);
            return playerLocal.z > doorLocal.z + .05f;
        }

        /// <summary>
        /// Whether the player stands inside this stall: behind the door and within the stall's walls.
        /// Unlike distance, this cannot pick a neighbouring stall.
        /// </summary>
        public bool Contains(Movement player)
        {
            if (!enabled || player == null || !IsInside(player)) return false;
            var local = transform.InverseTransformPoint(player.transform.position);
            return local.x >= footprint.min.x && local.x <= footprint.max.x && local.z >= footprint.min.z && local.z <= footprint.max.z;
        }

        /// <summary>The stall that contains the player, or null.</summary>
        public static StallDoor Containing(Movement player)
        {
            foreach (var door in FindObjectsByType<StallDoor>(FindObjectsSortMode.None))
                if (door.Contains(player)) return door;
            return null;
        }

        private Bounds LocalFootprint()
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = transform.InverseTransformPoint(new Vector3(
                        (corner & 1) == 0 ? world.min.x : world.max.x,
                        (corner & 2) == 0 ? world.min.y : world.max.y,
                        (corner & 4) == 0 ? world.min.z : world.max.z));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        public bool Interact(Movement player)
        {
            if (!CanInteract(player)) return false;
            var inside = IsInside(player);
            switch (State)
            {
                case StallDoorState.Open:
                    SetState(StallDoorState.Closed);
                    break;
                case StallDoorState.Closed:
                    SetState(inside ? StallDoorState.Locked : StallDoorState.Open);
                    break;
                case StallDoorState.Locked:
                    if (inside) SetState(StallDoorState.Open);
#if UNITY_EDITOR
                    else Debug.Log($"STALL_DOOR_LOCK_BLOCKED|stall={name}|player={player.name}|inside=False");
#endif
                    break;
            }
#if UNITY_EDITOR
            Debug.Log($"STALL_DOOR_INTERACT_OK|stall={name}|player={player.name}|inside={inside}|state={State}");
#endif
            return true;
        }

        // The host validates Interact; peers only apply its resulting state.
        public void ApplySharedState(StallDoorState value)
        {
            if (enabled && State != value) SetState(value);
        }

        private void SetState(StallDoorState value)
        {
            State = value;
            var offset = value == StallDoorState.Open ? Quaternion.Euler(0, OpenAngle, 0) : Quaternion.identity;
            DoorLeaf.localRotation = leafClosedRotation * offset;
            DoorColliderPivot.localRotation = colliderClosedRotation * offset;
            Physics.SyncTransforms();
        }
    }
}
