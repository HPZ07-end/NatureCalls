using System;
using System.IO;
using NatureCalls.Map;
using NatureCalls.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.Editor
{
    /// <summary>
    /// Migrates the existing DressingRoom door from its temporary UI overlay to the standalone LobbyUI scene.
    /// </summary>
    public static class DressingRoomLobbySceneMigration
    {
        private const string DressingRoomScenePath = "Assets/Scenes/DressingRoom.unity";

        [MenuItem("NatureCalls/Migrate Dressing Room Door To Lobby Scene")]
        public static void Migrate()
        {
            if (!File.Exists(DressingRoomScenePath))
                throw new FileNotFoundException("DressingRoom scene was not found.", DressingRoomScenePath);
            if (!File.Exists(LobbySceneNavigator.LobbyScenePath))
                throw new FileNotFoundException("LobbyUI scene was not found.", LobbySceneNavigator.LobbyScenePath);

            var scene = EditorSceneManager.OpenScene(DressingRoomScenePath, OpenSceneMode.Single);
            var door = UnityEngine.Object.FindFirstObjectByType<LobbyDoor>();
            if (door == null)
                throw new InvalidOperationException("DressingRoom has no LobbyDoor to migrate.");

            var navigator = door.GetComponent<LobbySceneNavigator>();
            if (navigator == null)
                navigator = door.gameObject.AddComponent<LobbySceneNavigator>();

            for (var index = door.Interacted.GetPersistentEventCount() - 1; index >= 0; index--)
            {
                if (door.Interacted.GetPersistentTarget(index) is LobbyUIController &&
                    door.Interacted.GetPersistentMethodName(index) == nameof(LobbyUIController.OpenLobbyPanel))
                    UnityEventTools.RemovePersistentListener(door.Interacted, index);
            }

            var hasNavigatorListener = false;
            for (var index = 0; index < door.Interacted.GetPersistentEventCount(); index++)
            {
                if (door.Interacted.GetPersistentTarget(index) == navigator &&
                    door.Interacted.GetPersistentMethodName(index) == nameof(LobbySceneNavigator.EnterLobby))
                {
                    hasNavigatorListener = true;
                    break;
                }
            }
            if (!hasNavigatorListener)
                UnityEventTools.AddPersistentListener(door.Interacted, navigator.EnterLobby);

            ReplaceDoorPromptListener(navigator);
            EnsureLobbySceneInBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + DressingRoomScenePath);
            Debug.Log("NatureCalls DressingRoom door now loads the standalone LobbyUI scene.");
        }

        private static void ReplaceDoorPromptListener(LobbySceneNavigator navigator)
        {
            var prompt = FindDoorPrompt();
            if (prompt == null) return;

            for (var index = prompt.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            {
                if (prompt.onClick.GetPersistentTarget(index) is LobbyUIController &&
                    prompt.onClick.GetPersistentMethodName(index) == nameof(LobbyUIController.OpenLobbyPanel))
                    UnityEventTools.RemovePersistentListener(prompt.onClick, index);
            }

            var hasNavigatorListener = false;
            for (var index = 0; index < prompt.onClick.GetPersistentEventCount(); index++)
            {
                if (prompt.onClick.GetPersistentTarget(index) == navigator &&
                    prompt.onClick.GetPersistentMethodName(index) == nameof(LobbySceneNavigator.EnterLobby))
                {
                    hasNavigatorListener = true;
                    break;
                }
            }
            if (!hasNavigatorListener)
                UnityEventTools.AddPersistentListener(prompt.onClick, navigator.EnterLobby);
        }

        private static Button FindDoorPrompt()
        {
            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.name == "DoorPrompt") return button;
            }
            return null;
        }

        private static void EnsureLobbySceneInBuildSettings()
        {
            foreach (var buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene.path == LobbySceneNavigator.LobbyScenePath)
                    return;
            }

            var scenes = new EditorBuildSettingsScene[EditorBuildSettings.scenes.Length + 1];
            Array.Copy(EditorBuildSettings.scenes, scenes, EditorBuildSettings.scenes.Length);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(LobbySceneNavigator.LobbyScenePath, true);
            EditorBuildSettings.scenes = scenes;
        }
    }
}
