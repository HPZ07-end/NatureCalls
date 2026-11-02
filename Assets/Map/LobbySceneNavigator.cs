using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Map
{
    /// <summary>
    /// Leaves the DressingRoom and enters the standalone Lobby UI scene.
    /// </summary>
    public sealed class LobbySceneNavigator : MonoBehaviour
    {
        public const string LobbyScenePath = "Assets/UI/Lobby/LobbyUI.unity";

        public void EnterLobby()
        {
            if (!Application.CanStreamedLevelBeLoaded(LobbyScenePath))
            {
                Debug.LogError("LobbyUI is not in Build Settings: " + LobbyScenePath);
                return;
            }

            SceneManager.LoadScene(LobbyScenePath, LoadSceneMode.Single);
        }
    }
}
