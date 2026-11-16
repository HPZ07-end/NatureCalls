#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Map;
using NatureCalls.Player;
using NatureCalls.UI;
using UnityEngine;

namespace NatureCalls.Network
{
    /// <summary>Explicit three-process test; marker files coordinate steps, never game state.</summary>
    public sealed class ServerProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public string role, status = "running", error = "";
            public bool connected, mapLoaded, facilities, leaderSuccession, returned, empty;
        }
        private Report report;
        private string directory;
        private bool finished;
        private string Marker(string name) => Path.Combine(directory, name + ".marker");
        private void Mark(string name) => File.WriteAllText(Marker(name), "observed");
        private bool Marked(string name) => File.Exists(Marker(name));
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
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
            yield return report.role == "server" ? Server() : Client();
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
            room.Maps[0].RoundSeconds = 45;
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
            lobby.GetComponent<LobbyView>().CodeInput.text = "127.0.0.1:" + Argument("-port");
            lobby.JoinRoom();
            yield return Await(() => LobbyRoom.Current != null && LobbyRoom.Current.TryGetMember(Session.Instance.Manager.LocalClientId, out _), "room membership");
            if (finished) yield break;
            var room = LobbyRoom.Current;
            report.connected = room.IsLeader == leader;
            if (!report.connected) { Finish("Wrong initial leader."); yield break; }
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
            if (leader) lobby.ChooseMap(0);
            yield return Await(() => Facilities.Current != null && FindObjectsByType<Avatar>(FindObjectsSortMode.None).Count(a => a.IsSpawned) == 2, "client map");
            if (finished) yield break;
            var own = FindObjectsByType<Avatar>(FindObjectsSortMode.None).Single(a => a.IsOwner);
            var world = FindFirstObjectByType<Playground>();
            report.mapLoaded = world.OfflinePlayer == null && Facilities.Current.IsOwnedByServer && !Facilities.Current.NetworkObject.IsPlayerObject;
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
