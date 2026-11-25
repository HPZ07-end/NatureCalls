using System;
using System.Collections.Generic;
using NatureCalls.Core;

namespace NatureCalls.Player
{
    /// <summary>
    /// Shared contract: who is the saboteur this round. Set by the match (server, mirrored to clients);
    /// read by needs (constipation), facilities (saboteur actions) and UI. Unknown players are Keepers.
    /// </summary>
    public static class RoleRegistry
    {
        private static readonly Dictionary<Movement, PlayerRole> roles = new();
        public static event Action Changed;
        public static PlayerRole Of(Movement player) => player != null && roles.TryGetValue(player, out var role) ? role : PlayerRole.Keeper;
        public static bool IsSaboteur(Movement player) => Of(player) == PlayerRole.Saboteur;
        public static void Set(Movement player, PlayerRole role) { if (player == null) return; roles[player] = role; Changed?.Invoke(); }
        public static void Clear() { roles.Clear(); Changed?.Invoke(); }
    }
}
