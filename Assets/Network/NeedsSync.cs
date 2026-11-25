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
        public NetworkVariable<float> SharedBladder = new(0);
        public NetworkVariable<float> SharedBowel = new(0);
        public NetworkVariable<float> SharedComposure = new(100);
        public NetworkVariable<bool> SharedEndurance = new(false);
        public NetworkVariable<bool> SharedVomiting = new(false);
        public NetworkVariable<float> SharedVomitProgress = new(0);
        public NetworkVariable<float> SharedComfortBoost = new(0);
        public NetworkVariable<float> SharedComfort = new(0);
        public NetworkVariable<bool> SharedOutOfControl = new(false);
        public NetworkVariable<float> SharedDisgust = new(0);
        public NetworkVariable<bool> SharedUnwiped = new(false);
        public NetworkVariable<bool> SharedHandsDirty = new(false);
        public NetworkVariable<int> SharedPaperRequired = new(0);
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

        private void PublishNeeds()
        {
            if (!IsServer || needs == null) return;
            SharedUnwiped.Value = needs.Unwiped;
            SharedHandsDirty.Value = needs.HandsDirty;
            SharedPaperRequired.Value = needs.PaperRequired;
            SharedBladder.Value = needs.Bladder; SharedBowel.Value = needs.Bowel; SharedComposure.Value = needs.Composure;
            SharedEndurance.Value = needs.InEndurance; SharedVomiting.Value = needs.Vomiting; SharedVomitProgress.Value = needs.VomitProgress; SharedComfortBoost.Value = needs.ComfortBoostRemaining; SharedComfort.Value = needs.Comfort; SharedOutOfControl.Value = needs.IsOutOfControl;
            if (Mathf.Abs(SharedUrgency.Value - needs.Urgency) > .05f) SharedUrgency.Value = needs.Urgency;
            if (Mathf.Abs(SharedDisgust.Value - needs.Disgust) > .05f) SharedDisgust.Value = needs.Disgust;
        }

        private void Update()
        {
            if (!IsSpawned || needs == null) return;
            if (!IsServer) { needs.ApplyShared(SharedUrgency.Value, SharedDisgust.Value, SharedUnwiped.Value, SharedHandsDirty.Value, SharedPaperRequired.Value); needs.ApplyVersusShared(SharedBladder.Value, SharedBowel.Value, SharedComposure.Value, SharedEndurance.Value, SharedVomiting.Value, SharedVomitProgress.Value, SharedComfortBoost.Value, SharedComfort.Value, SharedOutOfControl.Value); }
            else if (Time.time >= nextNeedsUpdate) { nextNeedsUpdate = Time.time + .2f; PublishNeeds(); }
        }
    }
}
