using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NatureCalls.Core
{
    public readonly struct LanBeacon
    {
        public const int Protocol = 1;
        public const int Port = 47777;
        public readonly string HostName;
        public readonly int GamePort;
        public readonly int Players;
        public readonly int MaxPlayers;
        public readonly bool SelectingMap;
        public LanBeacon(string hostName, int gamePort, int players, int maxPlayers, bool selectingMap)
        { HostName = hostName ?? ""; GamePort = gamePort; Players = players; MaxPlayers = maxPlayers; SelectingMap = selectingMap; }

        public string ToJson() => $"{{\"protocol\":{Protocol},\"host\":\"{Escape(HostName)}\",\"port\":{GamePort},\"players\":{Players},\"max\":{MaxPlayers},\"selectingMap\":{SelectingMap.ToString().ToLowerInvariant()}}}";
        public bool Joinable => !SelectingMap && Players < MaxPlayers;
        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        public static bool TryParse(string json, out LanBeacon beacon)
        {
            beacon = default;
            if (string.IsNullOrWhiteSpace(json)) return false;
            string Get(string name) => Regex.Match(json, $"\\\"{name}\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"").Groups[1].Value;
            bool Int(string name, out int value) => int.TryParse(Regex.Match(json, $"\\\"{name}\\\"\\s*:\\s*(\\d+)").Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out value);
            if (!Int("protocol", out var protocol) || protocol != Protocol || !Int("port", out var port) || port < 1 || port > 65535 ||
                !Int("players", out var players) || !Int("max", out var max) || players < 0 || max < 1 || players > max) return false;
            var selecting = Regex.Match(json, "\\\"selectingMap\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (!selecting.Success) return false;
            beacon = new LanBeacon(Get("host"), port, players, max, selecting.Value.EndsWith("true", StringComparison.OrdinalIgnoreCase));
            return true;
        }
    }
}
