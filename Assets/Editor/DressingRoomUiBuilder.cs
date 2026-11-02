using System;
using System.IO;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NatureCalls.Editor
{
    public static class DressingRoomUiBuilder
    {
        private const string ScenePath = "Assets/Scenes/DressingRoom.unity";
        private const string RootName = "DressingRoomUI";
        private static readonly Color Panel = new Color(.055f, .075f, .12f, .94f);
        private static readonly Color Accent = new Color(.25f, .72f, .95f);
        private static readonly Color Muted = new Color(.66f, .73f, .82f);

        [MenuItem("NatureCalls/Build Dressing Room UI")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Create the DressingRoom scene before building its UI.", ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find(RootName) != null)
                throw new InvalidOperationException("DressingRoomUI already exists. Refusing to overwrite existing UI work.");

            EnsureEventSystem();
            var canvas = CreateCanvas();
            var controller = new GameObject("DressingRoomUIController").AddComponent<LobbyUIController>();
            var lobbyPlaceholder = new GameObject("RoomLobbyPlaceholder");
            lobbyPlaceholder.SetActive(false);
            var prompt = CreateMirrorPrompt(canvas.transform, controller);
            var panel = CreateDressingPanel(canvas.transform, controller);
            AssignController(controller, lobbyPlaceholder, panel, prompt);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("NatureCalls DressingRoom UI created: " + ScenePath);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Canvas CreateCanvas()
        {
            var root = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        private static GameObject CreateMirrorPrompt(Transform parent, LobbyUIController controller)
        {
            var button = CreateButton("MirrorPrompt", parent, "按 E 换装\n（临时可点击）", new Color(.06f, .13f, .18f, .88f));
            SetRect(button.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 40f), new Vector2(250f, 62f));
            UnityEventTools.AddPersistentListener(button.onClick, controller.OpenDressingPanel);
            return button.gameObject;
        }

        private static GameObject CreateDressingPanel(Transform parent, LobbyUIController controller)
        {
            var panel = CreateImage("DressingRoomPanel", parent, Panel).gameObject;
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(640f, 480f));

            var title = CreateText("Title", panel.transform, "换装", 30, Color.white, TextAnchor.UpperLeft);
            SetTopLeft(title.rectTransform, 30f, -30f, 400f, 42f);
            var hint = CreateText("Hint", panel.transform, "选择外观后，可在镜子中查看效果。", 17, Muted, TextAnchor.UpperLeft);
            SetTopLeft(hint.rectTransform, 32f, -78f, 540f, 30f);
            var divider = CreateImage("Divider", panel.transform, new Color(Accent.r, Accent.g, Accent.b, .55f));
            SetTopLeft(divider.rectTransform, 30f, -118f, 580f, 2f);

            CreateOutfitButton(panel.transform, controller, 1, "经典蓝", new Color(.18f, .48f, .82f), -150f);
            CreateOutfitButton(panel.transform, controller, 2, "活力橙", new Color(.85f, .34f, .14f), -218f);
            CreateOutfitButton(panel.transform, controller, 3, "清洁绿", new Color(.18f, .65f, .36f), -286f);

            var close = CreateButton("DressingClose", panel.transform, "关闭", new Color(.22f, .27f, .34f));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(30f, 30f), new Vector2(130f, 44f));
            UnityEventTools.AddPersistentListener(close.onClick, controller.CloseDressingPanel);

            panel.SetActive(false);
            return panel;
        }

        private static void CreateOutfitButton(Transform parent, LobbyUIController controller, int outfitId, string label, Color color, float y)
        {
            var button = CreateButton("DressingOption" + outfitId, parent, label, color);
            SetTopLeft(button.GetComponent<RectTransform>(), 32f, y, 576f, 50f);
            UnityEventTools.AddIntPersistentListener(button.onClick, controller.SelectOutfit, outfitId);
        }

        private static void AssignController(LobbyUIController controller, GameObject lobbyPlaceholder, GameObject panel, GameObject prompt)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("gameBoothPanel").objectReferenceValue = lobbyPlaceholder;
            serialized.FindProperty("dressingRoomPanel").objectReferenceValue = panel;
            serialized.FindProperty("startInMirrorMode").boolValue = true;
            serialized.FindProperty("mirrorPrompt").objectReferenceValue = prompt;
            serialized.FindProperty("playerSlotLabels").arraySize = 0;
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
            var text = CreateText("Label", image.transform, label, 18, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8f, 4f, 8f, 4f);
            return button;
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
