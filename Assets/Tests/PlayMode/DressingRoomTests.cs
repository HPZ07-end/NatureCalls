using System.Collections;
using NUnit.Framework;
using NatureCalls.Map;
using NatureCalls.Network;
using NatureCalls.Player;
using NatureCalls.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Avatar = NatureCalls.Network.Avatar;

namespace NatureCalls.Tests
{
    public sealed class DressingRoomTests
    {
        private const string ScenePath = "Assets/Scenes/DressingRoom.unity";
        private Scene scene;
        private Playground world;
        private DressingMirror mirror;
        private LobbyDoor door;
        private LobbyController lobby;
        private LobbyView view;
        private int savedOutfit;
        private MapCatalog timedCatalog;
        private float savedRoundSeconds;

        [UnitySetUp]
        public IEnumerator Load()
        {
            savedOutfit = LocalProfile.OutfitIndex;
            Match.End();
            world = null; mirror = null; door = null; lobby = null;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out Playground foundWorld)) world = foundWorld;
                mirror ??= root.GetComponentInChildren<DressingMirror>(true);
                door ??= root.GetComponentInChildren<LobbyDoor>(true);
                lobby ??= root.GetComponentInChildren<LobbyController>(true);
            }
            yield return null;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.OfflinePlayer, Is.Not.Null);
            Assert.That(mirror, Is.Not.Null);
            Assert.That(door, Is.Not.Null);
            Assert.That(lobby, Is.Not.Null);
            view = lobby.GetComponent<LobbyView>();
            world.OfflinePlayer.GetComponent<NatureCalls.Input.PlayerInput>().enabled = false;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            LocalProfile.OutfitIndex = savedOutfit;
            if (timedCatalog != null) { timedCatalog[0].RoundSeconds = savedRoundSeconds; timedCatalog = null; }
            Match.End();
            var session = Session.Instance;
            if (session != null)
            {
                if (session.Active)
                {
                    var leaving = session.LeaveAsync();
                    yield return new WaitUntil(() => leaving.IsCompleted);
                }
                Object.Destroy(session.gameObject);
            }
            if (world != null) world.StopPractice();
            // Match flows load scenes in Single mode; leave only an empty scene behind for the next test.
            var empty = SceneManager.CreateScene("DressingRoomTestsEmpty" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var loaded = SceneManager.GetSceneAt(index);
                if (loaded != empty && loaded.isLoaded && (loaded.path == ScenePath || loaded.path == "Assets/Scenes/Playground.unity"))
                    yield return SceneManager.UnloadSceneAsync(loaded);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MirrorOutfitChangesAppearanceAndIsRemembered()
        {
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            Assert.That(input.TryInteract(), Is.False, "Nothing is in reach at the spawn point.");
            yield return Teleport(player, mirror.transform.position - Vector3.up);

            Assert.That(input.TryInteract(), Is.True);
            Assert.That(lobby.IsOutfitPanelOpen, Is.True);
            Assert.That(player.Controlled, Is.False, "Choosing an outfit pauses movement.");
            var appearance = player.GetComponent<PlayerAppearance>();
            Assert.That(lobby.OutfitButtons, Has.Count.EqualTo(appearance.Catalog.Count));

            int choice = (LocalProfile.OutfitIndex + 2) % appearance.Catalog.Count;
            lobby.PreviewOutfit(choice);
            Assert.That(appearance.OutfitIndex, Is.EqualTo(choice));
            lobby.ConfirmOutfit();

            Assert.That(lobby.IsOutfitPanelOpen, Is.False);
            Assert.That(player.Controlled, Is.True);
            Assert.That(LocalProfile.OutfitIndex, Is.EqualTo(choice));
            Assert.That(RendererColor(appearance), Is.EqualTo(appearance.Catalog[choice].Color));
        }

        [UnityTest]
        public IEnumerator DoorOpensBlurredLobbyAndOfflineTestLoadsTheChosenMapWithTheOutfit()
        {
            var player = world.OfflinePlayer;
            var outfit = (LocalProfile.OutfitIndex + 1) % player.GetComponent<PlayerAppearance>().Catalog.Count;
            LocalProfile.OutfitIndex = outfit;
            yield return Teleport(player, door.transform.position - Vector3.up);
            Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryInteract(), Is.True);
            Assert.That(lobby.IsLobbyOpen, Is.True);
            Assert.That(view.Blur.Active, Is.True, "The DressingRoom is blurred behind the Lobby UI.");
            Assert.That(player.Controlled, Is.False);

            lobby.StartOffline();
            yield return null;
            Assert.That(lobby.IsMapPanelOpen, Is.True, "Offline test goes straight to map selection.");
            Assert.That(lobby.MapButtons, Has.Count.GreaterThanOrEqualTo(2), "At least one map plus the random option.");

            lobby.ChooseMap(0);
            yield return null;
            yield return null;
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.Offline));
            Assert.That(Match.OfflineRemaining, Is.InRange(0, Match.RoundSeconds), "The offline round clock is running.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Scenes/Playground.unity"));
            var map = Object.FindFirstObjectByType<Playground>();
            Assert.That(map.OfflinePlayer, Is.Not.Null);
            var appearance = map.OfflinePlayer.GetComponent<PlayerAppearance>();
            Assert.That(appearance.OutfitIndex, Is.EqualTo(outfit), "The DressingRoom outfit is worn on the map.");

            var exit = Object.FindFirstObjectByType<MatchExit>();
            Assert.That(exit, Is.Not.Null, "The map shows the top-left exit button.");
            exit.Exit();
            float deadline = Time.realtimeSinceStartup + 5;
            while (SceneManager.GetActiveScene().path != ScenePath && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath), "Exit returns to the DressingRoom.");
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.None));
        }

        [UnityTest]
        public IEnumerator HostRoomWaitsForPlayersThenSpawnsTheOutfitOnTheChosenMap()
        {
            var outfit = (LocalProfile.OutfitIndex + 3) % world.OfflinePlayer.GetComponent<PlayerAppearance>().Catalog.Count;
            LocalProfile.OutfitIndex = outfit;
            lobby.OpenLobby();
            // A LAN room on this machine: the test must not depend on reaching Unity's Relay service.
            lobby.PreferLan = true;
            lobby.CreateRoom();
            float deadline = Time.realtimeSinceStartup + 10;
            while (LobbyRoom.Current == null && Time.realtimeSinceStartup < deadline) yield return null;
            var room = LobbyRoom.Current;
            Assert.That(room, Is.Not.Null, "Creating a room spawns the shared LobbyRoom: " + Session.Instance.Status);
            yield return null;
            yield return null;

            Assert.That(room.Members, Has.Count.EqualTo(1));
            Assert.That(room.Members[0].Outfit, Is.EqualTo(outfit), "The room shows each player's outfit.");
            Assert.That(room.CanStart, Is.False, "A room needs at least two players.");
            Assert.That(view.PrimaryLabel.text, Is.EqualTo("开始游戏"));
            Assert.That(view.PrimaryButton.interactable, Is.False);
            Assert.That(Object.FindObjectsByType<Avatar>(FindObjectsSortMode.None), Is.Empty, "No players spawn in the Lobby.");

            // Bypass the two-player requirement to exercise map loading and outfit sync with one process.
            room.Phase.Value = LobbyPhase.SelectingMap;
            yield return null;
            Assert.That(lobby.IsMapPanelOpen, Is.True);
            ShortenRound(room.Maps, 20);
            lobby.ChooseMap(0);
            deadline = Time.realtimeSinceStartup + 10;
            Avatar avatar = null;
            while (avatar == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                avatar = Object.FindFirstObjectByType<Avatar>();
            }
            Assert.That(avatar, Is.Not.Null, "The host's player spawns once the map has loaded.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Scenes/Playground.unity"));
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.Networked));
            Assert.That(avatar.IsOwner, Is.True);
            Assert.That(avatar.OutfitIndex.Value, Is.EqualTo(outfit));
            var appearance = avatar.GetComponent<PlayerAppearance>();
            Assert.That(RendererColor(appearance), Is.EqualTo(appearance.Catalog[outfit].Color));
            Assert.That(NetworkManager.Singleton, Is.SameAs(Session.Instance.Manager), "The map's own Network object yields to the Lobby's.");

            // The global round clock and the needs start once everyone is on the map.
            Assert.That(room.RoundRemaining, Is.InRange(0, 20));
            yield return null;
            Assert.That(ClockLabel().enabled, Is.True, "The map HUD shows the round clock.");
            var needs = avatar.GetComponent<PlayerNeeds>();
            Assert.That(needs.Ticking, Is.True, "The host runs every player's needs.");
            Assert.That(needs.Urgency, Is.InRange(0, NatureCalls.Core.NeedsRules.InitialUrgencyMax));

            // Any player's Urgency at the threshold loses the round for everyone.
            needs.ApplyShared(NatureCalls.Core.NeedsRules.UrgencyMax, 0);
            deadline = Time.realtimeSinceStartup + 8;
            while (SceneManager.GetActiveScene().path != ScenePath && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath), "A lost round ends in the DressingRoom.");
            yield return null;
            Assert.That(LobbyRoom.Current, Is.SameAs(room), "Players stay in the room after the round.");
            Assert.That(room.Phase.Value, Is.EqualTo(LobbyPhase.Waiting));
            Assert.That(room.RoundRemaining, Is.EqualTo(-1));
            Assert.That(Object.FindObjectsByType<Avatar>(FindObjectsSortMode.None), Is.Empty, "Map players are despawned.");
            var returned = Object.FindFirstObjectByType<LobbyController>();
            Assert.That(returned.IsLobbyOpen, Is.True, "The Lobby reopens on the room after the round.");
            yield return null;
            Assert.That(returned.GetComponent<LobbyView>().Status.text, Does.Contain("失败"), "The room shows why the round was lost.");
        }

        [UnityTest]
        public IEnumerator OfflineRoundEndsInTheDressingRoomWhenTimeIsUp()
        {
            lobby.OpenLobby();
            lobby.StartOffline();
            ShortenRound(Resources.FindObjectsOfTypeAll<MapCatalog>()[0], 1);
            lobby.ChooseMap(0);
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo("Assets/Scenes/Playground.unity"));
            float deadline = Time.realtimeSinceStartup + 5;
            while (SceneManager.GetActiveScene().path != ScenePath && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath));
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.None));
        }

        private void ShortenRound(MapCatalog catalog, float seconds)
        {
            timedCatalog = catalog;
            savedRoundSeconds = catalog[0].RoundSeconds;
            catalog[0].RoundSeconds = seconds;
        }

        private static UnityEngine.UI.Text ClockLabel()
        {
            var clock = Object.FindFirstObjectByType<MatchClock>();
            Assert.That(clock, Is.Not.Null, "The map HUD has a round clock.");
            return clock.transform.Find("MatchClock").GetComponent<UnityEngine.UI.Text>();
        }

        private static IEnumerator Teleport(Movement player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            yield return null;
        }

        private static Color RendererColor(PlayerAppearance appearance)
        {
            var block = new MaterialPropertyBlock();
            appearance.Body.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }
    }
}
