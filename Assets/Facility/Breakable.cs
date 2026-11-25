using System;
using System.Collections.Generic;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    /// <summary>Saboteur damage state for a shared water fixture or valve.</summary>
    public sealed class Breakable : InteractableComponent, ISharedFacilityState, IDirtSource, IInteractableProvider, IHoldInteractable
    {
        [SerializeField] private MonoBehaviour target;
        public MonoBehaviour TargetComponent { get => target; set => target = value; }
        public bool IsBroken { get; private set; }
        public float HoldSeconds => 2f; // 暂定
        public float DirtWeight => IsBroken ? 1f : 0f;
        public Vector3 DirtPosition => transform.position;
        public int SharedEntryCount => 1;
        public IEnumerable<IInteractable> Interactables { get { yield return this; } }
        public override float Distance => Interaction.ReachDistance;
        public override bool CanInteract(InteractionContext context) =>
            Interaction.InReach(context, transform) && ((RoleRegistry.IsSaboteur(context.Player) && !IsBroken && Cooldown.Ready(context.Player, this)) || (Item.FindCarriedBy(context.Player) is Wrench && IsBroken));
        public override string Prompt(InteractionContext context) => IsBroken ? "E  修理" : "E  长按弄坏";
        public override bool Perform(InteractionContext context)
        {
            if (Item.FindCarriedBy(context.Player) is Wrench && IsBroken) { if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this); return Repair(context.Player); }
            if (!CanInteract(context)) return false;
            if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
            Break(context.Player); return true;
        }
        public void Break(Movement player)
        {
            if (IsBroken || !Cooldown.Claim(player, this, 30f)) return;
            IsBroken = true;
            if (target is Toilet toilet) toilet.IsBroken = true;
            if (target is Sink sink) sink.IsBroken = true;
            DirtField.Register(this);
        }
        public bool Repair(Movement player)
        {
            if (!IsBroken || player == null) return false;
            IsBroken = false;
            if (target is Toilet toilet) toilet.IsBroken = false;
            if (target is Sink sink) sink.IsBroken = false;
            return true;
        }
        public SharedFacilityValue ReadShared(int index) => new SharedFacilityValue((ushort)(IsBroken ? 1 : 0));
        public void ApplyShared(int index, SharedFacilityValue value) { IsBroken = value.State != 0; if (target is Toilet t) t.IsBroken = IsBroken; if (target is Sink s) s.IsBroken = IsBroken; }
        public void ReleaseOwner(ulong clientId) { }
        protected override void OnEnable() { base.OnEnable(); DirtField.Register(this); }
        protected override void OnDisable() { DirtField.Unregister(this); base.OnDisable(); }
    }

    internal static class Cooldown
    {
        private static readonly Dictionary<(Movement, object), float> until = new();
        public static bool Ready(Movement p, object key) => p != null && (!until.TryGetValue((p, key), out var t) || Time.time >= t);
        public static bool Claim(Movement p, object key, float seconds) { if (!Ready(p, key)) return false; until[(p, key)] = Time.time + seconds; return true; }
    }
}
