using NatureCalls.Player;
using NatureCalls.Facility;
using NatureCalls.Network;
using System.Collections.Generic;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>Map2 bidirectional hatch and ladder traversal.</summary>
    public sealed class SewerAccess : InteractableComponent, ITraversalFacility, ISharedFacilityState, IInteractableProvider
    {
        public bool Ascending;
        public bool HasHatch = true;
        public Transform Top;
        public Transform Landing;
        public Transform Lid;
        public Collider ClosedCover;
        public Vector3 ApproachDirection = Vector3.forward;
        [Min(.75f)] public float ApproachDistance = 1.18f;
        private const float EntryRadius = 1.85f;
        private Quaternion closed;
        private bool open;
        private float openAngle;
        private float nextAutomaticRequest;
        public bool IsOpen => open;
        public IEnumerable<IInteractable> Interactables { get { yield return this; } }

        private void Awake()
        {
            closed = HasHatch ? Quaternion.identity : Lid.localRotation;
            if (HasHatch) SetOpen(false);
            var rising = Vector3.Cross(Lid.right, Top.position - Lid.position);
            openAngle = rising.y >= 0 ? 105 : -105;
        }

        private Vector3 Approach => (Ascending ? Landing : Top).position + ApproachDirection * ApproachDistance;

        public string ActionPrompt => HasHatch ? open ? "E  关闭井盖 · 走入井口下滑" : "E  打开井盖" : Ascending ? "E  爬梯返回地面" : "走入井口下滑";

        public override float Distance => 1.85f;
        public override Transform Target => HasHatch ? Lid : base.Target;
        public override Vector3 AimPoint => HasHatch ? Lid.position : Approach;
        // Aiming anywhere at the shaft (ladder, pillar, opening) selects it, so the ladder works from below too.
        public override bool Owns(Transform hit) => base.Owns(hit) || (Top != null && Top.parent != null && hit.IsChildOf(Top.parent));
        public override bool CanInteract(InteractionContext context) => !context.Busy &&
            (HasHatch && NearTop(context.Player) ? CanReachHatch(context.Player) : CanTraverse(context.Player));
        public override string Prompt(InteractionContext context) => HasHatch && NearTop(context.Player) ? ActionPrompt : "E  爬梯返回地面";
        public override bool Perform(InteractionContext context)
        {
            if (HasHatch && NearTop(context.Player))
            {
                if (context.Online) return context.Requests != null && context.Requests.RequestInteract(this);
                if (!CanInteract(context) || (open && PassageOccupied())) return false;
                SetOpen(!open);
                return true;
            }
            return context.Online ? context.Requests != null && context.Requests.RequestTraversal(this) : Traverse(context.Player);
        }

        private bool NearTop(Movement player) => player != null && Mathf.Abs(player.transform.position.y - Top.position.y) < .75f;

        private bool CanReachHatch(Movement player) => player != null && player.gameObject.scene == gameObject.scene &&
            !player.IsTraversing && !player.Immobile && !player.Aiming &&
            Vector3.Distance(player.transform.position, Lid.position) <= Distance;

        private bool PassageOccupied()
        {
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
                if (player.gameObject.scene == gameObject.scene &&
                    Mathf.Abs(player.transform.position.y - Top.position.y) < 1.8f &&
                    Vector3.ProjectOnPlane(player.transform.position - Top.position, Vector3.up).sqrMagnitude < .8f * .8f)
                    return true;
            return false;
        }

        private void Update()
        {
            if (!HasHatch || !open || Time.time < nextAutomaticRequest) return;
            foreach (var player in FindObjectsByType<Movement>(FindObjectsSortMode.None))
            {
                if (!player.Controlled || player.IsTraversing || player.Immobile || player.Aiming || player.LastInputMagnitude < .1f ||
                    (player.GetComponent<Posture>()?.State != PostureState.Standing) ||
                    Mathf.Abs(player.transform.position.y - Top.position.y) >= .45f ||
                    Vector3.ProjectOnPlane(player.transform.position - Top.position, Vector3.up).sqrMagnitude > .5f * .5f) continue;
                var sync = player.GetComponent<FacilitySync>();
                if (sync != null && sync.IsSpawned) sync.RequestTraversal(this);
                else Traverse(player);
                nextAutomaticRequest = Time.time + 1f;
                break;
            }
        }

        public bool CanTraverse(Movement player)
        {
            var posture = player != null ? player.GetComponent<Posture>() : null;
            if (player == null || player.gameObject.scene != gameObject.scene || posture == null ||
                posture.State != PostureState.Standing ||
                player.Immobile || player.Aiming || player.IsTraversing) return false;

            var feet = player.transform.position;
            bool descending = Mathf.Abs(feet.y - Top.position.y) < .75f;
            var mouth = descending ? Top.position : Landing.position;
            // Both Map2 shafts have a ladder and are traversable in both directions.
            // Ascending only describes the legacy approach orientation; it must not
            // disable climbing out of one of the shafts.
            if ((descending && HasHatch && !open) || Mathf.Abs(feet.y - mouth.y) >= .75f ||
                Vector3.ProjectOnPlane(feet - mouth, Vector3.up).sqrMagnitude > EntryRadius * EntryRadius) return false;
            // The hatch is reachable from the perimeter, but never through the adjacent stall wall.
            var raise = Vector3.up * .8f;
            var blockers = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
            var line = mouth - feet;
            foreach (var hit in Physics.RaycastAll(feet + raise, line.normalized, line.magnitude,
                blockers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(Top.parent)) return false;
            return true;
        }

        // Shared entry: whether the cover is open (State 1).
        public int SharedEntryCount => 1;
        public SharedFacilityValue ReadShared(int index) => new SharedFacilityValue(open ? (byte)1 : (byte)0);
        public void ApplyShared(int index, SharedFacilityValue value) => SetOpen(value.State != 0);
        public void ReleaseOwner(ulong clientId) { }

        public void SetOpen(bool value)
        {
            open = HasHatch && value;
            if (!HasHatch) return;
            Lid.localRotation = closed * Quaternion.AngleAxis(open ? openAngle : 0, Vector3.right);
            ClosedCover.enabled = !open;
        }

        public bool Traverse(Movement player)
        {
            if (!CanTraverse(player)) return false;
            if (!open) SetOpen(true);
            return player.Traverse(Route(player.transform.position), .95f);
        }

        public bool TraverseGranted(Movement player)
        {
            if (!open) SetOpen(true);
            return player.Traverse(Route(player.transform.position), .95f);
        }

        private Vector3[] Route(Vector3 from)
        {
            var direction = ApproachDirection * ApproachDistance;
            var upper = Top.position + Vector3.up * .06f;
            var lower = Landing.position + Vector3.up * .06f;
            bool ascending = from.y < (Top.position.y + Landing.position.y) * .5f;
            return ascending
                ? new[] { from, lower, upper, upper + direction }
                : new[] { from, upper, lower, lower + direction };
        }
    }
}
