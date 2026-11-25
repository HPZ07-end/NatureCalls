using NUnit.Framework;
using NatureCalls.Core;

namespace NatureCalls.Tests
{
    public sealed class PlumbingRulesTests
    {
        [Test] public void ContributionsReachBlockedThreshold() { Assert.That(PlumbingRules.AddPaper(70, 6), Is.EqualTo(100)); Assert.That(PlumbingRules.IsBlocked(100), Is.True); }
        [Test] public void ClearResetsAccumulation() => Assert.That(PlumbingRules.Clear(), Is.Zero);
        [Test] public void PaperNeedsFollowDefecationResult() { Assert.That(NeedsRules.PaperRequired(MessKind.None), Is.EqualTo(1)); Assert.That(NeedsRules.PaperRequired(MessKind.DirtyToilet), Is.EqualTo(2)); Assert.That(NeedsRules.PaperRequired(MessKind.Dropping), Is.EqualTo(3)); }
        [Test] public void CharacterProfileDefaultPreservesBehavior() { var p = CharacterProfile.Default; Assert.That(p.MoveSpeedScale, Is.EqualTo(1)); Assert.That(p.UrgencyRateScale, Is.EqualTo(1)); Assert.That(p.AimSteadiness, Is.EqualTo(1)); Assert.That(CharacterProfile.For(PlayerBodyType.Balanced).ReachHeight, Is.EqualTo(p.ReachHeight)); }
    }
}
