using NatureCalls.Player;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Facility
{
    public sealed class Urinal : MonoBehaviour
    {
        public Vector3 Center;
        public Vector3 Front;
        public Vector3 TargetCenter => transform.TransformPoint(Center);
        public Vector3 FrontPosition => transform.TransformPoint(Front);
        public float TargetRadius => NeedsRules.TargetRadius;
        public Movement Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;
        public bool Contains(Movement player) => player != null && Vector3.Distance(player.transform.position, FrontPosition) <= NeedsRules.UrinalDistance;
        public bool Occupy(Movement player)
        {
            if (!Contains(player) || (Occupant != null && Occupant != player)) return false;
            Occupant = player;
            return true;
        }
        public void ApplySharedOccupant(Movement player) => Occupant = player;
        public void Release(Movement player) { if (Occupant == player) Occupant = null; }
    }
}
