using System;
using System.IO;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Removes the legacy Lobby dressing page and keeps the return-to-DressingRoom action visible at all times.
    /// </summary>
    public static class LobbySceneNavigationCleanup
    {
        private const string ScenePath = "Assets/UI/Lobby/LobbyUI.unity";

        [MenuItem("NatureCalls/Clean Up Lobby Navigation")]
        public static void CleanUp()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("LobbyUI scene was not found.", ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = GameObject.Find("LobbyCanvas");
            var controller = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
            var returnButton = GameObject.Find("ReturnToDressingRoom");
            if (canvas == null || controller == null || returnButton == null)
                throw new InvalidOperationException("LobbyUI is missing its canvas, controller, or return button.");

            DestroyIfPresent("DressingNav");
            DestroyIfPresent("DressingRoomPanel");

            returnButton.transform.SetParent(canvas.transform, false);
            var rect = returnButton.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-48f, -42f);
            rect.sizeDelta = new Vector2(190f, 44f);
            returnButton.transform.SetAsLastSibling();

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("dressingRoomPanel").objectReferenceValue = null;
            serialized.FindProperty("selectedOutfitLabel").objectReferenceValue = null;
            serialized.FindProperty("outfitPreview").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            Selection.activeGameObject = returnButton;
            Debug.Log("NatureCalls LobbyUI cleaned up: legacy dressing page removed and return button made persistent.");
        }

        private static void DestroyIfPresent(string objectName)
        {
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == objectName)
                {
                    UnityEngine.Object.DestroyImmediate(transform.gameObject);
                    return;
                }
            }
        }
    }
}
