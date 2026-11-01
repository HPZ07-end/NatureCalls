using System;
using System.IO;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.Editor
{
    public static class DressingRoomLobbyBuilder
    {
        private const string ScenePath = "Assets/Scenes/DressingRoom.unity";
        private const string RootName = "DressingRoomLobbyUI";
        private static readonly Color Panel = new Color(.055f, .075f, .12f, 1f);
        private static readonly Color Accent = new Color(.25f, .72f, .95f);
        private static readonly Color Muted = new Color(.66f, .73f, .82f);

        [MenuItem("NatureCalls/Build Dressing Room Lobby UI")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Create the DressingRoom scene before building its lobby UI.", ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find(RootName) != null)
                throw new InvalidOperationException("DressingRoomLobbyUI already exists. Refusing to overwrite existing UI work.");

            var canvas = GameObject.Find("DressingRoomUI")?.GetComponent<Canvas>();
            var controller = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
            if (canvas == null || controller == null)
                throw new InvalidOperationException("Build the DressingRoom UI before building its lobby UI.");

            var root = new GameObject(RootName);
            root.transform.SetParent(canvas.transform, false);
            var doorPrompt = CreateDoorPrompt(root.transform, controller);
            var lobby = CreateLobbyPanel(root.transform, controller, out var roomInput, out var roomLabel, out var status,
                out var playerLabels, out var ready, out var start);
            AssignController(controller, lobby, doorPrompt, roomInput, roomLabel, status, playerLabels, ready, start);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            Selection.activeGameObject = root;
            Debug.Log("NatureCalls DressingRoom lobby UI created: " + ScenePath);
        }

        private static GameObject CreateDoorPrompt(Transform parent, LobbyUIController controller)
        {
            var button = CreateButton("DoorPrompt", parent, "按 E 进入联机大厅\n（临时可点击）", new Color(.13f, .08f, .05f, .9f));
            SetRect(button.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 116f), new Vector2(280f, 62f));
            UnityEventTools.AddPersistentListener(button.onClick, controller.OpenLobbyPanel);
            button.gameObject.SetActive(false);
            return button.gameObject;
        }

        private static GameObject CreateLobbyPanel(Transform parent, LobbyUIController controller, out InputField roomInput,
            out Text roomLabel, out Text status, out Text[] playerLabels, out Button ready, out Button start)
        {
            var panel = CreateImage("RoomLobbyPanel", parent, Panel).gameObject;
            SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero);
            var title = CreateText("Title", panel.transform, "联机大厅", 30, Color.white, TextAnchor.UpperLeft);
            SetTopLeft(title.rectTransform, 30f, -30f, 350f, 42f);
            var subtitle = CreateText("Subtitle", panel.transform, "房间满 4 人后自动开始", 17, Muted, TextAnchor.UpperLeft);
            SetTopLeft(subtitle.rectTransform, 32f, -78f, 450f, 28f);

            roomLabel = CreateText("RoomCode", panel.transform, "当前未加入房间", 19, Accent, TextAnchor.UpperLeft);
            SetTopLeft(roomLabel.rectTransform, 32f, -122f, 380f, 30f);
            roomInput = CreateInput("JoinCodeInput", panel.transform, "输入房间号", string.Empty);
            SetTopLeft(roomInput.GetComponent<RectTransform>(), 32f, -166f, 270f, 42f);
            var create = CreateButton("LobbyCreate", panel.transform, "创建房间", Accent);
            SetTopLeft(create.GetComponent<RectTransform>(), 318f, -166f, 130f, 42f);
            var join = CreateButton("LobbyJoin", panel.transform, "加入", new Color(.25f, .4f, .58f));
            SetTopLeft(join.GetComponent<RectTransform>(), 462f, -166f, 130f, 42f);
            UnityEventTools.AddPersistentListener(create.onClick, controller.OnCreateRoom);
            UnityEventTools.AddPersistentListener(join.onClick, controller.OnJoinRoom);

            var playersTitle = CreateText("PlayersTitle", panel.transform, "等待玩家", 18, Muted, TextAnchor.UpperLeft);
            SetTopLeft(playersTitle.rectTransform, 32f, -230f, 260f, 28f);
            playerLabels = new Text[4];
            for (var index = 0; index < playerLabels.Length; index++)
            {
                var slot = CreateImage("LobbyPlayerSlot" + (index + 1), panel.transform, new Color(.12f, .17f, .25f, .96f));
                SetTopLeft(slot.rectTransform, 32f + (index % 2) * 290f, -270f - (index / 2) * 84f, 270f, 68f);
                playerLabels[index] = CreateText("Label", slot.transform, "空位", 16, Color.white, TextAnchor.MiddleLeft);
                Stretch(playerLabels[index].rectTransform, 14f, 8f, 8f, 8f);
            }

            ready = CreateButton("LobbyReady", panel.transform, "准备（Mock）", new Color(.25f, .68f, .42f));
            SetRect(ready.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32f, 28f), new Vector2(144f, 42f));
            start = CreateButton("LobbyStart", panel.transform, "满 4 人自动开始", new Color(.55f, .33f, .2f));
            SetRect(start.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(190f, 28f), new Vector2(190f, 42f));
            var close = CreateButton("LobbyClose", panel.transform, "返回准备房", new Color(.22f, .27f, .34f));
            SetRect(close.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(394f, 28f), new Vector2(166f, 42f));
            UnityEventTools.AddPersistentListener(ready.onClick, controller.OnToggleReady);
            UnityEventTools.AddPersistentListener(start.onClick, controller.OnStartGame);
            UnityEventTools.AddPersistentListener(close.onClick, controller.CloseLobbyPanel);

            status = CreateText("Status", panel.transform, "创建或加入房间后等待其他玩家。", 16, Muted, TextAnchor.UpperLeft);
            SetRect(status.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32f, 88f), new Vector2(550f, 28f));
            panel.SetActive(false);
            return panel;
        }

        private static void AssignController(LobbyUIController controller, GameObject lobby, GameObject doorPrompt, InputField roomInput,
            Text roomLabel, Text status, Text[] playerLabels, Button ready, Button start)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("gameBoothPanel").objectReferenceValue = lobby;
            serialized.FindProperty("doorPrompt").objectReferenceValue = doorPrompt;
            serialized.FindProperty("roomCodeInput").objectReferenceValue = roomInput;
            serialized.FindProperty("roomCodeLabel").objectReferenceValue = roomLabel;
            serialized.FindProperty("statusLabel").objectReferenceValue = status;
            serialized.FindProperty("readyButton").objectReferenceValue = ready;
            serialized.FindProperty("startButton").objectReferenceValue = start;
            var array = serialized.FindProperty("playerSlotLabels");
            array.arraySize = playerLabels.Length;
            for (var index = 0; index < playerLabels.Length; index++)
                array.GetArrayElementAtIndex(index).objectReferenceValue = playerLabels[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var image = CreateImage(name, parent, color);
            var button = image.gameObject.AddComponent<Button>();
            var text = CreateText("Label", image.transform, label, 17, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8f, 4f, 8f, 4f);
            return button;
        }

        private static InputField CreateInput(string name, Transform parent, string placeholder, string value)
        {
            var image = CreateImage(name, parent, new Color(.15f, .2f, .3f, .96f));
            var input = image.gameObject.AddComponent<InputField>();
            input.text = value;
            input.textComponent = CreateText("Text", image.transform, value, 16, Color.white, TextAnchor.MiddleLeft);
            Stretch(input.textComponent.rectTransform, 12f, 6f, 12f, 6f);
            var placeholderText = CreateText("Placeholder", image.transform, placeholder, 16, Muted, TextAnchor.MiddleLeft);
            Stretch(placeholderText.rectTransform, 12f, 6f, 12f, 6f);
            input.placeholder = placeholderText;
            return input;
        }

        private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
