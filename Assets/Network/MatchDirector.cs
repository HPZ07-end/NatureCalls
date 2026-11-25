using System;
using Unity.Netcode;
using UnityEngine;
using NatureCalls.Core;
using NatureCalls.Player;
using NatureCalls.Map;
using NatureCalls.Facility;

namespace NatureCalls.Network
{
    public enum RandomEventKind : byte { Pressure, Blackout, Supply, StomachBug, Plumber }
    public static class RandomEvents
    {
        public static string Message(RandomEventKind kind) => kind switch { RandomEventKind.Pressure => "水压突增！", RandomEventKind.Blackout => "停电 10 秒！", RandomEventKind.Supply => "补给到货，售纸机附近有金币！", RandomEventKind.StomachBug => "集体闹肚子！", _ => "管道工路过，管路已疏通！" };
    }

    public struct MatchSettlementNet : INetworkSerializeByMemcpy, IEquatable<MatchSettlementNet>
    {
        public MatchWinner Winner; public int Produced, Cleaned, Accidents, Vomits, ToiletingSuccesses, Backflows;
        public bool Equals(MatchSettlementNet other) => Winner == other.Winner && Produced == other.Produced && Cleaned == other.Cleaned && Accidents == other.Accidents && Vomits == other.Vomits && ToiletingSuccesses == other.ToiletingSuccesses && Backflows == other.Backflows;
    }

    public struct MatchRoleEntry : INetworkSerializeByMemcpy, IEquatable<MatchRoleEntry>
    {
        public ulong ClientId;
        public PlayerRole Role;
        public bool Equals(MatchRoleEntry other) => ClientId == other.ClientId && Role == other.Role;
    }

    /// <summary>Small shared match helpers. LobbyRoom owns the network lifetime; this file owns versus state conventions.</summary>
    public static class MatchDirector
    {
        private static double blackoutUntil;
        private static RandomEventKind? forcedEvent;
        public static float BlackoutRemaining => Mathf.Max(0, (float)(blackoutUntil - Time.timeAsDouble));
        public static string LastEventText { get; private set; } = "";
        public static int EventSerial { get; private set; }
        public static void Reset() { blackoutUntil = 0; forcedEvent = null; LastEventText = ""; EventSerial = 0; ForceSaboteur = false; }
        public static void ForceNextEvent(RandomEventKind kind) => forcedEvent = kind;
        public static RandomEventKind NextEvent() { var kind = forcedEvent ?? (RandomEventKind)UnityEngine.Random.Range(0, 5); forcedEvent = null; return kind; }
        public static void TriggerEvent(RandomEventKind kind, Playground world = null)
        {
            world ??= UnityEngine.Object.FindFirstObjectByType<Playground>();
            switch (kind)
            {
                case RandomEventKind.Pressure: UnityEngine.Object.FindFirstObjectByType<Pipe>()?.AddPressure(40); break;
                case RandomEventKind.Blackout: blackoutUntil = Time.timeAsDouble + 10; break;
                case RandomEventKind.Supply: UnityEngine.Object.FindFirstObjectByType<PaperVendingMachine>()?.RestockCoins(2); break;
                case RandomEventKind.StomachBug: MatchContext.Schedule = MatchContext.Schedule.InsertWave((float)MatchContext.Elapsed, .8f, 10f); break;
                case RandomEventKind.Plumber: UnityEngine.Object.FindFirstObjectByType<Pipe>()?.Clear(); break;
            }
            LastEventText = RandomEvents.Message(kind); EventSerial++; MatchContext.LastEventText = LastEventText; MatchContext.EventSerial = EventSerial; MatchContext.BlackoutRemaining = BlackoutRemaining;
        }
        public static void ReceiveEvent(RandomEventKind kind, int serial)
        {
            LastEventText = RandomEvents.Message(kind); EventSerial = serial; MatchContext.LastEventText = LastEventText; MatchContext.EventSerial = serial;
            if (kind == RandomEventKind.Blackout) blackoutUntil = Time.timeAsDouble + 10;
            if (kind == RandomEventKind.StomachBug) MatchContext.Schedule = MatchContext.Schedule.InsertWave((float)MatchContext.Elapsed, .8f, 10f);
        }
        public static void TriggerNextEvent(Playground world = null) => TriggerEvent(NextEvent(), world);
        public static bool ForceSaboteur { get; set; }
        public static bool CommandLineSaboteur
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (var i = 0; i < args.Length; i++) if (args[i] == "-saboteur") return true;
                return false;
            }
        }
        public static float RandomOffset() => UnityEngine.Random.Range(-MatchRules.OffsetLimitSeconds, MatchRules.OffsetLimitSeconds);
        public static PlayerRole PickRole(int index, int count) => (ForceSaboteur || CommandLineSaboteur) && index == 0 || count > 1 && index == UnityEngine.Random.Range(0, count) ? PlayerRole.Saboteur : PlayerRole.Keeper;
        public static void ApplyRole(ulong clientId, PlayerRole role)
        {
            var player = UnityEngine.Object.FindObjectsByType<Movement>(FindObjectsSortMode.None);
            foreach (var movement in player)
            {
                var network = movement.GetComponent<NetworkObject>();
                if (network != null && network.OwnerClientId == clientId) { RoleRegistry.Set(movement, role); return; }
            }
        }
    }
}
