using NatureCalls.Player;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Facility
{
    public sealed class Toolbox : InteractableComponent
    {
        public Mop Mop;
        private void Start() { if (Mop != null && !Mop.IsPickedUp) Mop.Store(transform.position); }
        public bool CanInteract(Movement player)
        {
            if (player == null || Mop == null || Vector3.Distance(player.transform.position, transform.position) > NeedsRules.InteractionDistance) return false;
            var carried = Item.FindCarriedBy(player);
            return carried == Mop || (carried == null && Mop.Stored);
        }
        public override float Distance => NeedsRules.InteractionDistance;
        public override bool CanInteract(InteractionContext context) => CanInteract(context.Player);
        public override string Prompt(InteractionContext context) => "E  取放拖把";
        public override bool Perform(InteractionContext context) => context.Online ? context.Sync != null && context.Sync.RequestToolbox(this) : Interact(context.Player);

        public bool Interact(Movement player)
        {
            if (!CanInteract(player) || Mop == null) return false;
            var carried = Item.FindCarriedBy(player);
            if (carried == Mop) { Mop.Store(transform.position); return true; }
            if (carried != null || !Mop.Stored) return false;
            Mop.Take(player);
            return true;
        }
    }
}
