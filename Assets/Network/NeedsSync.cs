using NatureCalls.Core;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;

namespace NatureCalls.Network
{
    /// <summary>
    /// Shares a player's Urgency and Disgust. The host's <see cref="PlayerNeeds"/> is the authority; peers show
    /// the published copies. The owner reports a finished toileting and the host applies it.
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(PlayerNeeds), typeof(FacilitySync))]
    public sealed class NeedsSync : NetworkBehaviour
    {
        public NetworkVariable<float> SharedUrgency = new(0);
        public NetworkVariable<float> SharedDisgust = new(0);
        private PlayerNeeds needs;
        private FacilitySync facilities;
        private float nextNeedsUpdate;

        public override void OnNetworkSpawn()
        {
            needs = GetComponent<PlayerNeeds>();
            facilities = GetComponent<FacilitySync>();
        }

        /// <summary>The host starts this player's needs for a round.</summary>
        public void BeginNeeds()
        {
            if (IsServer && needs != null) needs.BeginRound(Random.value);
        }

        public bool NeedsFailed => needs != null && needs.Failed;
        public bool UrgencyFailed => needs != null && NeedsRules.UrgencyFailed(needs.Urgency);

        /// <summary>The owner finished a toileting; the host applies its needs and drops.</summary>
        public void ReportToileting(bool insideStall, Vector3 position)
        {
            if (IsSpawned && IsOwner) ToiletingDoneRpc(insideStall, position);
        }

        [Rpc(SendTo.Server)]
        private void ToiletingDoneRpc(bool insideStall, Vector3 position, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || needs == null || !needs.Ticking) return;
            needs.CompleteToileting(insideStall);
            PublishNeeds();
            if (!insideStall) facilities.AddDropping(position);
        }

        private void PublishNeeds()
        {
            if (!IsServer || needs == null) return;
            if (Mathf.Abs(SharedUrgency.Value - needs.Urgency) > .05f) SharedUrgency.Value = needs.Urgency;
            if (Mathf.Abs(SharedDisgust.Value - needs.Disgust) > .05f) SharedDisgust.Value = needs.Disgust;
        }

        private void Update()
        {
            if (!IsSpawned || needs == null) return;
            if (!IsServer) needs.ApplyShared(SharedUrgency.Value, SharedDisgust.Value);
            else if (Time.time >= nextNeedsUpdate) { nextNeedsUpdate = Time.time + .2f; PublishNeeds(); }
        }
    }
}
