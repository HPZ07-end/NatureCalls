using System.Collections.Generic;
using System.Linq;
using NatureCalls.Network;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    /// <summary>Inputs available to a facility action. The registry owns selection; facilities own meaning.</summary>
    public readonly struct InteractionContext
    {
        public readonly Movement Player;
        public readonly ulong ClientId;
        public readonly bool Online;
        public readonly FacilitySync Sync;
        public InteractionContext(Movement player, ulong clientId, bool online, FacilitySync sync)
        { Player = player; ClientId = clientId; Online = online; Sync = sync; }
    }

    public interface IInteractable
    {
        int Priority { get; }
        Transform Target { get; }
        float Distance { get; }
        bool CanInteract(InteractionContext context);
        string Prompt(InteractionContext context);
        bool Perform(InteractionContext context);
    }

    public interface ITraversalFacility
    {
        bool CanTraverse(Movement player);
        bool TraverseGranted(Movement player);
        void SetOpen(bool value);
    }
    public interface IMapFacility
    {
        int CoinCount { get; }
        ulong CoinOwner(int index);
        void ApplyCoin(int index, ulong owner);
        NatureCalls.Map.SewerRepairProgress RepairProgress { get; }
        void ApplyRepairProgress(NatureCalls.Map.SewerRepairProgress progress);
        bool Interact(int action, Movement player, ulong clientId, out int changedCoin, out PaperRoll dispensed);
    }

    public interface IInteractableProvider
    {
        IEnumerable<IInteractable> Interactables { get; }
    }

    /// <summary>Scene-local registry. Components register themselves and disappear with their scene.</summary>
    public static class InteractionRegistry
    {
        private static readonly List<IInteractable> entries = new();
        public static void Register(IInteractable value) { if (value != null && !entries.Contains(value)) entries.Add(value); }
        public static void Unregister(IInteractable value) => entries.Remove(value);
        public static IEnumerable<IInteractable> All => entries.Where(value => value != null && (!(value is UnityEngine.Object unity) || unity != null));
        public static IInteractable Resolve(InteractionContext context, UnityEngine.Camera camera)
        {
            var candidates = All.ToList();
            foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component is IInteractable interactable && !candidates.Contains(interactable)) candidates.Add(interactable);
            foreach (var provider in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IInteractableProvider>())
                foreach (var interactable in provider.Interactables) if (interactable != null && !candidates.Contains(interactable)) candidates.Add(interactable);
            candidates = candidates.Where(value => value.CanInteract(context)).ToList();
            if (candidates.Count == 0) return null;
            if (camera != null && context.Player != null && !context.Player.IsCrawling && !context.Player.Aiming)
            {
                var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
                var hit = Physics.RaycastAll(ray, Mathf.Max(1f, candidates.Max(value => value.Distance)), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                    .OrderBy(value => value.distance)
                    .Select(value => candidates.FirstOrDefault(item => value.transform == item.Target || value.transform.IsChildOf(item.Target)))
                    .FirstOrDefault(value => value != null);
                if (hit != null) return hit;
                const float cone = .965f; // about 15 degrees
                return candidates.Where(value => Vector3.Dot(ray.direction, (value.Target.position - ray.origin).normalized) >= cone)
                    .OrderBy(value => Vector3.Distance(ray.origin, value.Target.position))
                    .ThenBy(value => value.Priority).FirstOrDefault();
            }
            return candidates.OrderBy(value => Vector3.Distance(context.Player.transform.position, value.Target.position))
                .ThenBy(value => value.Priority).FirstOrDefault();
        }
    }

    public abstract class InteractableComponent : MonoBehaviour, IInteractable
    {
        public virtual int Priority => 0;
        public virtual Transform Target => transform;
        public virtual float Distance => 1.5f;
        public abstract bool CanInteract(InteractionContext context);
        public abstract string Prompt(InteractionContext context);
        public abstract bool Perform(InteractionContext context);
        protected virtual void OnEnable() => InteractionRegistry.Register(this);
        protected virtual void OnDisable() => InteractionRegistry.Unregister(this);
    }
}
