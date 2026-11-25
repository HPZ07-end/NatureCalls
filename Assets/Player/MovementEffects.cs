using System.Collections.Generic;
using System.Linq;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Player
{
    public interface IMovementEffectSource { bool AppliesTo(Movement player); MovementModifier GetModifier(Movement player); }
    public interface IForcedInputSource { bool ModifyInput(Movement player, ref Vector2 input); }
    public static class MovementEffectRegistry
    {
        private static readonly List<IMovementEffectSource> entries = new();
        public static void Register(IMovementEffectSource source) { if (source != null && !entries.Contains(source)) entries.Add(source); }
        public static void Unregister(IMovementEffectSource source) => entries.Remove(source);
        private static IEnumerable<IMovementEffectSource> Active => entries.Where(source => source != null && (!(source is Object unity) || unity != null));
        public static MovementModifier Combine(Movement player)
        {
            var result = new MovementModifier(1);
            foreach (var source in Active) if (source is not PlayerNeeds && source.AppliesTo(player)) result = MovementRules.Combine(result, source.GetModifier(player));
            // PlayerNeeds is the built-in body source; keep the registry contract resilient when a runtime
            // spawned player is enabled before its OnEnable registration callback runs.
            var needs = player != null ? player.GetComponent<PlayerNeeds>() : null;
            if (needs != null) result = MovementRules.Combine(result, needs.GetModifier(player));
            return result;
        }
        public static bool ForceCrawl(Movement player)
        {
            foreach (var source in Active) if (source.AppliesTo(player) && source.GetModifier(player).ForceCrawl) return true;
            return false;
        }
        public static bool ForcedInput(Movement player, ref Vector2 input)
        {
            bool changed = false;
            foreach (var source in Active) if (source is IForcedInputSource forced && source.AppliesTo(player)) changed |= forced.ModifyInput(player, ref input);
            return changed;
        }
    }
}
