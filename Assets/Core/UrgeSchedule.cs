using System;

namespace NatureCalls.Core
{
    /// <summary>
    /// Shared contract (GLOSSARY: GameFlow/UrgeWave): per-round collective urge peaks. The bowel urge rate of a player is
    /// base × profile × Multiplier(elapsed, personal offset). The match owns the values; needs only read them.
    /// </summary>
    public readonly struct UrgeSchedule
    {
        public readonly float[] Centers;     // seconds since round start
        public readonly float[] Amplitudes;  // extra rate at each peak (0 = none)
        public readonly float Width;         // bell width (seconds, ~ one standard deviation)
        public readonly float Length;        // whole round in seconds
        public UrgeSchedule(float[] centers, float[] amplitudes, float width, float length)
        { Centers = centers ?? Array.Empty<float>(); Amplitudes = amplitudes ?? Array.Empty<float>(); Width = Math.Max(1f, width); Length = length; }
        public static UrgeSchedule None => new(null, null, 1f, 0f);
        public float Multiplier(double elapsed, float personalOffset)
        {
            double sum = 0;
            for (int i = 0; i < Centers.Length && i < Amplitudes.Length; i++)
            {
                var x = (elapsed - Centers[i] - personalOffset) / Width;
                sum += Amplitudes[i] * Math.Exp(-0.5 * x * x);
            }
            return (float)(1 + sum);
        }
    }
}
