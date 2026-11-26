using System.Collections;
using System.IO;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
            // Keep the remote stall's room list off the course server; tests that need rooms supply their own.
            lobby.ServerRoomSource = () => System.Threading.Tasks.Task.FromResult<(int[], string)>((new int[0], null));
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
            LobbyRoom.RevealDurationSeconds = MatchRules.RevealSeconds;
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
        public IEnumerator RebuiltRoomHasCeilingTwoWorkingMirrorsAndLocalCamera()
        {
            Assert.That(scene.GetRootGameObjects().Any(root => root.name == "DressingRoomBlockout"), Is.False);
            var roomRoot = scene.GetRootGameObjects().Single(root => root.name == "DressingRoom");
            var ceiling = roomRoot.transform.Find("Ceiling").GetComponent<Collider>();
            Assert.That(ceiling.bounds.min.y, Is.EqualTo(2.8f).Within(.01f));
            Assert.That(Physics.Raycast(world.Spawns[0].position + Vector3.up * 1.8f, Vector3.up, out var overhead, 2), Is.True);
            Assert.That(overhead.collider, Is.SameAs(ceiling));
            AssertLocalCamera(world, world.OfflinePlayer);
            var mirrors = roomRoot.GetComponentsInChildren<DressingMirror>();
            Assert.That(mirrors, Has.Length.EqualTo(2));
            var reflections = roomRoot.GetComponentsInChildren<PlanarMirror>();
            Assert.That(reflections, Has.Length.EqualTo(2));
            Assert.That(reflections[0].ReflectionCamera.targetTexture, Is.Not.SameAs(reflections[1].ReflectionCamera.targetTexture));
            var player = world.OfflinePlayer;
            foreach (var target in mirrors)
            {
                yield return Teleport(player, target.StandingPoint.position);
                AimAt(player, target);
                Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryInteract(), Is.True);
                Assert.That(lobby.IsOutfitPanelOpen, Is.True);
                lobby.CancelOutfit();
            }
            yield return Teleport(player, world.Spawns[0].position);
            LookAt(player, new Vector3(2.2f, 1.5f, -.1f));
            yield return null;
            yield return null;
            CaptureRoom(world.Camera.Output, "dressing-room.png");
        }

        [UnityTest]
        public IEnumerator StoolCanBeCarriedPlacedAndJumpedOnToUseMirror()
        {
            var stool = Object.FindFirstObjectByType<DressingRoomStool>();
            Assert.That(stool, Is.Not.Null);
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            yield return Teleport(player, stool.transform.position + Vector3.back * .75f + Vector3.up * .02f);
            AimAt(player, stool);
            Assert.That(input.TryInteract(), Is.True);
            Assert.That(stool.Carrier, Is.SameAs(player));
            Assert.That(stool.GetComponent<Collider>().enabled, Is.False);
            yield return Teleport(player, new Vector3(2.1f, .02f, -1.7f));
            LookAt(player, new Vector3(4, 1.65f, -1.7f));
            Assert.That(input.TryAction(), Is.False, "A stool cannot be placed outside the room.");
            Assert.That(stool.IsCarried, Is.True);
            var target = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DressingMirror>()).OrderBy(value => value.transform.position.z).First();
            yield return Teleport(player, target.StandingPoint.position + Vector3.left * .95f);
            LookAt(player, target.StandingPoint.position + Vector3.up * 1.65f);
            Assert.That(input.TryAction(), Is.True);
            Assert.That(stool.IsCarried, Is.False);
            Assert.That(stool.transform.position.x, Is.EqualTo(target.StandingPoint.position.x).Within(.05f));
            Assert.That(stool.GetComponent<Collider>().enabled, Is.True);
            for (int index = 0; index < 12; index++) { player.Move(Vector2.zero, 1f / 60); yield return null; }
            Assert.That(player.Jump(), Is.True);
            for (int index = 0; index < 80; index++)
            {
                player.Move(player.transform.position.x < stool.transform.position.x ? Vector2.up : Vector2.zero, 1f / 60);
                yield return null;
            }
            Assert.That(stool.Supports(player), Is.True, "Production movement lands on the stool top.");
            Assert.That(stool.CanInteract(player), Is.False, "The player cannot pick up the stool beneath their feet.");
            AimAt(player, target);
            Assert.That(input.TryInteract(), Is.True);
            view.OutfitSwift.onClick.Invoke();
            yield return null;
            yield return null;
            CaptureRoom(world.Camera.Output, "dressing-room-stool-mirror.png");
            lobby.CancelOutfit();
        }

        private static void CaptureRoom(UnityEngine.Camera camera, string name)
        {
            var directory = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
            if (string.IsNullOrEmpty(directory)) return;
            var texture = new RenderTexture(1100, 760, 24);
            texture.Create();
            var previous = RenderTexture.active;
            var image = new Texture2D(1100, 760, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1100, 760), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.Destroy(texture); Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator MirrorsRenderEveryModelAtBothHeightsAndRespondToLateralMovement()
        {
            var player = world.OfflinePlayer;
            var appearance = player.GetComponent<PlayerAppearance>();
            var mirrors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DressingMirror>()).ToArray();
            foreach (var station in mirrors)
            foreach (var bodyType in new[] { PlayerBodyType.Swift, PlayerBodyType.Balanced, PlayerBodyType.Tall })
            foreach (float elevation in new[] { 0f, .36f })
            {
                appearance.SetBodyType(bodyType);
                yield return Teleport(player, station.StandingPoint.position + Vector3.up * elevation);
                AimAt(player, station);
                yield return null;
                yield return null;
                var reflection = station.GetComponent<PlanarMirror>();
                Assert.That(reflection.RenderReflection(), Is.True);
                var normal = -reflection.Surface.transform.forward;
                var offset = world.Camera.Output.transform.position - reflection.Surface.transform.position;
                var expected = world.Camera.Output.transform.position - 2 * Vector3.Dot(offset, normal) * normal;
                Assert.That(Vector3.Distance(reflection.ReflectionCamera.transform.position, expected), Is.LessThan(.001f));
                var visible = ReadMirror(reflection.ReflectionCamera);
                var mask = reflection.ReflectionCamera.cullingMask;
                reflection.ReflectionCamera.cullingMask &= ~(1 << NatureCalls.Camera.CameraController.LocalBodyLayer);
                Color32[] empty;
                try { empty = ReadMirror(reflection.ReflectionCamera); }
                finally { reflection.ReflectionCamera.cullingMask = mask; }
                int bodyPixels = visible.Zip(empty, (a, b) => Mathf.Abs(a.r-b.r) + Mathf.Abs(a.g-b.g) + Mathf.Abs(a.b-b.b)).Count(delta => delta > 35);
                Assert.That(bodyPixels, Is.GreaterThan(20), $"{station.name}/{bodyType}/{elevation}: the rendered image must contain the local body.");
                CaptureRoom(world.Camera.Output, $"mirror-{station.name}-{bodyType}-{(elevation == 0 ? "ground" : "stool")}.png");
            }
            var target = mirrors[0].GetComponent<PlanarMirror>();
            var prior = target.ReflectionCamera.transform.position;
            yield return Teleport(player, mirrors[0].StandingPoint.position + Vector3.forward * .2f);
            AimAt(player, mirrors[0]);
            yield return null;
            Assert.That(target.UpdateView(), Is.True);
            Assert.That(Vector3.Distance(prior, target.ReflectionCamera.transform.position), Is.GreaterThan(.15f));
        }

        private static Color32[] ReadMirror(UnityEngine.Camera camera)
        {
            var texture = new RenderTexture(224, 256, 24); texture.Create();
            var previous = RenderTexture.active;
            var image = new Texture2D(224, 256, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 224, 256), 0, 0); image.Apply();
                return image.GetPixels32();
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.Destroy(texture); Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator MirrorSurfaceDisplaysAllFourTextureCornersWithoutCroppingOrFlippingVertically()
        {
            var stations = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DressingMirror>()).ToArray();
            foreach (var station in stations)
            {
                yield return Teleport(world.OfflinePlayer, station.StandingPoint.position);
                AimAt(world.OfflinePlayer, station);
                yield return null;
                yield return null;
                var mirror = station.GetComponent<PlanarMirror>();
                var material = mirror.Surface.sharedMaterial;
                var previousMap = material.GetTexture("_BaseMap");
                var pattern = new Texture2D(16, 16, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    pattern.SetPixel(x, y, y < 8 ? (x < 8 ? Color.red : Color.green) : (x < 8 ? Color.blue : Color.yellow));
                pattern.Apply();
                var camera = world.Camera.Output;
                var priorTarget = camera.targetTexture;
                var priorActive = RenderTexture.active;
                var target = new RenderTexture(1100, 760, 24); target.Create();
                var image = new Texture2D(1100, 760, TextureFormat.RGB24, false);
                try
                {
                    material.SetTexture("_BaseMap", pattern);
                    camera.targetTexture = target;
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1100, 760), 0, 0); image.Apply();
                    foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
                    {
                        var point = mirror.Surface.transform.TransformPoint(new Vector3(x * mirror.Size.x * .3f, y * mirror.Size.y * .3f, -.001f));
                        var pixel = camera.WorldToViewportPoint(point);
                        Assert.That(pixel.x, Is.InRange(0f, 1f)); Assert.That(pixel.y, Is.InRange(0f, 1f));
                        var actual = image.GetPixel(Mathf.RoundToInt(pixel.x * 1099), Mathf.RoundToInt(pixel.y * 759));
                        var expected = y < 0 ? (x < 0 ? Color.green : Color.red) : (x < 0 ? Color.yellow : Color.blue);
                        Assert.That(Vector3.Distance(new Vector3(actual.r, actual.g, actual.b), new Vector3(expected.r, expected.g, expected.b)),
                            Is.LessThan(.35f), $"{station.name} corner {x},{y}: the complete correctly oriented image must reach the visible surface.");
                    }
                    var directory = System.Environment.GetEnvironmentVariable("NATURECALLS_RUN_PATH");
                    if (!string.IsNullOrEmpty(directory)) File.WriteAllBytes(Path.Combine(directory, "mirror-uv-" + station.name + ".png"), image.EncodeToPNG());
                }
                finally
                {
                    material.SetTexture("_BaseMap", previousMap); camera.targetTexture = priorTarget; RenderTexture.active = priorActive;
                    target.Release(); Object.Destroy(target); Object.Destroy(image); Object.Destroy(pattern);
                }
            }
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
        public IEnumerator LobbyExitInterruptsOpeningAndDoesNotRefocusOrFreezeMovement()
        {
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            Assert.That(Vector3.Distance(room.Entry.position, room.Approach[0].position), Is.GreaterThan(2.5f));
            Assert.That(room.FlushButtons.All(button => button.name == "Flush Button"), Is.True, "Reuse the authored toilet buttons.");
            room.Enter();
            yield return Teleport(world.OfflinePlayer, room.Approach[0].position);
            LookAt(world.OfflinePlayer, room.Water[0].position);
            room.Focus(0); yield return new WaitForSecondsRealtime(.1f);
            lobby.CloseLobby(); yield return new WaitForSecondsRealtime(1);
            Assert.That(room.Focused, Is.False); Assert.That(world.OfflinePlayer.Controlled, Is.True);
            Assert.That(world.Camera.Brain.enabled, Is.True);
            yield return new WaitForSecondsRealtime(1);
            Assert.That(room.Focused, Is.False, "Looking at the same toilet must not trap the player in a refocus loop.");
            var position = world.OfflinePlayer.transform.position;
            world.OfflinePlayer.Move(Vector2.right, .1f);
            Assert.That(Vector3.Distance(position, world.OfflinePlayer.transform.position), Is.GreaterThan(.05f));
        }

        [UnityTest]
        public IEnumerator ServerRejectsMapConfirmationWhenReadinessNoLongerHolds()
        {
            lobby.PreferLan = true; lobby.CreateRoom();
            yield return null; yield return null;
            var room = LobbyRoom.Current;
            Assert.That(room, Is.Not.Null);
            room.Members.Add(new LobbyMember { ClientId = 999, Slot = 1, Ready = true });
            room.RequestStart(); yield return null;
            lobby.ChooseMap(0); yield return null;
            Assert.That(room.Phase.Value, Is.EqualTo(LobbyPhase.ConfirmingMap));
            var guest = room.Members[1]; guest.Ready = false; room.Members[1] = guest;
            lobby.ConfirmMapSelection(); yield return null;
            Assert.That(room.Phase.Value, Is.EqualTo(LobbyPhase.Waiting));
            Assert.That(SceneManager.GetSceneByPath("Assets/Scenes/Playground.unity").isLoaded, Is.False);
            Assert.That(world.OfflinePlayer, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LobbyDoorsHaveTexturedHangingSignsAndLetteringIsHiddenOutside()
        {
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            Assert.That(room.Inside, Is.False);
            Assert.That(room.RoomLettering.All(renderer => !renderer.enabled), Is.True, "Lobby lettering is hidden in the dressing room.");
            CaptureRoom(world.Camera.Output, "dressing-room-no-lobby-lettering.png");
            room.Enter(); yield return null;
            var player = world.OfflinePlayer;
            for (int index = 0; index < 2; index++)
            {
                var target = room.Doors[index];
                Assert.That(target, Is.Not.Null);
                var sign = target.DoorLeaf.Find(index == 0 ? "本地局域网连接" : "远程房间号连接");
                Assert.That(sign, Is.Not.Null);
                Assert.That(sign.GetComponentsInChildren<Renderer>().Any(renderer => renderer.sharedMaterials.Any(mat =>
                    mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null && mat.GetTexture("_BaseMap").name.Contains(index == 0 ? "LAN" : "Relay"))), Is.True);
                yield return Teleport(player, new Vector3(room.Approach[index].position.x, .03f, -.95f));
                LookAt(player, sign.position);
                yield return null; yield return null;
                CaptureRoom(world.Camera.Output, index == 0 ? "lobby-lan-door-closed.png" : "lobby-relay-door-closed.png");
                AimAt(player, target); yield return null;
                Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryInteract(), Is.True);
                Assert.That(target.State, Is.EqualTo(StallDoorState.Open));
                CaptureRoom(world.Camera.Output, index == 0 ? "lobby-lan-door-sign.png" : "lobby-relay-door-sign.png");
            }
            room.Return();
            Assert.That(room.RoomLettering.All(renderer => !renderer.enabled), Is.True);
        }

        [UnityTest]
        public IEnumerator ToiletLobbyLooksBlendToWaterAndReturnWithoutConnecting()
        {
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            Assert.That(room.Water, Has.Length.EqualTo(2));
            var player = world.OfflinePlayer;
            room.Enter();
            Assert.That(room.Inside, Is.True);
            LookAt(player, new Vector3(8, 1.2f, 1.8f));
            yield return null; yield return null;
            CaptureRoom(world.Camera.Output, "toilet-lobby-room.png");
            for (int mode = 0; mode < 2; mode++)
            {
                yield return Teleport(player, room.Approach[mode].position);
                LookAt(player, room.Water[mode].position);
                float deadline = Time.unscaledTime + 3;
                while (!room.Focused && Time.unscaledTime < deadline) yield return null;
                Assert.That(room.Focused, Is.True, "Looking at the nearby toilet starts the view change.");
                Assert.That(room.Transitioning, Is.True, "The camera blends over time.");
                yield return new WaitForSecondsRealtime(1);
                Assert.That(room.Mode, Is.EqualTo(mode));
                Assert.That(Vector3.Dot(world.Camera.Output.transform.forward, Vector3.down), Is.GreaterThan(.99f));
                Assert.That(view.LobbyPanel.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(Vector3.Distance(view.LobbyPanel.transform.position, room.Water[mode].position), Is.LessThan(.13f));
                foreach (var button in view.LobbyPanel.GetComponentsInChildren<Button>())
                    Assert.That(button.GetComponent<Image>().color.a, Is.Zero, "Water text has no button backdrop.");
                Assert.That(view.Blur.Active, Is.False);
                ClickFloating(view.JoinButton, world.Camera.Output);
                yield return null;
                view.CodeInput.text = "";
                Assert.That(room.TryFlush(FlushRay(room)), Is.False, "Typing must not activate the physical button.");
                view.CodeInput.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
                Assert.That(room.TryFlush(FlushRay(room)), Is.True); yield return null;
                Assert.That(LobbyRoom.Current, Is.Null, "Empty input must not submit a connection.");
                Assert.That(view.Status.text, Does.Contain(mode == 0 ? "IP" : "房间号"));
                CaptureRoom(world.Camera.Output, mode == 0 ? "toilet-lobby-lan.png" : "toilet-lobby-relay.png");
                lobby.CloseLobby(); yield return new WaitForSecondsRealtime(1);
                Assert.That(player.Controlled, Is.True); Assert.That(room.Focused, Is.False);
                Assert.That(world.Camera.Brain.enabled, Is.True);
            }
            room.Return(); Assert.That(room.Inside, Is.False);
            Assert.That(Vector3.Distance(player.transform.position, room.ReturnPoint.position), Is.LessThan(.1f));
        }

        [UnityTest]
        public IEnumerator ToiletLobbyFlushCreatesLanRoomUsingExistingController()
        {
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            room.Enter(); room.Focus(0);
            yield return new WaitForSecondsRealtime(1);
            room.SelectCreate();
            Assert.That(Session.Instance, Is.Null, "Selecting create does not connect before flushing.");
            Assert.That(room.TryFlush(FlushRay(room)), Is.True);
            yield return null; yield return null;
            Assert.That(LobbyRoom.Current, Is.Not.Null);
            Assert.That(Session.Instance.Active, Is.True);
            Assert.That(lobby.PreferLan, Is.True);
            Assert.That(LobbyRoom.Current.Members.Count, Is.EqualTo(1));
            Assert.That(LobbyRoom.Current.CanStart, Is.False);
            CaptureRoom(world.Camera.Output, "toilet-lobby-lan-host.png");
            lobby.CloseLobby(); yield return new WaitForSecondsRealtime(1);
            Assert.That(room.Focused, Is.False); Assert.That(world.OfflinePlayer.Controlled, Is.True);
            Assert.That(Session.Instance.Active, Is.True, "Closing the view retains the room; LeaveRoom disconnects explicitly.");
        }

        // A Session whose broker is a closed local port: requests fail fast and never reach the course server.
        private Session LocalBrokerSession()
        {
            var method = typeof(LobbyController).GetMethod("EnsureSession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var session = (Session)method.Invoke(lobby, null);
            session.BrokerHost = "127.0.0.1"; session.BrokerPort = 9;
            return session;
        }

        private IEnumerator FocusRemoteJoin(ToiletLobby room)
        {
            room.Enter(); room.Focus(1);
            yield return new WaitForSecondsRealtime(1);
            room.SelectJoin(); view.CodeInput.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
            for (int frame = 0; frame < 4; frame++) yield return null;
        }

        [UnityTest]
        public IEnumerator ToiletLobbyRemoteJoinListsServerRoomsOnWater()
        {
            lobby.ServerRoomSource = () => System.Threading.Tasks.Task.FromResult<(int[], string)>((new[] { 328, 571 }, null));
            var session = LocalBrokerSession();
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            yield return FocusRemoteJoin(room);
            var list = lobby.ServerRooms;
            Assert.That(list, Is.Not.Null);
            Assert.That(list.gameObject.activeInHierarchy, Is.True, "Choosing join in the remote stall shows the server rooms.");
            var rooms = list.GetComponentsInChildren<Button>().Where(button => button.interactable).ToArray();
            Assert.That(rooms.Select(button => button.GetComponentInChildren<Text>().text), Is.EquivalentTo(new[] { "328", "571" }));
            foreach (var button in rooms) Assert.That(button.GetComponent<Image>().color.a, Is.Zero, "Water text has no button backdrop.");
            Assert.That(view.CreateServerButton == null || !view.CreateServerButton.gameObject.activeInHierarchy, Is.True);
            CaptureRoom(world.Camera.Output, "toilet-lobby-server-rooms.png");
            ClickFloating(rooms.Single(button => button.GetComponentInChildren<Text>().text == "328"), world.Camera.Output);
            Assert.That(view.CodeInput.text, Is.EqualTo("328"));
            float deadline = Time.unscaledTime + 12;
            while (session.Busy && Time.unscaledTime < deadline) yield return null;
            yield return null;
            Assert.That(session.Active, Is.False);
            Assert.That(view.Status.text, Does.Contain("加入服务器房间失败"), "The click goes through the room-code lookup.");
            room.SelectCreate(); yield return null;
            Assert.That(list.gameObject.activeInHierarchy, Is.False, "Creating hides the list.");
            lobby.CloseLobby(); yield return new WaitForSecondsRealtime(1);
        }

        [UnityTest]
        public IEnumerator ToiletLobbyRemoteListExplainsUnreachableServer()
        {
            lobby.ServerRoomSource = () => System.Threading.Tasks.Task.FromResult<(int[], string)>((null, "无法连接课程服务器。"));
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            yield return FocusRemoteJoin(room);
            var entries = lobby.ServerRooms.GetComponentsInChildren<Button>();
            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].interactable, Is.False);
            Assert.That(entries[0].GetComponentInChildren<Text>().text, Does.Contain("无法连接"));
            Assert.That(Session.Instance, Is.Null, "Listing rooms never opens a Session.");
            lobby.CloseLobby(); yield return new WaitForSecondsRealtime(1);
        }

        [UnityTest]
        public IEnumerator ToiletLobbyRemoteCreateRequestsCourseServerRoom()
        {
            var session = LocalBrokerSession();
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            room.Enter(); room.Focus(1);
            yield return new WaitForSecondsRealtime(1);
            room.SelectCreate();
            Assert.That(room.TryFlush(FlushRay(room)), Is.True);
            yield return null;
            Assert.That(lobby.PreferLan, Is.False);
            float deadline = Time.unscaledTime + 12;
            while (session.Busy && Time.unscaledTime < deadline) yield return null;
            yield return null;
            Assert.That(view.Status.text, Does.Contain("创建服务器房间失败"), "The remote stall creates on the course server.");
            Assert.That(session.Active, Is.False, "No LAN host or Relay room is opened instead.");
            lobby.CloseLobby(); yield return new WaitForSecondsRealtime(1);
        }

        [UnityTest]
        public IEnumerator ToiletLobbyEKeyFlushesButtonUnderPointer()
        {
            // Regression: in the water view the player is parked (not controlled), so E must reach the flush button
            // through the mouse pointer rather than the crosshair. Batch runs are never Application.isFocused, so this
            // drives the method the parked-player E branch calls, with a virtual mouse that ignores focus.
            var room = Object.FindFirstObjectByType<ToiletLobby>();
            room.Enter(); room.Focus(0);
            yield return new WaitForSecondsRealtime(1);
            room.SelectCreate();
            var player = world.OfflinePlayer;
            Assert.That(player.Controlled, Is.False, "The water view parks the player.");
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var background = settings.backgroundBehavior; var editorBehavior = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                mouse.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = Vector2.zero });
                yield return null;
                Assert.That(input.TryFlushLobbyFromPointer(), Is.False, "Pointing away from the button does nothing.");
                var screen = world.Camera.Output.WorldToScreenPoint(room.FlushButtons[0].Head.position);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(screen.x, screen.y) });
                yield return null;
                Assert.That(input.TryFlushLobbyFromPointer(), Is.True, "E with the pointer on the flush button flushes.");
                yield return null; yield return null;
                Assert.That(LobbyRoom.Current, Is.Not.Null, "The flush creates the room.");
                Assert.That(Session.Instance.Active, Is.True);
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = editorBehavior;
            }
            lobby.CloseLobby(); yield return new WaitForSecondsRealtime(1);
        }

        [UnityTest]
        public IEnumerator FloatingOutfitTextChecksPreviewChoicesAndReceivesMouseClicks()
        {
            var player = world.OfflinePlayer;
            yield return Teleport(player, mirror.StandingPoint.position);
            AimAt(player, mirror);
            Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryInteract(), Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            var floating = view.OutfitPanel.GetComponent<FloatingOutfitMenu>();
            Assert.That(floating, Is.Not.Null);
            Assert.That(floating.transform.parent, Is.Null, "Floating text has its own world-space canvas.");
            Assert.That(floating.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(floating.GetComponent<Image>(), Is.Null, "There is no panel backdrop.");
            Assert.That(view.Blur.Active, Is.False, "The mirror remains visible behind the text.");
            var body = new[] { view.OutfitSwift, view.OutfitBalanced, view.OutfitTall };
            Assert.That(body.Count(button => button.GetComponent<OutfitChoice>().Selected), Is.EqualTo(1));
            Assert.That(view.OutfitBalanced.GetComponent<OutfitChoice>().Check.activeSelf, Is.True);
            ClickFloating(view.OutfitSwift, world.Camera.Output);
            int choice = (savedOutfit + 1) % lobby.OutfitButtons.Count;
            ClickFloating(lobby.OutfitButtons[choice], world.Camera.Output);
            Assert.That(body.Count(button => button.GetComponent<OutfitChoice>().Check.activeSelf), Is.EqualTo(1));
            Assert.That(view.OutfitSwift.GetComponent<OutfitChoice>().Selected, Is.True);
            Assert.That(lobby.OutfitButtons.Count(button => button.GetComponent<OutfitChoice>().Check.activeSelf), Is.EqualTo(1));
            Assert.That(lobby.OutfitButtons[choice].GetComponent<OutfitChoice>().Selected, Is.True);
            Assert.That(LocalProfile.OutfitIndex, Is.EqualTo(savedOutfit), "Checks follow the preview, before confirmation.");
            foreach (var button in body.Concat(lobby.OutfitButtons))
                Assert.That(button.GetComponent<Image>().color.a, Is.Zero, "Option hit areas have no visible button box.");

            var camera = world.Camera.Output;
            var priorTarget = camera.targetTexture;
            var target = new RenderTexture(1100, 760, 24); target.Create();
            try
            {
                camera.targetTexture = target;
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4]; ((RectTransform)floating.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = camera.WorldToViewportPoint(corner);
                    Assert.That(point.x, Is.InRange(0f, 1f)); Assert.That(point.y, Is.InRange(0f, 1f));
                }
                CaptureRoom(camera, "floating-outfit-ui.png");
            }
            finally { camera.targetTexture = priorTarget; target.Release(); Object.Destroy(target); }
            ClickFloating(view.OutfitCancel, camera);
            Assert.That(lobby.IsOutfitPanelOpen, Is.False);
            Assert.That(player.GetComponent<PlayerAppearance>().BodyType, Is.EqualTo(PlayerBodyType.Balanced));
            Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryInteract(), Is.True);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(view.OutfitBalanced.GetComponent<OutfitChoice>().Check.activeSelf, Is.True, "Reopening restores the confirmed body check.");
            ClickFloating(view.OutfitTall, camera);
            ClickFloating(lobby.OutfitButtons[choice], camera);
            ClickFloating(view.OutfitConfirm, camera);
            Assert.That(LocalProfile.BodyType, Is.EqualTo(PlayerBodyType.Tall));
            Assert.That(LocalProfile.OutfitIndex, Is.EqualTo(choice));
        }

        private static Ray FlushRay(ToiletLobby room)
        {
            var camera = room.World.Camera.Output;
            var point = camera.WorldToViewportPoint(room.FlushButtons[room.Mode].Head.position);
            Assert.That(point.x, Is.InRange(0f, 1f)); Assert.That(point.y, Is.InRange(0f, 1f));
            Assert.That(room.View.LobbyPanel.transform.Find("Flush Confirm"), Is.Null, "No clickable flush subtitle remains.");
            return camera.ViewportPointToRay(point);
        }

        private static void ClickFloating(Button button, UnityEngine.Camera camera)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var data = new PointerEventData(EventSystem.current) { position = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.FirstOrDefault().gameObject?.GetComponentInParent<Button>(), Is.SameAs(button), "The visible row must receive the pointer hit.");
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator MirrorOutfitChangesAppearanceAndIsRemembered()
        {
            var player = world.OfflinePlayer;
            var input = player.GetComponent<NatureCalls.Input.PlayerInput>();
            Assert.That(input.TryInteract(), Is.False, "Nothing is in reach at the spawn point.");
            yield return Teleport(player, mirror.StandingPoint.position);
            AimAt(player, mirror);

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

        private IEnumerator OfflineMap(int mapIndex, string mapPath)
        {
            var player = world.OfflinePlayer;
            var outfit = (LocalProfile.OutfitIndex + 1) % player.GetComponent<PlayerAppearance>().Catalog.Count;
            LocalProfile.SetAppearance(PlayerBodyType.Tall, outfit);
            yield return Teleport(player, door.StandingPoint.position);
            AimAt(player, door);
            Assert.That(player.GetComponent<NatureCalls.Input.PlayerInput>().TryInteract(), Is.True);
            var entryRoom = Object.FindFirstObjectByType<ToiletLobby>();
            Assert.That(entryRoom.Inside, Is.True);
            entryRoom.Focus(0);
            yield return new WaitForSecondsRealtime(1);
            Assert.That(lobby.IsLobbyOpen, Is.True);
            Assert.That(view.Blur.Active, Is.False, "The lobby text floats on water without a screen backdrop.");
            Assert.That(player.Controlled, Is.False);

            lobby.StartOffline();
            yield return null;
            Assert.That(lobby.IsMapPanelOpen, Is.True, "Offline test goes straight to map selection.");
            Assert.That(lobby.MapButtons, Has.Count.GreaterThanOrEqualTo(2), "At least one map plus the random option.");
            CaptureRoom(world.Camera.Output, "water-map-list-" + mapIndex + ".png");

            lobby.MapButtons[mapIndex].onClick.Invoke();
            yield return null;
            Assert.That(lobby.AwaitingMapConfirmation, Is.True);
            Assert.That(SceneManager.GetSceneByPath(mapPath).isLoaded, Is.False, "Selecting a map must not load it.");
            Assert.That(view.MapHint.text, Does.Not.Contain("MapLayoutPrototype"));
            CaptureRoom(world.Camera.Output, "selected-map-confirmation-" + mapIndex + ".png");
            Assert.That(entryRoom.TryFlush(FlushRay(entryRoom)), Is.True, "A second physical flush confirms the selected map.");
            Assert.That(entryRoom.Departing, Is.True);
            Assert.That(SceneManager.GetSceneByPath(mapPath).isLoaded, Is.False, "The suction animation runs before map loading.");
            yield return new WaitForSecondsRealtime(.95f);
            Assert.That(world.OfflinePlayer.transform.localScale.x, Is.LessThan(.9f));
            CaptureRoom(world.Camera.Output, "suction-character-" + mapIndex + ".png");
            yield return AwaitPlayableMap(mapPath);
            Assert.That(Match.Mode, Is.EqualTo(MatchMode.Offline));
            Assert.That(MatchContext.Phase, Is.EqualTo(MatchPhase.Reveal).Or.EqualTo(MatchPhase.Playing), "The versus round has entered Reveal or Playing.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(mapPath));
            var map = Object.FindFirstObjectByType<Playground>();
            Assert.That(map.OfflinePlayer, Is.Not.Null);
            AssertLocalCamera(map, map.OfflinePlayer);
            Assert.That(map.Camera.FirstPerson.Priority.Value, Is.GreaterThan(map.Camera.ThirdPerson.Priority.Value));
            var appearance = map.OfflinePlayer.GetComponent<PlayerAppearance>();
            Assert.That(appearance.OutfitIndex, Is.EqualTo(outfit), "The DressingRoom outfit is worn on the map.");
            Assert.That(appearance.BodyType, Is.EqualTo(PlayerBodyType.Tall));

            if (mapIndex == 3)
            {
                var shaft = Object.FindObjectsByType<SewerAccess>(FindObjectsSortMode.None).Single(item => !item.Ascending);
                var person = map.OfflinePlayer;
                yield return Teleport(person, shaft.Top.position + Vector3.forward * 1.1f);
                var context = new InteractionContext(person, 0, false, null);
                Assert.That(shaft.IsOpen, Is.False, "The hatch starts closed.");
                Assert.That(shaft.ClosedCover.enabled, Is.True);
                Assert.That(shaft.Perform(context), Is.True);
                Assert.That(shaft.IsOpen, Is.True);
                Assert.That(person.IsTraversing, Is.False, "Opening the hatch does not start traversal.");
                Assert.That(shaft.Perform(context), Is.True, "The hatch can be closed from outside.");
                Assert.That(shaft.IsOpen, Is.False);
                Assert.That(shaft.Perform(context), Is.True);
                yield return Teleport(person, shaft.Top.position + Vector3.forward * .4f);
                person.Move(Vector2.up, .01f);
                yield return null;
                Assert.That(person.IsTraversing, Is.True, "Walking into an open hatch starts descent.");
                yield return new WaitForSeconds(1.2f);
                Assert.That(person.transform.position.y, Is.LessThan(shaft.Top.position.y - 1f));
                var ladder = Object.FindObjectsByType<SewerAccess>(FindObjectsSortMode.None).Single(item => item.Ascending);
                yield return Teleport(person, ladder.Landing.position + ladder.ApproachDirection * ladder.ApproachDistance);
                Assert.That(ladder.Perform(context), Is.True, "B continues to return the player via the ladder.");
                yield return new WaitForSeconds(1.8f);
                Assert.That(person.transform.position.y, Is.GreaterThan(ladder.Landing.position.y + 1f));
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

            // A server-owned fixture supplies a prepared second member until confirmation.
            // This is a one-process loading regression, not a real two-client network test.
            room.Members.Add(new LobbyMember { ClientId = 999, Slot = 1, Ready = true });
            Assert.That(room.CanStart, Is.True);
            room.RequestStart();
            yield return null;
            Assert.That(lobby.IsMapPanelOpen, Is.True);
            ShortenRound(room.Maps, 20, mapIndex);
            LobbyRoom.RevealDurationSeconds = 0;
            lobby.ChooseMap(mapIndex);
            yield return null; yield return null;
            Assert.That(room.Phase.Value, Is.EqualTo(LobbyPhase.ConfirmingMap));
            Assert.That(room.SelectedMap.Value, Is.EqualTo(mapIndex));
            lobby.ConfirmMapSelection();
            room.Members.RemoveAt(1); // The fixture has no transport client to report scene-loaded.
            deadline = Time.realtimeSinceStartup + 10;
            Avatar avatar = null;
            while (avatar == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                avatar = Object.FindFirstObjectByType<Avatar>();
            }
            Assert.That(avatar, Is.Not.Null, "The host's player spawns once the map has loaded.");
            yield return AwaitPlayableMap(mapPath);
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
            Assert.That(room.Roles, Has.Count.EqualTo(1), "Host publishes a role entry.");
            Assert.That(RoleRegistry.Of(avatar.GetComponent<Movement>()), Is.EqualTo(room.Roles[0].Role), "Host applies the published role locally.");
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


            // The global round clock and the needs start once everyone is on the map.
            Assert.That(room.RoundRemaining, Is.InRange(0, Match.RoundSeconds + MatchRules.CleanupSeconds + MatchRules.RevealSeconds));
            yield return null;
            Assert.That(ClockLabel().enabled, Is.True, "The map HUD shows the round clock.");
            var needs = avatar.GetComponent<PlayerNeeds>();
            Assert.That(needs.Ticking, Is.True, "The host runs every player's needs.");
            float elapsedNeeds = Mathf.Max(0, 20 - (float)room.RoundRemaining);
            float urgencyBound = Mathf.Min(NeedsRules.UrgencyMax, NeedsRules.InitialUrgencyMax + elapsedNeeds * NeedsRules.UrgencyPerSecond + .1f);
            Assert.That(needs.Urgency, Is.InRange(0, urgencyBound), "Needs may grow while the map interaction checks run.");

            // Reaching a need threshold no longer ends an versus match.
            needs.ApplyShared(NatureCalls.Core.NeedsRules.UrgencyMax, 0);
            yield return new WaitForSeconds(.2f);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(mapPath), "A threshold does not end the versus match.");
            Assert.That(room.VersusPhase.Value, Is.EqualTo(MatchPhase.Playing));
            // Force the shortened match end, then observe Ended before the 8-second settlement hold.
            LobbyRoom.SettlementDurationSeconds = 0;
            room.EndForTests(MatchWinner.Keepers);
            deadline = Time.realtimeSinceStartup + 3;
            while (room.VersusPhase.Value != MatchPhase.Ended && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(room.VersusPhase.Value, Is.EqualTo(MatchPhase.Ended));
            deadline = Time.realtimeSinceStartup + 3;
            while (SceneManager.GetActiveScene().path != ScenePath && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath));
            Assert.That(room.Phase.Value, Is.EqualTo(LobbyPhase.Waiting));
        }

        [UnityTest]
        public IEnumerator OfflineRoundEndsInTheDressingRoomWhenTimeIsUp()
        {
            lobby.OpenLobby();
            lobby.StartOffline();
            ShortenRound(Resources.FindObjectsOfTypeAll<MapCatalog>()[0], 1);
            Match.CleanupDurationSeconds = 0;
            Match.SettlementDurationSeconds = 0;
            lobby.ChooseMap(0);
            Assert.That(lobby.AwaitingMapConfirmation, Is.True);
            lobby.ConfirmMapSelection();
            yield return AwaitPlayableMap("Assets/Scenes/Playground.unity");
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

        private static void AimAt(Movement player, Component target)
        {
            var interactable = target as NatureCalls.Facility.IInteractable;
            if (interactable != null) { LookAt(player, interactable.AimPoint); return; }
            var colliders = target.GetComponentsInChildren<Collider>();
            var bounds = colliders.Length > 0 ? colliders[0].bounds : new Bounds(target.transform.position, Vector3.zero);
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            LookAt(player, bounds.center);
        }
        private static void LookAt(Movement player, Vector3 target)
        {
            var angles = Quaternion.LookRotation(target - player.transform.position - Vector3.up * 1.65f).eulerAngles;
            player.Look(new Vector2(Mathf.DeltaAngle(player.transform.eulerAngles.y, angles.y) / player.Sensitivity,
                -Mathf.DeltaAngle(player.Pitch, angles.x) / player.Sensitivity));
        }

        private static IEnumerator AwaitPlayableMap(string path)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (Time.realtimeSinceStartup < deadline)
            {
                var map = Object.FindFirstObjectByType<Playground>();
                if (SceneManager.GetActiveScene().path == path && map != null && map.Camera.Target != null &&
                    map.Camera.Target.Controlled && Object.FindFirstObjectByType<DepartureFade>() == null) yield break;
                yield return null;
            }
            Assert.Fail("The destination map and local controls did not become ready: " + path);
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
