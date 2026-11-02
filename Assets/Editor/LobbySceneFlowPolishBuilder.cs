using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Removes obsolete one-item navigation and finalizes the DressingRoom door instruction.
    /// </summary>
    public static class LobbySceneFlowPolishBuilder
    {
        private const string LobbyScenePath = "Assets/UI/Lobby/LobbyUI.unity";
        private const string DressingRoomScenePath = "Assets/Scenes/DressingRoom.unity";

        [MenuItem("NatureCalls/Polish Lobby Flow UI")]
        public static void Polish()
        {
            PolishLobbyScene();
            PolishDressingRoomScene();
        }

        private static void PolishLobbyScene()
        {
            if (!File.Exists(LobbyScenePath))
                throw new FileNotFoundException("LobbyUI scene was not found.", LobbyScenePath);

            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            var navigation = GameObject.Find("Navigation");
            if (navigation != null) UnityEngine.Object.DestroyImmediate(navigation);

            var gameBoothPanel = GameObject.Find("GameBoothPanel");
            if (gameBoothPanel == null)
                throw new InvalidOperationException("LobbyUI is missing GameBoothPanel.");

            var panelRect = gameBoothPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(48f, 34f);
            panelRect.offsetMax = new Vector2(-48f, -150f);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + LobbyScenePath);
        }

        private static void PolishDressingRoomScene()
        {
            if (!File.Exists(DressingRoomScenePath))
                throw new FileNotFoundException("DressingRoom scene was not found.", DressingRoomScenePath);

            var scene = EditorSceneManager.OpenScene(DressingRoomScenePath, OpenSceneMode.Single);
            var prompt = FindInactiveButton("DoorPrompt");
            if (prompt == null)
                throw new InvalidOperationException("DressingRoom is missing DoorPrompt.");

            var label = prompt.GetComponentInChildren<Text>(true);
            if (label == null)
                throw new InvalidOperationException("DoorPrompt is missing its label.");
            label.text = "按 E 进入联机大厅";

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + DressingRoomScenePath);
        }

        private static Button FindInactiveButton(string objectName)
        {
            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.name == objectName) return button;
            }
            return null;
        }
    }
}
