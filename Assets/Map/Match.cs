using NatureCalls.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Map
{
    public enum MatchMode : byte { None, Offline, Networked }

    /// <summary>
    /// The game started from the Lobby: offline or networked, and which map was chosen.
    /// <see cref="MatchMode.None"/> means a scene was opened directly (development and tests).
    /// </summary>
    public static class Match
    {
        public const string DressingRoomScenePath = "Assets/Scenes/DressingRoom.unity";

        public static MatchMode Mode { get; private set; }
        public static int MapIndex { get; private set; } = -1;
        public static string ReturnMessage { get; private set; } = "";
        public static float RoundSeconds { get; private set; } = RoundClock.DefaultSeconds;
        public const string RoundOverMessage = "时间到，本局结束。";
        private static double offlineRoundEnd = -1;

        /// <summary>Seconds left in an offline round; -1 before it starts or outside an offline match.</summary>
        public static double OfflineRemaining =>
            Mode == MatchMode.Offline && offlineRoundEnd >= 0 ? RoundClock.Remaining(offlineRoundEnd, Time.timeAsDouble) : -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => End();

        public static void Begin(MatchMode mode, MapCatalog catalog, int mapIndex)
        {
            Mode = mode;
            MapIndex = mapIndex;
            RoundSeconds = catalog[mapIndex].RoundSeconds;
            offlineRoundEnd = -1;
            ReturnMessage = "";
            SceneManager.LoadScene(catalog[mapIndex].ScenePath, LoadSceneMode.Single);
        }

        public static void ReturnToDressingRoom(string message = "")
        {
            End();
            ReturnMessage = message ?? "";
            SceneManager.LoadScene(DressingRoomScenePath, LoadSceneMode.Single);
        }

        /// <summary>Starts the offline round clock once the map is ready; a networked round runs on the Lobby room.</summary>
        public static void StartOfflineRound()
        {
            if (Mode == MatchMode.Offline) offlineRoundEnd = Time.timeAsDouble + RoundSeconds;
        }

        /// <summary>Ends an offline round when its time is up.</summary>
        public static void TickOfflineRound()
        {
            if (Mode == MatchMode.Offline && offlineRoundEnd >= 0 && RoundClock.IsOver(offlineRoundEnd, Time.timeAsDouble))
                ReturnToDressingRoom(RoundOverMessage);
        }

        /// <summary>The team-loss line; <paramref name="who"/> is empty offline.</summary>
        public static string FailureMessage(string who, bool urgency) =>
            "失败：" + (string.IsNullOrEmpty(who) ? "" : who + " 的") + (urgency ? "急迫值满了。" : "恶心值达到上限。");

        /// <summary>Returns the message once, for the Lobby status line.</summary>
        public static string TakeReturnMessage()
        {
            var message = ReturnMessage;
            ReturnMessage = "";
            return message;
        }

        /// <summary>Forgets the current match without loading a scene.</summary>
        public static void End()
        {
            Mode = MatchMode.None;
            MapIndex = -1;
            offlineRoundEnd = -1;
        }
    }
}
