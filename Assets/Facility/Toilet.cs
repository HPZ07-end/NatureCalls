using UnityEngine;
using NatureCalls.Player;

namespace NatureCalls.Facility
{
    public sealed class Toilet : MonoBehaviour
    {
        public Transform Anchor;
        public Transform Interior;
        [Min(.1f)] public float InteractionDistance = 1.7f;
        public bool CanEnter(Movement player) => Anchor != null && player != null &&
            Vector3.Distance(player.transform.position, Anchor.position) <= InteractionDistance;
        public bool Enter(Movement player)
        {
            if (!CanEnter(player)) return false;
            player.Enter(Anchor);
            return true;
        }
    }
}
