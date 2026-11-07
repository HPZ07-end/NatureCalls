using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    [DisallowMultipleComponent]
    public sealed class PaperRoll : MonoBehaviour
    {
        public const int CarryCapacityPerPlayer = 1;

        [Min(.1f)] public float InteractionDistance = 1.5f;
        [Min(.1f)] public float ThrowSpeed = 4f;
        [Min(0)] public float ThrowUpwardSpeed = 1.5f;
        public Movement PickedUpBy { get; private set; }
        /// <summary>True while this peer only shows a roll that another player's machine is simulating.</summary>
        public bool IsFollowing { get; private set; }
        private Vector3 followPosition, followVelocity;
        private Quaternion followRotation;
        private float followReceived;
        public bool IsPickedUp => PickedUpBy != null;

        public bool CanPickup(Movement player) => !IsPickedUp && player != null && CountCarriedBy(player) < CarryCapacityPerPlayer &&
            Vector3.Distance(player.transform.position, transform.position) <= InteractionDistance;

        public static int CountCarriedBy(Movement player)
        {
            if (player == null) return 0;
            var count = 0;
            foreach (var roll in FindObjectsByType<PaperRoll>(FindObjectsSortMode.None))
                if (roll.PickedUpBy == player) count++;
            return count;
        }

        public static PaperRoll FindCarriedBy(Movement player)
        {
            if (player == null) return null;
            foreach (var roll in FindObjectsByType<PaperRoll>(FindObjectsSortMode.None))
                if (roll.PickedUpBy == player) return roll;
            return null;
        }

        public bool Pickup(Movement player)
        {
            if (!CanPickup(player)) return false;
            ApplySharedPickup(player);
#if UNITY_EDITOR
            Debug.Log($"PAPER_ROLL_PICKUP_OK|roll={name}|player={player.name}");
#endif
            return true;
        }

        // Applied after the server has validated the pickup. Repeating a snapshot is safe.
        public void ApplySharedPickup(Movement player)
        {
            if (player == null || PickedUpBy == player) return;
            PickedUpBy = player;
            IsFollowing = false;
            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.useGravity = false;
            }
            transform.SetParent(player.transform, true);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        public bool Throw(Movement player, Vector3 direction)
        {
            if (player == null || PickedUpBy != player) return false;
            var forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = player.transform.forward;

            var position = player.transform.position + Vector3.up + forward * .65f;
            var velocity = forward * ThrowSpeed + Vector3.up * ThrowUpwardSpeed;
            ApplySharedThrow(position, transform.rotation, velocity, true);
#if UNITY_EDITOR
            Debug.Log($"PAPER_ROLL_THROW_OK|roll={name}|player={player.name}|speed={velocity.magnitude:F2}");
#endif
            return true;
        }

        /// <summary>
        /// Shows a roll in flight on another player's machine: kinematic here, eased toward the latest report and
        /// extrapolated along its arc between reports, so it moves smoothly instead of jumping every update.
        /// </summary>
        public void FollowShared(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!IsFollowing)
            {
                ApplySharedThrow(position, rotation, Vector3.zero, false);
                // This peer moves the transform itself; Rigidbody interpolation would fight it.
                GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
            }
            IsFollowing = true;
            followPosition = position;
            followRotation = rotation;
            followVelocity = velocity;
            followReceived = Time.time;
        }

        /// <summary>Places a roll that has come to rest after a throw.</summary>
        public void ApplySharedRest(Vector3 position, Quaternion rotation)
        {
            ApplySharedThrow(position, rotation, Vector3.zero, false);
        }

        private void Update()
        {
            if (!IsFollowing) return;
            var elapsed = Mathf.Min(Time.time - followReceived, .15f);
            var predicted = followPosition + followVelocity * elapsed + .5f * Physics.gravity * elapsed * elapsed;
            var blend = 1 - Mathf.Exp(-20 * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, predicted, blend), Quaternion.Slerp(transform.rotation, followRotation, blend));
        }

        public void ApplySharedThrow(Vector3 position, Quaternion rotation, Vector3 velocity, bool simulate)
        {
            PickedUpBy = null;
            IsFollowing = false;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = true;

            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = !simulate;
            body.useGravity = simulate;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = simulate ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.ContinuousSpeculative;
            if (simulate) body.linearVelocity = velocity;
        }
    }
}
