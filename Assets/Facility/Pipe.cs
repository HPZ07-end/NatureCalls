using System;
using System.Collections.Generic;
using UnityEngine;
using NatureCalls.Core;

namespace NatureCalls.Facility
{
    /// <summary>Shared blocked state for a group of water fixtures.</summary>
    public sealed class Pipe : MonoBehaviour, ISharedFacilityState
    {
        [SerializeField] private MonoBehaviour[] fixtureComponents;
        [SerializeField] private bool authority = true;
        private readonly List<IWaterFixture> fixtures = new();
        public int Accumulation { get; private set; }
        public bool IsBlocked => PlumbingRules.IsBlocked(Accumulation);
        public event Action<IWaterFixture> BackflowOccurred;
        public int SharedEntryCount => 1;
        public IReadOnlyList<IWaterFixture> Fixtures => fixtures;
        private void Awake()
        {
            fixtures.Clear();
            if (fixtureComponents != null) foreach (var component in fixtureComponents)
                if (component is IWaterFixture fixture && !fixtures.Contains(fixture)) fixtures.Add(fixture);
        }
        private void Update()
        {
            if (!authority || !IsBlocked || fixtures.Count == 0) return;
            // 暂定，待用户确认：每秒 1/45，Unity frame sampling is bounded to one event per second.
            if (UnityEngine.Random.value < Time.deltaTime / PlumbingRules.BackflowDenominator) TriggerBackflow();
        }
        public void AddStool() { Accumulation = PlumbingRules.AddStool(Accumulation); }
        public void AddPaper(int units) { Accumulation = PlumbingRules.AddPaper(Accumulation, units); }
        public void Clear() { Accumulation = PlumbingRules.Clear(); }
        public void TriggerBackflow(IWaterFixture fixture = null)
        {
            if (!IsBlocked || fixtures.Count == 0) return;
            var chosen = fixture ?? fixtures[UnityEngine.Random.Range(0, fixtures.Count)];
            chosen?.Backflow(); BackflowOccurred?.Invoke(chosen);
        }
        public SharedFacilityValue ReadShared(int index) => new SharedFacilityValue((byte)Mathf.Clamp(Accumulation, 0, 255), IsBlocked ? 1UL : 0UL);
        public void ApplyShared(int index, SharedFacilityValue value) => Accumulation = value.State;
        public void ReleaseOwner(ulong clientId) { }
    }
}
