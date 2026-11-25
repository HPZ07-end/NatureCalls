using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Network
{
    /// <summary>Broadcasts a small, versioned LAN room beacon while a direct host is in the lobby.</summary>
    public sealed class LanBeaconBroadcaster : MonoBehaviour
    {
        public string HostName = "NatureCalls Host";
        public ushort GamePort = 8000;
        public int Players;
        public int MaxPlayers = 4;
        public bool SelectingMap;
        private UdpClient socket;
        private float next;
        private void OnEnable() { socket = new UdpClient { EnableBroadcast = true }; }
        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            var bytes = System.Text.Encoding.UTF8.GetBytes(new LanBeacon(HostName, GamePort, Players, MaxPlayers, SelectingMap).ToJson());
            try { socket.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, LanBeacon.Port)); } catch (SocketException) { }
        }
        private void OnDisable() { socket?.Close(); socket = null; }
    }

    public readonly struct LanRoomEntry
    {
        public readonly string Address;
        public readonly LanBeacon Beacon;
        public readonly float LastSeen;
        public LanRoomEntry(string address, LanBeacon beacon, float lastSeen) { Address = address; Beacon = beacon; LastSeen = lastSeen; }
    }

    /// <summary>Receives and expires LAN beacons. UI can consume Snapshot without owning sockets.</summary>
    public sealed class LanBeaconListener : IDisposable
    {
        private readonly UdpClient socket = new(LanBeacon.Port);
        private readonly Dictionary<string, LanRoomEntry> rooms = new();
        public LanBeaconListener() { socket.Client.ReceiveTimeout = 20; }
        public IReadOnlyCollection<LanRoomEntry> Snapshot(float now)
        {
            while (socket.Available > 0)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    var data = socket.Receive(ref endpoint);
                    if (LanBeacon.TryParse(System.Text.Encoding.UTF8.GetString(data), out var beacon))
                        rooms[endpoint.Address.ToString()] = new LanRoomEntry(endpoint.Address.ToString(), beacon, now);
                }
                catch (SocketException) { break; }
            }
            foreach (var key in new List<string>(rooms.Keys))
                if (now - rooms[key].LastSeen > 4f) rooms.Remove(key);
            return rooms.Values;
        }
        public void Dispose() => socket.Close();
    }
}
