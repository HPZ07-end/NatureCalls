using System;
using System.Collections.Generic;

namespace NatureCalls.Player
{
    public enum MatchStatKind : byte { MessProduced, MessCleaned, Accident, Vomit, ToiletingSuccess, Backflow }
    /// <summary>Round counters shared by body and facility code; UI reads snapshots after settlement.</summary>
    public static class MatchStats
    {
        private static readonly Dictionary<MatchStatKind, int> values = new();
        public static event Action<MatchStatKind, int> Changed;
        public static int Get(MatchStatKind kind) => values.TryGetValue(kind, out var value) ? value : 0;
        public static int Count(MatchStatKind kind) => Get(kind);
        public static void Count(MatchStatKind kind, int amount) { if (amount <= 0) return; values[kind] = Get(kind) + amount; Changed?.Invoke(kind, values[kind]); }
        public static MatchSettlement Snapshot(NatureCalls.Core.MatchWinner winner, string reason) => new(winner, reason, Get(MatchStatKind.MessProduced), Get(MatchStatKind.MessCleaned), Get(MatchStatKind.Accident), Get(MatchStatKind.Vomit), Get(MatchStatKind.ToiletingSuccess), Get(MatchStatKind.Backflow));
        public static void Reset() => values.Clear();
    }
}
