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
        public readonly float[] Widths;      // per-wave widths; Width remains the default width
        public UrgeSchedule(float[] centers, float[] amplitudes, float width, float length)
        { Centers = centers ?? Array.Empty<float>(); Amplitudes = amplitudes ?? Array.Empty<float>(); Width = Math.Max(1f, width); Length = length; Widths = new float[Centers.Length]; for (var i = 0; i < Widths.Length; i++) Widths[i] = Width; }
        private UrgeSchedule(float[] centers, float[] amplitudes, float width, float length, float[] widths)
        { Centers = centers; Amplitudes = amplitudes; Width = width; Length = length; Widths = widths; }
        public static UrgeSchedule None => new(null, null, 1f, 0f);
        public float Multiplier(double elapsed, float personalOffset)
        {
            double sum = 0;
            for (int i = 0; i < Centers.Length && i < Amplitudes.Length; i++)
            {
                var x = (elapsed - Centers[i] - personalOffset) / (Widths != null && i < Widths.Length ? Widths[i] : Width);
                sum += Amplitudes[i] * Math.Exp(-0.5 * x * x);
            }
            return (float)(1 + sum);
        }
        public UrgeSchedule InsertWave(float center, float amplitude, float width)
        {
            var centers = new float[Centers.Length + 1]; var amplitudes = new float[Amplitudes.Length + 1]; var widths = new float[centers.Length];
            Array.Copy(Centers, centers, Centers.Length); Array.Copy(Amplitudes, amplitudes, Amplitudes.Length); Array.Copy(Widths ?? Array.Empty<float>(), widths, Math.Min(Widths?.Length ?? 0, centers.Length));
            centers[centers.Length - 1] = center; amplitudes[amplitudes.Length - 1] = amplitude; widths[widths.Length - 1] = Math.Max(1f, width);
            return new UrgeSchedule(centers, amplitudes, Width, Length, widths);
        }
    }
}
