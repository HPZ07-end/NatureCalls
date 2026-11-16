using NatureCalls.Core;
using NatureCalls.Facility;
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
        private StallDoor unwipedStall;
        public bool Unwiped { get; private set; }
        public bool Toileting { get; set; }
        public void MarkUnwiped(StallDoor stall) { unwipedStall = stall; Unwiped = true; }
        public void Wipe() { Unwiped = false; unwipedStall = null; }
        public void LeaveUnwiped() { if (!Unwiped) return; AddDisgust(NeedsRules.UnwipedDisgust); Wipe(); }
        public void AddDisgust(float value) { if (Ticking) Disgust += value; }
        public void CompleteUrination() { if (Ticking) Urgency = NeedsRules.UrgencyAfterUrination(Urgency); }


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
            if (Ticking) Urgency = NeedsRules.TickUrgency(Urgency, Time.deltaTime, posture.IsSquatting || Toileting);
            if (!Ticking) return;
            if (Unwiped && unwipedStall != null && !unwipedStall.Contains(GetComponent<Movement>())) LeaveUnwiped();
            var world = FindFirstObjectByType<Map.Playground>();
            if (world != null) foreach (var mess in world.Messes)
                if (mess != null && mess.Kind == MessKind.Puddle)
                {
                    float amount = NeedsRules.PuddleDisgust(Vector3.ProjectOnPlane(transform.position - mess.transform.position, Vector3.up).magnitude, Time.deltaTime);
                    if (amount > 0) { AddDisgust(amount); break; }
                }
        }

        /// <summary>A finished toileting clears Urgency; outside a stall it also adds Disgust.</summary>
        public void CompleteToileting(bool insideStall)
        {
            if (!Ticking) return;
            Urgency = 0;
            Disgust = NeedsRules.DisgustAfterToileting(Disgust, insideStall);
        }

        /// <summary>Peers show the authority's values.</summary>
        public void ApplyShared(float urgency, float disgust, bool unwiped = false)
        {
            Urgency = urgency;
            Disgust = disgust;
            Unwiped = unwiped;
        }
    }
}
