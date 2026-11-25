using NatureCalls.Player;
using NatureCalls.Core;
using UnityEngine;
using Unity.Netcode;
namespace NatureCalls.Facility
{
    public sealed class Urinal : MonoBehaviour, ISharedFacilityState
    {
        public Vector3 Center;
        public Vector3 Front;
        public Vector3 TargetCenter => transform.TransformPoint(Center);
        public Vector3 FrontPosition => transform.TransformPoint(Front);
        public float TargetRadius => NeedsRules.TargetRadius;
        public Movement Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;
        public int SharedEntryCount => 1;
        public SharedFacilityValue ReadShared(int index)
        {
            var network = Occupant != null ? Occupant.GetComponentInParent<NetworkObject>() : null;
            return new SharedFacilityValue(0, network != null ? network.OwnerClientId + 1 : 0);
        }
        public void ApplyShared(int index, SharedFacilityValue value) => Occupant = FindMovement(value.Owner);
        public void ReleaseOwner(ulong clientId) { if (ReadShared(0).Owner == clientId + 1) Occupant = null; }
        public bool Contains(Movement player) => player != null && Vector3.Distance(player.transform.position, FrontPosition) <= NeedsRules.UrinalDistance;
        public bool Occupy(Movement player)
        {
            if (!Contains(player) || (Occupant != null && Occupant != player)) return false;
            Occupant = player;
            return true;
        }
        public void ApplySharedOccupant(Movement player) => Occupant = player;
        public void Release(Movement player) { if (Occupant == player) Occupant = null; }
        private static Movement FindMovement(ulong owner)
        {
            if (owner == 0) return null;
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
            {
                var network = player.GetComponentInParent<NetworkObject>();
                if (network != null && network.OwnerClientId + 1 == owner) return player;
            }
            return null;
        }
    }
}
