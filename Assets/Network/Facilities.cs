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
    public struct FacilityStateEntry : INetworkSerializeByMemcpy, IEquatable<FacilityStateEntry>
    {
        public ushort Id;
        public byte State;
        public ulong Owner;
        public bool Equals(FacilityStateEntry other) => Id == other.Id && State == other.State && Owner == other.Owner;
    }
    /// <summary>Server-owned facility state for one map, independent of every player.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Facilities : NetworkBehaviour
    {
        public static Facilities Current { get; private set; }
        // legacy: migrate to FacilityStateEntry (planned: with plumbing / unified toileting)
        public NetworkList<byte> SharedDoors;
        // legacy: migrate to FacilityStateEntry (planned: with plumbing / unified toileting)
        public NetworkList<ItemSnapshot> SharedItems;
        // Toilet occupants: 0 when free, otherwise the sitting player's client id + 1.
        // legacy: migrate to FacilityStateEntry (planned: with plumbing / unified toileting)
        public NetworkList<ulong> SharedToilets;
        // Where players toileted outside a stall during this round.
        // legacy: migrate to FacilityStateEntry (planned: with plumbing / unified toileting)
        public NetworkList<MessSnapshot> SharedMesses;
        private Map.Playground world;
        internal ITraversalFacility[] TraversalSources { get; private set; } = Array.Empty<ITraversalFacility>();
        public NetworkList<FacilityStateEntry> SharedFacilityState;
        // Generic shared state: every ISharedFacilityState under the map, in hierarchy order; entry ids are assigned
        // consecutively over (source, local index), so every peer derives the same ids from the same map.
        private ISharedFacilityState[] sharedSources = Array.Empty<ISharedFacilityState>();
        private int[] sharedFirstId = Array.Empty<int>();
        /// <summary>Provider interactables reachable through the generic FacilitySync.RequestInteract, in hierarchy order.</summary>
        internal List<IInteractable> SharedInteractables { get; } = new();


        internal StallDoor[] doors;
        internal Item[] items;
        internal Toilet[] toilets;
        internal Urinal[] urinals;
        internal Toolbox[] toolboxes;
        // legacy: migrate to FacilityStateEntry (planned: with plumbing / unified toileting)
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
            SharedFacilityState = new NetworkList<FacilityStateEntry>();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            Map.Playground.MessAuthority = AddMess;
            world = FindFirstObjectByType<Map.Playground>();
            if (world != null)
            {
                TraversalSources = world.GetComponentsInChildren<MonoBehaviour>().OfType<ITraversalFacility>()
                    .OrderBy(access => ((MonoBehaviour)access).name).ToArray();
                var owners = world.GetComponentsInChildren<MonoBehaviour>(true).OrderBy(item => FacilityPath(item.transform), StringComparer.Ordinal).ToArray();
                sharedSources = owners.OfType<ISharedFacilityState>().ToArray();
                sharedFirstId = new int[sharedSources.Length];
                for (int i = 0, next = 0; i < sharedSources.Length; next += sharedSources[i].SharedEntryCount, i++) sharedFirstId[i] = next;
                SharedInteractables.Clear();
                foreach (var provider in owners.OfType<IInteractableProvider>()) SharedInteractables.AddRange(provider.Interactables);
                if (IsServer) PublishShared();
                SharedFacilityState.OnListChanged += FacilityStateChanged;
                ApplyGenericFacilityState();
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
        /// <summary>Server: performs a generic interactable for a player after re-checking it with a server-side context, then publishes.</summary>
        internal void PerformShared(int id, Movement player, ulong clientId, bool busy)
        {
            if (!IsServer || player == null || id < 0 || id >= SharedInteractables.Count) return;
            var interactable = SharedInteractables[id];
            var context = new InteractionContext(player, clientId, false, null, busy);
            if (!interactable.CanInteract(context) || !interactable.Perform(context)) return;
            PublishShared();
            PublishPickedUpItems();
        }

        /// <summary>Server: writes every shared entry that differs from the list (seeds the list on first call).</summary>
        internal void PublishShared()
        {
            if (!IsServer) return;
            for (int i = 0; i < sharedSources.Length; i++)
                for (int k = 0; k < sharedSources[i].SharedEntryCount; k++)
                {
                    var id = (ushort)(sharedFirstId[i] + k); var value = sharedSources[i].ReadShared(k); var current = GetFacilityState(id, out var found);
                    if (!found || current.State != value.State || current.Owner != value.Owner) SetFacilityState(id, value.State, value.Owner);
                }
        }

        // An interaction may hand the player an item (e.g. a dispenser); mirror it into the item list.
        private void PublishPickedUpItems()
        {
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
            {
                var carrier = items[i].PickedUpBy;
                if (carrier == null || SharedItems[i].Mode == ItemSnapshot.Carried) continue;
                var network = carrier.GetComponent<NetworkObject>();
                SharedItems[i] = Snapshot(items[i], ItemSnapshot.Carried, network != null ? network.OwnerClientId : 0);
            }
        }

        internal void ReleaseOwned(ulong clientId)
        {
            if (!IsServer) return;
            foreach (var source in sharedSources) source.ReleaseOwner(clientId);
            PublishShared();
        }

        private FacilityStateEntry GetFacilityState(ushort id, out bool found)
        {
            for (int i = 0; i < SharedFacilityState.Count; i++) if (SharedFacilityState[i].Id == id) { found = true; return SharedFacilityState[i]; }
            found = false; return new FacilityStateEntry { Id = id };
        }
        private void SetFacilityState(ushort id, byte state, ulong owner)
        {
            for (int i = 0; i < SharedFacilityState.Count; i++) if (SharedFacilityState[i].Id == id) { SharedFacilityState[i] = new FacilityStateEntry { Id = id, State = state, Owner = owner }; return; }
            SharedFacilityState.Add(new FacilityStateEntry { Id = id, State = state, Owner = owner });
        }
        private void FacilityStateChanged(NetworkListEvent<FacilityStateEntry> change) => ApplyGenericFacilityState();
        private void ApplyGenericFacilityState()
        {
            if (IsServer) return;
            for (int i = 0; i < sharedSources.Length; i++)
                for (int k = 0; k < sharedSources[i].SharedEntryCount; k++)
                {
                    var value = GetFacilityState((ushort)(sharedFirstId[i] + k), out var found);
                    if (found) sharedSources[i].ApplyShared(k, new SharedFacilityValue(value.State, value.Owner));
                }
        }
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
            for (int i = 0; i < SharedMesses.Count; i++) world.ShowMess(i, SharedMesses[i].Kind, SharedMesses[i].Position, SharedMesses[i].ToiletIndex);
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
            if (Map.Playground.MessAuthority == (Func<MessKind, Vector3, int, int>)AddMess) Map.Playground.MessAuthority = null;
            SharedFacilityState.OnListChanged -= FacilityStateChanged;
            SharedDoors.OnListChanged -= DoorChanged;
            SharedItems.OnListChanged -= ItemChanged;
            SharedToilets.OnListChanged -= ToiletChanged;
            SharedMesses.OnListChanged -= MessesChanged;
            SharedUrinals.OnListChanged -= ToiletChanged;
            if (Current == this) Current = null;
        }
    }
}
