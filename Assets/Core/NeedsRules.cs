using System;

namespace NatureCalls.Core
{
    /// <summary>
    /// Urgency and Disgust for one player, and the team-loss condition they feed.
    /// Urgency rises at a constant rate (full once a minute from zero) except while toileting, and is cleared
    /// after defecation, or halved after urination. Outside defecation leaves a dropping and adds Disgust.
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
        public const float UrinatingSeconds = 3f;
        public const float PerfectAccuracy = .8f, FloorAccuracy = .4f, UrinalAccuracy = .7f;
        public const int PaperUses = 3;
        public const float UnwipedDisgust = 15f, DirtyToiletDisgust = 10f, CleaningDisgust = 5f;
        public const float PuddleRadius = 1f, PuddleDisgustPerSecond = 2f, CleaningSeconds = 3f;
        public const float TargetRadius = .25f, InteractionDistance = 1.5f, UrinalDistance = .8f, ButtOffset = .4f;
        public const float CleaningFacingCosine = .5f;
        public const float DriftMinimum = .04f, DriftMaximum = .35f, CorrectionSpeed = .3f, MouseAimScale = .003f;
        public static float DriftAmplitude(float urgency) => DriftMinimum + (DriftMaximum - DriftMinimum) * Math.Clamp(urgency / UrgencyMax, 0, 1);
        public static float UrgencyAfterUrination(float urgency) => Math.Max(0, urgency) * .5f;
        public static MessKind Defecation(float accuracy) => accuracy >= PerfectAccuracy ? MessKind.None : accuracy >= FloorAccuracy ? MessKind.DirtyToilet : MessKind.Dropping;
        public static MessKind Urination(float accuracy) => accuracy >= UrinalAccuracy ? MessKind.None : MessKind.Puddle;
        public static int UsePaper(int remaining) => Math.Max(0, remaining - 1);
        public static float TickCleaning(float elapsed, float seconds, bool valid) => valid ? Math.Min(CleaningSeconds, elapsed + Math.Max(0, seconds)) : 0;
        public static float PuddleDisgust(float distance, float seconds) => distance <= PuddleRadius ? PuddleDisgustPerSecond * Math.Max(0, seconds) : 0;

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
