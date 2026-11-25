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
    public enum LobbyPhase : byte { Waiting, SelectingMap, InGame, ConfirmingMap, Departing }

    public struct LobbyMember : INetworkSerializeByMemcpy, IEquatable<LobbyMember>
    {
        public ulong ClientId;
        public int Slot;
        public int Outfit;
        public PlayerBodyType BodyType;
        public bool Ready;

        public bool Equals(LobbyMember other) =>
            ClientId == other.ClientId && Slot == other.Slot && Outfit == other.Outfit && BodyType == other.BodyType && Ready == other.Ready;
        public override bool Equals(object obj) => obj is LobbyMember other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ClientId, Slot, Outfit, BodyType, Ready);
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
        public NetworkVariable<int> SelectedMap = new(-1);
        // Server time at which the current round ends; -1 while no round is running.
        public NetworkVariable<double> RoundEndTime = new(-1);
        public const string RoundOverMessage = "时间到，本局结束，回到房间。";
        private readonly HashSet<ulong> loaded = new();
        private readonly HashSet<ulong> departed = new();
        private float departureDeadline;
        private bool spawnedPlayers;
        private bool serverLoaded;

        public MapCatalog Maps => maps;
        public bool IsLeader => IsClient && Members.Count > 0 && LocalClientId == LeaderId;
        public ulong LeaderId => Members.Count > 0 ? Members[0].ClientId : ulong.MaxValue;
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
            if (IsClient) SetAppearanceRpc(LocalProfile.BodyType, LocalProfile.OutfitIndex);
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

        public int ReadyMembers()
        {
            int ready = 0;
            foreach (var member in Members)
                if (member.ClientId != LeaderId && member.Ready) ready++;
            return ready;
        }

        /// <summary>Seconds left in the running round, or -1 before the players have spawned.</summary>
        public double RoundRemaining => RoundEndTime.Value < 0 ? -1 : RoundClock.Remaining(RoundEndTime.Value, NetworkManager.ServerTime.Time);

        public bool CanStart => Phase.Value == LobbyPhase.Waiting && LobbyRules.CanStart(Members.Count, ReadyMembers());

        public void SetReady(bool ready) => SetReadyRpc(ready);
        public void SetAppearance(PlayerBodyType bodyType, int outfit) => SetAppearanceRpc(bodyType, outfit);
        public void RequestStart() => RequestStartRpc();
        public void CancelMapSelection() => CancelMapSelectionRpc();
        public void ChooseMap(int choice) => ChooseMapRpc(choice);
        public void ConfirmMap() => ConfirmMapRpc();

        private void AddMember(ulong clientId)
        {
            if ((!IsHost && clientId == NetworkManager.ServerClientId) || TryGetMember(clientId, out _)) return;
            if (Phase.Value == LobbyPhase.ConfirmingMap) { SelectedMap.Value = -1; Phase.Value = LobbyPhase.Waiting; }
            Members.Add(new LobbyMember { ClientId = clientId, Slot = Session.Instance.Slot(clientId), Outfit = 0 });
        }

        private void RemoveMember(ulong clientId)
        {
            if (Phase.Value == LobbyPhase.ConfirmingMap) { SelectedMap.Value = -1; Phase.Value = LobbyPhase.Waiting; }
            for (int index = 0; index < Members.Count; index++)
                if (Members[index].ClientId == clientId) { Members.RemoveAt(index); break; }
            loaded.Remove(clientId);
            departed.Remove(clientId);
            if (Phase.Value == LobbyPhase.Departing) TryFinishDeparture();
            if (Members.Count == 0)
            {
                if (Phase.Value == LobbyPhase.InGame) EndRound("");
                else Phase.Value = LobbyPhase.Waiting;
                loaded.Clear();
                serverLoaded = false;
                spawnedPlayers = false;
                return;
            }
            if (Phase.Value == LobbyPhase.SelectingMap && Members.Count < LobbyRules.MinPlayers) Phase.Value = LobbyPhase.Waiting;
            else if (Phase.Value == LobbyPhase.InGame) TrySpawnPlayers();
        }

        private void ChangeMember(ulong clientId, Func<LobbyMember, LobbyMember> change)
        {
            for (int index = 0; index < Members.Count; index++)
                if (Members[index].ClientId == clientId) { Members[index] = change(Members[index]); return; }
        }

        private bool FromLeader(RpcParams rpc) => Members.Count > 0 && rpc.Receive.SenderClientId == LeaderId;

        [Rpc(SendTo.Server)]
        private void SetAppearanceRpc(PlayerBodyType bodyType, int outfit, RpcParams rpc = default)
        {
            if (!OutfitSelection.IsValidBodyType(bodyType)) return;
            ChangeMember(rpc.Receive.SenderClientId, member =>
            {
                member.Outfit = Mathf.Max(0, outfit);
                member.BodyType = bodyType;
                return member;
            });
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
            if (FromLeader(rpc) && CanStart) Phase.Value = LobbyPhase.SelectingMap;
        }

        [Rpc(SendTo.Server)]
        private void CancelMapSelectionRpc(RpcParams rpc = default)
        {
            if (FromLeader(rpc) && Phase.Value == LobbyPhase.ConfirmingMap)
            { SelectedMap.Value = -1; Phase.Value = LobbyPhase.SelectingMap; return; }
            if (FromLeader(rpc) && Phase.Value == LobbyPhase.SelectingMap) Phase.Value = LobbyPhase.Waiting;
        }

        [Rpc(SendTo.Server)]
        private void ChooseMapRpc(int choice, RpcParams rpc = default)
        {
            if (!FromLeader(rpc) || Phase.Value != LobbyPhase.SelectingMap || !MapSelection.IsValid(choice, maps.Count)) return;
            SelectedMap.Value = MapSelection.Resolve(choice, maps.Count, count => UnityEngine.Random.Range(0, count));
            Phase.Value = LobbyPhase.ConfirmingMap;
        }

        [Rpc(SendTo.Server)]
        private void ConfirmMapRpc(RpcParams rpc = default)
        {
            if (!FromLeader(rpc) || Phase.Value != LobbyPhase.ConfirmingMap || SelectedMap.Value < 0 || SelectedMap.Value >= maps.Count) return;
            if (!LobbyRules.CanStart(Members.Count, ReadyMembers())) { SelectedMap.Value = -1; Phase.Value = LobbyPhase.Waiting; return; }
            int mapIndex = SelectedMap.Value;
            Phase.Value = LobbyPhase.Departing;
            departed.Clear(); departureDeadline = Time.unscaledTime + 10;
            BeginDepartureRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void BeginDepartureRpc()
        {
            if (!IsClient) return;
            var presentation = FindFirstObjectByType<NatureCalls.UI.ToiletLobby>();
            if (presentation != null) presentation.PlayDeparture(() => DepartureReadyRpc());
            else DepartureReadyRpc();
        }

        [Rpc(SendTo.Server)]
        private void DepartureReadyRpc(RpcParams rpc = default)
        {
            if (Phase.Value != LobbyPhase.Departing || !TryGetMember(rpc.Receive.SenderClientId, out _)) return;
            departed.Add(rpc.Receive.SenderClientId); TryFinishDeparture();
        }

        private void TryFinishDeparture()
        {
            if (!IsServer || Phase.Value != LobbyPhase.Departing || Members.Count == 0) return;
            foreach (var member in Members) if (!departed.Contains(member.ClientId)) return;
            int mapIndex = SelectedMap.Value;
            Phase.Value = LobbyPhase.InGame;
            loaded.Clear();
            serverLoaded = false;
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
            {
                if (IsServer) { serverLoaded = true; TrySpawnPlayers(); }
                if (IsClient) MapLoadedRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void MapLoadedRpc(RpcParams rpc = default)
        {
            if (!TryGetMember(rpc.Receive.SenderClientId, out _)) return;
            loaded.Add(rpc.Receive.SenderClientId);
            TrySpawnPlayers();
        }

        // Spawn only after every member has loaded the map, so no client receives a player into the old scene.
        private void TrySpawnPlayers()
        {
            if (!IsServer || !serverLoaded || Phase.Value != LobbyPhase.InGame || spawnedPlayers || Members.Count == 0) return;
            foreach (var member in Members)
                if (!loaded.Contains(member.ClientId)) return;
            var world = FindFirstObjectByType<Playground>();
            if (world == null) { Debug.LogError("LOBBY_MAP_WITHOUT_PLAYGROUND"); return; }
            Session.Instance.SpawnFacilities();
            spawnedPlayers = true;
            foreach (var member in Members)
            {
                var spawn = world.Spawns[Mathf.Clamp(member.Slot, 0, world.Spawns.Length - 1)];
                var player = Instantiate(NetworkManager.NetworkConfig.PlayerPrefab, spawn.position, spawn.rotation);
                var appearance = player.GetComponent<AvatarAppearance>();
                appearance.OutfitIndex.Value = member.Outfit;
                appearance.BodyType.Value = member.BodyType;
                player.GetComponent<NetworkObject>().SpawnAsPlayerObject(member.ClientId, true);
            }
            // The global round clock and everyone's needs start once everyone is on the map.
            RoundEndTime.Value = NetworkManager.ServerTime.Time + Match.RoundSeconds;
            foreach (var player in FindObjectsByType<NeedsSync>(FindObjectsSortMode.None)) player.BeginNeeds();
        }

        private void Update()
        {
            if (IsServer && Phase.Value == LobbyPhase.Departing && Time.unscaledTime > departureDeadline)
            { EndRound("转场等待超时，已返回准备房，请重试。"); return; }
            if (!IsServer || Phase.Value != LobbyPhase.InGame || RoundEndTime.Value < 0) return;
            // Any player's Urgency or Disgust at its threshold loses the round for everyone.
            foreach (var player in FindObjectsByType<NeedsSync>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned || !player.NeedsFailed) continue;
                var who = TryGetMember(player.OwnerClientId, out var member) ? $"玩家 {member.Slot + 1}" : "一名玩家";
                EndRound(Match.FailureMessage(who, player.UrgencyFailed));
                return;
            }
            if (RoundClock.IsOver(RoundEndTime.Value, NetworkManager.ServerTime.Time)) EndRound(RoundOverMessage);
        }

        // The round is over: everyone goes back to the DressingRoom and stays in the room for another round.
        private void EndRound(string message)
        {
            SelectedMap.Value = -1;
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
            if (Facilities.Current != null && Facilities.Current.IsSpawned) Facilities.Current.NetworkObject.Despawn(true);
            ReturnToRoomRpc(message);
        }

        [Rpc(SendTo.Everyone)]
        private void ReturnToRoomRpc(string message)
        {
            Match.ReturnToDressingRoom(message);
        }
    }
}
