using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>Map2 descent at A and ladder return at B.</summary>
    public sealed class SewerAccess : MonoBehaviour
    {
        public bool Ascending;
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

        private void Awake()
        {
            closed = Lid.localRotation;
            var rising = Vector3.Cross(Lid.right, Top.position - Lid.position);
            openAngle = rising.y >= 0 ? 105 : -105;
        }

        private Vector3 Approach => (Ascending ? Landing : Top).position + ApproachDirection * ApproachDistance;

        public string Prompt => Ascending ? "E  爬梯返回地面" : "E  打开井盖并下滑";

        public bool CanTraverse(Movement player)
        {
            if (player == null || player.gameObject.scene != gameObject.scene ||
                player.GetComponent<Posture>().State != PostureState.Standing ||
                player.Immobile || player.Aiming || player.IsTraversing) return false;

            var feet = player.transform.position;
            if (Ascending) return Mathf.Abs(feet.y - Approach.y) < .4f && Vector3.Distance(feet, Approach) <= 1.1f;
            var mouth = Top.position;
            if (Mathf.Abs(feet.y - mouth.y) >= .45f ||
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

        public void SetOpen(bool value)
        {
            open = value;
            Lid.localRotation = closed * Quaternion.AngleAxis(value ? openAngle : 0, Vector3.right);
            ClosedCover.enabled = !value;
        }

        public bool Traverse(Movement player)
        {
            if (!CanTraverse(player)) return false;
            if (!open) SetOpen(true);
            return player.Traverse(Route(player.transform.position), Ascending ? 1.55f : .95f);
        }

        internal bool TraverseGranted(Movement player)
        {
            if (!open) SetOpen(true);
            return player.Traverse(Route(player.transform.position), Ascending ? 1.55f : .95f);
        }

        private Vector3[] Route(Vector3 from)
        {
            var direction = ApproachDirection * ApproachDistance;
            var upper = Top.position + Vector3.up * .06f;
            var lower = Landing.position + Vector3.up * .06f;
            return Ascending
                ? new[] { from, lower, upper, upper + direction }
                : new[] { from, upper, lower, lower + direction };
        }
    }
}
