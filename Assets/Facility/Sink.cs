using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    public sealed class Sink : InteractableComponent, IWaterFixture, IInteractableProvider
    {
        [SerializeField] private Pipe pipe;
        public Pipe ConnectedPipe => pipe;
        public void SetPipe(Pipe value) => pipe = value;
        public bool IsBroken { get; set; }
        private static readonly System.Collections.Generic.HashSet<Movement> water = new();
        public bool HasWater(Movement player) => water.Contains(player);
        public override float Distance => Interaction.ReachDistance;
        public System.Collections.Generic.IEnumerable<IInteractable> Interactables { get { yield return this; } }
        public override bool CanInteract(InteractionContext context) => Interaction.InReach(context, transform) && context.Player != null && !IsBroken &&
            (RoleRegistry.IsSaboteur(context.Player) || context.Player.GetComponent<PlayerNeeds>()?.HandsDirty == true);
        public override string Prompt(InteractionContext context) => RoleRegistry.IsSaboteur(context.Player) ? (HasWater(context.Player) ? "E  洒水" : "E  接水") : "E  洗手";
        public override bool Perform(InteractionContext context)
        {
            if (RoleRegistry.IsSaboteur(context.Player))
            {
                if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
                if (!Cooldown.Claim(context.Player, this, 15f)) return false;
                if (HasWater(context.Player)) { water.Remove(context.Player); MessSinkRegistry.Current?.AddMess(Core.MessKind.Puddle, context.Player.transform.position + context.Player.transform.forward * 1.5f, -1, context.Player); }
                else water.Add(context.Player);
                return true;
            }
            if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
            context.Player.GetComponent<PlayerNeeds>()?.WashHands(); return true;
        }
        public void Backflow() => MessSinkRegistry.Current?.AddMess(Core.MessKind.Backflow, transform.position);
        // 接水：需要手持水桶，水桶由清洁装置实现。暂留接口位置。
        public bool TryFillBucket() => false;
    }
}
