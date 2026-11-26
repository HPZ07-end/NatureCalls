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
        public const float BladderFullSeconds = 70f, BowelFullSeconds = 90f;
        public const float BladderPerSecond = UrgencyMax / BladderFullSeconds, BowelPerSecond = UrgencyMax / BowelFullSeconds;
        public const float ComposureMax = 100f, ViewRadius = 8f, GroundMessRadius = 1f;
        public const float EnduranceMinimumSeconds = 15f, EnduranceMaximumSeconds = 25f;
        public const float ComfortBowelDelay = .3f, ComfortWindowSeconds = 60f;
        public const float VisionInterval = .25f, VisionRadius = 8f, VisionRate = 1f, VisionCap = 4f, GroundComposureRate = 2f;
        public const float LossOfControlBase = .3f, LossOfControlTimeout = 8f, LossOfControlInputWeight = .3f;
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
        public const int PaperRollUnits = 6;
        [Obsolete("Use PaperRollUnits and PaperRequired instead.")] public const int PaperUses = PaperRollUnits;
        public const float UnwipedDisgust = 15f, DirtyToiletDisgust = 10f, CleaningDisgust = 5f;
        public const float PuddleRadius = 1f, PuddleDisgustPerSecond = 2f, CleaningSeconds = 3f;
        public const float TargetRadius = .25f, InteractionDistance = 1.5f, UrinalDistance = .8f, ButtOffset = .4f;
        /// <summary>Temporary tuning value: urgency rises faster while the player is aiming.</summary>
        public const float AimingUrgencyRate = 1.5f;
        public const float CleaningFacingCosine = .5f;
        public const float DriftMinimum = .04f, DriftMaximum = .35f, CorrectionSpeed = .3f, MouseAimScale = .003f;
        public static float DriftAmplitude(float urgency) => DriftMinimum + (DriftMaximum - DriftMinimum) * Math.Clamp(urgency / UrgencyMax, 0, 1);
        public static float UrgencyAfterUrination(float urgency) => Math.Max(0, urgency) * .5f;
        public static MessKind Defecation(float accuracy) => accuracy >= PerfectAccuracy ? MessKind.None : accuracy >= FloorAccuracy ? MessKind.DirtyToilet : MessKind.Dropping;
        public static MessKind Urination(float accuracy) => accuracy >= UrinalAccuracy ? MessKind.None : MessKind.Puddle;
        public static int UsePaper(int remaining) => Math.Max(0, remaining - 1);
        public static int PaperRequired(MessKind result) => result switch
        {
            MessKind.None => 1,
            MessKind.DirtyToilet => 2,
            MessKind.Dropping => 3,
            _ => 1
        };
        public static float TickCleaning(float elapsed, float seconds, bool valid) => valid ? Math.Min(CleaningSeconds, elapsed + Math.Max(0, seconds)) : 0;
        public static float PuddleDisgust(float distance, float seconds) => distance <= PuddleRadius ? PuddleDisgustPerSecond * Math.Max(0, seconds) : 0;

        public static float InitialUrgency(float random01) => Math.Clamp(random01, 0f, 1f) * InitialUrgencyMax;
        public static float TickBladder(float value, float seconds, float profileScale = 1f, bool paused = false) => paused ? value : Math.Clamp(value + BladderPerSecond * Math.Max(0, seconds) * Math.Max(0, profileScale), 0, UrgencyMax);
        public static float TickBowel(float value, float seconds, float matchScale = 1f, float profileScale = 1f, float comfort = 0, bool saboteur = false, bool paused = false, float earlyScale = 1f) => paused ? value : Math.Clamp(value + BowelPerSecond * Math.Max(0, seconds) * Math.Max(0, matchScale) * Math.Max(0, profileScale) * (1f - ComfortBowelDelay * Math.Clamp(comfort, 0, 1)) * (saboteur ? .5f : 1f) * Math.Max(0, earlyScale), 0, UrgencyMax);
        public static float Highest(float bladder, float bowel) => Math.Max(bladder, bowel);
        public static bool BlocksJump(float urgency) => urgency >= 40f;
        public static bool BlocksRun(float urgency) => urgency >= 70f;
        public static float MovementScale(float urgency) => BlocksRun(urgency) ? .8f : 1f;
        public static float ViewNarrowing(float urgency) => Math.Clamp((urgency - 90f) / 10f, 0, 1);
        public static float ComfortBowelScale(float comfort) => 1f - ComfortBowelDelay * Math.Clamp(comfort, 0, 1);
        public static float EnduranceDuration(float random01, float profileBonus = 0, float comfort = 0) => EnduranceMinimumSeconds + Math.Clamp(random01, 0, 1) * (EnduranceMaximumSeconds - EnduranceMinimumSeconds) + Math.Max(0, profileBonus) + Math.Clamp(comfort, 0, 1) * 5f;
        public static float VisionComposureLoss(float totalWeight, float seconds, bool habituated) => Math.Min(VisionCap, Math.Max(0, totalWeight) * VisionRate * Math.Max(0, seconds) * (habituated ? .5f : 1f));
        public static float LossOfControlProbability(float distanceFactor, float control) => Math.Clamp(LossOfControlBase + Math.Max(0, distanceFactor) + (1f - Math.Clamp(control, 0, 1)), 0, 1);
        public static int PaperRequiredForSaboteur(MessKind result, bool saboteur) => saboteur ? Math.Max(1, (PaperRequired(result) + 1) / 2) : PaperRequired(result);

        public static float TickUrgency(float urgency, float deltaSeconds, bool toileting) =>
            toileting ? urgency : Math.Min(UrgencyMax, urgency + UrgencyPerSecond * Math.Max(0f, deltaSeconds));
        public static float TickUrgency(float urgency, float deltaSeconds, bool toileting, float rateScale) =>
            toileting ? urgency : Math.Min(UrgencyMax, urgency + UrgencyPerSecond * Math.Max(0f, deltaSeconds) * Math.Max(0f, rateScale));

        public static float DisgustAfterToileting(float disgust, bool insideStall) =>
            insideStall ? disgust : disgust + OutsideStallDisgust;

        public static bool UrgencyFailed(float urgency) => urgency >= UrgencyMax;
        public static bool DisgustFailed(float disgust) => disgust >= DisgustThreshold;
        public static bool Failed(float urgency, float disgust) => UrgencyFailed(urgency) || DisgustFailed(disgust);
    }
}
