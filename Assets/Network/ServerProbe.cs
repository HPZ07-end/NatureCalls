#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NatureCalls.Core;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using NatureCalls.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NatureCalls.Network
{
    /// <summary>Explicit three-process test; marker files coordinate steps, never game state.</summary>
    public sealed class ServerProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public string role, status = "running", error = "";
            public bool connected, mapLoaded, appearance, facilities, leaderSuccession, returned, empty, peerVisible, lanList;
            public int members;
            public int roomCode;
        }
        private Report report;
        private string directory;
        private bool finished;
        private bool RemoteMode => HasArgument("-probe-remote");
        private bool LanMode => HasArgument("-probe-lan") || HasArgument("-probe-lan-host");
        private int MapIndex => int.TryParse(Argument("-probe-map"), out var index) ? index : 0;
        private string Marker(string name) => Path.Combine(directory, name + ".marker");
        private void Mark(string name) => File.WriteAllText(Marker(name), "observed");
        private bool Marked(string name) => File.Exists(Marker(name));
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
        private static bool HasArgument(string name) => Environment.GetCommandLineArgs().Contains(name);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!Environment.GetCommandLineArgs().Contains("-server-probe")) return;
            var probe = new GameObject("Explicit server verification").AddComponent<ServerProbe>();
            DontDestroyOnLoad(probe.gameObject);
            probe.directory = Argument("-probe-directory");
            probe.report = new Report { role = Argument("-probe-role") };
        }
        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(directory)) { Debug.LogError("Missing probe directory"); Application.Quit(1); yield break; }
            if (report.role == "server") yield return Server();
            else if (RemoteMode) yield return RemoteClient();
            else if (LanMode) yield return LanClient();
            else yield return Client();
        }
        private IEnumerator Await(Func<bool> condition, string label, float seconds = 15)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!finished && !condition() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!finished && !condition()) Finish("Timed out: " + label);
        }
        private IEnumerator Server()
        {
            yield return Await(() => LobbyRoom.Current != null, "server startup");
            if (finished) yield break;
            var session = Session.Instance;
            var room = LobbyRoom.Current;
            if (!session.Manager.IsServer || session.Manager.IsClient || room.Members.Count != 0 ||
                FindFirstObjectByType<Playground>().OfflinePlayer != null || UnityEngine.Camera.allCamerasCount != 0 || Application.targetFrameRate != 30)
            { Finish("Server created a local player, camera, member or did not cap frame rate."); yield break; }
            room.Maps[MapIndex].RoundSeconds = 45;
            Mark("server-ready");
            yield return Await(() => room.Members.Count == 2, "two members");
            if (finished) yield break;
            report.connected = !Enumerable.Range(0, room.Members.Count).Any(i => room.Members[i].ClientId == Unity.Netcode.NetworkManager.ServerClientId);
            yield return Await(() => Facilities.Current != null && FindObjectsByType<Avatar>(FindObjectsSortMode.None).Length == 2, "server map");
            if (finished) yield break;
            report.mapLoaded = Facilities.Current.IsOwnedByServer && !Facilities.Current.NetworkObject.IsPlayerObject;
            yield return Await(() => Marked("leader-facilities") && Marked("follower-facilities"), "client facility verification", 60);
            if (finished) yield break;
            report.facilities = Facilities.Current.SharedMesses.Count == 2;
            yield return Await(() => Marked("succession"), "leader succession");
            if (finished) yield break;
            report.leaderSuccession = room.Members.Count == 1;
            yield return Await(() => Marked("follower-returned"), "round return", 40);
            if (finished) yield break;
            report.returned = room.Phase.Value == LobbyPhase.Waiting && Facilities.Current == null;
            yield return Await(() => room.Members.Count == 0, "empty room");
            if (finished) yield break;
            report.empty = session.Manager.IsListening && room.Phase.Value == LobbyPhase.Waiting &&
                room.RoundEndTime.Value < 0 && FindObjectsByType<Avatar>(FindObjectsSortMode.None).Length == 0;
            Finish(report.connected && report.mapLoaded && report.facilities && report.leaderSuccession && report.returned && report.empty ? "" : "Server assertion failed.");
        }
        private IEnumerator Client()
        {
            bool leader = report.role == "leader";
            yield return Await(() => Marked(leader ? "server-ready" : "leader-connected"), "join order", 40);
            if (finished) yield break;
            var lobby = FindFirstObjectByType<LobbyController>();
            lobby.OpenLobby();
            var joinText = Argument("-probe-join");
            lobby.GetComponent<LobbyView>().CodeInput.text = string.IsNullOrEmpty(joinText)
                ? "127.0.0.1:" + Argument("-port") : joinText;
            lobby.JoinRoom();
            yield return Await(() => LobbyRoom.Current != null && LobbyRoom.Current.TryGetMember(Session.Instance.Manager.LocalClientId, out _), "room membership");
            if (finished) yield break;
            var room = LobbyRoom.Current;
            report.connected = room.IsLeader == leader;
            if (!report.connected) { Finish("Wrong initial leader."); yield break; }
            room.SetAppearance(leader ? PlayerBodyType.Tall : PlayerBodyType.Balanced, leader ? 1 : 2);
            yield return Await(() => room.TryGetMember(room.LocalClientId, out var local) &&
                local.BodyType == (leader ? PlayerBodyType.Tall : PlayerBodyType.Balanced) && local.Outfit == (leader ? 1 : 2),
                "confirmed appearance in room");
            if (finished) yield break;
            if (leader) Mark("leader-connected");
            else
            {
                room.SetReady(true);
                yield return Await(() => room.CanStart, "ready replication");
                if (finished) yield break;
                room.RequestStart(); // A ready non-leader must still be rejected.
                yield return new WaitForSecondsRealtime(.4f);
                if (room.Phase.Value != LobbyPhase.Waiting) { Finish("Non-leader started the round."); yield break; }
                Mark("follower-ready");
            }
            if (leader) yield return Await(() => Marked("follower-ready") && room.CanStart, "ready members");
            if (finished) yield break;
            if (leader) lobby.PressPrimary();
            yield return Await(() => room.Phase.Value == LobbyPhase.SelectingMap, "leader start");
            if (finished) yield break;
            Mark(report.role + "-selecting");
            yield return Await(() => Marked("leader-selecting") && Marked("follower-selecting"), "selection seen");
            if (finished) yield break;
            if (leader) lobby.ChooseMap(MapIndex);
            yield return Await(() => room.Phase.Value == LobbyPhase.ConfirmingMap, "map pending confirmation");
            if (finished) yield break;
            if (leader) lobby.ConfirmMapSelection();
            yield return Await(() => Facilities.Current != null && FindObjectsByType<Avatar>(FindObjectsSortMode.None).Count(a => a.IsSpawned) == 2, "client map");
            if (finished) yield break;
            var own = FindObjectsByType<Avatar>(FindObjectsSortMode.None).Single(a => a.IsOwner);
            report.appearance = FindObjectsByType<Avatar>(FindObjectsSortMode.None).All(player =>
            {
                var first = player.OwnerClientId == room.LeaderId;
                var appearance = player.GetComponent<PlayerAppearance>();
                var shared = player.GetComponent<AvatarAppearance>();
                var bodyType = first ? PlayerBodyType.Tall : PlayerBodyType.Balanced;
                var outfit = first ? 1 : 2;
                return appearance.BodyType == bodyType && appearance.OutfitIndex == outfit &&
                    shared.BodyType.Value == bodyType && shared.OutfitIndex.Value == outfit &&
                    appearance.ActiveAnimator != null && appearance.ActiveAnimator.gameObject.activeInHierarchy;
            });
            if (!report.appearance) { Finish("A local or remote Player did not show the confirmed bodyType and outfit."); yield break; }
            var world = FindFirstObjectByType<Playground>();
            var screenCameras = UnityEngine.Camera.allCameras
                .Where(camera => camera.isActiveAndEnabled && camera.targetTexture == null).ToArray();
            report.mapLoaded = Match.MapIndex == MapIndex && world.OfflinePlayer == null &&
                world.Camera.Target == own.GetComponent<Movement>() && screenCameras.Length == 1 &&
                screenCameras[0] == world.Camera.Output &&
                Facilities.Current.IsOwnedByServer && !Facilities.Current.NetworkObject.IsPlayerObject;
            var movement = own.GetComponent<Movement>();
            var input = own.GetComponent<NatureCalls.Input.PlayerInput>();
            var sync = own.GetComponent<FacilitySync>();
            input.enabled = false;
            var controller = own.GetComponent<CharacterController>();
            controller.enabled = false;
            var door = Facilities.Current.doors[0];
            var roll = Facilities.Current.items.OfType<PaperRoll>().First();
            int rollIndex = Array.IndexOf(Facilities.Current.items, roll);
            if (leader) movement.transform.position = door.DoorColliderPivot.position - door.transform.forward * .35f;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Step("door", leader, () => sync.RequestDoor(door), () => door.State == StallDoorState.Open);
            if (finished) yield break;
            if (!leader) movement.transform.position = roll.transform.position + Vector3.right * .3f;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Step("pickup", !leader, () => sync.RequestPickup(roll), () => roll.IsPickedUp && roll.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled));
            if (finished) yield break;
            if (!leader) movement.transform.position = world.Spawns[1].position;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Step("throw", !leader, () => sync.RequestThrow(roll, Vector3.forward), () => !roll.IsPickedUp && roll.GetComponentsInChildren<Renderer>(true).All(r => r.enabled));
            if (finished) yield break;
            yield return Await(() => Facilities.Current.SharedItems[rollIndex].Mode == ItemSnapshot.Landed, "paper landing");
            if (finished) yield break;
            var toilet = door.GetComponentInChildren<Toilet>();
            if (leader) movement.transform.position = door.DoorColliderPivot.position + door.transform.forward * .35f;
            yield return new WaitForSecondsRealtime(.6f);
            // Enable real timed C handling; input has no physical keys in this explicit probe.
            input.enabled = true;
            yield return Step("seat", leader, () => input.TryToilet() && input.TryToilet(), () => toilet.IsOccupied);
            if (finished) yield break;
            yield return Step("seat-released", false, null, () => !toilet.IsOccupied);
            if (finished) yield break;
            if (!leader) movement.transform.position = world.Spawns[1].position;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Step("outside", !leader, () => input.TryToilet(), () => Facilities.Current.SharedMesses.Count == 2 && world.DroppingCount == 2);
            if (finished) yield break;
            var box = FindFirstObjectByType<Toolbox>();
            if (!leader) movement.transform.position = box.transform.position;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Step("mop-take", !leader, () => sync.RequestToolbox(box), () => box.Mop.IsPickedUp);
            if (finished) yield break;
            yield return Step("mop-return", !leader, () => sync.RequestToolbox(box), () => box.Mop.Stored && !box.Mop.IsPickedUp);
            if (finished) yield break;
            report.facilities = true;
            Mark(report.role + "-facilities");
            yield return Await(() => Marked("leader-facilities") && Marked("follower-facilities"), "both clients verified");
            if (finished) yield break;
            if (leader)
            {
                var leaving = Session.Instance.LeaveAsync();
                yield return Await(() => leaving.IsCompleted, "leader leave");
                if (!finished) Finish(report.mapLoaded ? "" : "Client map assertion failed.");
                yield break;
            }
            yield return Await(() => room.Members.Count == 1 && room.IsLeader, "successor");
            if (finished) yield break;
            report.leaderSuccession = true;
            Mark("succession");
            yield return Await(() => room.Phase.Value == LobbyPhase.Waiting && Match.Mode == MatchMode.None &&
                FindFirstObjectByType<Playground>()?.OfflinePlayer != null, "return to room", 40);
            if (finished) yield break;
            report.returned = Facilities.Current == null && room.Members.Count == 1 && room.IsLeader;
            Mark("follower-returned");
            var leave = Session.Instance.LeaveAsync();
            yield return Await(() => leave.IsCompleted, "follower leave");
            if (!finished) Finish(report.mapLoaded && report.returned ? "" : "Follower assertion failed.");
        }

        private IEnumerator RemoteClient()
        {
            bool leader = report.role == "leader";
            var lobby = FindFirstObjectByType<LobbyController>();
            lobby.OpenLobby();
            if (leader && HasArgument("-probe-create-server-room"))
            {
                lobby.CreateServerRoom();
                yield return Await(() => Session.Instance != null && Session.Instance.ServerRoomCode >= 100 && LobbyRoom.Current != null,
                    "server room creation", 40);
                if (finished) yield break;
                report.roomCode = Session.Instance.ServerRoomCode;
                File.WriteAllText(Path.Combine(directory, "room-code.txt"), report.roomCode.ToString());
            }
            else
            {
                yield return Await(() => File.Exists(Path.Combine(directory, "room-code.txt")), "leader room code", 40);
                if (finished) yield break;
                var join = Argument("-probe-join");
                if (string.IsNullOrEmpty(join)) join = File.ReadAllText(Path.Combine(directory, "room-code.txt")).Trim();
                lobby.GetComponent<LobbyView>().CodeInput.text = join;
                lobby.JoinRoom();
            }
            yield return Await(() => LobbyRoom.Current != null &&
                LobbyRoom.Current.TryGetMember(Session.Instance.Manager.LocalClientId, out _), "room membership", 40);
            if (finished) yield break;
            var room = LobbyRoom.Current;
            report.members = room.Members.Count;
            report.connected = room.IsLeader == leader;
            if (!report.connected) { Finish("Wrong initial leader."); yield break; }
            if (leader) Mark("leader-connected");
            yield return Await(() => room.Members.Count == 2, "two members", 40);
            if (finished) yield break;
            report.members = room.Members.Count;
            room.SetAppearance(leader ? PlayerBodyType.Tall : PlayerBodyType.Balanced, leader ? 1 : 2);
            yield return Await(() => HasBothAppearances(room), "appearance sync", 20);
            if (finished) yield break;
            if (leader)
            {
                yield return Await(() => Marked("remote-follower-ready"), "follower ready", 30);
                lobby.PressPrimary();
            }
            else
            {
                room.SetReady(true);
                yield return Await(() => room.CanStart, "ready replication", 20);
                if (finished) yield break;
                Mark("remote-follower-ready");
            }
            yield return Await(() => room.Phase.Value == LobbyPhase.SelectingMap, "map selection", 20);
            if (finished) yield break;
            if (leader) lobby.ChooseMap(MapIndex);
            yield return Await(() => FindObjectsByType<Avatar>(FindObjectsSortMode.None).Count(a => a.IsSpawned) == 2,
                "two visible players", 60);
            if (finished) yield break;
            var avatars = FindObjectsByType<Avatar>(FindObjectsSortMode.None);
            report.peerVisible = avatars.Length == 2 && avatars.All(a => a.IsSpawned &&
                a.GetComponent<PlayerAppearance>() != null && a.GetComponent<PlayerAppearance>().ActiveAnimator != null);
            report.appearance = HasWorldAppearance(room);
            if (!report.peerVisible || !report.appearance) { Finish("Remote client peer or appearance assertion failed."); yield break; }
            Mark("remote-" + report.role + "-verified");
            yield return Await(() => Marked("remote-leader-verified") && Marked("remote-follower-verified"), "peer verification", 20);
            if (finished) yield break;
            var leaving = Session.Instance.LeaveAsync();
            yield return Await(() => leaving.IsCompleted, "client leave", 20);
            if (!finished) Finish("");
        }

        private IEnumerator LanClient()
        {
            bool host = HasArgument("-probe-lan-host");
            var lobby = FindFirstObjectByType<LobbyController>();
            lobby.OpenLobby();
            if (host)
            {
                lobby.PreferLan = true;
                lobby.CreateRoom();
                yield return Await(() => LobbyRoom.Current != null && LobbyRoom.Current.TryGetMember(Session.Instance.Manager.LocalClientId, out _), "LAN host", 30);
                if (finished) yield break;
                Mark("lan-host-connected");
            }
            else
            {
                yield return Await(() => Marked("lan-host-connected"), "LAN host marker", 30);
                if (finished) yield break;
                var view = lobby.GetComponent<LobbyView>();
                Button found = null;
                float deadline = Time.realtimeSinceStartup + 15;
                while (found == null && Time.realtimeSinceStartup < deadline)
                {
                    if (view.LanRooms != null)
                        found = view.LanRooms.GetComponentsInChildren<Button>(false).FirstOrDefault(button => button.gameObject.activeInHierarchy && button.interactable);
                    if (found == null) yield return null;
                }
                if (found == null) { Finish("LAN room list did not produce an actionable button."); yield break; }
                report.lanList = true;
                found.onClick.Invoke();
            }
            yield return Await(() => LobbyRoom.Current != null && LobbyRoom.Current.TryGetMember(Session.Instance.Manager.LocalClientId, out _), "LAN membership", 30);
            if (finished) yield break;
            var room = LobbyRoom.Current;
            yield return Await(() => room.Members.Count == 2, "LAN two members", 30);
            if (finished) yield break;
            report.members = room.Members.Count;
            report.connected = room.IsLeader == host;
            if (!report.connected) { Finish("LAN initial leader assertion failed."); yield break; }
            Mark("lan-" + (host ? "host" : "follower") + "-verified");
            yield return Await(() => Marked("lan-host-verified") && Marked("lan-follower-verified"), "LAN peer verification", 20);
            if (finished) yield break;
            var leaving = Session.Instance.LeaveAsync();
            yield return Await(() => leaving.IsCompleted, "LAN client leave", 20);
            if (!finished) Finish("");
        }

        private static bool HasAppearance(LobbyRoom room, PlayerBodyType bodyType, int outfit)
        {
            for (int index = 0; index < room.Members.Count; index++)
                if (room.Members[index].BodyType == bodyType && room.Members[index].Outfit == outfit) return true;
            return false;
        }

        private static bool HasBothAppearances(LobbyRoom room)
        {
            return room.Members.Count == 2 &&
                HasAppearance(room, PlayerBodyType.Tall, 1) &&
                HasAppearance(room, PlayerBodyType.Balanced, 2);
        }

        private static bool HasWorldAppearance(LobbyRoom room)
        {
            return FindObjectsByType<Avatar>(FindObjectsSortMode.None).All(player =>
            {
                bool leader = player.OwnerClientId == room.LeaderId;
                var appearance = player.GetComponent<PlayerAppearance>();
                var shared = player.GetComponent<AvatarAppearance>();
                return appearance != null && shared != null &&
                    appearance.BodyType == (leader ? PlayerBodyType.Tall : PlayerBodyType.Balanced) &&
                    appearance.OutfitIndex == (leader ? 1 : 2) &&
                    shared.BodyType.Value == (leader ? PlayerBodyType.Tall : PlayerBodyType.Balanced) &&
                    shared.OutfitIndex.Value == (leader ? 1 : 2);
            });
        }
        private IEnumerator Step(string name, bool acting, Func<bool> request, Func<bool> observed)
        {
            if (acting)
            {
                if (!request()) { Finish("Request rejected locally: " + name); yield break; }
                Mark(name + "-issued");
            }
            yield return Await(() => (request == null || Marked(name + "-issued")) && observed(), name, 10);
            if (finished) yield break;
            Mark(name + "-" + report.role);
            yield return Await(() => Marked(name + "-leader") && Marked(name + "-follower"), name + " peer", 10);
        }
        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup > 150) Finish("Probe timeout.");
        }
        private void Finish(string error)
        {
            if (finished) return;
            finished = true;
            report.error = error;
            report.status = string.IsNullOrEmpty(error) ? "passed" : "failed";
            File.WriteAllText(Path.Combine(directory, report.role + ".json"), JsonUtility.ToJson(report, true));
            Debug.Log("Server probe " + report.role + ": " + report.status + " " + error);
            Application.Quit(string.IsNullOrEmpty(error) ? 0 : 1);
        }
    }
}
#endif
