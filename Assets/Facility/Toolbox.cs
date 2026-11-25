using NatureCalls.Player;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Facility
{
    public sealed class Toolbox : InteractableComponent
    {
        public Mop Mop;
        public Wrench Wrench;
        public Plunger Plunger;
        private void Start() { if (Mop != null && !Mop.IsPickedUp) Mop.Store(transform.position); if (Wrench != null && !Wrench.IsPickedUp) Wrench.Store(transform.position); if (Plunger != null && !Plunger.IsPickedUp) Plunger.Store(transform.position); }
        public bool CanInteract(Movement player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > NeedsRules.InteractionDistance) return false;
            var carried = Item.FindCarriedBy(player);
            if (Wrench == null && Plunger == null) return Mop != null && (carried == Mop || (carried == null && Mop.Stored));
            return carried == Mop || carried == Wrench || carried == Plunger || (carried == null && ((Mop != null && Mop.Stored) || (Wrench != null && Wrench.Stored) || (Plunger != null && Plunger.Stored)));
        }
        public override float Distance => NeedsRules.InteractionDistance;
        public override bool CanInteract(InteractionContext context) => CanInteract(context.Player);
        public override string Prompt(InteractionContext context) => "E  取放工具";
        public override bool Perform(InteractionContext context) => context.Online ? context.Requests != null && context.Requests.RequestToolbox(this) : Interact(context.Player);

        public bool Interact(Movement player)
        {
            if (!CanInteract(player)) return false;
            var carried = Item.FindCarriedBy(player);
            if (carried != null) { carried.Store(transform.position); return true; }
            Item next = Mop != null && Mop.Stored ? Mop : Wrench != null && Wrench.Stored ? Wrench : Plunger != null && Plunger.Stored ? Plunger : null;
            if (next == null) return false;
            next.Take(player);
            return true;
        }
    }
}
