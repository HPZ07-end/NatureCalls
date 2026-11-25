using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    public sealed class Sink : InteractableComponent, IWaterFixture
    {
        [SerializeField] private Pipe pipe;
        public Pipe ConnectedPipe => pipe;
        public override float Distance => Interaction.ReachDistance;
        public override bool CanInteract(InteractionContext context) => Interaction.InReach(context, transform) && context.Player != null && context.Player.GetComponent<PlayerNeeds>()?.HandsDirty == true;
        public override string Prompt(InteractionContext context) => "E  洗手";
        public override bool Perform(InteractionContext context)
        {
            if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
            context.Player.GetComponent<PlayerNeeds>()?.WashHands(); return true;
        }
        public void Backflow() => FindFirstObjectByType<Map.Playground>()?.AddMess(Core.MessKind.Backflow, transform.position);
        // 接水：需要手持水桶，水桶由清洁装置实现。暂留接口位置。
        public bool TryFillBucket() => false;
    }
}
