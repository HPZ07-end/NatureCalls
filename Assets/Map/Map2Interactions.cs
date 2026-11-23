using System;
using NatureCalls.Facility;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Map
{
    public enum SewerRepairProgress : byte { Blocked, Inspected, Isolated, Cleared, Running, Confirmed }

    /// <summary>The Map2 supply and sewer stations. Mutated by the offline player or the room authority.</summary>
    public sealed class Map2Interactions : MonoBehaviour
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
        public int CoinCount => Coins.Length;

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
            ApplyRepairProgress(SewerRepairProgress.Blocked);
        }

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

        public bool IsWet(Vector3 feet)
        {
            foreach (var zone in WetZones)
                if (zone.bounds.Contains(feet + Vector3.up * .07f)) return true;
            return false;
        }

        public int Nearby(Movement player)
        {
            if (player == null || player.IsTraversing || player.Aiming || player.Immobile ||
                player.GetComponent<Posture>().State != PostureState.Standing) return -1;
            var center = player.transform.position + Vector3.up * .55f;
            const float rangeSquared = 1.15f * 1.15f;
            var best = rangeSquared;
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
                if (Mathf.Abs(target.position.y - center.y) > .95f) return;
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

        public bool Interact(int action, Movement player, ulong clientId, out int changedCoin, out PaperRoll dispensed)
        {
            changedCoin = -1;
            dispensed = null;
            if (action != Nearby(player) || !CanUse(action, player, clientId)) return false;
            if (action >= FirstCoin)
            {
                changedCoin = action - FirstCoin;
                ApplyCoin(changedCoin, clientId + 1);
                return true;
            }
            if (action == Vending)
            {
                int coin = Array.IndexOf(coinOwners, clientId + 1);
                var roll = VendingStock[coin];
                roll.Take(player);
                dispensed = roll;
                changedCoin = coin;
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

        public ulong CoinOwner(int index) => coinOwners[index];

        public void ApplyCoin(int index, ulong owner)
        {
            if (index < 0 || index >= coinOwners.Length) return;
            coinOwners[index] = owner;
            Coins[index].gameObject.SetActive(owner == 0);
        }

        public void ApplyRepairProgress(SewerRepairProgress progress)
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
