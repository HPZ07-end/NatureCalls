using System;
using System.Collections.Generic;

namespace NatureCalls.UI
{
    [Serializable]
    public sealed class LobbyPlayer
    {
        public string Nickname;
        public bool IsHost;
        public bool IsReady;
        public string Outfit;

        public LobbyPlayer(string nickname, bool isHost, bool isReady, string outfit)
        {
            Nickname = nickname;
            IsHost = isHost;
            IsReady = isReady;
            Outfit = outfit;
        }
    }

    /// <summary>
    /// Local-only lobby state used by the UI prototype.
    /// Replace this boundary with a room service when the multiplayer direction is confirmed.
    /// </summary>
    public sealed class LobbyMockData
    {
        public const int MaxPlayers = 4;
        public const string DefaultRoomCode = "NC42";
        private static int nextRoomNumber = 4201;

        public string RoomCode { get; private set; }
        public bool HasRoom { get; private set; }
        public bool GameStarted { get; private set; }
        public List<LobbyPlayer> Players { get; } = new List<LobbyPlayer>();

        public LobbyMockData()
        {
            ResetToDemoRoom();
        }

        public void ResetToDemoRoom()
        {
            RoomCode = DefaultRoomCode;
            HasRoom = true;
            GameStarted = false;
            Players.Clear();
            Players.Add(new LobbyPlayer("小明", true, false, "经典蓝"));
            Players.Add(new LobbyPlayer("小红", false, true, "活力橙"));
        }

        public void CreateRoom(string nickname)
        {
            RoomCode = "NC" + nextRoomNumber.ToString("D4");
            nextRoomNumber++;
            HasRoom = true;
            GameStarted = false;
            Players.Clear();
            Players.Add(new LobbyPlayer(string.IsNullOrWhiteSpace(nickname) ? "玩家1" : nickname, true, false, "经典蓝"));
        }

        public bool TryJoin(string roomCode, string nickname, out string message)
        {
            if (string.Equals(roomCode, "FULL4", StringComparison.OrdinalIgnoreCase))
            {
                message = "房间已满（Mock）";
                return false;
            }

            if (!string.Equals(roomCode, RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                message = "房间不存在，请检查房间号";
                return false;
            }

            if (Players.Count >= MaxPlayers)
            {
                message = "房间已满";
                return false;
            }

            Players.Add(new LobbyPlayer(string.IsNullOrWhiteSpace(nickname) ? "新玩家" : nickname, false, false, "经典蓝"));
            message = "已加入房间（Mock）";
            return true;
        }

        public int LocalPlayerIndex => Players.Count == 0 ? -1 : Players.Count - 1;

        public bool ToggleLocalReady()
        {
            if (LocalPlayerIndex < 0) return false;
            Players[LocalPlayerIndex].IsReady = !Players[LocalPlayerIndex].IsReady;
            return Players[LocalPlayerIndex].IsReady;
        }

        public void SetLocalOutfit(string outfit)
        {
            if (LocalPlayerIndex >= 0)
                Players[LocalPlayerIndex].Outfit = outfit;
        }

        public bool TryStart(out string message)
        {
            if (Players.Count == 0 || !Players[0].IsHost)
            {
                message = "只有房主可以开始游戏";
                return false;
            }

            if (LocalPlayerIndex >= 0 && !Players[LocalPlayerIndex].IsReady)
            {
                message = "请先准备";
                return false;
            }

            GameStarted = true;
            message = "模拟游戏已开始";
            return true;
        }

        public void Leave()
        {
            HasRoom = false;
            GameStarted = false;
            Players.Clear();
        }
    }
}
