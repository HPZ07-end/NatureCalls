using System;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;
using PlayerInput = NatureCalls.Input.PlayerInput;

namespace NatureCalls.Network
{
    // The scene facilities are not NetworkObjects. The host player's Avatar carries their state.
    public struct PaperRollSnapshot : INetworkSerializeByMemcpy, IEquatable<PaperRollSnapshot>
    {
        public byte Mode; // 0: on the map, 1: carried, 2: thrown
        public ulong Carrier;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;

        public bool Equals(PaperRollSnapshot other) => Mode == other.Mode && Carrier == other.Carrier &&
            Position == other.Position && Rotation == other.Rotation && Velocity == other.Velocity;
        public override bool Equals(object obj) => obj is PaperRollSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Mode, Carrier, Position, Rotation, Velocity);
    }

    [RequireComponent(typeof(NetworkObject), typeof(Movement), typeof(Posture))]
    [RequireComponent(typeof(PlayerAppearance))]
    public sealed class Avatar : NetworkBehaviour
    {
        public NetworkVariable<PostureState> SharedPosture = new(PostureState.Standing,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> ColorIndex = new(0);
        // Server-write; -1 (direct play without a Lobby room) shows the identity color instead.
        public NetworkVariable<int> OutfitIndex = new(-1);
        public NetworkList<byte> SharedDoors;
        public NetworkList<PaperRollSnapshot> SharedPaperRolls;
        // Toilet occupants: 0 when free, otherwise the sitting player's client id + 1.
        public NetworkList<ulong> SharedToilets;
        // Where players toileted outside a stall during this round.
        public NetworkList<Vector3> SharedDroppings;
        // Server-write copies of this player's needs; the host's PlayerNeeds is the authority.
        public NetworkVariable<float> SharedUrgency = new(0);
        public NetworkVariable<float> SharedDisgust = new(0);
        private PlayerNeeds needs;
        private float nextNeedsUpdate;
        private Movement movement;
        private Posture posture;
        private PlayerAppearance appearance;
        private Map.Playground world;
        private StallDoor[] doors;
        private PaperRoll[] paperRolls;
        private Toilet[] toilets;
        private float nextPaperRollUpdate;
        private static int facilitySceneHandle = -1;
        private static StallDoor[] sceneDoors;
        private static PaperRoll[] scenePaperRolls;
        private static Toilet[] sceneToilets;
        private static readonly Color[] Colors = { new(.12f, .5f, .9f), new(.95f, .35f, .15f), new(.65f, .3f, .85f), new(.15f, .75f, .4f) };

        private void Awake()
        {
            SharedDoors = new NetworkList<byte>();
            SharedPaperRolls = new NetworkList<PaperRollSnapshot>();
            SharedToilets = new NetworkList<ulong>();
            SharedDroppings = new NetworkList<Vector3>();
        }

        public override void OnNetworkSpawn()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            appearance = GetComponent<PlayerAppearance>();
            needs = GetComponent<PlayerNeeds>();
            world = FindFirstObjectByType<Map.Playground>();
            if (world != null)
            {
                // Capture the map ordering once, before a carried roll is reparented to a player.
                if (facilitySceneHandle != world.gameObject.scene.handle || sceneDoors == null || scenePaperRolls == null || sceneToilets == null)
                {
                    facilitySceneHandle = world.gameObject.scene.handle;
                    sceneDoors = FindObjectsByType<StallDoor>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene == world.gameObject.scene)
                        .OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                    scenePaperRolls = FindObjectsByType<PaperRoll>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene == world.gameObject.scene)
                        .OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                    sceneToilets = FindObjectsByType<Toilet>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene == world.gameObject.scene)
                        .OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                }
                doors = sceneDoors;
                paperRolls = scenePaperRolls;
                toilets = sceneToilets;
            }
            if (OwnsFacilityState)
            {
                if (IsServer)
                {
                    foreach (var door in doors) SharedDoors.Add((byte)door.State);
                    foreach (var roll in paperRolls) SharedPaperRolls.Add(Snapshot(roll, 0, 0));
                    foreach (var toilet in toilets) SharedToilets.Add(0);
                }
                SharedDoors.OnListChanged += DoorChanged;
                SharedPaperRolls.OnListChanged += PaperRollChanged;
                SharedToilets.OnListChanged += ToiletChanged;
                SharedDroppings.OnListChanged += DroppingsChanged;
                ShowDroppings();
                if (!IsServer) ApplyAllFacilityState();
            }
            if (IsServer) ColorIndex.Value = Session.Instance.Slot(OwnerClientId);
            ColorIndex.OnValueChanged += AppearanceChanged;
            OutfitIndex.OnValueChanged += AppearanceChanged;
            RefreshAppearance();
            SharedPosture.OnValueChanged += PostureChanged;
            posture.Set(SharedPosture.Value);
            posture.Changed += PublishPosture;
            movement.SetControl(IsOwner);
            GetComponent<PlayerInput>().enabled = IsOwner;
            if (IsOwner) world.Camera.Bind(movement);
            name = "Player " + (ColorIndex.Value + 1) + (IsOwner ? " (you)" : " (remote)");
            if (!IsServer) FindHostAvatar()?.ApplyAllFacilityState();
        }
        private bool OwnsFacilityState => OwnerClientId == NetworkManager.ServerClientId && doors != null && paperRolls != null && toilets != null;

        private static string FacilityPath(Transform item)
        {
            var path = "";
            for (var node = item; node != null; node = node.parent)
                path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
            return path;
        }

        private Avatar FindHostAvatar()
        {
            foreach (var avatar in FindObjectsByType<Avatar>(FindObjectsSortMode.None))
                if (avatar.IsSpawned && avatar.OwnerClientId == NetworkManager.ServerClientId) return avatar;
            return null;
        }

        private Avatar ServerFacilityAvatar()
        {
            if (!IsServer || !NetworkManager.ConnectedClients.TryGetValue(NetworkManager.ServerClientId, out var host)) return null;
            return host.PlayerObject != null ? host.PlayerObject.GetComponent<Avatar>() : null;
        }

        public bool RequestDoor(StallDoor door)
        {
            if (!IsSpawned || !IsOwner || doors == null) return false;
            var index = Array.IndexOf(doors, door);
            if (index < 0) return false;
            InteractDoorRpc(index);
            return true;
        }

        public bool RequestPickup(PaperRoll roll)
        {
            if (!IsSpawned || !IsOwner || paperRolls == null) return false;
            var index = Array.IndexOf(paperRolls, roll);
            if (index < 0) return false;
            PickupPaperRollRpc(index);
            return true;
        }

        public bool RequestThrow(PaperRoll roll, Vector3 direction)
        {
            if (!IsSpawned || !IsOwner || paperRolls == null) return false;
            var index = Array.IndexOf(paperRolls, roll);
            if (index < 0) return false;
            ThrowPaperRollRpc(index, direction);
            return true;
        }

        public bool RequestSeat(Toilet toilet)
        {
            if (!IsSpawned || !IsOwner || toilets == null) return false;
            var index = Array.IndexOf(toilets, toilet);
            if (index < 0 || toilet.IsOccupied) return false;
            SeatToiletRpc(index);
            return true;
        }

        public void ReleaseSeat(Toilet toilet)
        {
            if (!IsSpawned || !IsOwner || toilets == null) return;
            toilet.Release(movement);
            var index = Array.IndexOf(toilets, toilet);
            if (index >= 0) ReleaseToiletRpc(index);
        }

        // The host checks the seat is free and the player stands in its stall, then lets the owner sit down.
        [Rpc(SendTo.Server)]
        private void SeatToiletRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilityAvatar();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.toilets.Length ||
                index >= host.SharedToilets.Count || host.SharedToilets[index] != 0) return;
            var toilet = host.toilets[index];
            var stall = toilet.GetComponentInParent<StallDoor>();
            if (stall != null && !stall.Contains(movement)) return;
            host.SharedToilets[index] = OwnerClientId + 1;
            toilet.ApplySharedOccupant(movement);
            SeatGrantedRpc(index);
        }

        [Rpc(SendTo.Owner)]
        private void SeatGrantedRpc(int index)
        {
            if (toilets == null || index < 0 || index >= toilets.Length || posture.IsSquatting) return;
            toilets[index].Seat(movement);
        }

        [Rpc(SendTo.Server)]
        private void ReleaseToiletRpc(int index, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId == OwnerClientId) ReleaseToiletsOf(OwnerClientId);
        }

        // Frees every seat the player holds (on standing up, or when they leave the map).
        private void ReleaseToiletsOf(ulong clientId)
        {
            var host = ServerFacilityAvatar();
            if (host == null || host.toilets == null) return;
            for (var index = 0; index < host.toilets.Length && index < host.SharedToilets.Count; index++)
            {
                if (host.SharedToilets[index] != clientId + 1) continue;
                host.SharedToilets[index] = 0;
                host.toilets[index].ApplySharedOccupant(null);
            }
        }

        /// <summary>The host starts this player's needs for a round.</summary>
        public void BeginNeeds()
        {
            if (IsServer && needs != null) needs.BeginRound(UnityEngine.Random.value);
        }

        public bool NeedsFailed => needs != null && needs.Failed;
        public bool UrgencyFailed => needs != null && Core.NeedsRules.UrgencyFailed(needs.Urgency);

        /// <summary>The owner finished a toileting; the host applies its needs and drops.</summary>
        public void ReportToileting(bool insideStall, Vector3 position)
        {
            if (IsSpawned && IsOwner) ToiletingDoneRpc(insideStall, position);
        }

        [Rpc(SendTo.Server)]
        private void ToiletingDoneRpc(bool insideStall, Vector3 position, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || needs == null || !needs.Ticking) return;
            needs.CompleteToileting(insideStall);
            PublishNeeds();
            var host = ServerFacilityAvatar();
            if (!insideStall && host != null) host.SharedDroppings.Add(position);
        }

        private void DroppingsChanged(NetworkListEvent<Vector3> change) => ShowDroppings();

        private void ShowDroppings()
        {
            if (world == null) return;
            var positions = new System.Collections.Generic.List<Vector3>();
            foreach (var position in SharedDroppings) positions.Add(position);
            world.ShowDroppings(positions);
        }

        private void PublishNeeds()
        {
            if (!IsServer || needs == null) return;
            if (Mathf.Abs(SharedUrgency.Value - needs.Urgency) > .05f) SharedUrgency.Value = needs.Urgency;
            if (Mathf.Abs(SharedDisgust.Value - needs.Disgust) > .05f) SharedDisgust.Value = needs.Disgust;
        }

        [Rpc(SendTo.Server)]
        private void InteractDoorRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilityAvatar();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.doors.Length ||
                index >= host.SharedDoors.Count) return;
            var door = host.doors[index];
            if (!door.Interact(movement)) return;
            host.SharedDoors[index] = (byte)door.State;
        }

        [Rpc(SendTo.Server)]
        private void PickupPaperRollRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilityAvatar();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.paperRolls.Length ||
                index >= host.SharedPaperRolls.Count) return;
            var roll = host.paperRolls[index];
            if (!roll.Pickup(movement)) return;
            host.SharedPaperRolls[index] = Snapshot(roll, 1, OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        private void ThrowPaperRollRpc(int index, Vector3 direction, RpcParams rpc = default)
        {
            var host = ServerFacilityAvatar();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.paperRolls.Length ||
                index >= host.SharedPaperRolls.Count) return;
            var roll = host.paperRolls[index];
            if (!roll.Throw(movement, direction)) return;
            host.SharedPaperRolls[index] = Snapshot(roll, 2, 0);
        }

        private static PaperRollSnapshot Snapshot(PaperRoll roll, byte mode, ulong carrier)
        {
            var body = roll.GetComponent<Rigidbody>();
            return new PaperRollSnapshot
            {
                Mode = mode, Carrier = carrier, Position = roll.transform.position,
                Rotation = roll.transform.rotation,
                Velocity = body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero
            };
        }

        private void DoorChanged(NetworkListEvent<byte> change) => ApplyAllFacilityState();
        private void PaperRollChanged(NetworkListEvent<PaperRollSnapshot> change) => ApplyAllFacilityState();
        private void ToiletChanged(NetworkListEvent<ulong> change) => ApplyAllFacilityState();

        private static Movement MovementOf(ulong clientId)
        {
            foreach (var avatar in FindObjectsByType<Avatar>(FindObjectsSortMode.None))
                if (avatar.IsSpawned && avatar.OwnerClientId == clientId) return avatar.movement;
            return null;
        }

        private void ApplyAllFacilityState()
        {
            if (!OwnsFacilityState || IsServer) return;
            for (var index = 0; index < doors.Length && index < SharedDoors.Count; index++)
                doors[index].ApplySharedState((StallDoorState)SharedDoors[index]);
            for (var index = 0; index < paperRolls.Length && index < SharedPaperRolls.Count; index++)
            {
                var state = SharedPaperRolls[index];
                if (state.Mode == 1)
                {
                    foreach (var avatar in FindObjectsByType<Avatar>(FindObjectsSortMode.None))
                        if (avatar.IsSpawned && avatar.OwnerClientId == state.Carrier)
                        { paperRolls[index].ApplySharedPickup(avatar.movement); break; }
                }
                else if (state.Mode == 2)
                    paperRolls[index].ApplySharedThrow(state.Position, state.Rotation, state.Velocity, false);
            }
            for (var index = 0; index < toilets.Length && index < SharedToilets.Count; index++)
                toilets[index].ApplySharedOccupant(SharedToilets[index] == 0 ? null : MovementOf(SharedToilets[index] - 1));
        }

        private void Update()
        {
            if (IsSpawned && needs != null)
            {
                if (!IsServer) needs.ApplyShared(SharedUrgency.Value, SharedDisgust.Value);
                else if (Time.time >= nextNeedsUpdate) { nextNeedsUpdate = Time.time + .2f; PublishNeeds(); }
            }
            if (!IsSpawned || !OwnsFacilityState || !IsServer || Time.time < nextPaperRollUpdate) return;
            nextPaperRollUpdate = Time.time + .1f;
            for (var index = 0; index < paperRolls.Length && index < SharedPaperRolls.Count; index++)
            {
                if (SharedPaperRolls[index].Mode != 2) continue;
                var current = Snapshot(paperRolls[index], 2, 0);
                var previous = SharedPaperRolls[index];
                if (Vector3.Distance(current.Position, previous.Position) > .01f ||
                    Quaternion.Angle(current.Rotation, previous.Rotation) > 1f ||
                    Vector3.Distance(current.Velocity, previous.Velocity) > .05f)
                    SharedPaperRolls[index] = current;
            }
        }
        private void PublishPosture(PostureState value)
        {
            if (!IsSpawned || !IsOwner) return;
            SharedPosture.Value = value;
            if (IsServer && value == PostureState.Standing) ReleaseToiletsOf(OwnerClientId);
        }
        private void PostureChanged(PostureState previous, PostureState current)
        {
            posture.Set(current);
            if (IsServer && current == PostureState.Standing) ReleaseToiletsOf(OwnerClientId);
        }
        private void AppearanceChanged(int previous, int current) => RefreshAppearance();
        private void RefreshAppearance()
        {
            if (OutfitIndex.Value >= 0 && appearance.Catalog != null && appearance.Catalog.Contains(OutfitIndex.Value))
                appearance.ApplyOutfit(OutfitIndex.Value);
            else
                appearance.ApplyIdentityColor(Colors[Mathf.Abs(ColorIndex.Value) % Colors.Length]);
        }
        public override void OnNetworkDespawn()
        {
            if (OwnsFacilityState)
            {
                SharedDoors.OnListChanged -= DoorChanged;
                SharedPaperRolls.OnListChanged -= PaperRollChanged;
                SharedToilets.OnListChanged -= ToiletChanged;
                SharedDroppings.OnListChanged -= DroppingsChanged;
            }
            else if (IsServer) ReleaseToiletsOf(OwnerClientId);
            SharedPosture.OnValueChanged -= PostureChanged;
            ColorIndex.OnValueChanged -= AppearanceChanged;
            OutfitIndex.OnValueChanged -= AppearanceChanged;
            if (posture != null) posture.Changed -= PublishPosture;
            if (movement != null) movement.SetControl(false);
            GetComponent<PlayerInput>().enabled = false;
            if (world != null && world.Camera.Target == movement) world.Camera.Unbind();
        }
    }
}
