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
    /// <summary>
    /// Replaces the temporary in-lobby dressing button with a return route to the 3D DressingRoom scene.
    /// </summary>
    public static class LobbySceneNavigationBuilder
    {
        private const string ScenePath = "Assets/UI/Lobby/LobbyUI.unity";
        private const string TemporaryButtonName = "TestOpneButton";
        private const string ReturnButtonName = "ReturnToDressingRoom";

        [MenuItem("NatureCalls/Configure Lobby Return To DressingRoom")]
        public static void Configure()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("LobbyUI scene was not found.", ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
            var gameBoothPanel = GameObject.Find("GameBoothPanel");
            if (controller == null || gameBoothPanel == null)
                throw new InvalidOperationException("LobbyUI must contain LobbyUIController and GameBoothPanel.");

            var temporaryButton = GameObject.Find(TemporaryButtonName);
            if (temporaryButton != null)
                UnityEngine.Object.DestroyImmediate(temporaryButton);

            var returnButton = GameObject.Find(ReturnButtonName);
            if (returnButton == null)
            {
                returnButton = CreateButton(ReturnButtonName, gameBoothPanel.transform, "返回换装准备房");
                var rect = returnButton.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-36f, -36f);
                rect.sizeDelta = new Vector2(190f, 44f);

                var button = returnButton.GetComponent<Button>();
                UnityEventTools.AddPersistentListener(button.onClick, controller.ReturnToDressingRoom);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            Selection.activeGameObject = returnButton;
            Debug.Log("NatureCalls LobbyUI navigation configured: temporary dressing button removed and return button added.");
        }

        private static GameObject CreateButton(string name, Transform parent, string label)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(.22f, .27f, .34f, 1f);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);
            return root;
        }
    }
}
