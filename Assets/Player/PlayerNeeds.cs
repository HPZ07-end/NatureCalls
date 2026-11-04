using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Player
{
    /// <summary>
    /// The player's Urgency and Disgust. Only the authority (the offline player, or the host for every networked
    /// player) changes them; other peers receive the values. They change only while a round is running.
    /// </summary>
    [RequireComponent(typeof(Posture))]
    public sealed class PlayerNeeds : MonoBehaviour
    {
        public float Urgency { get; private set; }
        public float Disgust { get; private set; }
        /// <summary>Set by whoever owns the round (offline Playground or the host); peers leave it off.</summary>
        public bool Ticking { get; private set; }
        public bool Failed => NeedsRules.Failed(Urgency, Disgust);

        private Posture posture;

        private void Awake() => posture = GetComponent<Posture>();

        /// <summary>Starts this player's needs for a new round.</summary>
        public void BeginRound(float random01)
        {
            Urgency = NeedsRules.InitialUrgency(random01);
            Disgust = 0;
            Ticking = true;
        }

        public void StopRound() => Ticking = false;

        private void Update()
        {
            if (Ticking) Urgency = NeedsRules.TickUrgency(Urgency, Time.deltaTime, posture.IsSquatting);
        }

        /// <summary>A finished toileting clears Urgency; outside a stall it also adds Disgust.</summary>
        public void CompleteToileting(bool insideStall)
        {
            if (!Ticking) return;
            Urgency = 0;
            Disgust = NeedsRules.DisgustAfterToileting(Disgust, insideStall);
        }

        /// <summary>Peers show the authority's values.</summary>
        public void ApplyShared(float urgency, float disgust)
        {
            Urgency = urgency;
            Disgust = disgust;
        }
    }
}
