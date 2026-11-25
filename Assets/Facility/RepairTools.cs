using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    public sealed class Wrench : Item { }
    public sealed class Plunger : Item { }
    public static class ToolSpeed { public static float Multiplier(Movement player) => 1f; }
    public static class RepairTools
    {
        public static bool Repair(Movement player, Breakable target) => Item.FindCarriedBy(player) is Wrench && target != null && target.IsBroken && target.Repair(player);
        public static bool Clear(Movement player, Toilet toilet) => Item.FindCarriedBy(player) is Plunger && toilet != null && toilet.ConnectedPipe != null && toilet.ConnectedPipe.IsBlocked && toilet.ConnectedPipe.ClearAndReport();
    }
}
