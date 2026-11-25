using System.Linq;
using NatureCalls.Player;
using NatureCalls.UI;
using NatureCalls.Facility;
using UnityEngine;

namespace NatureCalls.Map
{
    /// <summary>
    /// compat: adapts the new local room to this checkout's proximity-based PlayerInput.
    /// Replace with the public branch's InteractionRegistry when that input migration is integrated.
    /// </summary>
    public sealed class DressingRoomInteraction : MonoBehaviour
    {
        public Playground World;
        public LobbyController Lobby;

        public bool Owns(Movement player) => World != null && World.OfflinePlayer == player && player != null;
        private bool Available(Movement player) => Owns(player) && player.Controlled && !player.IsTraversing &&
            (Lobby == null || (!Lobby.IsLobbyOpen && !Lobby.IsOutfitPanelOpen));

        private Component Focus(Movement player)
        {
            if (!Available(player)) return null;
            var scene = gameObject.scene;
            var candidates = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                .Where(value => value is DressingMirror mirror && mirror.CanInteract(player) ||
                    value is LobbyDoor door && door.CanInteract(player) || value is DressingRoomStool stool && stool.CanInteract(player) ||
                    value is StallDoor stall && stall.CanInteract(player))
                .Cast<Component>().ToArray();
            if (candidates.Length == 0) return null;
            var ray = new Ray(player.transform.position + Vector3.up * World.Camera.EyeHeight, player.ViewRotation * Vector3.forward);
            var hit = Physics.RaycastAll(ray, 2.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                .OrderBy(value => value.distance)
                .Select(value => candidates.FirstOrDefault(item => value.transform == item.transform || value.transform.IsChildOf(item.transform)))
                .FirstOrDefault(value => value != null);
            return hit != null ? hit : candidates.Where(value => Vector3.Dot(ray.direction, (AimPoint(value) - ray.origin).normalized) >= .965f)
                .OrderBy(value => Vector3.Cross(ray.direction, AimPoint(value) - ray.origin).sqrMagnitude).FirstOrDefault();
        }

        public string Prompt(Movement player) => Focus(player) switch
        {
            DressingMirror => "E  看镜子",
            LobbyDoor => "E  进入大厅",
            DressingRoomStool stool => stool.Prompt(),
            StallDoor => "E  开关隔间门",
            _ => ""
        };

        public bool Interact(Movement player) => Focus(player) switch
        {
            DressingMirror mirror => mirror.Interact(player),
            LobbyDoor door => door.Interact(player),
            DressingRoomStool stool => stool.Interact(player),
            StallDoor stall => stall.Interact(player),
            _ => false
        };

        public bool PlaceStool(Movement player) => Available(player) && DressingRoomStool.CarriedBy(player) is { } stool && stool.Place(player);

        public static Vector3 AimPoint(Component target)
        {
            if (target is DressingRoomStool stool) return stool.AimPoint;
            if (target is StallDoor door) return door.DoorColliderPivot.TransformPoint(new Vector3(.55f, 1, 0));
            var colliders = target.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return target.transform.position;
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
            return bounds.center;
        }
    }
}
