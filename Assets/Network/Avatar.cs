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
        public const byte OnMap = 0, Carried = 1, Flying = 2, Landed = 3;
        public byte Mode; // OnMap (untouched), Carried, Flying (simulated by Simulator), Landed (at rest after a throw)
        public ulong Carrier;
        // Client id + 1 of the player whose machine simulates a flying roll; 0 when no one does.
        public ulong Simulator;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;

        public bool Equals(PaperRollSnapshot other) => Mode == other.Mode && Carrier == other.Carrier && Simulator == other.Simulator &&
            Position == other.Position && Rotation == other.Rotation && Velocity == other.Velocity;
        public override bool Equals(object obj) => obj is PaperRollSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Mode, Carrier, Simulator, Position, Rotation, Velocity);
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

        // A thrown roll belongs to the thrower until it lands: the thrower's machine simulates it right away
        // (no round trip), streams its motion through the host, and hands it back once it comes to rest.
        public bool RequestThrow(PaperRoll roll, Vector3 direction)
        {
            if (!IsSpawned || !IsOwner || paperRolls == null) return false;
            var index = Array.IndexOf(paperRolls, roll);
            if (index < 0 || !roll.Throw(movement, direction)) return false;
            var body = roll.GetComponent<Rigidbody>();
            ThrowPaperRollRpc(index, roll.transform.position, roll.transform.rotation, body != null ? body.linearVelocity : Vector3.zero);
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
        private void ThrowPaperRollRpc(int index, Vector3 position, Quaternion rotation, Vector3 velocity, RpcParams rpc = default)
        {
            var host = ServerFacilityAvatar();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.paperRolls.Length ||
                index >= host.SharedPaperRolls.Count) return;
            var state = host.SharedPaperRolls[index];
            if (state.Mode != PaperRollSnapshot.Carried || state.Carrier != OwnerClientId) return;
            var roll = host.paperRolls[index];
            if (!IsOwnedByServer) roll.FollowShared(position, rotation, velocity);
            host.SharedPaperRolls[index] = Moving(PaperRollSnapshot.Flying, OwnerClientId + 1, position, rotation, velocity);
        }

        // The simulating player reports the flight; the host relays it to everyone else.
        [Rpc(SendTo.Server)]
        private void PaperRollMotionRpc(int index, Vector3 position, Quaternion rotation, Vector3 velocity, bool landed, RpcParams rpc = default)
        {
            var host = ServerFacilityAvatar();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.paperRolls.Length ||
                index >= host.SharedPaperRolls.Count) return;
            var state = host.SharedPaperRolls[index];
            if (state.Mode != PaperRollSnapshot.Flying || state.Simulator != OwnerClientId + 1) return;
            if (!IsOwnedByServer)
            {
                if (landed) host.paperRolls[index].ApplySharedRest(position, rotation);
                else host.paperRolls[index].FollowShared(position, rotation, velocity);
            }
            host.SharedPaperRolls[index] = landed
                ? Moving(PaperRollSnapshot.Landed, 0, position, rotation, Vector3.zero)
                : Moving(PaperRollSnapshot.Flying, OwnerClientId + 1, position, rotation, velocity);
        }

        private static PaperRollSnapshot Moving(byte mode, ulong simulator, Vector3 position, Quaternion rotation, Vector3 velocity) =>
            new() { Mode = mode, Simulator = simulator, Position = position, Rotation = rotation, Velocity = velocity };

        // On the simulating player's machine: stream each roll it is flying, ~10 times a second, until it rests.
        private float nextFlightReport;
        private readonly System.Collections.Generic.Dictionary<int, float> restingSince = new();
        private void ReportFlights()
        {
            if (!IsOwner || paperRolls == null || Time.time < nextFlightReport) return;
            var host = FindHostAvatar();
            if (host == null) return;
            nextFlightReport = Time.time + .1f;
            for (var index = 0; index < paperRolls.Length && index < host.SharedPaperRolls.Count; index++)
            {
                var state = host.SharedPaperRolls[index];
                if (state.Mode != PaperRollSnapshot.Flying || state.Simulator != OwnerClientId + 1) { restingSince.Remove(index); continue; }
                var roll = paperRolls[index];
                var body = roll.GetComponent<Rigidbody>();
                var velocity = body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero;
                bool still = body == null || body.IsSleeping() || velocity.sqrMagnitude < .0025f;
                if (!still) restingSince.Remove(index);
                else if (!restingSince.ContainsKey(index)) restingSince[index] = Time.time;
                bool landed = still && Time.time - restingSince[index] > .3f;
                if (landed) restingSince.Remove(index);
                PaperRollMotionRpc(index, roll.transform.position, roll.transform.rotation, velocity, landed);
            }
        }

        // A simulating player left mid-flight: the host keeps the roll where it was last seen.
        private void LandFlightsOf(ulong clientId)
        {
            var host = ServerFacilityAvatar();
            if (host == null || host.paperRolls == null) return;
            for (var index = 0; index < host.paperRolls.Length && index < host.SharedPaperRolls.Count; index++)
            {
                var state = host.SharedPaperRolls[index];
                if (state.Mode != PaperRollSnapshot.Flying || state.Simulator != clientId + 1) continue;
                host.paperRolls[index].ApplySharedRest(state.Position, state.Rotation);
                host.SharedPaperRolls[index] = Moving(PaperRollSnapshot.Landed, 0, state.Position, state.Rotation, Vector3.zero);
            }
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
                if (state.Mode == PaperRollSnapshot.Carried)
                {
                    foreach (var avatar in FindObjectsByType<Avatar>(FindObjectsSortMode.None))
                        if (avatar.IsSpawned && avatar.OwnerClientId == state.Carrier)
                        { paperRolls[index].ApplySharedPickup(avatar.movement); break; }
                }
                // The simulating player already has the roll in flight; everyone else follows its reports.
                else if (state.Mode == PaperRollSnapshot.Flying && state.Simulator != NetworkManager.LocalClientId + 1)
                    paperRolls[index].FollowShared(state.Position, state.Rotation, state.Velocity);
                else if (state.Mode == PaperRollSnapshot.Landed)
                    paperRolls[index].ApplySharedRest(state.Position, state.Rotation);
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
            if (IsSpawned) ReportFlights();
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
            else if (IsServer) { ReleaseToiletsOf(OwnerClientId); LandFlightsOf(OwnerClientId); }
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
