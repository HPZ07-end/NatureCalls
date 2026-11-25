using System;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Player;
using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Map
{
    public enum SewerRepairProgress : byte { Blocked, Inspected, Isolated, Cleared, Running, Confirmed }

    /// <summary>The Map2 supply and sewer stations. Mutated by the offline player or the room authority.</summary>
    public sealed class Map2Interactions : MonoBehaviour, IMovementEffectSource, ISharedFacilityState, IInteractableProvider
    {
        public Transform[] Coins;
        public Transform VendingMachine;
        public PaperRoll[] VendingStock;
        public PaperRoll EmergencyPaper;
        public Transform UpstreamWindow;
        public Transform Valve;
        public Transform InspectionPort;
        public Transform DownstreamWindow;
        public Transform Handwheel;
        public Transform InspectionLid;
        public Transform AugerCrank;
        public Transform UpstreamWater;
        public Transform DownstreamWater;
        public BoxCollider[] WetZones;
        private Quaternion handwheelRest, lidRest, crankRest;
        private Vector3 upstreamRest, downstreamRest;
        private ulong[] coinOwners;
        private SewerAccess[] sewerAccesses;
        private const ulong Spent = ulong.MaxValue;
        private const int Vending = 0, Upstream = 1, ValveAction = 2, Inspection = 3, Downstream = 4, FirstCoin = 5;

        public SewerRepairProgress RepairProgress { get; private set; }
        // Shared entries: one per coin (Owner = client id + 1, or Spent), then the repair progress (State).
        public int SharedEntryCount => Coins.Length + 1;
        public SharedFacilityValue ReadShared(int index) =>
            index < Coins.Length ? new SharedFacilityValue(0, coinOwners[index]) : new SharedFacilityValue((byte)RepairProgress);
        public void ApplyShared(int index, SharedFacilityValue value)
        {
            if (index < Coins.Length) ApplyCoin(index, value.Owner);
            else ApplyRepairProgress((SewerRepairProgress)value.State);
        }
        public void ReleaseOwner(ulong clientId)
        {
            for (int i = 0; i < coinOwners.Length; i++) if (coinOwners[i] == clientId + 1) ApplyCoin(i, 0);
        }
        private readonly List<IInteractable> interactables = new();
        public IEnumerable<IInteractable> Interactables => interactables;
        private sealed class Station : IInteractable
        {
            private readonly Map2Interactions owner; private readonly Transform target; private readonly int action;
            public Station(Map2Interactions owner, Transform target, int action) { this.owner = owner; this.target = target; this.action = action; }
            public int Action => action;
            public int Priority => action; public Transform Target => target; public float Distance => Reach + .55f;
            public Vector3 AimPoint => target != null ? target.position : Vector3.zero;
            public bool CanInteract(InteractionContext context) => !context.Busy && target != null && context.Player != null && InReach(context.Player, target) && owner.CanUse(action, context.Player, context.ClientId);
            public string Prompt(InteractionContext context) => owner.Prompt(action, context.ClientId, context.Player);
            public bool Perform(InteractionContext context) => context.Online ? context.Sync != null && context.Sync.RequestInteract(this) : owner.Interact(action, context.Player, context.ClientId);
        }

        private void OnEnable() => MovementEffectRegistry.Register(this);

        private void Awake()
        {
            coinOwners = new ulong[Coins.Length];
            sewerAccesses = GetComponentsInChildren<SewerAccess>();
            handwheelRest = Handwheel.localRotation;
            lidRest = InspectionLid.localRotation;
            crankRest = AugerCrank.localRotation;
            upstreamRest = UpstreamWater.localPosition;
            downstreamRest = DownstreamWater.localPosition;
            foreach (var roll in VendingStock) roll.Store(VendingMachine.position);
            if (EmergencyPaper != null) EmergencyPaper.PromptLabel = "拾取应急卷纸";
            ApplyRepairProgress(SewerRepairProgress.Blocked);
            interactables.Clear();
            interactables.Add(new Station(this, VendingMachine, Vending));
            interactables.Add(new Station(this, UpstreamWindow, Upstream));
            interactables.Add(new Station(this, Valve, ValveAction));
            interactables.Add(new Station(this, InspectionPort, Inspection));
            interactables.Add(new Station(this, DownstreamWindow, Downstream));
            for (int i = 0; i < Coins.Length; i++) interactables.Add(new Station(this, Coins[i], FirstCoin + i));
        }

        private void Start() { foreach (var value in interactables) InteractionRegistry.Register(value); }
        private void OnDisable() { MovementEffectRegistry.Unregister(this); foreach (var value in interactables) InteractionRegistry.Unregister(value); }

        public int Wallet(ulong player)
        {
            int total = 0;
            foreach (ulong owner in coinOwners)
                if (owner == player + 1) total++;
            return total;
        }

        public SewerAccess NearbySewerAccess(Movement player)
        {
            foreach (var access in sewerAccesses)
                if (access.CanTraverse(player)) return access;
            return null;
        }

        public bool CanPickupEmergencyPaper(Movement player) =>
            EmergencyPaper != null && EmergencyPaper.isActiveAndEnabled && EmergencyPaper.CanPickup(player);

        public bool AppliesTo(Movement player) => player != null && IsWet(player.transform.position);
        public MovementModifier GetModifier(Movement player) => new MovementModifier(1, 8);

        public bool IsWet(Vector3 feet)
        {
            foreach (var zone in WetZones)
                if (zone.bounds.Contains(feet + Vector3.up * .07f)) return true;
            return false;
        }

        // Map2 station reach: measured from a point .55 m above the feet, within 1.15 m and a .95 m height band.
        private const float Reach = 1.15f, ReachHeightBand = .95f;
        private static Vector3 ReachCenter(Movement player) => player.transform.position + Vector3.up * .55f;
        private static bool InReach(Movement player, Transform target)
        {
            var center = ReachCenter(player);
            return Mathf.Abs(target.position.y - center.y) <= ReachHeightBand && (target.position - center).sqrMagnitude < Reach * Reach;
        }

        public int Nearby(Movement player)
        {
            if (player == null || player.IsTraversing || player.Aiming || player.Immobile ||
                player.GetComponent<Posture>().State != PostureState.Standing) return -1;
            var center = ReachCenter(player);
            var best = Reach * Reach;
            int action = -1;

            Check(VendingMachine, Vending);
            Check(UpstreamWindow, Upstream);
            Check(Valve, ValveAction);
            Check(InspectionPort, Inspection);
            Check(DownstreamWindow, Downstream);
            for (int i = 0; i < Coins.Length; i++)
                if (coinOwners[i] == 0) Check(Coins[i], FirstCoin + i);
            return action;

            void Check(Transform target, int index)
            {
                if (Mathf.Abs(target.position.y - center.y) > ReachHeightBand) return;
                var distance = (target.position - center).sqrMagnitude;
                if (distance >= best) return;
                action = index;
                best = distance;
            }
        }

        public string Prompt(int action, ulong player, Movement movement = null)
        {
            if (action >= FirstCoin) return "E  拾取金币";
            return action switch
            {
                Vending when Wallet(player) == 0 => "售纸机 · 需要 1 枚金币",
                Vending when movement != null && Item.CountCarriedBy(movement) >= Item.CarryCapacityPerPlayer => "售纸机 · 需空手取卷纸",
                Vending => $"E  用 1 枚金币买卷纸（持有 {Wallet(player)} 枚）",
                Upstream => RepairProgress == SewerRepairProgress.Blocked ? "E  查看上游堵塞" : "上游观察窗",
                ValveAction => RepairProgress switch
                {
                    SewerRepairProgress.Inspected => "E  关闭阀门",
                    SewerRepairProgress.Cleared => "E  打开阀门",
                    _ => RepairProgress == SewerRepairProgress.Blocked ? "先查看上游观察窗" : "A 支路阀门"
                },
                Inspection => RepairProgress == SewerRepairProgress.Isolated ? "E  用疏通器清除堵塞" : "检修口 · 先关闭阀门",
                Downstream => RepairProgress == SewerRepairProgress.Running ? "E  复查回流水位" :
                    RepairProgress == SewerRepairProgress.Confirmed ? "疏通完成 · 回流正常" : "回流观察窗 · 疏通后复查",
                _ => ""
            };
        }

        /// <summary>Offline player or room authority performs one station. Online clients request it through the generic FacilitySync.RequestInteract.</summary>
        public bool Interact(int action, Movement player, ulong clientId)
        {
            var station = interactables.OfType<Station>().FirstOrDefault(value => value.Action == action);
            if (station == null || player == null || !InReach(player, station.Target) || !CanUse(action, player, clientId)) return false;
            if (action >= FirstCoin)
            {
                ApplyCoin(action - FirstCoin, clientId + 1);
                return true;
            }
            if (action == Vending)
            {
                int coin = Array.IndexOf(coinOwners, clientId + 1);
                VendingStock[coin].Take(player);
                ApplyCoin(coin, Spent);
                return true;
            }
            ApplyRepairProgress(NextRepairStep(action));
            return true;
        }

        public bool CanUse(int action, Movement player, ulong clientId)
        {
            if (action >= FirstCoin)
                return action - FirstCoin < coinOwners.Length && coinOwners[action - FirstCoin] == 0;
            if (action == Vending)
            {
                int coin = Array.IndexOf(coinOwners, clientId + 1);
                return coin >= 0 && VendingStock[coin].Stored && Item.CountCarriedBy(player) < Item.CarryCapacityPerPlayer;
            }
            return NextRepairStep(action) != RepairProgress;
        }

        private SewerRepairProgress NextRepairStep(int action) =>
            (action, RepairProgress) switch
            {
                (Upstream, SewerRepairProgress.Blocked) => SewerRepairProgress.Inspected,
                (ValveAction, SewerRepairProgress.Inspected) => SewerRepairProgress.Isolated,
                (Inspection, SewerRepairProgress.Isolated) => SewerRepairProgress.Cleared,
                (ValveAction, SewerRepairProgress.Cleared) => SewerRepairProgress.Running,
                (Downstream, SewerRepairProgress.Running) => SewerRepairProgress.Confirmed,
                _ => RepairProgress
            };

        private void ApplyCoin(int index, ulong owner)
        {
            if (index < 0 || index >= coinOwners.Length) return;
            coinOwners[index] = owner;
            Coins[index].gameObject.SetActive(owner == 0);
        }

        private void ApplyRepairProgress(SewerRepairProgress progress)
        {
            RepairProgress = progress;
            UpstreamWater.localPosition = upstreamRest + Vector3.up * (progress < SewerRepairProgress.Cleared ? .1f : 0);
            DownstreamWater.localPosition = downstreamRest - Vector3.up * (progress < SewerRepairProgress.Running ? .09f : 0);
        }

        private void Update()
        {
            bool isolated = RepairProgress is SewerRepairProgress.Isolated or SewerRepairProgress.Cleared;
            Handwheel.localRotation = Quaternion.RotateTowards(Handwheel.localRotation,
                handwheelRest * Quaternion.AngleAxis(isolated ? 105 : 0, Vector3.forward), Time.deltaTime * 260);
            InspectionLid.localRotation = Quaternion.RotateTowards(InspectionLid.localRotation,
                lidRest * Quaternion.AngleAxis(RepairProgress == SewerRepairProgress.Cleared ? 100 : 0, Vector3.right), Time.deltaTime * 250);
            AugerCrank.localRotation = RepairProgress == SewerRepairProgress.Cleared
                ? AugerCrank.localRotation * Quaternion.AngleAxis(Time.deltaTime * 360, Vector3.forward)
                : crankRest;
        }
    }
}
