using System;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;

namespace NatureCalls.Network
{
    // Snapshots are published by the server-owned Facilities object.
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

    /// <summary>
    /// Player-owned requests; the server validates them and publishes results through Facilities.
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(Movement), typeof(Posture))]
    public sealed class FacilitySync : NetworkBehaviour
    {
        private Movement movement;
        private Posture posture;
        private StallDoor[] doors => Facilities.Current != null ? Facilities.Current.doors : null;
        private PaperRoll[] paperRolls => Facilities.Current != null ? Facilities.Current.paperRolls : null;
        private Toilet[] toilets => Facilities.Current != null ? Facilities.Current.toilets : null;

        public override void OnNetworkSpawn()
        {
            movement = GetComponent<Movement>();
            posture = GetComponent<Posture>();
            posture.Changed += PostureChanged;
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
            var host = ServerFacilities();
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
        }

        [Rpc(SendTo.Server)]
        private void PickupPaperRollRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.paperRolls.Length ||
                index >= host.SharedPaperRolls.Count) return;
            var roll = host.paperRolls[index];
            if (!roll.Pickup(movement)) return;
            host.SharedPaperRolls[index] = Facilities.Snapshot(roll, 1, OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        private void ThrowPaperRollRpc(int index, Vector3 position, Quaternion rotation, Vector3 velocity, RpcParams rpc = default)
        {
            var host = ServerFacilities();
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
            var host = ServerFacilities();
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
        private readonly Dictionary<int, float> restingSince = new();
        private void ReportFlights()
        {
            if (!IsOwner || paperRolls == null || Time.time < nextFlightReport) return;
            var host = Facilities.Current;
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
            var host = ServerFacilities();
            if (host == null || host.paperRolls == null) return;
            for (var index = 0; index < host.paperRolls.Length && index < host.SharedPaperRolls.Count; index++)
            {
                var state = host.SharedPaperRolls[index];
                if (state.Mode != PaperRollSnapshot.Flying || state.Simulator != clientId + 1) continue;
                host.paperRolls[index].ApplySharedRest(state.Position, state.Rotation);
                host.SharedPaperRolls[index] = Moving(PaperRollSnapshot.Landed, 0, state.Position, state.Rotation, Vector3.zero);
            }
        }

        /// <summary>Host only: records a dropping left where a player toileted outside a stall.</summary>
        public void AddDropping(Vector3 position)
        {
            var host = ServerFacilities();
            if (host != null) host.SharedDroppings.Add(position);
        }

        private void PostureChanged(PostureState state)
        {
            if (IsSpawned && IsServer && state == PostureState.Standing) ReleaseToiletsOf(OwnerClientId);
        }

        private void Update()
        {
            if (IsSpawned) ReportFlights();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) { ReleaseToiletsOf(OwnerClientId); LandFlightsOf(OwnerClientId); }
            if (posture != null) posture.Changed -= PostureChanged;
        }
    }
}
