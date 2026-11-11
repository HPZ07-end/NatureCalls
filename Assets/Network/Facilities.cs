using System;
using System.Collections.Generic;
using System.Linq;
using NatureCalls.Facility;
using NatureCalls.Player;
using Unity.Netcode;
using UnityEngine;

namespace NatureCalls.Network
{
    /// <summary>Server-owned facility state for one map, independent of every player.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Facilities : NetworkBehaviour
    {
        public static Facilities Current { get; private set; }
        public NetworkList<byte> SharedDoors;
        public NetworkList<PaperRollSnapshot> SharedPaperRolls;
        // Toilet occupants: 0 when free, otherwise the sitting player's client id + 1.
        public NetworkList<ulong> SharedToilets;
        // Where players toileted outside a stall during this round.
        public NetworkList<Vector3> SharedDroppings;
        private Map.Playground world;
        internal StallDoor[] doors;
        internal PaperRoll[] paperRolls;
        internal Toilet[] toilets;
        private static int facilitySceneHandle = -1;
        private static StallDoor[] sceneDoors;
        private static PaperRoll[] scenePaperRolls;
        private static Toilet[] sceneToilets;

        private void Awake()
        {
            SharedDoors = new NetworkList<byte>();
            SharedPaperRolls = new NetworkList<PaperRollSnapshot>();
            SharedToilets = new NetworkList<ulong>();
            SharedDroppings = new NetworkList<Vector3>();
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
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
            if (doors == null) return;
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
            ApplyAllFacilityState();
            ShowDroppings();
        }
        private static string FacilityPath(Transform item)
        {
            var path = "";
            for (var node = item; node != null; node = node.parent)
                path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
            return path;
        }

        internal static PaperRollSnapshot Snapshot(PaperRoll roll, byte mode, ulong carrier)
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
            foreach (var sync in FindObjectsByType<FacilitySync>(FindObjectsSortMode.None))
                if (sync.IsSpawned && sync.OwnerClientId == clientId) return sync.GetComponent<Movement>();
            return null;
        }

        internal void ApplyAllFacilityState()
        {
            if (doors == null || IsServer) return;
            for (var index = 0; index < doors.Length && index < SharedDoors.Count; index++)
                doors[index].ApplySharedState((StallDoorState)SharedDoors[index]);
            for (var index = 0; index < paperRolls.Length && index < SharedPaperRolls.Count; index++)
            {
                var state = SharedPaperRolls[index];
                if (state.Mode == PaperRollSnapshot.Carried)
                {
                    var carrier = MovementOf(state.Carrier);
                    if (carrier != null) paperRolls[index].ApplySharedPickup(carrier);
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

        private void DroppingsChanged(NetworkListEvent<Vector3> change) => ShowDroppings();

        private void ShowDroppings()
        {
            if (world == null) return;
            var positions = new List<Vector3>();
            foreach (var position in SharedDroppings) positions.Add(position);
            world.ShowDroppings(positions);
        }

        public override void OnNetworkDespawn()
        {
            SharedDoors.OnListChanged -= DoorChanged;
            SharedPaperRolls.OnListChanged -= PaperRollChanged;
            SharedToilets.OnListChanged -= ToiletChanged;
            SharedDroppings.OnListChanged -= DroppingsChanged;
            if (Current == this) Current = null;
        }
    }
}
