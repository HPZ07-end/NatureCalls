using System.Collections;
using System.Linq;
using NUnit.Framework;
using NatureCalls.Core;
using NatureCalls.Facility;
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
        private PlayerBodyType savedBodyType;
        private MapCatalog timedCatalog;
        private float savedRoundSeconds;
        private int timedMapIndex;

        [UnitySetUp]
        public IEnumerator Load()
        {
            savedOutfit = LocalProfile.OutfitIndex;
            savedBodyType = LocalProfile.BodyType;
            LocalProfile.SetAppearance(PlayerBodyType.Balanced, savedOutfit);
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
            LocalProfile.SetAppearance(savedBodyType, savedOutfit);
            if (timedCatalog != null) { timedCatalog[timedMapIndex].RoundSeconds = savedRoundSeconds; timedCatalog = null; }
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
                if (loaded != empty && loaded.isLoaded && (loaded.path == ScenePath || loaded.path == "Assets/Scenes/Playground.unity" ||
                    loaded.path == "Assets/Scenes/MapLayoutPrototype.unity" || loaded.path == "Assets/Scenes/Map1.unity" || loaded.path == "Assets/Scenes/Map2.unity"))
                    yield return SceneManager.UnloadSceneAsync(loaded);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ServerIsNotAMemberAndLeaderFollowsEntryOrder()
        {
            lobby.PreferLan = true;
            lobby.CreateRoom();
            yield return null;
            var session = Session.Instance;
            var leaving = session.LeaveAsync();
            yield return new WaitUntil(() => leaving.IsCompleted && !session.Manager.ShutdownInProgress);
            session.LobbyMode = true;
            Assert.That(session.Manager.StartServer(), Is.True);
            yield return null;
            var room = LobbyRoom.Current;
            Assert.That(room, Is.Not.Null);
            Assert.That(room.Members.Count, Is.Zero, "The server never occupies a player slot.");
            Assert.That(room.IsLeader, Is.False);
            room.Members.Add(new LobbyMember { ClientId = 7, Slot = 2 });
            room.Members.Add(new LobbyMember { ClientId = 4, Slot = 0, Ready = true });
            Assert.That(room.LeaderId, Is.EqualTo(7), "Entry order, not slot or client id, determines the leader.");
            Assert.That(room.CanStart, Is.True);
            room.RequestStart();
            yield return null;
            Assert.That(room.Phase.Value, Is.EqualTo(LobbyPhase.Waiting), "The server is not allowed to act as a player leader.");
            room.Members.RemoveAt(0);
            Assert.That(room.LeaderId, Is.EqualTo(4));
            Assert.That(room.ReadyMembers(), Is.Zero, "The new leader's old ready flag does not count as another player.");
            Assert.That(room.CanStart, Is.False);
            room.Members.Clear();
        }

        [UnityTest]
        public IEnumerator MirrorOutfitChangesAppearanceAndIsRemembered()
        {
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            Assert.That(input.TryInteract(), Is.False, "Nothing is in reach at the spawn point.");
            yield return Teleport(player, mirror.transform.position - Vector3.up);

            TestAim.AimAt(player, mirror);
            Assert.That(input.TryInteract(), Is.True);
            Assert.That(lobby.IsOutfitPanelOpen, Is.True);
            Assert.That(player.Controlled, Is.False, "Choosing an outfit pauses movement.");
            var appearance = player.GetComponent<PlayerAppearance>();
            Assert.That(lobby.OutfitButtons, Has.Count.EqualTo(appearance.Catalog.Count));
            view.OutfitSwift.onClick.Invoke();
            Assert.That(appearance.BodyType, Is.EqualTo(PlayerBodyType.Swift));
            Assert.That(appearance.ActiveAnimator.avatar.isValid, Is.True);
            view.OutfitBalanced.onClick.Invoke();
            Assert.That(appearance.HasRiggedBody, Is.True);
            var balanced = appearance.ActiveAnimator;
            Assert.That(balanced.avatar.isHuman, Is.False);

            int choice = (LocalProfile.OutfitIndex + 2) % appearance.Catalog.Count;
            view.OutfitTall.onClick.Invoke();
            lobby.PreviewOutfit(choice);
            Assert.That(appearance.BodyType, Is.EqualTo(PlayerBodyType.Tall));
            var tall = appearance.ActiveAnimator;
            Assert.That(tall, Is.Not.SameAs(balanced));
            Assert.That(tall.avatar.isHuman, Is.False);
            Assert.That(balanced.gameObject.activeInHierarchy, Is.False);
            Assert.That(tall.gameObject.activeInHierarchy, Is.True);
            Assert.That(tall.GetComponentsInChildren<Renderer>().All(r =>
                r.gameObject.layer == NatureCalls.Camera.CameraController.LocalBodyLayer), Is.True);
            Assert.That(appearance.OutfitIndex, Is.EqualTo(choice));
            Assert.That(LocalProfile.BodyType, Is.EqualTo(PlayerBodyType.Balanced), "Preview isn't persisted.");
            view.OutfitCancel.onClick.Invoke();
            Assert.That(appearance.ActiveAnimator, Is.SameAs(balanced));
            Assert.That(appearance.OutfitIndex, Is.EqualTo(LocalProfile.OutfitIndex));

            Assert.That(input.TryInteract(), Is.True);
            view.OutfitTall.onClick.Invoke();
            lobby.PreviewOutfit(choice);
            lobby.ConfirmOutfit();

            Assert.That(lobby.IsOutfitPanelOpen, Is.False);
            Assert.That(player.Controlled, Is.True);
            Assert.That(LocalProfile.OutfitIndex, Is.EqualTo(choice));
            Assert.That(LocalProfile.BodyType, Is.EqualTo(PlayerBodyType.Tall));
            Assert.That(RendererColor(appearance), Is.EqualTo(appearance.Catalog[choice].Color));
            Assert.That(tall.GetComponentsInChildren<Renderer>().Where(r => r != appearance.OutfitRenderer)
                .All(r => !r.HasPropertyBlock()), Is.True, "Skin, pants, hair and shoes keep their own materials.");
        }

        [UnityTest]
        public IEnumerator DoorOpensBlurredLobbyAndOfflineTestLoadsTheChosenMapWithTheOutfit() =>
            OfflineMap(0, "Assets/Scenes/Playground.unity");

        [UnityTest]
        public IEnumerator LobbyLoadsLayoutMapWithFirstPersonAndOutfit() =>
            OfflineMap(1, "Assets/Scenes/MapLayoutPrototype.unity");

        [UnityTest]
        public IEnumerator LobbyLoadsMap1WithFirstPersonAndOutfit() =>
            OfflineMap(2, "Assets/Scenes/Map1.unity");

        [UnityTest]
        public IEnumerator LobbyLoadsMap2AndAllowsSewerRoundTrip() =>
            OfflineMap(3, "Assets/Scenes/Map2.unity");

        [UnityTest]
        public IEnumerator Map2SuppliesAndShaftAreReachableFromSpawn()
        {
            lobby.OpenLobby();
            lobby.StartOffline();
            yield return null;
            lobby.MapButtons[3].onClick.Invoke();
            yield return null;
            yield return null;
            var map = Object.FindFirstObjectByType<Playground>();
            var player = map.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            var vending = map.GetComponent<PaperVendingMachine>();
            var emergency = Object.FindObjectsByType<PaperRoll>(FindObjectsSortMode.None).Single(r => r.PromptLabel.Contains("应急"));
            Assert.That(emergency.GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.True);
            input.enabled = false;
            yield return WalkTo(player, 6f, 1.32f);
            TestAim.AimAt(player, emergency);
            input.RefreshFocus();
            Assert.That(input.FocusPrompt, Does.Contain("应急卷纸"));
            Assert.That(input.TryInteract(), Is.True);
            Assert.That(input.HeldItem, Is.SameAs(emergency));
            Assert.That(input.PaperRemainingUses, Is.EqualTo(6));

            yield return WalkTo(player, 6f, 1.86f);
            yield return WalkTo(player, 4.4f, 1.86f);
            yield return WalkTo(player, 4.4f, 5.25f);
            TestAim.AimAt(player, map.GetComponentsInChildren<SewerAccess>().Single(item => !item.Ascending));
            input.RefreshFocus();
            Assert.That(input.FocusPrompt, Does.Contain("打开井盖"));
            Assert.That(input.TryInteract(), Is.True);
            Assert.That(player.IsTraversing, Is.True);
            float arrival = Time.realtimeSinceStartup + 4;
            while (player.IsTraversing && Time.realtimeSinceStartup < arrival) yield return null;
            Assert.That(player.IsTraversing, Is.False);
            Assert.That(player.transform.position.y, Is.EqualTo(-3.2f).Within(.18f));
            input.enabled = true;
        }

        private IEnumerator OfflineMap(int mapIndex, string mapPath)
        {
            var player = world.OfflinePlayer;
            var outfit = (LocalProfile.OutfitIndex + 1) % player.GetComponent<PlayerAppearance>().Catalog.Count;
            LocalProfile.SetAppearance(PlayerBodyType.Tall, outfit);
            yield return Teleport(player, door.transform.position - Vector3.up);
            TestAim.AimAt(player, door);
            Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryInteract(), Is.True);
            Assert.That(lobby.IsLobbyOpen, Is.True);
            Assert.That(view.Blur.Active, Is.True, "The DressingRoom is blurred behind the Lobby UI.");
            Assert.That(player.Controlled, Is.False);

            lobby.StartOffline();
            yield return null;
            Assert.That(lobby.IsMapPanelOpen, Is.True, "Offline test goes straight to map selection.");
            Assert.That(lobby.MapButtons, Has.Count.GreaterThanOrEqualTo(2), "At least one map plus the random option.");

            lobby.MapButtons[mapIndex].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.Offline));
            Assert.That(Match.OfflineRemaining, Is.InRange(0, Match.RoundSeconds), "The offline round clock is running.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(mapPath));
            var map = Object.FindFirstObjectByType<Playground>();
            Assert.That(map.OfflinePlayer, Is.Not.Null);
            AssertLocalCamera(map, map.OfflinePlayer);
            Assert.That(map.Camera.FirstPerson.Priority.Value, Is.GreaterThan(map.Camera.ThirdPerson.Priority.Value));
            var appearance = map.OfflinePlayer.GetComponent<PlayerAppearance>();
            Assert.That(appearance.OutfitIndex, Is.EqualTo(outfit), "The DressingRoom outfit is worn on the map.");
            Assert.That(appearance.BodyType, Is.EqualTo(PlayerBodyType.Tall));

            if (mapPath == "Assets/Scenes/Map2.unity")
            {
                var accesses = map.GetComponentsInChildren<SewerAccess>();
                Assert.That(accesses, Has.Length.EqualTo(2));
                Assert.That(Object.FindObjectsByType<StallDoor>(FindObjectsSortMode.None), Has.Length.EqualTo(3));
                var walker = map.OfflinePlayer;
                var input = walker.GetComponent<NatureCalls.Input.PlayerInput>();
                var vending = map.GetComponent<PaperVendingMachine>();
                var repair = map.GetComponent<Map2Repair>();
                var pipe = Object.FindFirstObjectByType<Pipe>();
                Assert.That(pipe, Is.Not.Null);
                pipe.AddPaper(20);
                Assert.That(pipe.IsBlocked, Is.True);
                yield return StandAtMap2Station(walker, vending, vending.Coins[0], "拾取金币");
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(vending.Wallet(0), Is.EqualTo(1));
                yield return StandAtMap2Station(walker, vending, vending.VendingMachine, "买卷纸");
                Assert.That(input.TryInteract(), Is.True);
                Assert.That(vending.Wallet(0), Is.Zero);
                Assert.That(input.HeldItem, Is.TypeOf<PaperRoll>());

                var wetZone = Object.FindObjectsByType<WetZone>(FindObjectsSortMode.None).OrderBy(zone => zone.name, System.StringComparer.Ordinal).First();
                var wet = wetZone.GetComponent<BoxCollider>().bounds.center;
                wet.y = .03f;
                yield return Teleport(walker, wet);
                Assert.That(wetZone.IsWet(walker.transform.position), Is.True);
                walker.Move(Vector2.right, .08f);
                var beforeRelease = walker.transform.position;
                walker.Move(Vector2.zero, .02f);
                Assert.That(Vector3.Distance(beforeRelease, walker.transform.position), Is.GreaterThan(.003f));

                foreach (var access in accesses.OrderBy(access => access.Ascending))
                {
                    var origin = access.Ascending ? access.Landing : access.Top;
                    var destination = access.Ascending ? access.Top : access.Landing;
                    yield return Teleport(walker, origin.position + access.ApproachDirection * access.ApproachDistance + Vector3.up * .03f);
                    TestAim.AimAt(walker, access);
                    Assert.That(input.TryInteract(), Is.True, access.name);
                    Assert.That(walker.IsTraversing, Is.True);
                    float arrival = Time.realtimeSinceStartup + 4;
                    while (walker.IsTraversing && Time.realtimeSinceStartup < arrival) yield return null;
                    Assert.That(walker.IsTraversing, Is.False);
                    Assert.That(walker.transform.position.y, Is.EqualTo(destination.position.y).Within(.18f));
                    if (!access.Ascending)
                    {
                        input.enabled = false;
                        yield return WalkTo(walker, 3.05f, 7.5f);
                        yield return WalkTo(walker, 9.5f, 7.5f);
                        Assert.That(walker.transform.position.y, Is.EqualTo(-3.8f).Within(.18f));
                        yield return WalkTo(walker, 9.5f, 2.35f);
                        Assert.That(walker.transform.position.y, Is.EqualTo(-2.7f).Within(.18f));
                        yield return WalkTo(walker, 12.24f, 2.35f);
                        yield return WalkTo(walker, 12.24f, 7.4f);
                        Assert.That(walker.transform.position.y, Is.EqualTo(-2.7f).Within(.18f));
                        input.enabled = true;
                        var stations = new[] { repair.UpstreamWindow, repair.Valve, repair.InspectionPort,
                            repair.Valve, repair.DownstreamWindow };
                        var prompts = new[] { "查看上游堵塞", "关闭阀门", "清除堵塞", "打开阀门", "复查回流" };
                        for (int i = 0; i < stations.Length; i++)
                        {
                            yield return StandAtMap2Station(walker, repair, stations[i], prompts[i]);
                            Assert.That(input.TryInteract(), Is.True, prompts[i]);
                            Assert.That((int)repair.RepairProgress, Is.EqualTo(i + 1));
                        }
                        Assert.That(pipe.Accumulation, Is.Zero);
                        Assert.That(pipe.IsBlocked, Is.False);
                    }
                }
            }

            var exit = Object.FindFirstObjectByType<MatchExit>();
            Assert.That(exit, Is.Not.Null, "The map shows the top-left exit button.");
            exit.Exit();
            float deadline = Time.realtimeSinceStartup + 5;
            while (SceneManager.GetActiveScene().path != ScenePath && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath), "Exit returns to the DressingRoom.");
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.None));
        }

        private static IEnumerator WalkTo(Movement player, float x, float z)
        {
            var target = new Vector3(x, 0, z);
            float deadline = Time.realtimeSinceStartup + 7;
            while (Time.realtimeSinceStartup < deadline)
            {
                var current = player.transform.position;
                current.y = 0;
                var offset = target - current;
                if (offset.magnitude <= .1f) yield break;
                var direction = player.transform.InverseTransformDirection(Vector3.ClampMagnitude(offset * 4, 1));
                player.Move(new Vector2(direction.x, direction.z), Mathf.Min(Time.deltaTime, .045f));
                yield return null;
            }
            Assert.Fail($"Blocked walking to ({x}, {z}) from {player.transform.position}.");
        }

        private static IEnumerator StandAtMap2Station(Movement player, IInteractableProvider provider, Transform station, string prompt)
        {
            for (int index = 0; index < 12; index++)
            {
                var direction = Quaternion.Euler(0, index * 30, 0) * Vector3.forward;
                var origin = station.position + direction * .65f + Vector3.up * .2f;
                if (!Physics.Raycast(origin, Vector3.down, out var floor, 1.65f, 1, QueryTriggerInteraction.Ignore)) continue;
                yield return Teleport(player, floor.point + Vector3.up * .02f);
                var interactable = provider.Interactables.First(item => item.Target == station);
                if (interactable.CanInteract(new InteractionContext(player, 0, false, null)) && interactable.Prompt(new InteractionContext(player, 0, false, null)).Contains(prompt))
                {
                    TestAim.AimAt(player, station);
                    var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
                    input.RefreshFocus();
                    Assert.That(input.FocusPrompt, Does.Contain(prompt), station.name + ": crosshair focus " + ((input.Focus?.Target != null) ? input.Focus.Target.name : "none") +
                        " view " + player.ViewRay.direction.ToString("F2") + " pitch " + player.Pitch.ToString("F1"));
                    yield break;
                }
            }
            Assert.Fail("No reachable prompt at " + station.name + " for " + prompt);
        }

        [UnityTest]
        public IEnumerator HostRoomWaitsForPlayersThenSpawnsTheOutfitOnTheChosenMap() =>
            HostMap(0, "Assets/Scenes/Playground.unity");

        [UnityTest]
        public IEnumerator HostLoadsLayoutMapWithNetworkPlayerAndFacilities() =>
            HostMap(1, "Assets/Scenes/MapLayoutPrototype.unity");

        [UnityTest]
        public IEnumerator HostLoadsMap1AndPublishesCrawlingPosture() =>
            HostMap(2, "Assets/Scenes/Map1.unity");

        [UnityTest]
        public IEnumerator HostLoadsMap2AndGrantsSewerDescent() =>
            HostMap(3, "Assets/Scenes/Map2.unity");

        private IEnumerator HostMap(int mapIndex, string mapPath)
        {
            var outfit = (LocalProfile.OutfitIndex + 3) % world.OfflinePlayer.GetComponent<PlayerAppearance>().Catalog.Count;
            LocalProfile.SetAppearance(PlayerBodyType.Tall, outfit);
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
            Assert.That(room.Members[0].BodyType, Is.EqualTo(PlayerBodyType.Tall));
            Assert.That(room.CanStart, Is.False, "A room needs at least two players.");
            Assert.That(view.PrimaryLabel.text, Is.EqualTo("开始游戏"));
            Assert.That(view.PrimaryButton.interactable, Is.False);
            Assert.That(Object.FindObjectsByType<Avatar>(FindObjectsSortMode.None), Is.Empty, "No players spawn in the Lobby.");

            // Bypass the two-player requirement to exercise map loading and outfit sync with one process.
            room.Phase.Value = LobbyPhase.SelectingMap;
            yield return null;
            Assert.That(lobby.IsMapPanelOpen, Is.True);
            ShortenRound(room.Maps, 20, mapIndex);
            lobby.ChooseMap(mapIndex);
            deadline = Time.realtimeSinceStartup + 10;
            Avatar avatar = null;
            while (avatar == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                avatar = Object.FindFirstObjectByType<Avatar>();
            }
            Assert.That(avatar, Is.Not.Null, "The host's player spawns once the map has loaded.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(mapPath));
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.Networked));
            Assert.That(avatar.IsOwner, Is.True);
            var map = Object.FindFirstObjectByType<Playground>();
            Assert.That(map.OfflinePlayer, Is.Null);
            AssertLocalCamera(map, avatar.GetComponent<Movement>());
            Assert.That(Facilities.Current, Is.Not.Null);
            Assert.That(avatar.GetComponent<AvatarAppearance>().OutfitIndex.Value, Is.EqualTo(outfit));
            Assert.That(avatar.GetComponent<AvatarAppearance>().BodyType.Value, Is.EqualTo(PlayerBodyType.Tall));
            var appearance = avatar.GetComponent<PlayerAppearance>();
            Assert.That(appearance.BodyType, Is.EqualTo(PlayerBodyType.Tall));
            Assert.That(RendererColor(appearance), Is.EqualTo(appearance.Catalog[outfit].Color));
            Assert.That(NetworkManager.Singleton, Is.SameAs(Session.Instance.Manager), "The map's own Network object yields to the Lobby's.");
            if (mapPath == "Assets/Scenes/Map1.unity")
            {
                var passage = Object.FindObjectsByType<VentPassage>(FindObjectsSortMode.None).First();
                var movement = avatar.GetComponent<Movement>();
                var spawn = movement.transform.position;
                yield return Teleport(movement, passage.transform.TransformPoint(new Vector3(-.25f, .015f, 0)));
                movement.Move(Vector2.zero, .02f);
                Assert.That(avatar.SharedPosture.Value, Is.EqualTo(PostureState.Crawling));
                yield return Teleport(movement, spawn);
                movement.Move(Vector2.zero, .02f);
                Assert.That(avatar.SharedPosture.Value, Is.EqualTo(PostureState.Standing));
            }
            if (mapPath == "Assets/Scenes/Map2.unity")
            {
                var access = map.GetComponentsInChildren<SewerAccess>().Single(item => !item.Ascending);
                var movement = avatar.GetComponent<Movement>();
                var input = movement.GetComponent<NatureCalls.Input.PlayerInput>();
                var vending = map.GetComponent<PaperVendingMachine>();
                var repair = map.GetComponent<Map2Repair>();
                yield return StandAtMap2Station(movement, vending, vending.Coins[0], "拾取金币");
                Assert.That(input.TryInteract(), Is.True);
                yield return null;
                Assert.That(vending.Wallet(avatar.OwnerClientId), Is.EqualTo(1));
                yield return StandAtMap2Station(movement, vending, vending.VendingMachine, "买卷纸");
                Assert.That(input.TryInteract(), Is.True);
                yield return null;
                Assert.That(vending.Wallet(avatar.OwnerClientId), Is.Zero);
                Assert.That(input.HeldItem, Is.TypeOf<PaperRoll>());
                yield return Teleport(movement, access.Top.position + access.ApproachDirection * access.ApproachDistance + Vector3.up * .03f);
                TestAim.AimAt(movement, access);
                Assert.That(input.TryInteract(), Is.True);
                yield return null;
                Assert.That(movement.IsTraversing, Is.True);
                Assert.That(access.ClosedCover.enabled, Is.False);
                float arrival = Time.realtimeSinceStartup + 4;
                while (movement.IsTraversing && Time.realtimeSinceStartup < arrival) yield return null;
                Assert.That(movement.transform.position.y, Is.EqualTo(access.Landing.position.y).Within(.18f));
                yield return StandAtMap2Station(movement, repair, repair.UpstreamWindow, "查看上游堵塞");
                Assert.That(input.TryInteract(), Is.True);
                yield return null;
                Assert.That(repair.RepairProgress, Is.EqualTo(SewerRepairProgress.Inspected));
            }

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

        private void ShortenRound(MapCatalog catalog, float seconds, int index = 0)
        {
            timedCatalog = catalog;
            timedMapIndex = index;
            savedRoundSeconds = catalog[index].RoundSeconds;
            catalog[index].RoundSeconds = seconds;
        }

        private static void AssertLocalCamera(Playground map, Movement player)
        {
            // Mirrors render to textures and are separate from the player's screen output.
            var screenCameras = UnityEngine.Camera.allCameras
                .Where(camera => camera.isActiveAndEnabled && camera.targetTexture == null).ToArray();
            Assert.That(screenCameras, Has.Length.EqualTo(1), "Only one camera renders to the player's screen.");
            Assert.That(screenCameras[0], Is.SameAs(map.Camera.Output));
            Assert.That(map.Camera.Target, Is.SameAs(player), "The screen camera follows the local player.");
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
            appearance.OutfitRenderer.GetPropertyBlock(block, 1);
            return block.GetColor("_BaseColor");
        }
    }
}
