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
        public static float BlackoutRemaining { get; internal set; }
        public static string LastEventText { get; internal set; } = "";
        public static int EventSerial { get; internal set; }
        public static MatchWinner Winner { get; internal set; }
        public static string EndReason { get; internal set; } = "";
        public static MatchSettlement Settlement { get; internal set; }
        public static bool Versus => Phase == MatchPhase.Reveal || Phase == MatchPhase.Playing;
        public static float OffsetFor(Movement player) => player != null && offsets.TryGetValue(player, out var offset) ? offset : 0f;
        public static void SetOffset(Movement player, float offset) { if (player != null) offsets[player] = offset; }
        public static float BowelMultiplier(Movement player) => Phase == MatchPhase.Playing ? Schedule.Multiplier(Elapsed, OffsetFor(player)) : 1f;
        public static void Reset() { offsets.Clear(); Phase = MatchPhase.None; Schedule = UrgeSchedule.None; Elapsed = 0; Tidiness = 100f; BlackoutRemaining = 0; LastEventText = ""; EventSerial = 0; Winner = MatchWinner.None; EndReason = ""; Settlement = default; }
    }

    public readonly struct MatchSettlement
    {
        public readonly MatchWinner Winner; public readonly string Reason;
        public readonly int Produced, Cleaned, Accidents, Vomits, ToiletingSuccesses, Backflows;
        public MatchSettlement(MatchWinner winner, string reason, int produced, int cleaned, int accidents, int vomits, int toiletingSuccesses, int backflows) { Winner = winner; Reason = reason ?? ""; Produced = produced; Cleaned = cleaned; Accidents = accidents; Vomits = vomits; ToiletingSuccesses = toiletingSuccesses; Backflows = backflows; }
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
