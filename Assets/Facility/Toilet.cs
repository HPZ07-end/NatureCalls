using System.Linq;
using UnityEngine;
using NatureCalls.Player;

namespace NatureCalls.Facility
{
    /// <summary>
    /// A toilet a player can sit on. One player at a time: the seat is occupied until that player stands up.
    /// Which toilet a player uses is decided by the stall they stand in (<see cref="StallDoor.Containing"/>).
    /// </summary>
    public sealed class Toilet : InteractableComponent, ISharedFacilityState, IWaterFixture, IInteractableProvider, IHoldInteractable
    {
        public Transform Anchor;
        public Transform Interior;
        public Movement Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;
        [SerializeField] private Pipe pipe;
        [SerializeField] private Transform flushTarget;
        public Pipe ConnectedPipe => pipe;
        public void SetPipe(Pipe value) => pipe = value;
        public void SetFlushTarget(Transform value) => flushTarget = value;
        public bool IsBackflow { get; private set; }
        /// <summary>Versus: broken by the saboteur until repaired (owned by the saboteur-actions work).</summary>
        public bool IsBroken { get; set; }
        /// <summary>Versus: a toilet a player losing control can run to.</summary>
        public bool IsAvailable => !IsOccupied && !IsBackflow && !IsBroken;
        public float HoldSeconds => RoleRegistry.IsSaboteur(Occupant) ? 0f : 2f;
        public System.Collections.Generic.IEnumerable<IInteractable> Interactables { get { yield return this; } }
        public override int Priority => 100;
        public override float Distance => Interaction.ReachDistance;
        public override Transform Target => flushTarget != null ? flushTarget : transform;
        public override Vector3 AimPoint => flushTarget != null ? flushTarget.position : transform.position + transform.up * .55f - transform.forward * .12f; // fallback: above and behind the bowl
        public override bool CanInteract(InteractionContext context) => Interaction.InReach(context, Target) &&
            ((Item.FindCarriedBy(context.Player) is Plunger && IsBackflow && ConnectedPipe != null && ConnectedPipe.IsBlocked) ||
             (RoleRegistry.IsSaboteur(context.Player) && Item.FindCarriedBy(context.Player) is PaperRoll && !IsBroken && Cooldown.Ready(context.Player, this)) ||
             ((!IsBroken && (!IsOccupied || Occupant == context.Player) && context.Player != Occupant && context.Player.Seat != Anchor)));
        public override string Prompt(InteractionContext context) => RoleRegistry.IsSaboteur(context.Player) && Item.FindCarriedBy(context.Player) is PaperRoll ? "E  塞纸" : "E  冲水";
        public override bool Perform(InteractionContext context)
        {
            if (RoleRegistry.IsSaboteur(context.Player) && Item.FindCarriedBy(context.Player) is PaperRoll paper)
            {
                if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
                if (!Cooldown.Claim(context.Player, this, 20f) || pipe == null) return false;
                pipe.AddSabotagePaper(); paper.gameObject.SetActive(false); return true;
            }
            if (Item.FindCarriedBy(context.Player) is Plunger && pipe != null && pipe.IsBlocked)
            { if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this); return pipe.ClearAndReport(); }
            if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
            if (pipe != null && pipe.IsBlocked) pipe.TriggerBackflow(this); return true;
        }
        public void Backflow() { IsBackflow = true; var sink = MessSinkRegistry.Current; sink?.AddMess(Core.MessKind.Backflow, TargetCenter, sink.ToiletIndex(this)); }
        public void ClearBackflow() => IsBackflow = false;
        public void SetBackflowShared(bool value) => IsBackflow = value;
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
