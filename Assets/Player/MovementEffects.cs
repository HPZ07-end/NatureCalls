using System.Collections.Generic;
using System.Linq;
using NatureCalls.Core;
using UnityEngine;
namespace NatureCalls.Player
{
    public interface IMovementEffectSource { bool AppliesTo(Movement player); MovementModifier GetModifier(Movement player); }
    public static class MovementEffectRegistry
    {
        private static readonly List<IMovementEffectSource> entries = new();
        public static void Register(IMovementEffectSource source) { if (source != null && !entries.Contains(source)) entries.Add(source); }
        public static void Unregister(IMovementEffectSource source) => entries.Remove(source);
        private static IEnumerable<IMovementEffectSource> Active => entries.Where(source => source != null && (!(source is Object unity) || unity != null));
        public static MovementModifier Combine(Movement player)
        {
            var result = new MovementModifier(1);
            foreach (var source in Active) if (source.AppliesTo(player)) result = MovementRules.Combine(result, source.GetModifier(player));
            return result;
        }
        public static bool ForceCrawl(Movement player)
        {
            foreach (var source in Active) if (source.AppliesTo(player) && source.GetModifier(player).ForceCrawl) return true;
            return false;
        }
    }
}
