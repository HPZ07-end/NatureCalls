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
