namespace NatureCalls.Core
{
    /// <summary>
    /// Room start rules from the Glossary Lobby entry: 2–4 players, every non-host player ready.
    /// </summary>
    public static class LobbyRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;

        public static bool HasCapacity(int playerCount) => playerCount < MaxPlayers;

        public static bool CanStart(int playerCount, int readyGuests)
        {
            var guests = playerCount - 1;
            return playerCount >= MinPlayers && playerCount <= MaxPlayers && readyGuests >= guests;
        }
    }
}
