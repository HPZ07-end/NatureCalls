using NatureCalls.Core;
using NatureCalls.Player;
using UnityEngine;

namespace NatureCalls.UI
{
    /// <summary>Connects <see cref="VersusHudFeed"/> to the match (MatchContext) and body (PlayerNeeds) read-only state.</summary>
    internal static class VersusHudWiring
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Wire()
        {
            VersusHudFeed.LatestEvent = () => (MatchContext.LastEventText, MatchContext.EventSerial);
            VersusHudFeed.Blackout = () => Mathf.Clamp01(MatchContext.BlackoutRemaining);
            VersusHudFeed.Outcome = () =>
            {
                var s = MatchContext.Settlement;
                var keepersWon = MatchContext.Winner == MatchWinner.Keepers;
                var reason = string.IsNullOrEmpty(MatchContext.EndReason) ? s.Reason : MatchContext.EndReason;
                var stats = $"产生污物 {s.Produced}   清理 {s.Cleaned}\n失禁 {s.Accidents}   呕吐 {s.Vomits}   倒灌 {s.Backflows}\n成功如厕 {s.ToiletingSuccesses}\n\n干净值  {Mathf.CeilToInt(Mathf.Max(0, MatchContext.Tidiness))}";
                return (keepersWon, reason, stats);
            };
            VersusHudFeed.Bladder = p => Needs(p) is { } n ? n.Bladder / 100f : 0f;
            VersusHudFeed.Bowel = p => Needs(p) is { } n ? n.Bowel / 100f : 0f;
            VersusHudFeed.Composure = p => Needs(p) is { } n ? n.Composure / 100f : 1f;
            VersusHudFeed.Endurance = p => Needs(p) is { } n && n.InEndurance ? Mathf.Clamp01(.55f + .45f * n.AimJitterIntensity) : 0f;
            VersusHudFeed.LostControl = p => Needs(p) is { } n && n.IsOutOfControl;
            VersusHudFeed.Vomit = p => Needs(p) is { } n && n.Vomiting ? Mathf.Max(.05f, n.VomitCountdownProgress) : 0f;
            VersusHudFeed.Narrow = p => Needs(p) is { } n ? n.NarrowingIntensity : 0f;
            VersusHudFeed.Comfort = p => Needs(p) is { } n ? Mathf.Max(n.Toileting ? n.ComfortPresentationIntensity : 0f, Mathf.Clamp01(n.ComfortBoostRemaining / 15f) * .6f) : 0f;
        }

        private static PlayerNeeds Needs(Movement player) => player != null && player.TryGetComponent<PlayerNeeds>(out var needs) ? needs : null;
    }
}
