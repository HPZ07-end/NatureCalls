using System;
using Unity.Netcode;
using UnityEngine;
using NatureCalls.Core;
using NatureCalls.Player;

namespace NatureCalls.Network
{
    public enum RandomEventKind : byte { Pressure, Blackout, Supply, StomachBug, Plumber }
    public static class RandomEvents
    {
        public static string Message(RandomEventKind kind) => kind switch { RandomEventKind.Pressure => "水压突增！", RandomEventKind.Blackout => "停电 10 秒！", RandomEventKind.Supply => "补给到货，售纸机附近有金币！", RandomEventKind.StomachBug => "集体闹肚子！", _ => "管道工路过，管路已疏通！" };
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
