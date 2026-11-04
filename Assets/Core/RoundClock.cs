using System;

namespace NatureCalls.Core
{
    /// <summary>
    /// The global round timer: a round lasts a fixed time from its start and then ends for everyone.
    /// </summary>
    public static class RoundClock
    {
        public const float DefaultSeconds = 180f;

        public static double Remaining(double endTime, double now) => Math.Max(0, endTime - now);

        public static bool IsOver(double endTime, double now) => now >= endTime;

        /// <summary>Whole seconds, rounded up so the display reaches 00:00 only when the round ends.</summary>
        public static string Format(double remaining)
        {
            var seconds = (int)Math.Ceiling(Math.Max(0, remaining));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
