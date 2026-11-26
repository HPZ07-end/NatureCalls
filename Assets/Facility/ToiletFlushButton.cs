using UnityEngine;
using NatureCalls.Player;

namespace NatureCalls.Facility
{
    /// <summary>Small, ray-addressable flush control. The toilet body is not a flush target.</summary>
    public sealed class ToiletFlushButton : InteractableComponent
    {
        [SerializeField] private Toilet toilet;
        public Toilet Toilet => toilet != null ? toilet : GetComponentInParent<Toilet>();
        public override int Priority => 110;
        public override float Distance => 1.5f;
        public override bool CanInteract(InteractionContext context) => Toilet != null && Interaction.InReach(context, transform) &&
            !Toilet.IsBroken && (!Toilet.IsOccupied || Toilet.Occupant == context.Player);
        public override string Prompt(InteractionContext context) => "E  冲水";
        public override bool Perform(InteractionContext context)
        {
            if (Toilet == null) return false;
            if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
            if (Toilet.ConnectedPipe != null && Toilet.ConnectedPipe.IsBlocked) Toilet.Backflow();
            return true;
        }
        public void SetToilet(Toilet value) => toilet = value;
    }
}
