using System.Linq;
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
        public override int Priority => 50;
        protected override void OnEnable() { base.OnEnable(); }
        private void Start()
        {
            if (Wrench == null) Wrench = FindObjectsByType<Wrench>(FindObjectsSortMode.None).OrderBy(item => Vector3.Distance(item.transform.position, transform.position)).FirstOrDefault();
            if (Plunger == null) Plunger = FindObjectsByType<Plunger>(FindObjectsSortMode.None).OrderBy(item => Vector3.Distance(item.transform.position, transform.position)).FirstOrDefault();
            if (Mop != null && !Mop.IsPickedUp) Mop.Store(transform.position);
            if (Wrench != null && !Wrench.IsPickedUp) Wrench.Store(transform.position);
            if (Plunger != null && !Plunger.IsPickedUp) Plunger.Store(transform.position);
        }
        public bool CanInteract(Movement player)
        {
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > NeedsRules.InteractionDistance) return false;
            var carried = Item.FindCarriedBy(player);
            if (Wrench == null && Plunger == null) return Mop != null && (carried == Mop || (carried == null && Mop.Stored));
            if (carried == null && FindObjectsByType<Item>(FindObjectsSortMode.None).Any(item => !item.Stored && item.CanPickup(player) &&
                Vector3.Distance(item.transform.position, player.transform.position) < .4f)) return false;
            return carried == Mop || carried == Wrench || carried == Plunger || (carried == null && ((Mop != null && Mop.Stored) || (Wrench != null && Wrench.Stored) || (Plunger != null && Plunger.Stored)));
        }
        public override float Distance => NeedsRules.InteractionDistance;
        public override bool CanInteract(InteractionContext context) => CanInteract(context.Player);
        public bool IsTargetedBy(Movement player)
        {
            if (player == null) return false;
            var ray = player.ViewRay;
            var offset = AimPoint - ray.origin;
            if (Vector3.Dot(ray.direction, offset.normalized) >= .965f && offset.magnitude <= Distance + Interaction.ReachHeightBand)
                return true;
            return Physics.RaycastAll(ray, Distance + Interaction.ReachHeightBand, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Any(hit => hit.transform == transform || hit.transform.IsChildOf(transform));
        }
        public override string Prompt(InteractionContext context)
        {
            var carried = Item.FindCarriedBy(context.Player);
            if (carried != null) return carried == Mop ? "E  放回拖把" : "E  放回工具";
            Item next = Mop != null && Mop.Stored ? Mop : Wrench != null && Wrench.Stored ? Wrench : Plunger != null && Plunger.Stored ? Plunger : null;
            return next is Mop ? "E  拿取拖把" : next is Wrench ? "E  拿取扳手" : next is Plunger ? "E  拿取疏通器" : "工具箱为空";
        }
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
