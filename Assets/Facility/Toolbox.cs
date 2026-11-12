using NatureCalls.Player;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Facility
{
    public sealed class Toolbox : MonoBehaviour
    {
        public Mop Mop;
        private void Start() { if (Mop != null && !Mop.IsPickedUp) Mop.Store(transform.position); }
        public bool CanInteract(Movement player)
        {
            if (player == null || Mop == null || Vector3.Distance(player.transform.position, transform.position) > NeedsRules.InteractionDistance) return false;
            var carried = Item.FindCarriedBy(player);
            return carried == Mop || (carried == null && Mop.Stored);
        }
        public bool Interact(Movement player)
        {
            if (!CanInteract(player) || Mop == null) return false;
            var carried = Item.FindCarriedBy(player);
            if (carried == Mop) { Mop.Store(transform.position); return true; }
            if (carried != null || !Mop.Stored) return false;
            Mop.Take(player);
            return true;
        }
    }
}
