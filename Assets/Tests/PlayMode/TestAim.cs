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
            var context = new InteractionContext(player, 0, false, null); var target = InteractionRegistry.All.Where(item => item.Target != null && item.CanInteract(context)).OrderBy(item => Vector3.Distance(item.AimPoint, player.transform.position)).FirstOrDefault()?.AimPoint;
            if (target.HasValue) player.LookAt(target.Value);
        }
        /// <summary>Turns the first-person view toward the interactable that owns <paramref name="target"/> (its aim point), so E selects it by crosshair.</summary>
        public static void AimAt(Movement player, Component target)
        {
            var owner = InteractionRegistry.All.FirstOrDefault(item => item.Target != null && (item.Target == target.transform || target.transform.IsChildOf(item.Target) || item.Target.IsChildOf(target.transform)));
            player.LookAt(owner != null ? owner.AimPoint : target.transform.position);
        }
        public static IEnumerator LookAt(Movement player, Transform target)
        {
            player.LookAt(target.position);
            yield return null;
            yield return null;
        }
    }
}
