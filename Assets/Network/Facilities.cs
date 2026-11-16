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
    public struct MessSnapshot : INetworkSerializeByMemcpy, IEquatable<MessSnapshot>
    {
        public MessKind Kind;
        public Vector3 Position;
        public int ToiletIndex;
        public bool Equals(MessSnapshot other) => Kind == other.Kind && Position == other.Position && ToiletIndex == other.ToiletIndex;
    }
    /// <summary>Server-owned facility state for one map, independent of every player.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Facilities : NetworkBehaviour
    {
        public static Facilities Current { get; private set; }
        public NetworkList<byte> SharedDoors;
        public NetworkList<ItemSnapshot> SharedItems;
        // Toilet occupants: 0 when free, otherwise the sitting player's client id + 1.
        public NetworkList<ulong> SharedToilets;
        // Where players toileted outside a stall during this round.
        public NetworkList<MessSnapshot> SharedMesses;
        private Map.Playground world;
        internal StallDoor[] doors;
        internal Item[] items;
        internal Toilet[] toilets;
        internal Urinal[] urinals;
        internal Toolbox[] toolboxes;
        public NetworkList<ulong> SharedUrinals;
        private static int facilitySceneHandle = -1;
        private static StallDoor[] sceneDoors;
        private static Item[] sceneItems;
        private static Toilet[] sceneToilets;

        private void Awake()
        {
            SharedUrinals = new NetworkList<ulong>();
            SharedDoors = new NetworkList<byte>();
            SharedItems = new NetworkList<ItemSnapshot>();
            SharedToilets = new NetworkList<ulong>();
            SharedMesses = new NetworkList<MessSnapshot>();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            world = FindFirstObjectByType<Map.Playground>();
            if (world != null)
            {
                // Capture the map ordering once, before a carried roll is reparented to a player.
                if (facilitySceneHandle != world.gameObject.scene.handle || sceneDoors == null || sceneItems == null || sceneToilets == null)
                {
                    facilitySceneHandle = world.gameObject.scene.handle;
                    sceneDoors = FindObjectsByType<StallDoor>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene == world.gameObject.scene)
                        .OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                    sceneItems = FindObjectsByType<Item>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene == world.gameObject.scene)
                        .OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                    sceneToilets = FindObjectsByType<Toilet>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene == world.gameObject.scene)
                        .OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                }
                urinals = FindObjectsByType<Urinal>(FindObjectsSortMode.None).OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                toolboxes = FindObjectsByType<Toolbox>(FindObjectsSortMode.None).OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                doors = sceneDoors;
                items = sceneItems;
                toilets = sceneToilets;
            }
            if (doors == null) return;
            if (IsServer)
            {
                foreach (var door in doors) SharedDoors.Add((byte)door.State);
                foreach (var roll in items) SharedItems.Add(Snapshot(roll, roll.Stored ? ItemSnapshot.Stored : ItemSnapshot.OnMap, 0));
                foreach (var toilet in toilets) SharedToilets.Add(0);
                foreach (var urinal in urinals) SharedUrinals.Add(0);
            }
            SharedDoors.OnListChanged += DoorChanged;
            SharedItems.OnListChanged += ItemChanged;
            SharedToilets.OnListChanged += ToiletChanged;
            SharedMesses.OnListChanged += MessesChanged;
            SharedUrinals.OnListChanged += ToiletChanged;
            ApplyAllFacilityState();
            ShowMesses();
        }
        private static string FacilityPath(Transform item)
        {
            var path = "";
            for (var node = item; node != null; node = node.parent)
                path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
            return path;
        }

        internal static ItemSnapshot Snapshot(Item roll, byte mode, ulong carrier)
        {
            var body = roll.GetComponent<Rigidbody>();
            return new ItemSnapshot
            {
                Mode = mode, Carrier = carrier, Uses = roll.RemainingUses, Position = roll.transform.position,
                Rotation = roll.transform.rotation,
                Velocity = body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero
            };
        }

        private void DoorChanged(NetworkListEvent<byte> change) => ApplyAllFacilityState();
        private void ItemChanged(NetworkListEvent<ItemSnapshot> change) => ApplyAllFacilityState();
        private void ToiletChanged(NetworkListEvent<ulong> change) => ApplyAllFacilityState();

        private static Movement MovementOf(ulong clientId)
        {
            foreach (var sync in FindObjectsByType<FacilitySync>(FindObjectsSortMode.None))
                if (sync.IsSpawned && sync.OwnerClientId == clientId) return sync.GetComponent<Movement>();
            return null;
        }

        internal void ApplyAllFacilityState()
        {
            if (doors == null || IsServer) return;
            for (var index = 0; index < doors.Length && index < SharedDoors.Count; index++)
                doors[index].ApplySharedState((StallDoorState)SharedDoors[index]);
            for (var index = 0; index < items.Length && index < SharedItems.Count; index++)
            {
                var state = SharedItems[index];
                if (items[index] is PaperRoll paper) paper.ApplyUses(state.Uses);
                if (state.Mode == ItemSnapshot.Stored) { items[index].Store(state.Position); continue; }
                if (state.Mode == ItemSnapshot.Carried)
                {
                    var carrier = MovementOf(state.Carrier);
                    if (carrier != null) items[index].Take(carrier);
                }
                // The simulating player already has the roll in flight; everyone else follows its reports.
                else if (state.Mode == ItemSnapshot.Flying && state.Simulator != NetworkManager.LocalClientId + 1)
                    items[index].FollowShared(state.Position, state.Rotation, state.Velocity);
                else if (state.Mode == ItemSnapshot.Landed)
                    items[index].ApplySharedRest(state.Position, state.Rotation);
            }
            for (var index = 0; index < urinals.Length && index < SharedUrinals.Count; index++)
                urinals[index].ApplySharedOccupant(SharedUrinals[index] == 0 ? null : MovementOf(SharedUrinals[index] - 1));
            for (var index = 0; index < toilets.Length && index < SharedToilets.Count; index++)
                toilets[index].ApplySharedOccupant(SharedToilets[index] == 0 ? null : MovementOf(SharedToilets[index] - 1));
        }

        private void MessesChanged(NetworkListEvent<MessSnapshot> change) => ShowMesses();
        private void ShowMesses()
        {
            if (world == null) return;
            var states = new List<MessSnapshot>();
            foreach (var state in SharedMesses) states.Add(state);
            world.ShowMesses(states);
        }
        public int AddMess(MessKind kind, Vector3 position, int toiletIndex = -1)
        {
            if (!IsServer || kind == MessKind.None) return -1;
            if (kind == MessKind.DirtyToilet)
                foreach (var state in SharedMesses) if (state.Kind == kind && state.ToiletIndex == toiletIndex) return -1;
            SharedMesses.Add(new MessSnapshot { Kind = kind, Position = position, ToiletIndex = toiletIndex });
            return SharedMesses.Count - 1;
        }
        public void RemoveMess(int index)
        {
            if (IsServer && index >= 0 && index < SharedMesses.Count) SharedMesses[index] = default;
        }
        public override void OnNetworkDespawn()
        {
            SharedDoors.OnListChanged -= DoorChanged;
            SharedItems.OnListChanged -= ItemChanged;
            SharedToilets.OnListChanged -= ToiletChanged;
            SharedMesses.OnListChanged -= MessesChanged;
            SharedUrinals.OnListChanged -= ToiletChanged;
            if (Current == this) Current = null;
        }
    }
}
