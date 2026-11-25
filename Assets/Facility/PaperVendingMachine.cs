using System;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    /// <summary>Generic coin-operated paper dispenser. Each instance owns its own coins and stock.</summary>
    public sealed class PaperVendingMachine : MonoBehaviour, ISharedFacilityState, IInteractableProvider
    {
        public Transform VendingMachine;
        public PaperRoll[] VendingStock;
        public Transform[] Coins;
        private const ulong Spent = ulong.MaxValue;
        private ulong[] coinOwners;
        private readonly List<IInteractable> interactables = new();
        public IEnumerable<IInteractable> Interactables => interactables;
        private const int Vending = 0, FirstCoin = 1;

        private sealed class Station : IInteractable
        {
            private readonly PaperVendingMachine owner; private readonly Transform target; private readonly int action;
            public Station(PaperVendingMachine owner, Transform target, int action) { this.owner = owner; this.target = target; this.action = action; }
            public int Priority => action; public Transform Target => target; public float Distance => Interaction.ReachDistance;
            public Vector3 AimPoint => target != null ? target.position : Vector3.zero;
            public bool CanInteract(InteractionContext context) => Interaction.InReach(context, target) && owner.CanUse(action, context.Player, context.ClientId);
            public string Prompt(InteractionContext context) => owner.Prompt(action, context.ClientId, context.Player);
            public bool Perform(InteractionContext context) => context.Online ? context.Requests != null && context.Requests.RequestInteract(this) : owner.Interact(action, context.Player, context.ClientId);
        }

        public int SharedEntryCount => Coins?.Length ?? 0;
        public SharedFacilityValue ReadShared(int index) => new SharedFacilityValue(0, coinOwners[index]);
        public void ApplyShared(int index, SharedFacilityValue value) { if (index >= 0 && index < coinOwners.Length) ApplyCoin(index, value.Owner); }
        public void ReleaseOwner(ulong clientId)
        { for (int i = 0; i < coinOwners.Length; i++) if (coinOwners[i] == clientId + 1) ApplyCoin(i, 0); }

        private void Awake()
        {
            Coins ??= Array.Empty<Transform>(); VendingStock ??= Array.Empty<PaperRoll>();
            coinOwners = new ulong[Coins.Length];
            foreach (var roll in VendingStock) if (roll != null && VendingMachine != null) roll.Store(VendingMachine.position);
            interactables.Clear();
            interactables.Add(new Station(this, VendingMachine, Vending));
            for (int i = 0; i < Coins.Length; i++) interactables.Add(new Station(this, Coins[i], FirstCoin + i));
        }
        private void OnEnable() { foreach (var value in interactables) InteractionRegistry.Register(value); }
        private void OnDisable() { foreach (var value in interactables) InteractionRegistry.Unregister(value); }

        public int Wallet(ulong player) => coinOwners.Count(owner => owner == player + 1);
        public int AvailableCoins => coinOwners.Count(owner => owner == 0);
        public int LastRestockCount { get; private set; }
        public void RestockCoins(int count) { LastRestockCount = Math.Min(Math.Max(0, count), coinOwners.Length); for (int i = 0; i < coinOwners.Length && count > 0; i++) if (coinOwners[i] != 0) { ApplyCoin(i, 0); count--; } }
        public void InitializeForTests(Transform[] coins) { Coins = coins ?? Array.Empty<Transform>(); Awake(); }
        public string Prompt(int action, ulong player, Movement movement = null)
        {
            if (action >= FirstCoin) return "E  拾取金币";
            if (Wallet(player) == 0) return "售纸机 · 需要 1 枚金币";
            if (movement != null && Item.CountCarriedBy(movement) >= Item.CarryCapacityPerPlayer) return "售纸机 · 需空手取卷纸";
            return $"E  用 1 枚金币买卷纸（持有 {Wallet(player)} 枚）";
        }
        public bool CanUse(int action, Movement player, ulong clientId)
        {
            if (action >= FirstCoin) return action - FirstCoin < coinOwners.Length && coinOwners[action - FirstCoin] == 0;
            int coin = Array.IndexOf(coinOwners, clientId + 1);
            return coin >= 0 && coin < VendingStock.Length && VendingStock[coin] != null && VendingStock[coin].Stored &&
                   player != null && Item.CountCarriedBy(player) < Item.CarryCapacityPerPlayer;
        }
        public bool Interact(int action, Movement player, ulong clientId)
        {
            if (player == null || !Interaction.InReach(player, action >= FirstCoin ? Coins[action - FirstCoin] : VendingMachine) || !CanUse(action, player, clientId)) return false;
            if (action >= FirstCoin) { ApplyCoin(action - FirstCoin, clientId + 1); return true; }
            int coin = Array.IndexOf(coinOwners, clientId + 1);
            VendingStock[coin].Take(player); ApplyCoin(coin, Spent); return true;
        }
        private void ApplyCoin(int index, ulong owner)
        {
            if (index < 0 || index >= coinOwners.Length) return;
            coinOwners[index] = owner;
            Coins[index].gameObject.SetActive(owner == 0);
        }
    }
}
