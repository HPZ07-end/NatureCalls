using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Online: the local player's request channel to the server (implemented by Network/FacilitySync).</summary>
        public readonly IFacilityRequests Requests;
        /// <summary>The player is mid-toileting or cleaning. Doors, pickups and the toolbox still work; traversal and busy-gated stations do not.</summary>
        public readonly bool Busy;
        public InteractionContext(Movement player, ulong clientId, bool online, IFacilityRequests requests, bool busy = false)
        { Player = player; ClientId = clientId; Online = online; Requests = requests; Busy = busy; }
    }

    /// <summary>
    /// Requests a facility sends to the server when online. Declared here so Facility and Map never reference Network;
    /// Network/FacilitySync implements it and the server re-validates every request.
    /// </summary>
    public interface IFacilityRequests
    {
        bool RequestPickup(Item item);
        bool RequestDoor(StallDoor door);
        bool RequestToolbox(Toolbox toolbox);
        bool RequestTraversal(ITraversalFacility access);
        bool RequestInteract(IInteractable interactable);
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
        /// <summary>Owner side, after the server granted the traversal: move the player.</summary>
        bool TraverseGranted(Movement player);
        /// <summary>Opens or closes the passage (a cover). Shared through ISharedFacilityState when the facility implements it.</summary>
        void SetOpen(bool value);
    }
    /// <summary>One shared value of a facility: a small state code and an owner (0 = nobody; the meaning is the facility's own).</summary>
    public readonly struct SharedFacilityValue
    {
        public readonly ushort State;
        public readonly ulong Owner;
        public SharedFacilityValue(ushort state, ulong owner = 0) { State = state; Owner = owner; }
    }

    /// <summary>
    /// A facility whose state is shared online. It declares a fixed number of entries; Network/Facilities gives them
    /// stable ids (map hierarchy order), seeds them on the server, publishes what the server changes and applies them on
    /// every peer, including late joiners. The facility never talks to the network itself.
    /// </summary>
    public interface ISharedFacilityState
    {
        int SharedEntryCount { get; }
        SharedFacilityValue ReadShared(int index);
        void ApplyShared(int index, SharedFacilityValue value);
        /// <summary>Server: drop whatever a disconnected client held. The change is published afterwards.</summary>
        void ReleaseOwner(ulong clientId);
    }
    public interface ISharedFacilityAuthority { void SetAuthority(bool value); }

    /// <summary>Network adapters expose an owner id without making Facility depend on Network.</summary>
    public interface IClientOwner { ulong ClientId { get; } }

    public interface IInteractableProvider
    {
        IEnumerable<IInteractable> Interactables { get; }
    }

    /// <summary>Input-side long press contract. The action is still validated by Perform on the authority.</summary>
    public interface IHoldInteractable
    {
        float HoldSeconds { get; }
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
                var focused = candidates.Where(value => Vector3.Dot(ray.direction, (value.AimPoint - ray.origin).normalized) >= cone)
                    .OrderBy(value => Vector3.Cross(ray.direction, value.AimPoint - ray.origin).sqrMagnitude)
                    .ThenBy(value => value.Priority).FirstOrDefault();
                if (focused != null) return focused;
                // Automated movement can update body yaw before the camera pitch catches up. Keep E
                // target-directed while accepting the same narrow horizontal cone at reachable height.
                var flat = Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized;
                var horizontal = candidates.Where(value => Mathf.Abs(value.AimPoint.y - ray.origin.y) <= 2f)
                    .Where(value => Mathf.Abs(Vector3.Dot(flat, Vector3.ProjectOnPlane(value.AimPoint - ray.origin, Vector3.up).normalized)) >= 0f)
                    .OrderBy(value => Vector3.Cross(flat, Vector3.ProjectOnPlane(value.AimPoint - ray.origin, Vector3.up)).sqrMagnitude)
                    .ThenBy(value => value.Priority).FirstOrDefault();
                return horizontal;
            }
            return candidates.OrderBy(value => Vector3.Distance(context.Player.transform.position, value.AimPoint))
                .ThenBy(value => value.Priority).FirstOrDefault();
        }
    }

    /// <summary>Shared reach contract for map stations and generic facility providers.</summary>
    public static class Interaction
    {
        public const float Reach = 1.15f;
        public const float ReachHeightBand = .95f;
        public const float ReachDistance = Reach + .55f;
        public static Vector3 ReachCenter(Movement player) => player.transform.position + Vector3.up * .55f;
        public static bool InReach(Movement player, Transform target)
        {
            if (player == null || target == null) return false;
            var center = ReachCenter(player);
            return Mathf.Abs(target.position.y - center.y) <= ReachHeightBand &&
                (target.position - center).sqrMagnitude < Reach * Reach;
        }
        public static bool InReach(InteractionContext context, Transform target) =>
            !context.Busy && InReach(context.Player, target);
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
