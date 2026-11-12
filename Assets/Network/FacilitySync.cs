using System;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Core;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;

namespace NatureCalls.Network
{
    // Snapshots are published by the server-owned Facilities object.
    public struct ItemSnapshot : INetworkSerializeByMemcpy, IEquatable<ItemSnapshot>
    {
        public const byte OnMap = 0, Carried = 1, Flying = 2, Landed = 3, Stored = 4;
        public int Uses;
        public byte Mode; // OnMap (untouched), Carried, Flying (simulated by Simulator), Landed (at rest after a throw)
        public ulong Carrier;
        // Client id + 1 of the player whose machine simulates a flying roll; 0 when no one does.
        public ulong Simulator;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;

        public bool Equals(ItemSnapshot other) => Uses == other.Uses && Mode == other.Mode && Carrier == other.Carrier && Simulator == other.Simulator &&
            Position == other.Position && Rotation == other.Rotation && Velocity == other.Velocity;
        public override bool Equals(object obj) => obj is ItemSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Mode, Carrier, Simulator, Position, Rotation, Velocity);
    }

    /// <summary>
    /// Player-owned requests; the server validates them and publishes results through Facilities.
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(Movement), typeof(Posture))]
    public sealed class FacilitySync : NetworkBehaviour
    {
        private Movement movement;
        private Posture posture;
        private StallDoor[] doors => Facilities.Current != null ? Facilities.Current.doors : null;
        private Item[] items => Facilities.Current != null ? Facilities.Current.items : null;
        private Toilet[] toilets => Facilities.Current != null ? Facilities.Current.toilets : null;

        public override void OnNetworkSpawn()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();

            Facilities.Current?.ApplyAllFacilityState();
        }

        private Facilities ServerFacilities() => IsServer ? Facilities.Current : null;

        public bool RequestDoor(StallDoor door)
        {
            if (!IsSpawned || !IsOwner || doors == null) return false;
            var index = Array.IndexOf(doors, door);
            if (index < 0) return false;
            InteractDoorRpc(index);
            return true;
        }

        public bool RequestPickup(Item roll)
        {
            if (!IsSpawned || !IsOwner || items == null) return false;
            var index = Array.IndexOf(items, roll);
            if (index < 0) return false;
            PickupItemRpc(index);
            return true;
        }

        // A thrown roll belongs to the thrower until it lands: the thrower's machine simulates it right away
        // (no round trip), streams its motion through the host, and hands it back once it comes to rest.
        public bool RequestThrow(Item roll, Vector3 direction)
        {
            if (!IsSpawned || !IsOwner || items == null) return false;
            var index = Array.IndexOf(items, roll);
            if (index < 0 || !roll.Throw(movement, direction)) return false;
            var body = roll.GetComponent<Rigidbody>();
            ThrowItemRpc(index, roll.transform.position, roll.transform.rotation, body != null ? body.linearVelocity : Vector3.zero);
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

        // The host checks the seat is free and the player stands in its stall, then lets the owner sit down.
        [Rpc(SendTo.Server)]
        private void SeatToiletRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.toilets.Length ||
                index >= host.SharedToilets.Count || host.SharedToilets[index] != 0) return;
            var toilet = host.toilets[index];
            var stall = toilet.GetComponentInParent<StallDoor>();
            if (stall != null && !stall.Contains(movement)) return;
            if (activeMode != 0) return;
            activeMode = 1; activeIndex = index; began = Time.time;
            GetComponent<PlayerNeeds>().Toileting = true;
            host.SharedToilets[index] = OwnerClientId + 1;
            toilet.ApplySharedOccupant(movement);
            SeatGrantedRpc(index);
        }

        [Rpc(SendTo.Owner)]
        private void SeatGrantedRpc(int index)
        {
            if (toilets == null || index < 0 || index >= toilets.Length || posture.IsSquatting) return;
            GetComponent<Input.PlayerInput>().BeginGranted(toilets[index], null);
        }

        // Frees every seat the player holds (on standing up, or when they leave the map).
        private void ReleaseToiletsOf(ulong clientId)
        {
            var host = ServerFacilities();
            if (host == null || host.toilets == null) return;
            for (var index = 0; index < host.toilets.Length && index < host.SharedToilets.Count; index++)
            {
                if (host.SharedToilets[index] != clientId + 1) continue;
                host.SharedToilets[index] = 0;
                host.toilets[index].ApplySharedOccupant(null);
            }
        }

        [Rpc(SendTo.Server)]
        private void InteractDoorRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.doors.Length ||
                index >= host.SharedDoors.Count) return;
            var door = host.doors[index];
            if (!door.Interact(movement)) return;
            host.SharedDoors[index] = (byte)door.State;
            GetComponent<PlayerNeeds>().LeaveUnwiped();
        }

        [Rpc(SendTo.Server)]
        private void PickupItemRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.items.Length ||
                index >= host.SharedItems.Count) return;
            var roll = host.items[index];
            if (!roll.Pickup(movement)) return;
            host.SharedItems[index] = Facilities.Snapshot(roll, 1, OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        private void ThrowItemRpc(int index, Vector3 position, Quaternion rotation, Vector3 velocity, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.items.Length ||
                index >= host.SharedItems.Count) return;
            var state = host.SharedItems[index];
            if (state.Mode != ItemSnapshot.Carried || state.Carrier != OwnerClientId) return;
            var roll = host.items[index];
            if (!IsOwnedByServer) roll.FollowShared(position, rotation, velocity);
            var flying = Moving(ItemSnapshot.Flying, OwnerClientId + 1, position, rotation, velocity);
            flying.Uses = state.Uses;
            host.SharedItems[index] = flying;
        }

        // The simulating player reports the flight; the host relays it to everyone else.
        [Rpc(SendTo.Server)]
        private void ItemMotionRpc(int index, Vector3 position, Quaternion rotation, Vector3 velocity, bool landed, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.items.Length ||
                index >= host.SharedItems.Count) return;
            var state = host.SharedItems[index];
            if (state.Mode != ItemSnapshot.Flying || state.Simulator != OwnerClientId + 1) return;
            if (!IsOwnedByServer)
            {
                if (landed) host.items[index].ApplySharedRest(position, rotation);
                else host.items[index].FollowShared(position, rotation, velocity);
            }
            var moving = landed
                ? Moving(ItemSnapshot.Landed, 0, position, rotation, Vector3.zero)
                : Moving(ItemSnapshot.Flying, OwnerClientId + 1, position, rotation, velocity);
            moving.Uses = state.Uses;
            host.SharedItems[index] = moving;
        }

        private static ItemSnapshot Moving(byte mode, ulong simulator, Vector3 position, Quaternion rotation, Vector3 velocity) =>
            new() { Mode = mode, Simulator = simulator, Position = position, Rotation = rotation, Velocity = velocity };

        // On the simulating player's machine: stream each roll it is flying, ~10 times a second, until it rests.
        private float nextFlightReport;
        private readonly Dictionary<int, float> restingSince = new();
        private void ReportFlights()
        {
            if (!IsOwner || items == null || Time.time < nextFlightReport) return;
            var host = Facilities.Current;
            if (host == null) return;
            nextFlightReport = Time.time + .1f;
            for (var index = 0; index < items.Length && index < host.SharedItems.Count; index++)
            {
                var state = host.SharedItems[index];
                if (state.Mode != ItemSnapshot.Flying || state.Simulator != OwnerClientId + 1) { restingSince.Remove(index); continue; }
                var roll = items[index];
                var body = roll.GetComponent<Rigidbody>();
                var velocity = body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero;
                bool still = body == null || body.IsSleeping() || velocity.sqrMagnitude < .0025f;
                if (!still) restingSince.Remove(index);
                else if (!restingSince.ContainsKey(index)) restingSince[index] = Time.time;
                bool landed = still && Time.time - restingSince[index] > .3f;
                if (landed) restingSince.Remove(index);
                ItemMotionRpc(index, roll.transform.position, roll.transform.rotation, velocity, landed);
            }
        }

        // A simulating player left mid-flight: the host keeps the roll where it was last seen.
        private void LandFlightsOf(ulong clientId)
        {
            var host = ServerFacilities();
            if (host == null || host.items == null) return;
            for (var index = 0; index < host.items.Length && index < host.SharedItems.Count; index++)
            {
                var state = host.SharedItems[index];
                if (state.Mode != ItemSnapshot.Flying || state.Simulator != clientId + 1) continue;
                host.items[index].ApplySharedRest(state.Position, state.Rotation);
                var landed = Moving(ItemSnapshot.Landed, 0, state.Position, state.Rotation, Vector3.zero);
                landed.Uses = state.Uses;
                host.SharedItems[index] = landed;
            }
        }

        /// <summary>Host only: records a dropping left where a player toileted outside a stall.</summary>
        public void AddDropping(Vector3 position)
        {
            var host = ServerFacilities();
            if (host != null) host.AddMess(MessKind.Dropping, position);
        }

        private byte activeMode;
        private int activeIndex = -1;
        private float began;
        private int cleaningIndex = -1;
        private float cleaningBegan, cleaningHeartbeat;
        public float CleaningProgress => cleaningIndex < 0 ? 0 : Mathf.Clamp01((Time.time - cleaningBegan) / NeedsRules.CleaningSeconds);
        public bool RequestUrinal(Urinal urinal)
        {
            if (!IsSpawned || !IsOwner || Facilities.Current == null) return false;
            int index = Array.IndexOf(Facilities.Current.urinals, urinal);
            if (index < 0) return false;
            BeginUrinalRpc(index); return true;
        }
        public bool RequestOutside()
        {
            if (!IsSpawned || !IsOwner) return false;
            BeginOutsideRpc(); return true;
        }
        [Rpc(SendTo.Server)]
        private void BeginOutsideRpc(RpcParams rpc = default)
        {
            if (!IsServer || rpc.Receive.SenderClientId != OwnerClientId || activeMode != 0 || StallDoor.Containing(movement) != null) return;
            if (Facilities.Current == null || Facilities.Current.urinals.Any(u => u.Contains(movement))) return;
            activeMode = 3; began = Time.time; GetComponent<PlayerNeeds>().Toileting = true;
            OutsideGrantedRpc();
        }
        [Rpc(SendTo.Owner)]
        private void OutsideGrantedRpc() => GetComponent<Input.PlayerInput>().BeginGranted(null, null);
        [Rpc(SendTo.Server)]
        private void BeginUrinalRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (host == null || rpc.Receive.SenderClientId != OwnerClientId || activeMode != 0 || index < 0 || index >= host.urinals.Length || host.SharedUrinals[index] != 0) return;
            if (!host.urinals[index].Occupy(movement)) return;
            activeMode = 2; activeIndex = index; began = Time.time;
            GetComponent<PlayerNeeds>().Toileting = true;
            host.SharedUrinals[index] = OwnerClientId + 1;
            UrinalGrantedRpc(index);
        }
        [Rpc(SendTo.Owner)]
        private void UrinalGrantedRpc(int index) => GetComponent<Input.PlayerInput>().BeginGranted(null, Facilities.Current.urinals[index]);
        public void ReportResult(float accuracy, Vector3 position)
        {
            if (IsSpawned && IsOwner) ResultRpc(accuracy, position);
        }
        [Rpc(SendTo.Server)]
        private void ResultRpc(float accuracy, Vector3 position, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (host == null || rpc.Receive.SenderClientId != OwnerClientId || activeMode == 0 || !float.IsFinite(accuracy) || accuracy < 0 || accuracy > 1 ||
                !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return;
            float duration = activeMode == 2 ? NeedsRules.UrinatingSeconds : NeedsRules.ToiletingSeconds;
            if (Time.time - began < duration - .1f || Vector3.Distance(position, movement.transform.position) > 2) return;
            Toilet toilet = activeMode == 1 ? host.toilets[activeIndex] : null;
            Urinal urinal = activeMode == 2 ? host.urinals[activeIndex] : null;
            if (toilet != null && (host.SharedToilets[activeIndex] != OwnerClientId + 1 || !toilet.GetComponentInParent<StallDoor>().Contains(movement))) return;
            if (urinal != null && (host.SharedUrinals[activeIndex] != OwnerClientId + 1 || !urinal.Contains(movement))) return;
            Input.PlayerInput.ApplyResult(movement, toilet, urinal, accuracy, position, activeIndex);
            ClearUse();
        }
        private void ClearUse()
        {
            var host = ServerFacilities();
            if (host != null && activeMode == 2 && activeIndex >= 0)
            { host.SharedUrinals[activeIndex] = 0; host.urinals[activeIndex].Release(movement); }
            ReleaseToiletsOf(OwnerClientId);
            activeMode = 0; activeIndex = -1;
            GetComponent<PlayerNeeds>().Toileting = false;
        }
        public bool RequestWipe()
        {
            if (!IsSpawned || !IsOwner) return false;
            WipeRpc(); return true;
        }
        [Rpc(SendTo.Server)]
        private void WipeRpc(RpcParams rpc = default)
        {
            var host = ServerFacilities(); var needs = GetComponent<PlayerNeeds>();
            if (host == null || rpc.Receive.SenderClientId != OwnerClientId || !needs.Unwiped || Item.FindCarriedBy(movement) is not PaperRoll roll) return;
            if (!roll.Use()) return;
            needs.Wipe();
            int index = Array.IndexOf(host.items, roll);
            host.SharedItems[index] = Facilities.Snapshot(roll, ItemSnapshot.Carried, OwnerClientId);
        }
        public bool RequestToolbox(Toolbox toolbox)
        {
            if (!IsSpawned || !IsOwner || Facilities.Current == null) return false;
            int index = Array.IndexOf(Facilities.Current.toolboxes, toolbox);
            if (index < 0) return false;
            ToolboxRpc(index); return true;
        }
        [Rpc(SendTo.Server)]
        private void ToolboxRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (host == null || rpc.Receive.SenderClientId != OwnerClientId || index < 0 || index >= host.toolboxes.Length) return;
            var box = host.toolboxes[index];
            if (!box.Interact(movement)) return;
            int item = Array.IndexOf(host.items, box.Mop);
            host.SharedItems[item] = Facilities.Snapshot(box.Mop, box.Mop.Stored ? ItemSnapshot.Stored : ItemSnapshot.Carried, OwnerClientId);
        }
        public void RequestClean(int index, bool held)
        {
            if (IsSpawned && IsOwner) CleanRpc(index, held);
        }
        [Rpc(SendTo.Server)]
        private void CleanRpc(int index, bool held, RpcParams rpc = default)
        {
            if (!IsServer || rpc.Receive.SenderClientId != OwnerClientId) return;
            if (!held) { cleaningIndex = -1; return; }
            var world = FindFirstObjectByType<Map.Playground>();
            if (world == null || index < 0 || index >= world.Messes.Count || world.Messes[index] == null || !world.Messes[index].CanClean(movement))
            { cleaningIndex = -1; return; }
            if (cleaningIndex != index) { cleaningIndex = index; cleaningBegan = Time.time; }
            cleaningHeartbeat = Time.time;
        }
        private void TickCleaning()
        {
            if (!IsServer || cleaningIndex < 0) return;
            var world = FindFirstObjectByType<Map.Playground>();
            if (Time.time - cleaningHeartbeat > .5f || world == null || cleaningIndex >= world.Messes.Count || world.Messes[cleaningIndex] == null || !world.Messes[cleaningIndex].CanClean(movement))
            { cleaningIndex = -1; return; }
            if (Time.time - cleaningBegan < NeedsRules.CleaningSeconds) return;
            ServerFacilities().RemoveMess(cleaningIndex);
            GetComponent<PlayerNeeds>().AddDisgust(NeedsRules.CleaningDisgust);
            cleaningIndex = -1;
        }
        private void Update()
        {
            if (IsSpawned) { ReportFlights(); TickCleaning();
                if (IsServer && activeMode != 0 && Time.time - began > NeedsRules.ToiletingSeconds + 5) ClearUse();
            }
        }

        public override void OnNetworkDespawn()
        {
            // Detach carried objects before NGO destroys this avatar, including on receiving peers.
            var shared = Facilities.Current;
            if (items != null) for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null || item.PickedUpBy != movement) continue;
                item.ApplySharedRest(movement.transform.position + Vector3.up * .1f, item.transform.rotation);
                if (IsServer && shared != null && i < shared.SharedItems.Count)
                    shared.SharedItems[i] = Facilities.Snapshot(item, ItemSnapshot.Landed, 0);
            }
            if (IsServer) { ClearUse(); LandFlightsOf(OwnerClientId); }

        }
    }
}
