using NatureCalls.Player;
using UnityEngine;
using UnityEngine.Events;

namespace NatureCalls.Map
{
    [RequireComponent(typeof(Collider))]
    public sealed class DressingMirror : MonoBehaviour
    {
        [Min(.1f)] public float InteractionDistance = 1.5f;
        public UnityEvent PlayerEntered = new UnityEvent();
        public UnityEvent PlayerExited = new UnityEvent();
        public UnityEvent Interacted = new UnityEvent();

        private Movement nearbyPlayer;

        public bool CanInteract(Movement player) => player != null &&
            Vector3.Distance(player.transform.position, transform.position) <= InteractionDistance;

        public bool Interact(Movement player)
        {
            if (!CanInteract(player)) return false;
            Interacted.Invoke();
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<Movement>();
            if (player == null || nearbyPlayer == player) return;
            nearbyPlayer = player;
            PlayerEntered.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<Movement>();
            if (player == null || nearbyPlayer != player) return;
            nearbyPlayer = null;
            PlayerExited.Invoke();
        }

        private void OnDisable()
        {
            if (nearbyPlayer == null) return;
            nearbyPlayer = null;
            PlayerExited.Invoke();
        }
    }
}
