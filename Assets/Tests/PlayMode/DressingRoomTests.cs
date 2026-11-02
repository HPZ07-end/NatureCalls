using System.Collections;
using NUnit.Framework;
using NatureCalls.Map;
using NatureCalls.Player;
using NatureCalls.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NatureCalls.Tests
{
    public sealed class DressingRoomTests
    {
        private Scene scene;
        private Playground world;
        private DressingMirror mirror;
        private LobbyDoor door;
        private LobbySceneNavigator lobbyNavigator;
        private LobbyUIController dressingUi;

        [UnitySetUp]
        public IEnumerator Load()
        {
            world = null;
            mirror = null;
            door = null;
            lobbyNavigator = null;
            dressingUi = null;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/DressingRoom.unity", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath("Assets/Scenes/DressingRoom.unity");
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out Playground foundWorld)) world = foundWorld;
                mirror ??= root.GetComponentInChildren<DressingMirror>(true);
                door ??= root.GetComponentInChildren<LobbyDoor>(true);
                lobbyNavigator ??= root.GetComponentInChildren<LobbySceneNavigator>(true);
                dressingUi ??= root.GetComponentInChildren<LobbyUIController>(true);
            }
            yield return null;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.OfflinePlayer, Is.Not.Null);
            Assert.That(mirror, Is.Not.Null);
            Assert.That(door, Is.Not.Null);
            Assert.That(lobbyNavigator, Is.Not.Null);
            Assert.That(dressingUi, Is.Not.Null);
            world.OfflinePlayer.GetComponent<NatureCalls.Input.PlayerInput>().enabled = false;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (world != null) world.StopPractice();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MirrorRequiresProximityAndInvokesItsInteraction()
        {
            var player = world.OfflinePlayer;
            bool invoked = false;
            mirror.Interacted.AddListener(() => invoked = true);

            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            Assert.That(input.TryInteract(), Is.False);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = mirror.transform.position - Vector3.up;
            controller.enabled = true;
            yield return null;

            Assert.That(input.TryInteract(), Is.True);
            Assert.That(invoked, Is.True);
            Assert.That(dressingUi.IsDressingPanelOpen, Is.True);
            dressingUi.CloseDressingPanel();
            Assert.That(dressingUi.IsDressingPanelOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator DoorRequiresProximityAndTargetsTheStandaloneLobbyScene()
        {
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            Assert.That(input.TryInteract(), Is.False);

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = door.transform.position - Vector3.up;
            controller.enabled = true;
            yield return null;

            Assert.That(door.CanInteract(player.GetComponent<Movement>()), Is.True);
            Assert.That(Application.CanStreamedLevelBeLoaded(LobbySceneNavigator.LobbyScenePath), Is.True);
            Assert.That(HasLobbyNavigatorListener(), Is.True);
            Assert.That(FindDoorPromptLabel(), Is.EqualTo("按 E 进入联机大厅"));
        }

        private string FindDoorPromptLabel()
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "DoorPrompt")
                        return button.GetComponentInChildren<Text>(true)?.text;
                }
            }
            return null;
        }

        private bool HasLobbyNavigatorListener()
        {
            for (var index = 0; index < door.Interacted.GetPersistentEventCount(); index++)
            {
                if (door.Interacted.GetPersistentTarget(index) == lobbyNavigator &&
                    door.Interacted.GetPersistentMethodName(index) == nameof(LobbySceneNavigator.EnterLobby))
                    return true;
            }
            return false;
        }

        [Test]
        public void MockLobbyAutomaticallyStartsOnTheFourthPlayer()
        {
            var lobby = new LobbyMockData();
            lobby.ClearRoom();
            lobby.CreateRoom("玩家1");
            for (var player = 2; player <= LobbyMockData.MaxPlayers; player++)
                Assert.That(lobby.TryJoin(lobby.RoomCode, "玩家" + player, out _), Is.True);

            Assert.That(lobby.Players, Has.Count.EqualTo(LobbyMockData.MaxPlayers));
            Assert.That(lobby.GameStarted, Is.True);
        }
    }
}
