using System;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Map
{
    public enum SewerRepairProgress : byte { Blocked, Inspected, Isolated, Cleared, Running, Confirmed }

    // prototype: replaced by Facility Pipe/WaterValve (planned: plumbing)
    public sealed class Map2Repair : MonoBehaviour, ISharedFacilityState, IInteractableProvider
    {
        public Transform UpstreamWindow, Valve, InspectionPort, DownstreamWindow, Handwheel, InspectionLid, AugerCrank, UpstreamWater, DownstreamWater;
        public SewerRepairProgress RepairProgress { get; private set; }
        private Quaternion handwheelRest, lidRest, crankRest; private Vector3 upstreamRest, downstreamRest;
        private readonly List<IInteractable> interactables = new();
        public IEnumerable<IInteractable> Interactables => interactables;
        private const int Upstream = 0, ValveAction = 1, Inspection = 2, Downstream = 3;
        private sealed class Station : IInteractable
        {
            private readonly Map2Repair owner; private readonly Transform target; private readonly int action;
            public Station(Map2Repair owner, Transform target, int action) { this.owner = owner; this.target = target; this.action = action; }
            public int Priority => action; public Transform Target => target; public float Distance => Interaction.ReachDistance;
            public Vector3 AimPoint => target != null ? target.position : Vector3.zero;
            public bool CanInteract(InteractionContext context) => Interaction.InReach(context, target) && owner.CanUse(action);
            public string Prompt(InteractionContext context) => owner.Prompt(action);
            public bool Perform(InteractionContext context) => context.Online ? context.Requests != null && context.Requests.RequestInteract(this) : owner.Interact(action, context.Player, context.ClientId);
        }
        public int SharedEntryCount => 1;
        public SharedFacilityValue ReadShared(int index) => new SharedFacilityValue((byte)RepairProgress);
        public void ApplyShared(int index, SharedFacilityValue value) => ApplyRepairProgress((SewerRepairProgress)value.State);
        public void ReleaseOwner(ulong clientId) { }
        private void Awake()
        {
            handwheelRest = Handwheel.localRotation; lidRest = InspectionLid.localRotation; crankRest = AugerCrank.localRotation;
            upstreamRest = UpstreamWater.localPosition; downstreamRest = DownstreamWater.localPosition;
            ApplyRepairProgress(SewerRepairProgress.Blocked);
            interactables.Clear(); interactables.Add(new Station(this, UpstreamWindow, Upstream)); interactables.Add(new Station(this, Valve, ValveAction));
            interactables.Add(new Station(this, InspectionPort, Inspection)); interactables.Add(new Station(this, DownstreamWindow, Downstream));
        }
        private void Start() { foreach (var value in interactables) InteractionRegistry.Register(value); }
        private void OnDisable() { foreach (var value in interactables) InteractionRegistry.Unregister(value); }
        public string Prompt(int action) => action switch
        {
            Upstream => RepairProgress == SewerRepairProgress.Blocked ? "E  查看上游堵塞" : "上游观察窗",
            ValveAction => RepairProgress switch { SewerRepairProgress.Inspected => "E  关闭阀门", SewerRepairProgress.Cleared => "E  打开阀门", _ => RepairProgress == SewerRepairProgress.Blocked ? "先查看上游观察窗" : "A 支路阀门" },
            Inspection => RepairProgress == SewerRepairProgress.Isolated ? "E  用疏通器清除堵塞" : "检修口 · 先关闭阀门",
            Downstream => RepairProgress == SewerRepairProgress.Running ? "E  复查回流水位" : RepairProgress == SewerRepairProgress.Confirmed ? "疏通完成 · 回流正常" : "回流观察窗 · 疏通后复查",
            _ => ""
        };
        public bool CanUse(int action) => NextRepairStep(action) != RepairProgress;
        public bool Interact(int action, Movement player, ulong clientId)
        { if (player == null || !Interaction.InReach(player, action == Upstream ? UpstreamWindow : action == ValveAction ? Valve : action == Inspection ? InspectionPort : DownstreamWindow) || !CanUse(action)) return false; ApplyRepairProgress(NextRepairStep(action)); return true; }
        private SewerRepairProgress NextRepairStep(int action) => (action, RepairProgress) switch
        {
            (Upstream, SewerRepairProgress.Blocked) => SewerRepairProgress.Inspected, (ValveAction, SewerRepairProgress.Inspected) => SewerRepairProgress.Isolated,
            (Inspection, SewerRepairProgress.Isolated) => SewerRepairProgress.Cleared, (ValveAction, SewerRepairProgress.Cleared) => SewerRepairProgress.Running,
            (Downstream, SewerRepairProgress.Running) => SewerRepairProgress.Confirmed, _ => RepairProgress
        };
        private void ApplyRepairProgress(SewerRepairProgress progress)
        { RepairProgress = progress; UpstreamWater.localPosition = upstreamRest + Vector3.up * (progress < SewerRepairProgress.Cleared ? .1f : 0); DownstreamWater.localPosition = downstreamRest - Vector3.up * (progress < SewerRepairProgress.Running ? .09f : 0); }
        private void Update()
        { bool isolated = RepairProgress is SewerRepairProgress.Isolated or SewerRepairProgress.Cleared; Handwheel.localRotation = Quaternion.RotateTowards(Handwheel.localRotation, handwheelRest * Quaternion.AngleAxis(isolated ? 105 : 0, Vector3.forward), Time.deltaTime * 260); InspectionLid.localRotation = Quaternion.RotateTowards(InspectionLid.localRotation, lidRest * Quaternion.AngleAxis(RepairProgress == SewerRepairProgress.Cleared ? 100 : 0, Vector3.right), Time.deltaTime * 250); AugerCrank.localRotation = RepairProgress == SewerRepairProgress.Cleared ? AugerCrank.localRotation * Quaternion.AngleAxis(Time.deltaTime * 360, Vector3.forward) : crankRest; }
    }
}
