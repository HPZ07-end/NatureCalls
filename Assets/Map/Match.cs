using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Map
{
    public enum MatchMode : byte { None, Offline, Networked }

    public static class Match
    {
        public const string DressingRoomScenePath = "Assets/Scenes/DressingRoom.unity";
        public static MatchMode Mode { get; private set; }
        public static int MapIndex { get; private set; } = -1;
        public static string ReturnMessage { get; private set; } = "";
        public static float RoundSeconds { get; private set; } = MatchRules.DefaultLengthSeconds;
        public const string RoundOverMessage = "收尾完成，保洁队获胜。";
        private static double phaseStart = -1;
        private static double offlineRoundEnd = -1;
        public static double OfflineRemaining => Mode == MatchMode.Offline && offlineRoundEnd >= 0 ? RoundClock.Remaining(offlineRoundEnd, Time.timeAsDouble) : -1;
        public static bool IsPlaying => MatchContext.Phase == MatchPhase.Playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => End();

        public static void Begin(MatchMode mode, MapCatalog catalog, int mapIndex)
        {
            Prepare(mode, mapIndex, catalog[mapIndex].RoundSeconds);
            SceneManager.LoadScene(catalog[mapIndex].ScenePath, LoadSceneMode.Single);
        }
        internal static void BeginOfflineInCurrentScene() { if (Mode == MatchMode.None) Prepare(MatchMode.Offline, -1, MatchRules.DefaultLengthSeconds); }
        private static void Prepare(MatchMode mode, int mapIndex, float roundSeconds)
        {
            Mode = mode; MapIndex = mapIndex; RoundSeconds = Mathf.Max(1f, roundSeconds); offlineRoundEnd = -1; phaseStart = -1; ReturnMessage = "";
            MatchContext.Reset(); MatchContext.Schedule = MatchRules.Schedule(RoundSeconds);
            RoleRegistry.Clear(); MatchStats.Reset();
        }
        public static void StartOfflineRound()
        {
            if (Mode != MatchMode.Offline || phaseStart >= 0) return;
            phaseStart = Time.timeAsDouble; offlineRoundEnd = phaseStart + MatchRules.RevealSeconds + RoundSeconds + MatchRules.CleanupSeconds;
            MatchContext.Phase = MatchPhase.Reveal; MatchContext.Elapsed = 0; MatchContext.Tidiness = 100;
        }
        public static void TickOfflineRound()
        {
            if (Mode != MatchMode.Offline || phaseStart < 0) return;
            var now = Time.timeAsDouble; var since = now - phaseStart;
            if (MatchContext.Phase == MatchPhase.Reveal && since >= MatchRules.RevealSeconds) MatchContext.Phase = MatchPhase.Playing;
            MatchContext.Elapsed = System.Math.Max(0, since - MatchRules.RevealSeconds);
            if (MatchContext.Phase == MatchPhase.Playing && MatchContext.Tidiness <= 0) { Finish(MatchWinner.Saboteur, "干净值归零，捣蛋者获胜。", false); return; }
            if (now >= offlineRoundEnd) Finish(MatchWinner.Keepers, RoundOverMessage, true);
        }
        public static void SetTidiness(float value) => MatchContext.Tidiness = Mathf.Clamp(value, 0, 100);
        public static void Finish(MatchWinner winner, string message, bool cleanup)
        {
            MatchContext.Phase = MatchPhase.Ended; ReturnToDressingRoom(message);
        }
        public static void ReturnToDressingRoom(string message = "") { End(); ReturnMessage = message ?? ""; SceneManager.LoadScene(DressingRoomScenePath, LoadSceneMode.Single); }
        public static string FailureMessage(string who, bool urgency) => "";
        public static string TakeReturnMessage() { var message = ReturnMessage; ReturnMessage = ""; return message; }
        public static void End() { Mode = MatchMode.None; MapIndex = -1; offlineRoundEnd = -1; phaseStart = -1; MatchContext.Reset(); RoleRegistry.Clear(); }
    }
}
