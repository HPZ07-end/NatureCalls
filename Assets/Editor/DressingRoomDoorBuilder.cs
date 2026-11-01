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
    public static class DressingRoomDoorBuilder
    {
        private const string ScenePath = "Assets/Scenes/DressingRoom.unity";
        private const string ZoneName = "Door Interaction Zone";

        [MenuItem("NatureCalls/Build Dressing Room Door Interaction")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Create the DressingRoom scene before building its door interaction.", ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find(ZoneName) != null)
                throw new InvalidOperationException("Door Interaction Zone already exists. Refusing to overwrite existing interaction work.");

            var doorRoot = GameObject.Find("Door");
            var controller = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
            if (doorRoot == null || controller == null)
                throw new InvalidOperationException("Build the DressingRoom blockout and UI before building its door interaction.");

            var zone = new GameObject(ZoneName);
            zone.transform.SetParent(doorRoot.transform, false);
            zone.transform.position = new Vector3(-4.25f, 1f, 2.75f);
            var collider = zone.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.65f;
            var body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var door = zone.AddComponent<LobbyDoor>();
            var navigator = zone.AddComponent<LobbySceneNavigator>();
            door.InteractionDistance = 1.65f;
            UnityEventTools.AddPersistentListener(door.PlayerEntered, controller.ShowDoorPrompt);
            UnityEventTools.AddPersistentListener(door.PlayerExited, controller.HideDoorPrompt);
            UnityEventTools.AddPersistentListener(door.Interacted, navigator.EnterLobby);
            ReplaceDoorPromptListener(navigator);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save " + ScenePath);
            Selection.activeGameObject = zone;
            Debug.Log("NatureCalls DressingRoom door interaction created: " + ScenePath);
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
    }
}
