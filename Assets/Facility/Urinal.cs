using System.Linq;
using NatureCalls.Player;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Facility
{
    public sealed class Urinal : MonoBehaviour, ISharedFacilityState, IWaterFixture
    {
        public Vector3 Center;
        public Vector3 Front;
        public Vector3 TargetCenter => transform.TransformPoint(Center);
        public Vector3 FrontPosition => transform.TransformPoint(Front);
        public float TargetRadius => NeedsRules.TargetRadius;
        public Movement Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;
        [SerializeField] private Pipe pipe;
        public Pipe ConnectedPipe => pipe;
        public void SetPipe(Pipe value) => pipe = value;
        public int SharedEntryCount => 1;
        public SharedFacilityValue ReadShared(int index)
        {
            var owner = Occupant != null ? Occupant.GetComponentsInParent<MonoBehaviour>(true).OfType<IClientOwner>().FirstOrDefault() : null;
            return new SharedFacilityValue(0, owner != null ? owner.ClientId + 1 : 0);
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
        public void Backflow() => MessSinkRegistry.Current?.AddMess(Core.MessKind.Puddle, FrontPosition);
        private static Movement FindMovement(ulong owner)
        {
            if (owner == 0) return null;
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
            {
                var identity = player.GetComponentsInParent<MonoBehaviour>(true).OfType<IClientOwner>().FirstOrDefault();
                if (identity != null && identity.ClientId + 1 == owner) return player;
            }
            return null;
        }
    }
}
