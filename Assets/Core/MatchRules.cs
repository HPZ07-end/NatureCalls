using System;

namespace NatureCalls.Core
{
    public enum MatchWinner : byte { None, Keepers, Saboteur }
    public readonly struct MatchOutcome
    {
        public readonly MatchWinner Winner;
        public readonly string Reason;
        public MatchOutcome(MatchWinner winner, string reason) { Winner = winner; Reason = reason ?? string.Empty; }
    }

    /// <summary>Pure versus-match constants and decisions. Values are first-pass provisional tuning.</summary>
    public static class MatchRules
    {
        public const int DefaultWaveCount = 3;
        public const float DefaultLengthSeconds = 480f;
        public const float RevealSeconds = 4f;
        public const float CleanupSeconds = 45f;
        public const float SettlementSeconds = 8f;
        public const float WaveWidthSeconds = 20f;
        public const float TidinessKernel = 6f;
        public const float OffsetLimitSeconds = 20f;
        public const float RandomEventMinSeconds = 60f;
        public const float RandomEventMaxSeconds = 90f;

        public static UrgeSchedule Schedule(float length = DefaultLengthSeconds, int waveCount = DefaultWaveCount)
        {
            waveCount = Math.Max(1, Math.Min(3, waveCount));
            var centers = new float[waveCount];
            var amplitudes = new float[waveCount];
            var fractions = waveCount == 3 ? new[] { .25f, .55f, .85f } : waveCount == 2 ? new[] { .35f, .75f } : new[] { .6f };
            var strengths = waveCount == 3 ? new[] { .6f, 1.3f, 2f } : waveCount == 2 ? new[] { .8f, 1.7f } : new[] { 1f };
            for (var i = 0; i < waveCount; i++) { centers[i] = length * fractions[i]; amplitudes[i] = strengths[i]; }
            return new UrgeSchedule(centers, amplitudes, WaveWidthSeconds, length);
        }

        public static float Tidiness(float dirtTotal) => MathF.Max(0f, MathF.Min(100f, 100f - MathF.Max(0f, dirtTotal) * TidinessKernel));
        public static MatchOutcome Resolve(float tidiness, bool cleanupFinished) =>
            tidiness <= 0f ? new MatchOutcome(MatchWinner.Saboteur, "干净值归零，捣蛋者获胜。") :
            cleanupFinished ? new MatchOutcome(MatchWinner.Keepers, "撑过最后一波，保洁队获胜。") : new MatchOutcome(MatchWinner.None, "");
    }
}
