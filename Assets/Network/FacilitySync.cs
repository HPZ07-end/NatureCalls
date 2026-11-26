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
    public sealed class FacilitySync : NetworkBehaviour, IFacilityRequests, IClientOwner
    {
        public ulong ClientId => OwnerClientId;
        private Movement movement;
        private Posture posture;
        private float nextTraversal;
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

        /// <summary>Generic online request for a provider interactable (see Facilities.SharedInteractables). The server re-checks CanInteract.</summary>
        public bool RequestInteract(IInteractable interactable)
        {
            if (!IsSpawned || !IsOwner || Facilities.Current == null) return false;
            int id = Facilities.Current.SharedInteractables.IndexOf(interactable);
            if (id < 0) return false;
            InteractFacilityRpc(id); return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void InteractFacilityRpc(int id)
        {
            if (cleaningIndex < 0) ServerFacilities()?.PerformShared(id, movement, OwnerClientId, activeMode != 0);
        }

        public bool RequestTraversal(ITraversalFacility access)
        {
            if (!IsSpawned || !IsOwner || Facilities.Current == null) return false;
            int index = Array.IndexOf(Facilities.Current.TraversalSources, access);
            if (index < 0) return false;
            RequestTraversalRpc(index);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestTraversalRpc(int index)
        {
            var host = ServerFacilities();
            if (host == null || index < 0 || index >= host.TraversalSources.Length || Time.time < nextTraversal ||
                activeMode != 0 || !host.TraversalSources[index].CanTraverse(movement)) return;
            if (host.TraversalSources[index] is Map.SewerAccess shaft &&
                Mathf.Abs(movement.transform.position.y - shaft.Top.position.y) < .75f && shaft.HasHatch &&
                (!shaft.IsOpen || Vector3.ProjectOnPlane(movement.transform.position - shaft.Top.position, Vector3.up).sqrMagnitude > .65f * .65f)) return;
            nextTraversal = Time.time + 1.7f;
            host.TraversalSources[index].SetOpen(true);
            host.PublishShared();
            GrantTraversalRpc(index);
        }

        [Rpc(SendTo.Owner)]
        private void GrantTraversalRpc(int index)
        {
            var shared = Facilities.Current;
            if (shared != null && index >= 0 && index < shared.TraversalSources.Length)
                shared.TraversalSources[index].TraverseGranted(movement);
        }

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

        /// <summary>A walker's machine kicks a resting hard stool: it simulates the flight like a throw and the host relays it.</summary>
        public bool RequestKick(Item roll, Vector3 velocity)
        {
            if (!IsSpawned || !IsOwner || items == null) return false;
            var index = Array.IndexOf(items, roll);
            if (index < 0 || roll.IsPickedUp || roll.Stored) return false;
            roll.ApplySharedThrow(roll.transform.position + Vector3.up * .03f, roll.transform.rotation, velocity, true);
            KickItemRpc(index, roll.transform.position, roll.transform.rotation, velocity);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void KickItemRpc(int index, Vector3 position, Quaternion rotation, Vector3 velocity, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.items.Length ||
                index >= host.SharedItems.Count) return;
            var state = host.SharedItems[index];
            if (state.Mode != ItemSnapshot.OnMap && state.Mode != ItemSnapshot.Landed) return;
            if (!IsOwnedByServer) host.items[index].FollowShared(position, rotation, velocity);
            var flying = Moving(ItemSnapshot.Flying, OwnerClientId + 1, position, rotation, velocity);
            flying.Uses = state.Uses;
            host.SharedItems[index] = flying;
        }

        /// <summary>The owner's urine rebounded onto them; the server applies it where needs tick.</summary>
        public void ReportSplash() { if (IsSpawned && IsOwner) SplashRpc(); }

        [Rpc(SendTo.Server)]
        private void SplashRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            BodyEvents.Splash(movement, BodyEvents.SplashKind.OwnRebound);
        }

        public bool RequestSeat(Toilet toilet)
        {
            if (!IsSpawned || !IsOwner || toilets == null) return false;
            var index = Array.IndexOf(toilets, toilet);
            if (index < 0 || toilet.IsOccupied) return false;
            SeatToiletRpc(index);
            return true;
        }
        public bool RequestUrination(Toilet toilet)
        {
            if (!IsSpawned || !IsOwner || toilets == null) return false;
            int index = Array.IndexOf(toilets, toilet); if (index < 0 || toilet.IsOccupied) return false;
            SeatToiletRpc(index, true); return true;
        }

        // The host checks the seat is free and the player stands in its stall, then lets the owner sit down.
        [Rpc(SendTo.Server)]
        private void SeatToiletRpc(int index, bool urine = false, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.toilets.Length || host.toilets[index].IsOccupied) return;
            var toilet = host.toilets[index];
            var stall = toilet.GetComponentInParent<StallDoor>();
            if (stall != null && !stall.Contains(movement)) return;
            if (activeMode != 0 || posture.IsCrawling) return;
            activeMode = urine ? (byte)4 : (byte)1; activeIndex = index; began = Time.time;
            GetComponent<PlayerNeeds>().Toileting = true;
            toilet.ApplySharedOccupant(movement);
            host.PublishShared();
            SeatGrantedRpc(index);
        }

        [Rpc(SendTo.Owner)]
        private void SeatGrantedRpc(int index)
        {
            if (toilets == null || index < 0 || index >= toilets.Length || posture.IsSquatting) return;
            GetComponent<Input.PlayerInput>().BeginGranted(toilets[index], null);
        }


        [Rpc(SendTo.Server)]
        private void InteractDoorRpc(int index, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (rpc.Receive.SenderClientId != OwnerClientId || host == null || index < 0 || index >= host.doors.Length) return;
            var door = host.doors[index];
            if (!door.Interact(movement)) return;
            host.PublishShared();
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
            if (!IsServer || rpc.Receive.SenderClientId != OwnerClientId || activeMode != 0 || posture.IsCrawling ||
                StallDoor.Containing(movement) != null) return;
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
            if (host == null || rpc.Receive.SenderClientId != OwnerClientId || activeMode != 0 || posture.IsCrawling ||
                index < 0 || index >= host.urinals.Length || host.urinals[index].IsOccupied) return;
            if (!host.urinals[index].Occupy(movement)) return;
            activeMode = 2; activeIndex = index; began = Time.time;
            GetComponent<PlayerNeeds>().Toileting = true;
            host.PublishShared();
            UrinalGrantedRpc(index);
        }
        [Rpc(SendTo.Owner)]
        private void UrinalGrantedRpc(int index) => GetComponent<Input.PlayerInput>().BeginGranted(null, Facilities.Current.urinals[index]);
        /// <summary>Peeing while squatting (seat or floor): settled as urine; the squat reservation stays.</summary>
        public void ReportSeatedUrination(float accuracy, Vector3 position)
        {
            if (IsSpawned && IsOwner) SeatedUrinationRpc(accuracy, position);
        }
        [Rpc(SendTo.Server)]
        private void SeatedUrinationRpc(float accuracy, Vector3 position, RpcParams rpc = default)
        {
            var host = ServerFacilities();
            if (host == null || rpc.Receive.SenderClientId != OwnerClientId || (activeMode != 1 && activeMode != 3) || !float.IsFinite(accuracy) || accuracy < 0 || accuracy > 1 ||
                !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z) ||
                Vector3.Distance(position, movement.transform.position) > 2 || Time.time - lastSeatedUrine < NeedsRules.UrinatingSeconds - .1f) return;
            lastSeatedUrine = Time.time;
            Toilet toilet = activeMode == 1 && activeIndex >= 0 ? host.toilets[activeIndex] : null;
            Input.PlayerInput.ApplyUrination(movement, toilet, null, accuracy, position);
        }
        private float lastSeatedUrine = -999f;
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
            float duration = activeMode == 1 ? .1f : NeedsRules.UrinatingSeconds;
            if (Time.time - began < duration - .1f || Vector3.Distance(position, movement.transform.position) > 2) return;
            Toilet toilet = activeMode == 1 || activeMode == 4 ? host.toilets[activeIndex] : null;
            Urinal urinal = activeMode == 2 ? host.urinals[activeIndex] : null;
            if (toilet != null && (!toilet.IsOccupied || toilet.Occupant != movement || (activeMode == 1 && !toilet.GetComponentInParent<StallDoor>().Contains(movement)))) return;
            if (urinal != null && (!urinal.IsOccupied || urinal.Occupant != movement || !urinal.Contains(movement))) return;
            if (activeMode == 4) Input.PlayerInput.ApplyUrination(movement, toilet, null, accuracy, position);
            else Input.PlayerInput.ApplyResult(movement, toilet, urinal, accuracy, position, activeIndex);
            ClearUse();
        }
        private void ClearUse()
        {
            var host = ServerFacilities();
            if (host != null && activeMode == 2 && activeIndex >= 0) host.urinals[activeIndex].Release(movement);
            if (host != null) { foreach (var toilet in host.toilets) toilet.Release(movement); host.PublishShared(); }
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
            if (host == null || rpc.Receive.SenderClientId != OwnerClientId || !needs.Unwiped) return;
            // F without paper wipes by hand (approved 2026-11-24); the server decides which, like offline.
            if (Item.FindCarriedBy(movement) is not PaperRoll roll) { needs.WipeByHand(); return; }
            int amount = Math.Min(needs.PaperRequired, roll.RemainingUses);
            if (amount <= 0 || !roll.Use(amount)) return;
            needs.ConsumePaper(amount);
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
            if (item >= 0) host.SharedItems[item] = Facilities.Snapshot(box.Mop, box.Mop.Stored ? ItemSnapshot.Stored : ItemSnapshot.Carried, OwnerClientId);
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
            if (IsServer) { ClearUse(); LandFlightsOf(OwnerClientId); shared?.ReleaseOwned(OwnerClientId); }

        }
    }
}
