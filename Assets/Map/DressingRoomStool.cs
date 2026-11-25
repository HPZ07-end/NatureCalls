using System.Linq;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>Local preparation-room prop. E picks up, F places, and the existing jump reaches its top.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DressingRoomStool : MonoBehaviour
    {
        public Movement Carrier { get; private set; }
        public bool IsCarried => Carrier != null;
        private BoxCollider shape;
        private Transform roomParent;
        public Vector3 AimPoint => transform.TransformPoint(new Vector3(0, .30f, 0));
        private void Awake() { shape = GetComponent<BoxCollider>(); roomParent = transform.parent; }
        public static DressingRoomStool CarriedBy(Movement player) => player == null ? null : FindObjectsByType<DressingRoomStool>(FindObjectsSortMode.None).FirstOrDefault(stool => stool.Carrier == player);
        public bool CanInteract(Movement player) => player != null && player.Controlled &&
            !IsCarried && !Supports(player) && Vector3.Distance(player.transform.position, AimPoint) <= 1.5f;
        public string Prompt() => !IsCarried ? "E  搬起凳子 · Space 跳上凳子" :
            CanPlace(out _) ? "F  放下凳子" : "前方空间不足，请移动后按 F 放下";
        public bool Interact(Movement player)
        {
            if (!CanInteract(player)) return false;
            Carrier = player; shape.enabled = false;
            transform.SetParent(Carrier.transform, false);
            transform.localPosition = new Vector3(0, .80f, .65f); transform.localRotation = Quaternion.identity;
            return true;
        }
        public bool Place(Movement player)
        {
            if (player == null || Carrier != player || !CanPlace(out var position)) return false;
            var yaw = Carrier.transform.eulerAngles.y;
            transform.SetParent(roomParent, true);
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Carrier = null; shape.enabled = true; Physics.SyncTransforms(); return true;
        }
        public bool CanPlace(out Vector3 position)
        {
            position = default;
            if (!IsCarried) return false;
            var origin = Carrier.transform.position + Carrier.transform.forward * .95f + Vector3.up * .75f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 1.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.normal.y < .9f) return false;
            position = hit.point + Vector3.up * .006f;
            return !Physics.OverlapBox(position + Vector3.up * .18f, new Vector3(.24f, .17f, .21f), Carrier.transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(collider =>
                    !collider.transform.IsChildOf(transform) && !collider.transform.IsChildOf(Carrier.transform));
        }
        public bool Supports(Movement player)
        {
            if (player == null || IsCarried) return false;
            var local = transform.InverseTransformPoint(player.transform.position);
            return Mathf.Abs(local.x) < .30f && Mathf.Abs(local.z) < .27f && local.y > .23f && local.y < .58f;
        }
    }
}
