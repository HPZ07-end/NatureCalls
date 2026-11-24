using NatureCalls.Player;
using NatureCalls.Facility;
using UnityEngine;
using UnityEngine.Events;

namespace NatureCalls.Map
{
    [RequireComponent(typeof(Collider))]
    public sealed class DressingMirror : InteractableComponent
    {
        [Min(.1f)] public float InteractionDistance = 1.5f;
        public UnityEvent PlayerEntered = new UnityEvent();
        public UnityEvent PlayerExited = new UnityEvent();
        public UnityEvent Interacted = new UnityEvent();

        private Movement nearbyPlayer;

        public bool CanInteract(Movement player) => player != null &&
            Vector3.Distance(player.transform.position, transform.position) <= InteractionDistance;

        public override float Distance => InteractionDistance;
        public override bool CanInteract(InteractionContext context) => CanInteract(context.Player);
        public override string Prompt(InteractionContext context) => "E  看镜子";
        public override bool Perform(InteractionContext context) => Interact(context.Player);

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
