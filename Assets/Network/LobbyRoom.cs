using System;
using System.Collections.Generic;
using NatureCalls.Core;
using NatureCalls.Map;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NatureCalls.Network
{
    public enum LobbyPhase : byte { Waiting, SelectingMap, InGame }

    public struct LobbyMember : INetworkSerializeByMemcpy, IEquatable<LobbyMember>
    {
        public ulong ClientId;
        public int Slot;
        public int Outfit;
        public bool Ready;

        public bool Equals(LobbyMember other) =>
            ClientId == other.ClientId && Slot == other.Slot && Outfit == other.Outfit && Ready == other.Ready;
        public override bool Equals(object obj) => obj is LobbyMember other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ClientId, Slot, Outfit, Ready);
    }

    /// <summary>
    /// Host-authoritative room state shared by all players: members, ready flags, outfits and the chosen map.
    /// Spawned by <see cref="Session"/> when a Lobby room is created; survives the map scene load.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LobbyRoom : NetworkBehaviour
    {
        public static LobbyRoom Current { get; private set; }

        [SerializeField] private MapCatalog maps;
        public NetworkList<LobbyMember> Members;
        public NetworkVariable<LobbyPhase> Phase = new(LobbyPhase.Waiting);
        // Server time at which the current round ends; -1 while no round is running.
        public NetworkVariable<double> RoundEndTime = new(-1);
        public const string RoundOverMessage = "时间到，本局结束，回到房间。";
        private readonly HashSet<ulong> loaded = new();
        private bool spawnedPlayers;

        public MapCatalog Maps => maps;
        public bool IsHostPlayer => IsServer;
        public ulong LocalClientId => NetworkManager.LocalClientId;

        private void Awake() { Members = new NetworkList<LobbyMember>(); }

        public override void OnNetworkSpawn()
        {
            Current = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += SceneLoaded;
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += AddMember;
                NetworkManager.OnClientDisconnectCallback += RemoveMember;
                foreach (var id in NetworkManager.ConnectedClientsIds) AddMember(id);
            }
            SetOutfitRpc(LocalProfile.OutfitIndex);
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= AddMember;
                NetworkManager.OnClientDisconnectCallback -= RemoveMember;
            }
            if (Current == this) Current = null;
        }

        public bool TryGetMember(ulong clientId, out LobbyMember member)
        {
            foreach (var candidate in Members)
                if (candidate.ClientId == clientId) { member = candidate; return true; }
            member = default;
            return false;
        }

        public int ReadyGuests()
        {
            int ready = 0;
            foreach (var member in Members)
                if (member.ClientId != NetworkManager.ServerClientId && member.Ready) ready++;
            return ready;
        }

        /// <summary>Seconds left in the running round, or -1 before the players have spawned.</summary>
        public double RoundRemaining => RoundEndTime.Value < 0 ? -1 : RoundClock.Remaining(RoundEndTime.Value, NetworkManager.ServerTime.Time);

        public bool CanStart => Phase.Value == LobbyPhase.Waiting && LobbyRules.CanStart(Members.Count, ReadyGuests());

        public void SetReady(bool ready) => SetReadyRpc(ready);
        public void SetOutfit(int outfit) => SetOutfitRpc(outfit);
        public void RequestStart() => RequestStartRpc();
        public void CancelMapSelection() => CancelMapSelectionRpc();
        public void ChooseMap(int choice) => ChooseMapRpc(choice);

        private void AddMember(ulong clientId)
        {
            if (TryGetMember(clientId, out _)) return;
            Members.Add(new LobbyMember { ClientId = clientId, Slot = Session.Instance.Slot(clientId), Outfit = 0 });
        }

        private void RemoveMember(ulong clientId)
        {
            for (int index = 0; index < Members.Count; index++)
                if (Members[index].ClientId == clientId) { Members.RemoveAt(index); break; }
            loaded.Remove(clientId);
            if (Phase.Value == LobbyPhase.SelectingMap && Members.Count < LobbyRules.MinPlayers) Phase.Value = LobbyPhase.Waiting;
            else if (Phase.Value == LobbyPhase.InGame) TrySpawnPlayers();
        }

        private void ChangeMember(ulong clientId, Func<LobbyMember, LobbyMember> change)
        {
            for (int index = 0; index < Members.Count; index++)
                if (Members[index].ClientId == clientId) { Members[index] = change(Members[index]); return; }
        }

        private bool FromHost(RpcParams rpc) => rpc.Receive.SenderClientId == NetworkManager.ServerClientId;

        [Rpc(SendTo.Server)]
        private void SetOutfitRpc(int outfit, RpcParams rpc = default)
        {
            ChangeMember(rpc.Receive.SenderClientId, member => { member.Outfit = Mathf.Max(0, outfit); return member; });
        }

        [Rpc(SendTo.Server)]
        private void SetReadyRpc(bool ready, RpcParams rpc = default)
        {
            if (Phase.Value != LobbyPhase.Waiting) return;
            ChangeMember(rpc.Receive.SenderClientId, member => { member.Ready = ready; return member; });
        }

        [Rpc(SendTo.Server)]
        private void RequestStartRpc(RpcParams rpc = default)
        {
            if (FromHost(rpc) && CanStart) Phase.Value = LobbyPhase.SelectingMap;
        }

        [Rpc(SendTo.Server)]
        private void CancelMapSelectionRpc(RpcParams rpc = default)
        {
            if (FromHost(rpc) && Phase.Value == LobbyPhase.SelectingMap) Phase.Value = LobbyPhase.Waiting;
        }

        [Rpc(SendTo.Server)]
        private void ChooseMapRpc(int choice, RpcParams rpc = default)
        {
            if (!FromHost(rpc) || Phase.Value != LobbyPhase.SelectingMap || !MapSelection.IsValid(choice, maps.Count)) return;
            var mapIndex = MapSelection.Resolve(choice, maps.Count, count => UnityEngine.Random.Range(0, count));
            Phase.Value = LobbyPhase.InGame;
            loaded.Clear();
            spawnedPlayers = false;
            LoadMapRpc(mapIndex);
        }

        [Rpc(SendTo.Everyone)]
        private void LoadMapRpc(int mapIndex)
        {
            Match.Begin(MatchMode.Networked, maps, mapIndex);
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Match.Mode == MatchMode.Networked && Match.MapIndex >= 0 && scene.path == maps[Match.MapIndex].ScenePath)
                MapLoadedRpc();
        }

        [Rpc(SendTo.Server)]
        private void MapLoadedRpc(RpcParams rpc = default)
        {
            loaded.Add(rpc.Receive.SenderClientId);
            TrySpawnPlayers();
        }

        // Spawn only after every member has loaded the map, so no client receives a player into the old scene.
        private void TrySpawnPlayers()
        {
            if (spawnedPlayers || Members.Count == 0) return;
            foreach (var member in Members)
                if (!loaded.Contains(member.ClientId)) return;
            var world = FindFirstObjectByType<Playground>();
            if (world == null) { Debug.LogError("LOBBY_MAP_WITHOUT_PLAYGROUND"); return; }
            spawnedPlayers = true;
            foreach (var member in Members)
            {
                var spawn = world.Spawns[Mathf.Clamp(member.Slot, 0, world.Spawns.Length - 1)];
                var player = Instantiate(NetworkManager.NetworkConfig.PlayerPrefab, spawn.position, spawn.rotation);
                player.GetComponent<Avatar>().OutfitIndex.Value = member.Outfit;
                player.GetComponent<NetworkObject>().SpawnAsPlayerObject(member.ClientId, true);
            }
            // The global round clock and everyone's needs start once everyone is on the map.
            RoundEndTime.Value = NetworkManager.ServerTime.Time + Match.RoundSeconds;
            foreach (var avatar in FindObjectsByType<Avatar>(FindObjectsSortMode.None)) avatar.BeginNeeds();
        }

        private void Update()
        {
            if (!IsServer || Phase.Value != LobbyPhase.InGame || RoundEndTime.Value < 0) return;
            // Any player's Urgency or Disgust at its threshold loses the round for everyone.
            foreach (var avatar in FindObjectsByType<Avatar>(FindObjectsSortMode.None))
            {
                if (!avatar.IsSpawned || !avatar.NeedsFailed) continue;
                var who = TryGetMember(avatar.OwnerClientId, out var member) ? $"玩家 {member.Slot + 1}" : "一名玩家";
                EndRound(Match.FailureMessage(who, avatar.UrgencyFailed));
                return;
            }
            if (RoundClock.IsOver(RoundEndTime.Value, NetworkManager.ServerTime.Time)) EndRound(RoundOverMessage);
        }

        // The round is over: everyone goes back to the DressingRoom and stays in the room for another round.
        private void EndRound(string message)
        {
            RoundEndTime.Value = -1;
            Phase.Value = LobbyPhase.Waiting;
            for (int index = 0; index < Members.Count; index++)
            {
                var member = Members[index];
                member.Ready = false;
                Members[index] = member;
            }
            foreach (var client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject != null && client.PlayerObject.IsSpawned) client.PlayerObject.Despawn(true);
            ReturnToRoomRpc(message);
        }

        [Rpc(SendTo.Everyone)]
        private void ReturnToRoomRpc(string message)
        {
            Match.ReturnToDressingRoom(message);
        }
    }
}
