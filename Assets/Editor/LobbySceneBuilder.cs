using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NatureCalls.UI;

namespace NatureCalls.Editor
{
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/UI/Lobby/LobbyUI.unity";
        private static readonly Color Background = new Color(0.055f, 0.075f, 0.12f);
        private static readonly Color Panel = new Color(0.10f, 0.14f, 0.22f);
        private static readonly Color Accent = new Color(0.25f, 0.72f, 0.95f);
        private static readonly Color Muted = new Color(0.65f, 0.72f, 0.82f);

        [MenuItem("NatureCalls/Build Lobby UI Mock")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasObject = new GameObject("LobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateImage("Background", canvasObject.transform, Background);
            Stretch(background.rectTransform);

            var title = CreateText("Title", canvasObject.transform, "NATURE CALLS  ·  大厅", 36, Color.white, TextAnchor.UpperLeft);
            SetRect(title.rectTransform, 64, -50, 600, 54, new Vector2(0, 1), new Vector2(0, 1));
            var subtitle = CreateText("Subtitle", canvasObject.transform, "选择隔间，准备和队友开始一局合作游戏", 18, Muted, TextAnchor.UpperLeft);
            SetRect(subtitle.rectTransform, 68, -105, 680, 32, new Vector2(0, 1), new Vector2(0, 1));

            var navigation = CreateImage("Navigation", canvasObject.transform, new Color(0.07f, 0.10f, 0.16f));
            SetRect(navigation.rectTransform, 48, 34, 220, 690, new Vector2(0, 0), new Vector2(0, 0));
            var boothNav = CreateButton("GameBoothNav", navigation.transform, "游戏隔间", Accent);
            SetRect(boothNav.GetComponent<RectTransform>(), 20, -45, 180, 56, new Vector2(0, 1), new Vector2(0, 1));
            var dressingNav = CreateButton("DressingNav", navigation.transform, "换装隔间", new Color(0.20f, 0.27f, 0.38f));
            SetRect(dressingNav.GetComponent<RectTransform>(), 20, -115, 180, 56, new Vector2(0, 1), new Vector2(0, 1));

            var controllerObject = new GameObject("LobbyUIController");
            var controller = controllerObject.AddComponent<LobbyUIController>();

            var gamePanel = CreateImage("GameBoothPanel", canvasObject.transform, Panel).gameObject;
            SetRect(gamePanel.GetComponent<RectTransform>(), 300, 34, -48, -98, new Vector2(0, 0), new Vector2(1, 1));
            var gameTitle = CreateText("PanelTitle", gamePanel.transform, "游戏隔间", 30, Color.white, TextAnchor.UpperLeft);
            SetRect(gameTitle.rectTransform, 36, -36, 500, 44, new Vector2(0, 1), new Vector2(0, 1));
            var roomLabel = CreateText("RoomCode", gamePanel.transform, "房间号  NC42", 20, Accent, TextAnchor.UpperLeft);
            SetRect(roomLabel.rectTransform, 38, -92, 300, 36, new Vector2(0, 1), new Vector2(0, 1));

            var input = CreateInput("RoomCodeInput", gamePanel.transform, "输入房间号（Mock）", "NC42");
            SetRect(input.GetComponent<RectTransform>(), 360, -88, 230, 44, new Vector2(0, 1), new Vector2(0, 1));
            var create = CreateButton("CreateRoom", gamePanel.transform, "创建房间", Accent);
            SetRect(create.GetComponent<RectTransform>(), 38, -150, 165, 48, new Vector2(0, 1), new Vector2(0, 1));
            var join = CreateButton("JoinRoom", gamePanel.transform, "加入房间", new Color(0.25f, 0.40f, 0.58f));
            SetRect(join.GetComponent<RectTransform>(), 220, -150, 165, 48, new Vector2(0, 1), new Vector2(0, 1));

            var playersTitle = CreateText("PlayersTitle", gamePanel.transform, "玩家（最多 4 人）", 19, Muted, TextAnchor.UpperLeft);
            SetRect(playersTitle.rectTransform, 38, -225, 350, 32, new Vector2(0, 1), new Vector2(0, 1));
            var playerLabels = new Text[4];
            for (var i = 0; i < 4; i++)
            {
                var slot = CreateImage("PlayerSlot" + (i + 1), gamePanel.transform, new Color(0.13f, 0.18f, 0.28f));
                SetRect(slot.rectTransform, 38 + (i % 2) * 250, -275 - (i / 2) * 100, 220, 78, new Vector2(0, 1), new Vector2(0, 1));
                playerLabels[i] = CreateText("PlayerLabel", slot.transform, "空位", 16, Color.white, TextAnchor.MiddleLeft);
                Stretch(playerLabels[i].rectTransform, 14, 8, 8, 8);
            }

            var ready = CreateButton("Ready", gamePanel.transform, "准备", new Color(0.25f, 0.68f, 0.42f));
            SetRect(ready.GetComponent<RectTransform>(), 38, 38, 165, 48, new Vector2(0, 0), new Vector2(0, 0));
            var start = CreateButton("Start", gamePanel.transform, "开始游戏", new Color(0.76f, 0.38f, 0.24f));
            SetRect(start.GetComponent<RectTransform>(), 220, 38, 165, 48, new Vector2(0, 0), new Vector2(0, 0));
            var leave = CreateButton("Leave", gamePanel.transform, "离开房间", new Color(0.26f, 0.30f, 0.38f));
            SetRect(leave.GetComponent<RectTransform>(), 402, 38, 165, 48, new Vector2(0, 0), new Vector2(0, 0));
            var status = CreateText("Status", gamePanel.transform, "欢迎来到大厅", 16, Muted, TextAnchor.UpperLeft);
            SetRect(status.rectTransform, 38, 98, 590, 40, new Vector2(0, 0), new Vector2(0, 0));

            var dressingPanel = CreateImage("DressingRoomPanel", canvasObject.transform, Panel).gameObject;
            SetRect(dressingPanel.GetComponent<RectTransform>(), 300, 34, -48, -98, new Vector2(0, 0), new Vector2(1, 1));
            dressingPanel.SetActive(false);
            var dressingTitle = CreateText("PanelTitle", dressingPanel.transform, "换装隔间", 30, Color.white, TextAnchor.UpperLeft);
            SetRect(dressingTitle.rectTransform, 36, -36, 500, 44, new Vector2(0, 1), new Vector2(0, 1));
            var preview = CreateImage("OutfitPreview", dressingPanel.transform, Accent);
            SetRect(preview.rectTransform, 42, -120, 250, 250, new Vector2(0, 1), new Vector2(0, 1));
            var previewHint = CreateText("PreviewHint", dressingPanel.transform, "角色外观预览（Mock）", 16, Color.white, TextAnchor.MiddleCenter);
            Stretch(previewHint.rectTransform, 10, 10, 10, 10);
            var selected = CreateText("SelectedOutfit", dressingPanel.transform, "当前外观：经典蓝", 20, Color.white, TextAnchor.UpperLeft);
            SetRect(selected.rectTransform, 340, -120, 360, 36, new Vector2(0, 1), new Vector2(0, 1));
            CreateOutfitButton(dressingPanel.transform, "经典蓝", Accent, 340, -180);
            CreateOutfitButton(dressingPanel.transform, "活力橙", new Color(0.95f, 0.42f, 0.18f), 340, -240);
            CreateOutfitButton(dressingPanel.transform, "清洁绿", new Color(0.25f, 0.78f, 0.43f), 340, -300);
            var back = CreateButton("BackToBooth", dressingPanel.transform, "返回游戏隔间", new Color(0.25f, 0.40f, 0.58f));
            SetRect(back.GetComponent<RectTransform>(), 42, 42, 210, 48, new Vector2(0, 0), new Vector2(0, 0));

            AssignControllerReferences(controller, gamePanel, dressingPanel, input, roomLabel, status, playerLabels, ready, start, selected, preview);
            AddClick(boothNav, controller, nameof(LobbyUIController.ShowGameBooth));
            AddClick(dressingNav, controller, nameof(LobbyUIController.ShowDressingRoom));
            AddClick(create, controller, nameof(LobbyUIController.OnCreateRoom));
            AddClick(join, controller, nameof(LobbyUIController.OnJoinRoom));
            AddClick(ready, controller, nameof(LobbyUIController.OnToggleReady));
            AddClick(start, controller, nameof(LobbyUIController.OnStartGame));
            AddClick(leave, controller, nameof(LobbyUIController.OnLeaveRoom));
            AddClick(back, controller, nameof(LobbyUIController.ShowGameBooth));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = controllerObject;
            Debug.Log("NatureCalls lobby UI created: " + ScenePath);
        }

        private static void AssignControllerReferences(LobbyUIController controller, GameObject gamePanel, GameObject dressingPanel, InputField input, Text roomLabel, Text status, Text[] players, Button ready, Button start, Text selected, Image preview)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("gameBoothPanel").objectReferenceValue = gamePanel;
            serialized.FindProperty("dressingRoomPanel").objectReferenceValue = dressingPanel;
            serialized.FindProperty("roomCodeInput").objectReferenceValue = input;
            serialized.FindProperty("roomCodeLabel").objectReferenceValue = roomLabel;
            serialized.FindProperty("statusLabel").objectReferenceValue = status;
            serialized.FindProperty("readyButton").objectReferenceValue = ready;
            serialized.FindProperty("startButton").objectReferenceValue = start;
            serialized.FindProperty("selectedOutfitLabel").objectReferenceValue = selected;
            serialized.FindProperty("outfitPreview").objectReferenceValue = preview;
            var array = serialized.FindProperty("playerSlotLabels");
            array.arraySize = players.Length;
            for (var i = 0; i < players.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = players[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateOutfitButton(Transform parent, string outfit, Color color, float x, float y)
        {
            var button = CreateButton("Outfit_" + outfit, parent, outfit, color);
            SetRect(button.GetComponent<RectTransform>(), x, y, 220, 48, new Vector2(0, 1), new Vector2(0, 1));
            var controller = Object.FindObjectOfType<LobbyUIController>();
            AddClick(button, controller, nameof(LobbyUIController.SelectOutfit), outfit);
        }

        private static void AddClick(Button button, LobbyUIController target, string method, string argument = null)
        {
            if (argument == null) button.onClick.AddListener(() => target.SendMessage(method));
            else button.onClick.AddListener(() => target.SelectOutfit(argument));
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var image = CreateImage(name, parent, color);
            var button = image.gameObject.AddComponent<Button>();
            var text = CreateText("Label", image.transform, label, 17, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8, 4, 8, 4);
            return button;
        }

        private static InputField CreateInput(string name, Transform parent, string placeholder, string value)
        {
            var image = CreateImage(name, parent, new Color(0.15f, 0.20f, 0.30f));
            var input = image.gameObject.AddComponent<InputField>();
            input.text = value;
            input.textComponent = CreateText("Text", image.transform, value, 16, Color.white, TextAnchor.MiddleLeft);
            Stretch(input.textComponent.rectTransform, 12, 6, 12, 6);
            var placeholderText = CreateText("Placeholder", image.transform, placeholder, 16, Muted, TextAnchor.MiddleLeft);
            Stretch(placeholderText.rectTransform, 12, 6, 12, 6);
            input.placeholder = placeholderText;
            return input;
        }

        private static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == Vector2.zero ? Vector2.zero : new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
