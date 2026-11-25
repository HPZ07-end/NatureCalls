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
        /// <summary>The player is mid-toileting or cleaning. Doors, pickups and the toolbox still work (a seated player can lock the stall); map traversal and Map2 stations do not.</summary>
        public readonly bool Busy;
        public InteractionContext(Movement player, ulong clientId, bool online, FacilitySync sync, bool busy = false)
        { Player = player; ClientId = clientId; Online = online; Sync = sync; Busy = busy; }
    }

    public interface IInteractable
    {
        int Priority { get; }
        Transform Target { get; }
        Vector3 AimPoint { get; }
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
    public interface ISharedFacilityState
    {
        int EntryCount { get; }
        ulong OwnerAt(int index);
        void ApplyAt(int index, ulong owner);
        byte Progress { get; }
        void ApplyProgress(byte progress);
        bool Interact(int action, Movement player, ulong clientId, out int changedEntry, out PaperRoll dispensed);
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
        public static IInteractable Resolve(InteractionContext context, Ray? view)
        {
            var candidates = All.Where(value => value.CanInteract(context)).ToList();
            if (candidates.Count == 0) return null;
            if (view.HasValue)
            {
                var ray = view.Value;
                var hit = Physics.RaycastAll(ray, Mathf.Max(1f, candidates.Max(value => value.Distance)), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                    .OrderBy(value => value.distance)
                    .Select(value => candidates.FirstOrDefault(item => value.transform == item.Target || value.transform.IsChildOf(item.Target)))
                    .FirstOrDefault(value => value != null);
                if (hit != null) return hit;
                const float cone = .965f; // about 15 degrees
                return candidates.Where(value => Vector3.Dot(ray.direction, (value.AimPoint - ray.origin).normalized) >= cone)
                    .OrderBy(value => Vector3.Cross(ray.direction, value.AimPoint - ray.origin).sqrMagnitude)
                    .ThenBy(value => value.Priority).FirstOrDefault();
            }
            return candidates.OrderBy(value => Vector3.Distance(context.Player.transform.position, value.AimPoint))
                .ThenBy(value => value.Priority).FirstOrDefault();
        }
    }

    public abstract class InteractableComponent : MonoBehaviour, IInteractable
    {
        public virtual int Priority => 0;
        public virtual Transform Target => transform;
        public virtual Vector3 AimPoint
        {
            get
            {
                var bounds = new Bounds(transform.position, Vector3.zero); var found = false;
                foreach (var collider in GetComponentsInChildren<Collider>(true)) { if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds); }
                if (!found) foreach (var renderer in GetComponentsInChildren<Renderer>(true)) { if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds); }
                return bounds.center;
            }
        }
        public virtual float Distance => 1.5f;
        public abstract bool CanInteract(InteractionContext context);
        public abstract string Prompt(InteractionContext context);
        public abstract bool Perform(InteractionContext context);
        protected virtual void OnEnable() => InteractionRegistry.Register(this);
        protected virtual void OnDisable() => InteractionRegistry.Unregister(this);
    }
}
