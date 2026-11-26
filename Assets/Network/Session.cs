using NatureCalls.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Multiplayer;
using NatureCalls.Map;
using UnityEngine;
using UnityEngine.Networking;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Network
{
    public sealed class Session : MonoBehaviour
    {
        /// <summary>The persistent Session; a scene's own Network object yields to one carried over from the Lobby.</summary>
        public static Session Instance { get; private set; }
        public static bool CloudLinked => Guid.TryParse(Application.cloudProjectId, out _);

        public NetworkManager Manager;
        public UnityTransport Transport;
        // Set only on a map scene's own Network object (direct development play). Lobby rooms leave it empty.
        public Map.Playground World;
        public GameObject LobbyRoomPrefab;
        public GameObject FacilitiesPrefab;
        public string EnvironmentName = "production";
        [Range(2, 4)] public int MaxPlayers = 4;
        public ushort Port = 7777;
        [Header("Room broker")]
        public string BrokerHost = "49.233.132.244";
        public ushort BrokerPort = 8100;
        public int ServerRoomCode { get; private set; }
        public string Status { get; private set; } = "Offline practice / Cloud not connected";
        public bool Busy { get; private set; }
        public bool Active => session != null || Manager.IsListening || Manager.ShutdownInProgress;
        public string JoinCode => session?.Code ?? "";
        public bool IsRelay => session != null;
        /// <summary>Lobby rooms spawn players only after the host picks a map; direct play spawns them on connect.</summary>
        public bool LobbyMode { get; set; }
        private ISession session;
        private readonly Dictionary<ulong, int> slots = new();
        private bool destroyed;
        private IdleExitTimer idleExit;
        private bool idleExitRequested;
        private LanBeaconBroadcaster beacon;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                destroyed = true;
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ApplyBrokerArgument(Environment.GetCommandLineArgs());
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnServerStarted += ServerStarted;
            Manager.OnClientConnectedCallback += Connected;
            Manager.OnClientDisconnectCallback += Disconnected;
            Manager.OnClientStopped += Stopped;
            Manager.OnTransportFailure += TransportFailed;
        }
        private void Start()
        {
            if (!RuntimeMode.Dedicated || destroyed) return;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            var args = Environment.GetCommandLineArgs();
            ApplyBrokerArgument(args);
            int index = Array.IndexOf(args, "-port");
            Port = 8000;
            if (index >= 0 && (index + 1 >= args.Length || !ushort.TryParse(args[index + 1], out Port) || Port == 0))
            {
                Debug.LogError("SERVER_INVALID_PORT");
                Application.Quit(1);
                return;
            }
            idleExit = new IdleExitTimer(ParseIdleExit(args));
            if (!StartServer()) { Debug.LogError(Status); Application.Quit(1); }
        }

        public void ApplyBrokerArgument(string[] args) => (BrokerHost, BrokerPort) = ResolveBroker(BrokerHost, BrokerPort, args);

        /// <summary>The broker endpoint after an optional <c>-broker host[:port]</c> override.</summary>
        public static (string host, ushort port) ResolveBroker(string host, ushort port, string[] args)
        {
            int index = Array.IndexOf(args ?? Array.Empty<string>(), "-broker");
            if (index < 0 || index + 1 >= args.Length) return (host, port);
            var value = args[index + 1];
            var split = value.LastIndexOf(':');
            if (split > 0 && ushort.TryParse(value.Substring(split + 1), out var parsed) && parsed > 0)
                return (value.Substring(0, split), parsed);
            return string.IsNullOrWhiteSpace(value) ? (host, port) : (value, port);
        }

        [Serializable] private sealed class BrokerRoomResponse { public int protocol; public int code; public int port; public string error; }
        private static Task<UnityWebRequest> SendAsync(UnityWebRequest request)
        {
            var completion = new TaskCompletionSource<UnityWebRequest>();
            request.SendWebRequest().completed += _ => completion.TrySetResult(request);
            return completion.Task;
        }

        public async Task<bool> CreateServerRoomAsync()
        {
            if (Busy || Active) return false;
            Busy = true;
            try
            {
                using var request = new UnityWebRequest($"http://{BrokerHost}:{BrokerPort}/rooms", "POST");
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;
                await SendAsync(request);
                var response = JsonUtility.FromJson<BrokerRoomResponse>(request.downloadHandler.text);
                if (response != null && response.protocol != 1)
                    throw new InvalidOperationException("客户端版本与服务器不一致，请更新。");
                if (request.result != UnityWebRequest.Result.Success || response == null || response.port <= 0)
                    throw new InvalidOperationException(string.IsNullOrEmpty(response?.error) ? "服务器网络不通。" : response.error);
                ServerRoomCode = response.code;
                LobbyMode = true;
                Busy = false;
                if (!StartDirect(false, $"{BrokerHost}:{response.port}")) throw new InvalidOperationException(Status);
                Status = $"服务器房间 {ServerRoomCode} 已创建。";
                return true;
            }
            catch (Exception error) { Status = "创建服务器房间失败：" + error.Message; return false; }
            finally { Busy = false; }
        }

        [Serializable] private sealed class BrokerRoomEntry { public int code; public int port; }
        [Serializable] private sealed class BrokerRoomList { public int protocol; public BrokerRoomEntry[] rooms; public string error; }

        /// <summary>Server room codes currently alive on the broker; null with <paramref name="error"/> set when the broker is unreachable.</summary>
        public static async Task<(int[] codes, string error)> ListServerRoomsAsync(string brokerHost, ushort brokerPort)
        {
            try
            {
                using var request = UnityWebRequest.Get($"http://{brokerHost}:{brokerPort}/rooms");
                request.timeout = 10;
                await SendAsync(request);
                var response = request.downloadHandler == null ? null : JsonUtility.FromJson<BrokerRoomList>(request.downloadHandler.text ?? "");
                if (request.result != UnityWebRequest.Result.Success || response == null)
                    return (null, request.responseCode == 404 ? "服务器版本过旧，暂不支持房间列表。" : "无法连接课程服务器。");
                if (response.protocol != 1) return (null, "客户端版本与服务器不一致，请更新。");
                var rooms = response.rooms ?? Array.Empty<BrokerRoomEntry>();
                var codes = new int[rooms.Length];
                for (int index = 0; index < rooms.Length; index++) codes[index] = rooms[index].code;
                return (codes, null);
            }
            catch (Exception error) { return (null, "无法连接课程服务器：" + error.Message); }
        }

        public async Task<bool> JoinServerRoomAsync(int code)
        {
            if (Busy || Active) return false;
            Busy = true;
            try
            {
                using var request = UnityWebRequest.Get($"http://{BrokerHost}:{BrokerPort}/rooms/{code}");
                request.timeout = 10;
                await SendAsync(request);
                var response = JsonUtility.FromJson<BrokerRoomResponse>(request.downloadHandler.text);
                if (response != null && response.protocol != 1)
                    throw new InvalidOperationException("客户端版本与服务器不一致，请更新。");
                if (request.result != UnityWebRequest.Result.Success || response == null || response.port <= 0)
                    throw new InvalidOperationException(request.responseCode == 404 ? "房间不存在或已关闭。" : response?.error ?? "服务器网络不通。");
                ServerRoomCode = code;
                LobbyMode = true;
                Busy = false;
                return StartDirect(false, $"{BrokerHost}:{response.port}");
            }
            catch (Exception error) { Status = "加入服务器房间失败：" + error.Message; return false; }
            finally { Busy = false; }
        }

        private static int ParseIdleExit(string[] args)
        {
            int index = Array.IndexOf(args, "-idle-exit");
            if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out var seconds) || seconds <= 0)
                return -1;
            return seconds;
        }

        private void Update()
        {
            if (!RuntimeMode.Dedicated || idleExit == null || idleExitRequested || Manager == null) return;
            if (idleExit.Tick(Time.unscaledDeltaTime, Manager.ConnectedClientsIds.Count))
            {
                idleExitRequested = true;
                Debug.Log("No clients connected for the configured idle period; exiting.");
                Application.Quit(0);
            }
        }

        public bool StartServer()
        {
            if (Busy || Active) return false;
            LobbyMode = true;
            slots.Clear();
            Transport.UseWebSockets = false;
            UsingWebSockets = false;
            Transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
            bool started = Manager.StartServer();
            Status = started ? $"Server listening on UDP {Port}." : "Server transport could not start.";
            Debug.Log(Status);
            return started;
        }

        /// <summary>Literal IP, IP:port, or [IPv6]:port; no DNS or Cloud lookup.</summary>
        public static bool TryAddress(string value, ushort defaultPort, out string address, out ushort port)
        {
            address = "";
            port = defaultPort;
            value = value?.Trim() ?? "";
            if (!value.Contains("]:") && System.Net.IPAddress.TryParse(value, out var ip)) { address = ip.ToString(); return port != 0; }
            int split = value.LastIndexOf(':');
            if (split <= 0 || !ushort.TryParse(value.Substring(split + 1), out port) || port == 0) return false;
            var host = value.Substring(0, split);
            if (host.StartsWith("[") && host.EndsWith("]")) host = host.Substring(1, host.Length - 2);
            if (!System.Net.IPAddress.TryParse(host, out ip)) return false;
            address = ip.ToString();
            return true;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            destroyed = true;
            if (Manager != null)
            {
                Manager.ConnectionApprovalCallback = null;
                Manager.OnServerStarted -= ServerStarted;
                Manager.OnClientConnectedCallback -= Connected;
                Manager.OnClientDisconnectCallback -= Disconnected;
                Manager.OnClientStopped -= Stopped;
                Manager.OnTransportFailure -= TransportFailed;
            }
            if (session != null) session.RemovedFromSession -= Removed;
        }
        public int Slot(ulong client) => slots.TryGetValue(client, out var slot) ? slot : 0;
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var capacity = LobbyMode ? MaxPlayers : Mathf.Min(MaxPlayers, World.Spawns.Length);
            var free = Enumerable.Range(0, capacity).Where(index => !slots.ContainsValue(index)).ToArray();
            response.Pending = false;
            response.Approved = free.Length > 0;
            if (!response.Approved) { response.Reason = "Room is full (maximum four players)."; return; }
            var room = LobbyRoom.Current;
            if (LobbyMode && room != null && room.Phase.Value != LobbyPhase.Waiting)
            {
                response.Approved = false;
                response.Reason = "The game has already started.";
                return;
            }
            int index = free[0];
            slots[request.ClientNetworkId] = index;
            response.CreatePlayerObject = !LobbyMode;
            if (LobbyMode) return;
            response.Position = World.Spawns[index].position;
            response.Rotation = World.Spawns[index].rotation;
        }
        private void ServerStarted()
        {
            if (!LobbyMode) { SpawnFacilities(); return; }
            Instantiate(LobbyRoomPrefab).GetComponent<NetworkObject>().Spawn();
        }
        public void SpawnFacilities()
        {
            if (Manager.IsServer && Facilities.Current == null)
                Instantiate(FacilitiesPrefab).GetComponent<NetworkObject>().Spawn(true);
        }
        private void Connected(ulong id)
        {
            if (id == Manager.LocalClientId) Status = Manager.IsHost ? "Host connected" : "Client connected";
        }
        private void Disconnected(ulong id)
        {
            slots.Remove(id);
            if (id == Manager.LocalClientId)
            {
                ServerRoomCode = 0;
            }
            if (id == Manager.LocalClientId && !Busy)
            {
                Status = string.IsNullOrEmpty(Manager.DisconnectReason) ? "Connection ended." : Manager.DisconnectReason;
                PlayerInput.Unlock();
            }
        }
        private void Stopped(bool wasHost)
        {
            ServerRoomCode = 0;
            if (destroyed || Busy) return;
            if (World != null) World.Camera.Unbind();
            PlayerInput.Unlock();
            Status = "Disconnected. You can restart practice or join again.";
            if (session != null) _ = LeaveAsync();
            else LobbyMode = false;
            if (Match.Mode == MatchMode.Networked) Match.ReturnToDressingRoom(wasHost ? "" : "与服务器的连接已断开。");
        }
        private void TransportFailed() { ServerRoomCode = 0; Status = "Transport failed. Check the address, firewall and host."; PlayerInput.Unlock(); }
        private void Removed() { ServerRoomCode = 0; session = null; Status = "Session closed or player removed."; PlayerInput.Unlock(); }

        private async Task Authenticate()
        {
            if (!Guid.TryParse(Application.cloudProjectId, out _))
                throw new InvalidOperationException("Unity Cloud is not linked. Use local tests, or link a Cloud project before using Relay.");
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions().SetEnvironmentName(EnvironmentName);
                // Separate anonymous identities for independent Editor/MPPM/standalone processes.
                options.SetProfile("m1_" + System.Diagnostics.Process.GetCurrentProcess().Id);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        // Relay normally runs over UDP/DTLS. Networks that block UDP (some campus or office networks) still allow
        // secure WebSockets on port 443, so a failed attempt is retried once over WSS and WSS is remembered.
        private const string WebSocketPreferenceKey = "NatureCalls.RelayWebSockets";
        public static bool RelayWebSockets
        {
            get => PlayerPrefs.GetInt(WebSocketPreferenceKey, 0) == 1 || Environment.GetCommandLineArgs().Contains("-relay-wss");
            set { PlayerPrefs.SetInt(WebSocketPreferenceKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        /// <summary>Whether the current Relay connection uses secure WebSockets.</summary>
        public bool UsingWebSockets { get; private set; }

        public async Task ConnectRelayAsync(bool host, string code = "")
        {
            if (Busy || Active) return;
            Busy = true;
            try
            {
                if (!host && string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Enter a Session join code.");
                Status = "Connecting to Unity Services...";
                await Authenticate();
                if (destroyed) return;
                if (!LobbyMode) World.StopPractice();
                bool webSockets = RelayWebSockets;
                var failure = await TryConnectRelayAsync(host, code, webSockets);
                if (failure != null && !webSockets && !destroyed)
                {
                    Status = "Retrying Relay over WebSockets...";
                    failure = await TryConnectRelayAsync(host, code, true);
                    if (failure == null) RelayWebSockets = true;
                }
                if (failure != null) throw failure;
                Status = (host ? "Relay Host ready. Share the Session code." : "Joined Relay Session.") + (UsingWebSockets ? " (WebSocket)" : "");
            }
            catch (Exception error)
            {
                Status = "Connection failed: " + error.Message;
                PlayerInput.Unlock();
            }
            finally { Busy = false; }
        }

        // One attempt with the chosen protocol; returns the error instead of throwing so the caller can retry.
        private async Task<Exception> TryConnectRelayAsync(bool host, string code, bool webSockets)
        {
            slots.Clear();
            Transport.UseWebSockets = webSockets;
            UsingWebSockets = webSockets;
            var network = new NetworkOptions { RelayProtocol = webSockets ? RelayProtocol.WSS : RelayProtocol.Default };
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(35));
                if (host)
                    session = await MultiplayerService.Instance.CreateSessionAsync(
                        new SessionOptions { Name = "NatureCalls M1", MaxPlayers = MaxPlayers, IsPrivate = true }
                            .WithRelayNetwork().WithNetworkOptions(network), timeout.Token);
                else
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(),
                        new JoinSessionOptions().WithNetworkOptions(network), timeout.Token);
                session.RemovedFromSession += Removed;
                return null;
            }
            catch (Exception error)
            {
                if (session != null)
                {
                    try { await session.LeaveAsync(); } catch (Exception) { /* The original error is reported. */ }
                    session = null;
                }
                if (Manager.IsListening) Manager.Shutdown();
                UsingWebSockets = false;
                Transport.UseWebSockets = false;
                return error;
            }
        }
        /// <summary>The most likely reachable IPv4 address, shown as the room code of a direct (no Relay) room.</summary>
        public static string LocalAddress() => LocalAddresses().FirstOrDefault() ?? "127.0.0.1";

        /// <summary>
        /// Usable IPv4 addresses, best first: adapters with a gateway, then private LAN ranges, then the rest
        /// (e.g. Tailscale). Self-assigned 169.254.x.x addresses (adapters without a network) are skipped.
        /// </summary>
        public static string[] LocalAddresses()
        {
            var found = new List<(string address, int rank)>();
            foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                System.Net.NetworkInformation.IPInterfaceProperties properties;
                try { properties = adapter.GetIPProperties(); } catch (Exception) { continue; }
                bool hasGateway;
                try { hasGateway = properties.GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork); }
                catch (Exception) { hasGateway = false; }
                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    var bytes = unicast.Address.GetAddressBytes();
                    if (bytes[0] == 169 && bytes[1] == 254) continue;
                    bool lan = bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
                    found.Add((unicast.Address.ToString(), (hasGateway ? 0 : 2) + (lan ? 0 : 1)));
                }
            }
            return found.OrderBy(item => item.rank).Select(item => item.address).Distinct().ToArray();
        }
        public bool StartDirect(bool host, string address = "127.0.0.1")
        {
            if (Busy || Active) return false;
            try
            {
                if (Port == 7777)
                {
                    var argument = Array.IndexOf(Environment.GetCommandLineArgs(), "-port");
                    if (argument >= 0 && argument + 1 < Environment.GetCommandLineArgs().Length &&
                        ushort.TryParse(Environment.GetCommandLineArgs()[argument + 1], out var probePort) && probePort > 0)
                        Port = probePort;
                }
                if (!LobbyMode) World.StopPractice();
                slots.Clear();
                Transport.UseWebSockets = false; // LAN rooms use plain UDP.
                UsingWebSockets = false;
                if (!TryAddress(address, Port, out var ip, out var port))
                    throw new ArgumentException("Enter an IP address with an optional port (1–65535).");
                Port = port;
                Transport.SetConnectionData(ip, Port, host ? "0.0.0.0" : null);
                Status = host ? "Local test Host (no Relay)." : "Connecting to local test Host...";
                bool started = host ? Manager.StartHost() : Manager.StartClient();
                if (started && host && LobbyMode)
                {
                    beacon = gameObject.GetComponent<LanBeaconBroadcaster>() ?? gameObject.AddComponent<LanBeaconBroadcaster>();
                    beacon.GamePort = Port; beacon.MaxPlayers = MaxPlayers;
                }
                if (!started) Status = "Local transport could not start. Check address and port.";
                return started;
            }
            catch (Exception error) { Status = "Local connection failed: " + error.Message; return false; }
        }
        public async Task LeaveAsync()
        {
            if (Busy) return;
            Busy = true;
            try
            {
                if (session != null)
                {
                    var leaving = session;
                    leaving.RemovedFromSession -= Removed;
                    await leaving.LeaveAsync(); // SDK owns Relay network teardown.
                    session = null;
                }
                else if (Manager.IsListening) Manager.Shutdown();
                if (beacon != null) { Destroy(beacon); beacon = null; }
                Status = "Disconnected. Offline practice is available.";
            }
            catch (Exception error)
            {
                Status = "Leave failed: " + error.Message;
                session = null;
                if (Manager.IsListening) Manager.Shutdown();
            }
            finally
            {
                ServerRoomCode = 0;
                slots.Clear();
                if (World != null) World.Camera.Unbind();
                PlayerInput.Unlock();
                LobbyMode = false;
                Busy = false;
            }
        }
    }
}
