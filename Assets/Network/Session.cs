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
using UnityEngine;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Network
{
    public sealed class Session : MonoBehaviour
    {
        public NetworkManager Manager;
        public UnityTransport Transport;
        public Map.Playground World;
        public string EnvironmentName = "production";
        [Range(2, 4)] public int MaxPlayers = 4;
        public ushort Port = 7777;
        public string Status { get; private set; } = "Offline practice / Cloud not connected";
        public bool Busy { get; private set; }
        public bool Active => session != null || Manager.IsListening || Manager.ShutdownInProgress;
        public string JoinCode => session?.Code ?? "";
        public bool IsRelay => session != null;
        private ISession session;
        private readonly Dictionary<ulong, int> slots = new();
        private bool destroyed;

        private void Awake()
        {
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientConnectedCallback += Connected;
            Manager.OnClientDisconnectCallback += Disconnected;
            Manager.OnClientStopped += Stopped;
            Manager.OnTransportFailure += TransportFailed;
        }
        private void OnDestroy()
        {
            destroyed = true;
            if (Manager != null)
            {
                Manager.ConnectionApprovalCallback = null;
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
            var free = Enumerable.Range(0, Mathf.Min(MaxPlayers, World.Spawns.Length))
                .Where(index => !slots.ContainsValue(index)).ToArray();
            response.Pending = false;
            response.Approved = free.Length > 0;
            response.CreatePlayerObject = response.Approved;
            if (!response.Approved) { response.Reason = "Playground is full (maximum four players)."; return; }
            int index = free[0];
            slots[request.ClientNetworkId] = index;
            response.Position = World.Spawns[index].position;
            response.Rotation = World.Spawns[index].rotation;
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
            World.Camera.Unbind();
            PlayerInput.Unlock();
            Status = "Disconnected. You can restart practice or join again.";
            if (session != null) _ = LeaveAsync();
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
        public async Task ConnectRelayAsync(bool host, string code = "")
        {
            if (Busy || Active) return;
            Busy = true;
            Status = "Connecting to Unity Services...";
            try
            {
                if (!host && string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Enter a Session join code.");
                await Authenticate();
                if (destroyed) return;
                World.StopPractice();
                slots.Clear();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(35));
                if (host)
                    session = await MultiplayerService.Instance.CreateSessionAsync(
                        new SessionOptions { Name = "NatureCalls M1", MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork(), timeout.Token);
                else
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), new JoinSessionOptions(), timeout.Token);
                session.RemovedFromSession += Removed;
                Status = host ? "Relay Host ready. Share the Session code." : "Joined Relay Session.";
            }
            catch (Exception error)
            {
                Status = "Connection failed: " + error.Message;
                if (session != null)
                {
                    try { await session.LeaveAsync(); } catch (Exception) { /* Original error is shown in the UI. */ }
                    session = null;
                }
                if (Manager.IsListening) Manager.Shutdown();
                PlayerInput.Unlock();
            }
            finally { Busy = false; }
        }
        public bool StartDirect(bool host, string address = "127.0.0.1")
        {
            if (Busy || Active) return false;
            try
            {
                World.StopPractice();
                slots.Clear();
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
            finally { slots.Clear(); World.Camera.Unbind(); PlayerInput.Unlock(); Busy = false; }
        }
    }
}
