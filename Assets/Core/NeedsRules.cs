using System;

namespace NatureCalls.Core
{
    /// <summary>
    /// Urgency and Disgust for one player, and the team-loss condition they feed.
    /// Urgency rises at a constant rate (full once a minute from zero) except while toileting, and is cleared
    /// when a toileting finishes. Toileting outside a stall leaves a dropping and adds Disgust.
    /// </summary>
    public static class NeedsRules
    {
        public const float UrgencyMax = 100f;
        public const float UrgencyFullSeconds = 60f;
        public const float UrgencyPerSecond = UrgencyMax / UrgencyFullSeconds;
        /// <summary>Starting Urgency is drawn from [0, this) so players are not in step.</summary>
        public const float InitialUrgencyMax = 40f;
        public const float DisgustThreshold = 100f;
        public const float OutsideStallDisgust = 10f;
        /// <summary>A press of C toilets for this long, then the player stands up by themselves.</summary>
        public const float ToiletingSeconds = 5f;

        public static float InitialUrgency(float random01) => Math.Clamp(random01, 0f, 1f) * InitialUrgencyMax;

        public static float TickUrgency(float urgency, float deltaSeconds, bool toileting) =>
            toileting ? urgency : Math.Min(UrgencyMax, urgency + UrgencyPerSecond * Math.Max(0f, deltaSeconds));

        public static float DisgustAfterToileting(float disgust, bool insideStall) =>
            insideStall ? disgust : disgust + OutsideStallDisgust;

        public static bool UrgencyFailed(float urgency) => urgency >= UrgencyMax;
        public static bool DisgustFailed(float disgust) => disgust >= DisgustThreshold;
        public static bool Failed(float urgency, float disgust) => UrgencyFailed(urgency) || DisgustFailed(disgust);
    }
}
