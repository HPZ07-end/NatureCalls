using System.Collections;
using NatureCalls.Player;
using NatureCalls.Facility;
using PlayerInput = NatureCalls.Input.PlayerInput;
using System.Linq;
using NatureCalls.Camera;
using UnityEngine;
namespace NatureCalls.Tests
{
    internal static class TestAim
    {
        public static void LookAtNearest(PlayerInput input) { LookAtNearest(input.GetComponent<Movement>()); }
        public static void LookAtNearest(Movement player)
        {
            var context = new InteractionContext(player, 0, false, null); var values = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IInteractable>().Where(item => item.Target != null && item.CanInteract(context)); var target = values.OrderBy(item => Vector3.Distance(item.Target.position, player.transform.position)).FirstOrDefault()?.Target;
            if (target == null) return;
            var delta = target.position - player.transform.position; var flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (flat.sqrMagnitude > .0001f) player.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }
        public static IEnumerator LookAt(Movement player, Transform target)
        {
            var delta = target.position - player.transform.position;
            var flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (flat.sqrMagnitude > .0001f) player.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            var camera = Object.FindFirstObjectByType<CameraController>();
            if (camera != null && camera.Target == player)
            {
                var eye = camera.Output;
                if (eye != null) eye.transform.rotation = Quaternion.LookRotation(delta, Vector3.up);
            }
            yield return null;
        }
    }
}
