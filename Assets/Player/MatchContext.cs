using System;
using System.Collections.Generic;
using NatureCalls.Core;

namespace NatureCalls.Player
{
    public enum MatchPhase : byte { None, Reveal, Playing, Ended }

    /// <summary>Shared contract: round state the match publishes (server, mirrored to clients) and needs/UI read.</summary>
    public static class MatchContext
    {
        private static readonly Dictionary<Movement, float> offsets = new();
        public static MatchPhase Phase { get; set; }
        public static UrgeSchedule Schedule { get; set; } = UrgeSchedule.None;
        /// <summary>Seconds since the playing phase began (0 outside it).</summary>
        public static double Elapsed { get; set; }
        /// <summary>Tidiness 0–100, published by the match.</summary>
        public static float Tidiness { get; set; } = 100f;
        public static bool Versus => Phase == MatchPhase.Reveal || Phase == MatchPhase.Playing;
        public static float OffsetFor(Movement player) => player != null && offsets.TryGetValue(player, out var offset) ? offset : 0f;
        public static void SetOffset(Movement player, float offset) { if (player != null) offsets[player] = offset; }
        public static float BowelMultiplier(Movement player) => Phase == MatchPhase.Playing ? Schedule.Multiplier(Elapsed, OffsetFor(player)) : 1f;
        public static void Reset() { offsets.Clear(); Phase = MatchPhase.None; Schedule = UrgeSchedule.None; Elapsed = 0; Tidiness = 100f; }
    }

    /// <summary>Shared contract: body events raised by facilities and other players, consumed by needs.</summary>
    public static class BodyEvents
    {
        public enum SplashKind : byte { OwnRebound, OtherMess, Backflow, Vomit }
        public static event Action<Movement> Knocked;
        public static event Action<Movement, SplashKind> Splashed;
        public static void Knock(Movement target) => Knocked?.Invoke(target);
        public static void Splash(Movement target, SplashKind kind) => Splashed?.Invoke(target, kind);
    }
}
