using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.Facility
{
    /// <summary>
    /// Authority-side entry for a bowel movement's result (GLOSSARY Mess/StoolKind): draws the kind, then puts it on the
    /// floor (shared mess or hard-stool item) or into a toilet (pipe load, loose backflow). Called by toileting and loss of control.
    /// </summary>
    public static class StoolOutput
    {
        /// <summary>Tests: forces every draw to this kind. Null draws from StoolRules.</summary>
        public static StoolKind? Forced;

        public static StoolKind Roll(Movement player, bool lossOfControl = false)
        {
            if (Forced.HasValue) return Forced.Value;
            var needs = player != null ? player.GetComponent<PlayerNeeds>() : null;
            float urgency01 = needs != null ? needs.Urgency / NeedsRules.UrgencyMax : 0;
            // Comfort is implicit and owned by PlayerNeeds (versus body work); 0 until it is published there.
            return StoolRules.Roll(RoleRegistry.IsSaboteur(player), urgency01, lossOfControl, 0, Random.value);
        }

        /// <summary>A stool lands on the floor. Hard stools become a kickable item; if none is free it falls back to a heap.</summary>
        public static void Floor(Movement player, Vector3 position, StoolKind kind)
        {
            if (kind == StoolKind.Hard && HardStool.Spawn(position, player)) return;
            var mess = kind == StoolKind.Hard ? MessKind.Dropping : StoolRules.FloorMess(kind);
            MessSinkRegistry.Current?.AddMess(mess, position, -1, player);
        }

        /// <summary>A stool goes into a toilet: pipe load by kind; loose stool into an already blocked pipe backflows.</summary>
        public static void Bowl(Toilet toilet, StoolKind kind)
        {
            var pipe = toilet != null ? toilet.ConnectedPipe : null;
            if (pipe == null) return;
            bool wasBlocked = pipe.IsBlocked;
            pipe.AddLoad(StoolRules.PipeLoad(kind));
            if (StoolRules.Backflows(kind, wasBlocked)) pipe.TriggerBackflow(toilet);
        }
    }
}
