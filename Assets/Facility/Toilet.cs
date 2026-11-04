using UnityEngine;
using NatureCalls.Player;

namespace NatureCalls.Facility
{
    /// <summary>
    /// A toilet a player can sit on. One player at a time: the seat is occupied until that player stands up.
    /// Which toilet a player uses is decided by the stall they stand in (<see cref="StallDoor.Containing"/>).
    /// </summary>
    public sealed class Toilet : MonoBehaviour
    {
        public Transform Anchor;
        public Transform Interior;
        public Movement Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;

        public bool CanSeat(Movement player) => Anchor != null && player != null && (Occupant == null || Occupant == player);

        /// <summary>
        /// Where the player sits. Always the anchor for now; a later mechanic can offset this pose by how
        /// accurately the player lines up (留白 for toileting accuracy).
        /// </summary>
        public Transform SeatPose(Movement player) => Anchor;

        public bool Seat(Movement player)
        {
            if (!CanSeat(player)) return false;
            Occupant = player;
            player.Enter(SeatPose(player));
            return true;
        }

        public void Release(Movement player)
        {
            if (Occupant == player) Occupant = null;
        }

        /// <summary>The host decides who sits here; peers only record it.</summary>
        public void ApplySharedOccupant(Movement player) => Occupant = player;

        public static Toilet SeatedOn(Movement player)
        {
            if (player == null || player.Seat == null) return null;
            return player.Seat.GetComponentInParent<Toilet>();
        }
    }
}
