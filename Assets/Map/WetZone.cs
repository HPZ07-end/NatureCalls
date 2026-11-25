using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>A map-defined wet area with the shared movement modifier.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WetZone : MonoBehaviour, IMovementEffectSource
    {
        private BoxCollider area;
        private void Awake() => area = GetComponent<BoxCollider>();
        private void OnEnable() => MovementEffectRegistry.Register(this);
        private void OnDisable() => MovementEffectRegistry.Unregister(this);
        public bool IsWet(Vector3 feet) => area != null && area.bounds.Contains(feet + Vector3.up * .07f);
        public bool AppliesTo(Movement player) => player != null && IsWet(player.transform.position);
        public MovementModifier GetModifier(Movement player) => new MovementModifier(1, 8);
    }
}
