using System.Collections.Generic;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Player
{
    public interface IMovementEffectSource { bool AppliesTo(Movement player); MovementModifier GetModifier(Movement player); }
    public static class MovementEffectRegistry
    {
        public static MovementModifier Combine(Movement player)
        {
            var result = new MovementModifier();
            foreach (var source in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (source is IMovementEffectSource effect && effect.AppliesTo(player)) result = MovementRules.Combine(result, effect.GetModifier(player));
            return result;
        }
        public static bool ForceCrawl(Movement player)
        {
            foreach (var source in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (source is IMovementEffectSource effect && effect.AppliesTo(player) && effect.GetModifier(player).ForceCrawl) return true;
            return false;
        }
    }
}
