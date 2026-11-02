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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => End();

        public static void Begin(MatchMode mode, MapCatalog catalog, int mapIndex)
        {
            Mode = mode;
            MapIndex = mapIndex;
            ReturnMessage = "";
            SceneManager.LoadScene(catalog[mapIndex].ScenePath, LoadSceneMode.Single);
        }

        public static void ReturnToDressingRoom(string message = "")
        {
            End();
            ReturnMessage = message ?? "";
            SceneManager.LoadScene(DressingRoomScenePath, LoadSceneMode.Single);
        }

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
        }
    }
}
