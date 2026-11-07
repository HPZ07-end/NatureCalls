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
        public string EnvironmentName = "production";
        [Range(2, 4)] public int MaxPlayers = 4;
        public ushort Port = 7777;
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                destroyed = true;
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnServerStarted += ServerStarted;
            Manager.OnClientConnectedCallback += Connected;
            Manager.OnClientDisconnectCallback += Disconnected;
            Manager.OnClientStopped += Stopped;
            Manager.OnTransportFailure += TransportFailed;
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
            if (!LobbyMode) return;
            Instantiate(LobbyRoomPrefab).GetComponent<NetworkObject>().Spawn();
        }
        private void Connected(ulong id)
        {
            if (id == Manager.LocalClientId) Status = Manager.IsHost ? "Host connected" : "Client connected";
        }
        private void Disconnected(ulong id)
        {
            slots.Remove(id);
            if (id == Manager.LocalClientId && !Busy)
            {
                Status = string.IsNullOrEmpty(Manager.DisconnectReason) ? "Connection ended." : Manager.DisconnectReason;
                PlayerInput.Unlock();
            }
        }
        private void Stopped(bool wasHost)
        {
            if (destroyed || Busy) return;
            if (World != null) World.Camera.Unbind();
            PlayerInput.Unlock();
            Status = "Disconnected. You can restart practice or join again.";
            if (session != null) _ = LeaveAsync();
            else LobbyMode = false;
            if (Match.Mode == MatchMode.Networked) Match.ReturnToDressingRoom(wasHost ? "" : "与房主的连接已断开。");
        }
        private void TransportFailed() { Status = "Transport failed. Check the address, firewall and host."; PlayerInput.Unlock(); }
        private void Removed() { session = null; Status = "Session closed or player removed."; PlayerInput.Unlock(); }

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
                if (!LobbyMode) World.StopPractice();
                slots.Clear();
                Transport.UseWebSockets = false; // LAN rooms use plain UDP.
                UsingWebSockets = false;
                Transport.SetConnectionData(address.Trim(), Port, host ? "0.0.0.0" : null);
                Status = host ? "Local test Host (no Relay)." : "Connecting to local test Host...";
                bool started = host ? Manager.StartHost() : Manager.StartClient();
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
                slots.Clear();
                if (World != null) World.Camera.Unbind();
                PlayerInput.Unlock();
                LobbyMode = false;
                Busy = false;
            }
        }
    }
}
