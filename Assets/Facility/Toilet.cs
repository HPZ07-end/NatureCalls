using System.Linq;
using UnityEngine;
using NatureCalls.Player;

namespace NatureCalls.Facility
{
    /// <summary>
    /// A toilet a player can sit on. One player at a time: the seat is occupied until that player stands up.
    /// Which toilet a player uses is decided by the stall they stand in (<see cref="StallDoor.Containing"/>).
    /// </summary>
    public sealed class Toilet : InteractableComponent, ISharedFacilityState, IWaterFixture
    {
        public Transform Anchor;
        public Transform Interior;
        public Movement Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;
        [SerializeField] private Pipe pipe;
        public Pipe ConnectedPipe => pipe;
        public bool IsBackflow { get; private set; }
        public override float Distance => Interaction.ReachDistance;
        public override bool CanInteract(InteractionContext context) => Interaction.InReach(context, transform) && (!IsOccupied || Occupant == context.Player);
        public override string Prompt(InteractionContext context) => "E  冲水";
        public override bool Perform(InteractionContext context)
        {
            if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
            if (pipe != null && pipe.IsBlocked) pipe.TriggerBackflow(this); return true;
        }
        public void Backflow() { IsBackflow = true; var world = FindFirstObjectByType<Map.Playground>(); world?.AddMess(Core.MessKind.Backflow, TargetCenter, System.Array.IndexOf(FindObjectsByType<Toilet>(FindObjectsSortMode.InstanceID), this)); }
        public void ClearBackflow() => IsBackflow = false;
        public int SharedEntryCount => 1;
        public SharedFacilityValue ReadShared(int index)
        {
            var owner = Occupant != null ? Occupant.GetComponentsInParent<MonoBehaviour>(true).OfType<IClientOwner>().FirstOrDefault() : null;
            return new SharedFacilityValue(0, owner != null ? owner.ClientId + 1 : 0);
        }
        public void ApplyShared(int index, SharedFacilityValue value) => Occupant = FindMovement(value.Owner);
        public void ReleaseOwner(ulong clientId) { if (OwnerId() == clientId + 1) Occupant = null; }

        public bool CanSeat(Movement player) => Anchor != null && player != null && (Occupant == null || Occupant == player);

        public Vector3 TargetCenter => Interior != null ? Interior.position : Anchor.position;
        public float TargetRadius => Core.NeedsRules.TargetRadius;

        public bool Seat(Movement player)
        {
            if (!CanSeat(player)) return false;
            Occupant = player;
            player.Enter(Anchor);
            return true;
        }

        public void Release(Movement player)
        {
            if (Occupant == player) Occupant = null;
        }

        /// <summary>The host decides who sits here; peers only record it.</summary>
        public void ApplySharedOccupant(Movement player) => Occupant = player;

        private ulong OwnerId() => ReadShared(0).Owner;
        private static Movement FindMovement(ulong owner)
        {
            if (owner == 0) return null;
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
            {
                var identity = player.GetComponentsInParent<MonoBehaviour>(true).OfType<IClientOwner>().FirstOrDefault();
                if (identity != null && identity.ClientId + 1 == owner) return player;
            }
            return null;
        }

        public static Toilet SeatedOn(Movement player)
        {
            if (player == null || player.Seat == null) return null;
            return player.Seat.GetComponentInParent<Toilet>();
        }
    }
}
