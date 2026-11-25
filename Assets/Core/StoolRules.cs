using System;

namespace NatureCalls.Core
{
    /// <summary>GLOSSARY Mess/StoolKind: what a bowel movement comes out as.</summary>
    public enum StoolKind : byte { Soft, Hard, Loose }

    /// <summary>
    /// Pure rules for StoolKind: the draw, and each kind's pipe load, odour and dirt weight.
    /// 暂定（Agent 第一版数值，待用户确认）。
    /// </summary>
    public static class StoolRules
    {
        public const float SoftBase = .60f, HardBase = .25f, LooseBase = .15f;
        public const float SaboteurHardBonus = .35f;
        public const float UrgentLooseBonus = .35f;
        /// <summary>Urgency (0–1) at or above which a movement counts as urgent.</summary>
        public const float UrgentThreshold = .9f;
        public const float ComfortSoftBonus = .20f;
        /// <summary>Composure spent to pick a hard stool up with E.</summary>
        public const float PickupComposureCost = 8f;

        /// <summary>Chance of each kind (soft, hard, loose), summing to 1.</summary>
        public static (float soft, float hard, float loose) Odds(bool saboteur, float urgency01, bool lossOfControl, float comfort01)
        {
            float soft = SoftBase + ComfortSoftBonus * Clamp01(comfort01);
            float hard = HardBase + (saboteur ? SaboteurHardBonus : 0);
            float loose = LooseBase + (lossOfControl || urgency01 >= UrgentThreshold ? UrgentLooseBonus : 0);
            float total = soft + hard + loose;
            return (soft / total, hard / total, loose / total);
        }

        /// <summary>Draws a kind from <paramref name="random01"/> in [0, 1).</summary>
        public static StoolKind Roll(bool saboteur, float urgency01, bool lossOfControl, float comfort01, float random01)
        {
            var (soft, hard, _) = Odds(saboteur, urgency01, lossOfControl, comfort01);
            float draw = Clamp01(random01);
            if (draw < soft) return StoolKind.Soft;
            if (draw < soft + hard) return StoolKind.Hard;
            return StoolKind.Loose;
        }

        /// <summary>Pipe accumulation added when this kind goes into a toilet (PlumbingRules threshold is 100).</summary>
        public static int PipeLoad(StoolKind kind) => kind switch { StoolKind.Hard => 45, StoolKind.Loose => 10, _ => 25 };
        /// <summary>Loose stool flushed into a blocked pipe acts like an over-flush: it backflows.</summary>
        public static bool Backflows(StoolKind kind, bool pipeBlocked) => kind == StoolKind.Loose && pipeBlocked;
        /// <summary>Smell strength: scales odour particles and the Composure drain of seeing it.</summary>
        public static float Odor(StoolKind kind) => kind switch { StoolKind.Soft => 1.5f, StoolKind.Hard => .4f, _ => 1f };
        /// <summary>Weight in the dirt field (before the saboteur ×0.3).</summary>
        public static float DirtWeight(StoolKind kind) => kind switch { StoolKind.Soft => 1f, StoolKind.Hard => .6f, _ => 1.2f };
        /// <summary>Floor slipperiness of a loose stool, same as a WetZone.</summary>
        public const float LooseInertia = 8f;

        /// <summary>The shared mess entry a floor stool becomes; Hard has none (it is an Item).</summary>
        public static MessKind FloorMess(StoolKind kind) => kind switch { StoolKind.Loose => MessKind.LooseStool, StoolKind.Hard => MessKind.None, _ => MessKind.Dropping };
        /// <summary>The kind a shared mess entry shows, if it is a stool.</summary>
        public static StoolKind? KindOf(MessKind mess) => mess switch { MessKind.Dropping => StoolKind.Soft, MessKind.LooseStool => StoolKind.Loose, MessKind.Accident => StoolKind.Loose, _ => null };
        /// <summary>Odour of any mess entry (0 for odourless ones).</summary>
        public static float Odor(MessKind mess) => mess switch
        {
            MessKind.Dropping => Odor(StoolKind.Soft), MessKind.LooseStool => Odor(StoolKind.Loose), MessKind.Accident => 1.8f,
            MessKind.Vomit => 1.2f, MessKind.Backflow => 1.2f, MessKind.DirtyToilet => .6f, MessKind.Puddle => .3f, _ => 0f
        };

        private static float Clamp01(float value) => Math.Max(0, Math.Min(1, value));
    }
}
