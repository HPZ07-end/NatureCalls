using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.UI
{
    /// <summary>
    /// Controls the local lobby prototype. It intentionally contains no network dependency.
    /// </summary>
    public sealed class LobbyUIController : MonoBehaviour
    {
        private static readonly Vector2 MirrorDressingPanelSize = new Vector2(640f, 480f);
        private static readonly Vector2 MirrorDressingPanelOffset = new Vector2(-32f, 32f);

        [Header("Panels")]
        [SerializeField] private GameObject gameBoothPanel;
        [SerializeField] private GameObject dressingRoomPanel;

        [Header("In-world dressing")]
        [SerializeField] private bool startInMirrorMode;
        [SerializeField] private GameObject mirrorPrompt;
        [SerializeField] private GameObject doorPrompt;

        [Header("Game booth")]
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private Text roomCodeLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text[] playerSlotLabels;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startButton;

        [Header("Dressing room")]
        [SerializeField] private Text selectedOutfitLabel;
        [SerializeField] private Image outfitPreview;

        private readonly LobbyMockData mockData = new LobbyMockData();
        private readonly Dictionary<string, Color> outfitColors = new Dictionary<string, Color>
        {
            { "经典蓝", new Color(0.20f, 0.55f, 0.95f) },
            { "活力橙", new Color(0.95f, 0.42f, 0.18f) },
            { "清洁绿", new Color(0.25f, 0.78f, 0.43f) }
        };
        public bool IsDressingPanelOpen => dressingRoomPanel != null && dressingRoomPanel.activeSelf;
        public bool IsLobbyPanelOpen => gameBoothPanel != null && gameBoothPanel.activeSelf;
        private bool inWorldLobbyOpened;

        private void Start()
        {
            WireButtonListeners();
            if (startInMirrorMode)
            {
                if (gameBoothPanel != null) gameBoothPanel.SetActive(false);
                if (dressingRoomPanel != null) dressingRoomPanel.SetActive(false);
                HideMirrorPrompt();
                HideDoorPrompt();
                return;
            }
            ShowGameBooth();
            RefreshView("欢迎来到大厅，当前使用 Mock 房间数据");
        }

        private void WireButtonListeners()
        {
            foreach (var button in FindObjectsOfType<Button>(true))
            {
                switch (button.name)
                {
                    case "GameBoothNav": button.onClick.AddListener(ShowGameBooth); break;
                    case "DressingNav": button.onClick.AddListener(ShowDressingRoom); break;
                    case "CreateRoom": button.onClick.AddListener(OnCreateRoom); break;
                    case "JoinRoom": button.onClick.AddListener(OnJoinRoom); break;
                    case "Ready": button.onClick.AddListener(OnToggleReady); break;
                    case "Start": button.onClick.AddListener(OnStartGame); break;
                    case "Leave": button.onClick.AddListener(OnLeaveRoom); break;
                    case "BackToBooth": button.onClick.AddListener(ShowGameBooth); break;
                    case "Outfit_经典蓝": button.onClick.AddListener(() => SelectOutfit("经典蓝")); break;
                    case "Outfit_活力橙": button.onClick.AddListener(() => SelectOutfit("活力橙")); break;
                    case "Outfit_清洁绿": button.onClick.AddListener(() => SelectOutfit("清洁绿")); break;
                }
            }
        }

        public void ShowGameBooth()
        {
            SetLobbyChromeActive(true);
            gameBoothPanel.SetActive(true);
            if (dressingRoomPanel != null) dressingRoomPanel.SetActive(false);
            RefreshView();
        }

        public void ShowDressingRoom()
        {
            if (dressingRoomPanel == null)
            {
                Debug.LogWarning("The standalone LobbyUI no longer contains a dressing panel. Return to DressingRoom instead.");
                return;
            }
            SetLobbyChromeActive(true);
            gameBoothPanel.SetActive(false);
            dressingRoomPanel.SetActive(true);
            RefreshView();
        }

        /// <summary>
        /// Opens the in-world dressing UI without requiring any network component.
        /// The panel is placed in the lower-right corner so the mirror view remains visible.
        /// </summary>
        public void OpenDressingPanel()
        {
            if (gameBoothPanel != null)
                gameBoothPanel.SetActive(false);
            HideMirrorPrompt();
            HideDoorPrompt();
            PlayerInput.Unlock();

            SetLobbyChromeActive(false);

            if (dressingRoomPanel == null)
            {
                Debug.LogWarning("DressingPanel is not assigned.");
                return;
            }

            var panelTransform = dressingRoomPanel.GetComponent<RectTransform>();
            if (panelTransform != null)
            {
                panelTransform.anchorMin = new Vector2(1f, 0f);
                panelTransform.anchorMax = new Vector2(1f, 0f);
                panelTransform.pivot = new Vector2(1f, 0f);
                panelTransform.anchoredPosition = MirrorDressingPanelOffset;
                panelTransform.sizeDelta = MirrorDressingPanelSize;
            }

            dressingRoomPanel.SetActive(true);
        }

        /// <summary>
        /// Hides the in-world dressing UI without requiring any network component.
        /// </summary>
        public void CloseDressingPanel()
        {
            if (dressingRoomPanel != null)
                dressingRoomPanel.SetActive(false);
            ShowMirrorPrompt();
        }

        public void OpenLobbyPanel()
        {
            if (dressingRoomPanel != null) dressingRoomPanel.SetActive(false);
            HideMirrorPrompt();
            HideDoorPrompt();
            PlayerInput.Unlock();
            if (gameBoothPanel == null)
            {
                Debug.LogWarning("LobbyPanel is not assigned.");
                return;
            }

            if (startInMirrorMode && !inWorldLobbyOpened)
            {
                mockData.ClearRoom();
                inWorldLobbyOpened = true;
            }
            MakeLobbyPanelFullscreen();
            gameBoothPanel.SetActive(true);
            RefreshView("联机大厅（Mock）：创建房间或输入房间号加入。");
        }

        public void CloseLobbyPanel()
        {
            if (gameBoothPanel != null) gameBoothPanel.SetActive(false);
            ShowDoorPrompt();
        }

        public void ShowMirrorPrompt()
        {
            if (!startInMirrorMode || mirrorPrompt == null) return;
            if (IsDressingPanelOpen) return;
            mirrorPrompt.SetActive(true);
        }

        public void HideMirrorPrompt()
        {
            if (mirrorPrompt != null) mirrorPrompt.SetActive(false);
        }

        public void ShowDoorPrompt()
        {
            if (!startInMirrorMode || doorPrompt == null || IsLobbyPanelOpen) return;
            doorPrompt.SetActive(true);
        }

        public void HideDoorPrompt()
        {
            if (doorPrompt != null) doorPrompt.SetActive(false);
        }

        public void SelectOutfit(int outfitId)
        {
            Debug.Log("玩家选择了衣服: " + outfitId);
        }

        private void SetLobbyChromeActive(bool active)
        {
            if (dressingRoomPanel == null || dressingRoomPanel.transform.parent == null) return;

            SetSiblingActive(dressingRoomPanel.transform.parent, "Background", active);
            SetSiblingActive(dressingRoomPanel.transform.parent, "Navigation", active);
        }

        private void MakeLobbyPanelFullscreen()
        {
            if (gameBoothPanel == null) return;

            var panelTransform = gameBoothPanel.GetComponent<RectTransform>();
            if (panelTransform == null) return;

            panelTransform.anchorMin = Vector2.zero;
            panelTransform.anchorMax = Vector2.one;
            panelTransform.pivot = new Vector2(.5f, .5f);
            panelTransform.anchoredPosition = Vector2.zero;
            panelTransform.sizeDelta = Vector2.zero;
        }

        private static void SetSiblingActive(Transform parent, string name, bool active)
        {
            var child = parent.Find(name);
            if (child != null) child.gameObject.SetActive(active);
        }

        public void OnCreateRoom()
        {
            mockData.CreateRoom("玩家1");
            if (roomCodeInput != null)
                roomCodeInput.text = mockData.RoomCode;
            RefreshView("房间已创建，房间号：" + mockData.RoomCode);
        }

        public void OnJoinRoom()
        {
            var requestedCode = roomCodeInput == null ? string.Empty : roomCodeInput.text.Trim();
            if (string.IsNullOrEmpty(requestedCode))
                requestedCode = mockData.RoomCode;

            string message;
            mockData.TryJoin(requestedCode, "访客玩家", out message);
            RefreshView(message);
        }

        public void OnToggleReady()
        {
            var ready = mockData.ToggleLocalReady();
            RefreshView(ready ? "你已准备" : "你已取消准备");
        }

        public void OnStartGame()
        {
            string message;
            mockData.TryStart(out message);
            RefreshView(message);
        }

        public void OnLeaveRoom()
        {
            mockData.Leave();
            RefreshView("你已离开房间");
        }

        /// <summary>
        /// Leaves the standalone lobby UI and returns to the 3D dressing room.
        /// </summary>
        public void ReturnToDressingRoom()
        {
            const string dressingRoomScenePath = "Assets/Scenes/DressingRoom.unity";
            if (!Application.CanStreamedLevelBeLoaded(dressingRoomScenePath))
            {
                Debug.LogError("DressingRoom is not in Build Settings: " + dressingRoomScenePath);
                return;
            }

            PlayerInput.Unlock();
            SceneManager.LoadScene(dressingRoomScenePath, LoadSceneMode.Single);
        }

        public void SelectOutfit(string outfit)
        {
            mockData.SetLocalOutfit(outfit);
            RefreshView("已选择外观：" + outfit);
        }

        private void RefreshView(string message = null)
        {
            if (roomCodeLabel != null)
                roomCodeLabel.text = mockData.HasRoom ? "房间号  " + mockData.RoomCode : "当前未加入房间";

            if (statusLabel != null && message != null)
                statusLabel.text = message;

            if (roomCodeInput != null && mockData.HasRoom && string.IsNullOrEmpty(roomCodeInput.text))
                roomCodeInput.text = mockData.RoomCode;

            for (var index = 0; index < playerSlotLabels.Length; index++)
            {
                if (index >= mockData.Players.Count)
                {
                    playerSlotLabels[index].text = "空位";
                    continue;
                }

                var player = mockData.Players[index];
                var host = player.IsHost ? " · 房主" : string.Empty;
                var ready = player.IsReady ? "已准备" : "未准备";
                playerSlotLabels[index].text = string.Format("{0}{1}\n{2}\n外观：{3}", player.Nickname, host, ready, player.Outfit);
            }

            var localIndex = mockData.LocalPlayerIndex;
            var localReady = localIndex >= 0 && mockData.Players[localIndex].IsReady;
            if (readyButton != null)
            {
                var label = readyButton.GetComponentInChildren<Text>();
                if (label != null) label.text = localReady ? "已准备" : "准备";
            }

            if (startButton != null)
                startButton.interactable = mockData.HasRoom && !mockData.GameStarted && mockData.Players.Count >= LobbyMockData.MaxPlayers;

            if (selectedOutfitLabel != null)
                selectedOutfitLabel.text = localIndex >= 0 ? "当前外观：" + mockData.Players[localIndex].Outfit : "当前外观：经典蓝";

            if (outfitPreview != null)
            {
                var outfit = localIndex >= 0 ? mockData.Players[localIndex].Outfit : "经典蓝";
                outfitPreview.color = outfitColors[outfit];
            }
        }
    }
}
