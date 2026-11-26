using System;
using NatureCalls.Player;

namespace NatureCalls.UI
{
    /// <summary>
    /// Read-only inputs of the versus HUD. Each value has a safe default from the shared contract
    /// (<see cref="MatchContext"/>, <see cref="RoleRegistry"/>); the match and body systems replace the
    /// delegates with their own read-only properties when they are wired in. Values are 0–1 unless noted.
    /// </summary>
    public static class VersusHudFeed
    {
        /// <summary>Seconds left in the round, or a negative value when unknown.</summary>
        public static Func<double> TimeRemaining = () => MatchContext.Schedule.Length > 0 ? Math.Max(0, MatchContext.Schedule.Length - MatchContext.Elapsed) : -1;
        /// <summary>Latest random-event text and a serial that changes with every new event.</summary>
        public static Func<(string text, int serial)> LatestEvent = () => (null, 0);
        /// <summary>Round result once ended: whether keepers won, the reason, and a multi-line stats block.</summary>
        public static Func<(bool keepersWon, string reason, string stats)> Outcome = () => (MatchContext.Tidiness > 0, MatchContext.Tidiness > 0 ? "撑过了最后一波高峰" : "厕所被搞得一塌糊涂", "");
        /// <summary>Display name of a player (for the reveal and markers).</summary>
        public static Func<Movement, string> NameOf = player => player != null ? player.name.Replace("(Clone)", "").Trim() : "";

        // Local body state (the local player's own HUD only; nobody sees other players' urges).
        public static Func<Movement, float> Bladder = player => 0f;
        public static Func<Movement, float> Bowel = player => player != null && player.TryGetComponent<PlayerNeeds>(out var needs) ? needs.Urgency / 100f : 0f;
        public static Func<Movement, float> Composure = player => 1f;
        /// <summary>The stool kind the bowel is currently building toward (shown as the intestine fill style).</summary>
        public static Func<Movement, NatureCalls.Core.StoolKind> NextStool = player => NatureCalls.Core.StoolKind.Soft;
        /// <summary>0–1 how fast the player is moving, for the loose-stool slosh.</summary>
        public static Func<Movement, float> Speed = player => player != null && player.TryGetComponent<UnityEngine.CharacterController>(out var c) ? UnityEngine.Mathf.Clamp01(c.velocity.magnitude / 5f) : 0f;
        /// <summary>Hidden endurance intensity (no remaining time is ever shown).</summary>
        public static Func<Movement, float> Endurance = player => 0f;
        /// <summary>Vomit countdown progress (0 = none, 1 = about to vomit).</summary>
        public static Func<Movement, float> Vomit = player => 0f;
        /// <summary>Comfort presentation intensity (warm glow while relieving / relief buff).</summary>
        public static Func<Movement, float> Comfort = player => 0f;
        /// <summary>Screen narrowing at very high urge.</summary>
        public static Func<Movement, float> Narrow = player => 0f;
        public static Func<Movement, bool> LostControl = player => false;
        /// <summary>Saboteur ability cooldown summary, one line per ability, or empty.</summary>
        public static Func<Movement, string> Cooldowns = player => "";
        /// <summary>Blackout seconds remaining (random event), 0 when lights are on.</summary>
        public static Func<float> Blackout = () => 0f;
    }
}
