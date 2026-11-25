using NUnit.Framework;
using NatureCalls.Core;

namespace NatureCalls.Tests
{
    public sealed class StoolRulesTests
    {
        [Test] public void BaseOddsAreSoftHardLoose()
        {
            var (soft, hard, loose) = StoolRules.Odds(false, .5f, false, 0);
            Assert.That(soft, Is.EqualTo(.60f).Within(1e-5)); Assert.That(hard, Is.EqualTo(.25f).Within(1e-5)); Assert.That(loose, Is.EqualTo(.15f).Within(1e-5));
        }
        [Test] public void SaboteurFavoursHard() => Assert.That(StoolRules.Odds(true, .5f, false, 0).hard, Is.GreaterThan(StoolRules.Odds(false, .5f, false, 0).hard * 1.7f));
        [Test] public void UrgencyOrLossOfControlFavoursLoose()
        {
            float calm = StoolRules.Odds(false, .5f, false, 0).loose;
            Assert.That(StoolRules.Odds(false, .95f, false, 0).loose, Is.GreaterThan(calm * 2));
            Assert.That(StoolRules.Odds(false, .1f, true, 0).loose, Is.GreaterThan(calm * 2));
        }
        [Test] public void ComfortFavoursSoft() => Assert.That(StoolRules.Odds(false, .5f, false, 1).soft, Is.GreaterThan(StoolRules.Odds(false, .5f, false, 0).soft));
        [Test] public void OddsSumToOne([Values(false, true)] bool saboteur, [Values(0f, .95f)] float urgency, [Values(0f, 1f)] float comfort)
        {
            var (s, h, l) = StoolRules.Odds(saboteur, urgency, false, comfort);
            Assert.That(s + h + l, Is.EqualTo(1f).Within(1e-5));
        }
        [Test] public void RollPartitionsTheUnitInterval()
        {
            Assert.That(StoolRules.Roll(false, 0, false, 0, 0f), Is.EqualTo(StoolKind.Soft));
            Assert.That(StoolRules.Roll(false, 0, false, 0, .7f), Is.EqualTo(StoolKind.Hard));
            Assert.That(StoolRules.Roll(false, 0, false, 0, .99f), Is.EqualTo(StoolKind.Loose));
        }
        [Test] public void PipeOdorAndDirtFollowTheTable()
        {
            Assert.That(StoolRules.PipeLoad(StoolKind.Soft), Is.EqualTo(25)); Assert.That(StoolRules.PipeLoad(StoolKind.Hard), Is.EqualTo(45)); Assert.That(StoolRules.PipeLoad(StoolKind.Loose), Is.EqualTo(10));
            Assert.That(StoolRules.Odor(StoolKind.Soft), Is.GreaterThan(StoolRules.Odor(StoolKind.Loose))); Assert.That(StoolRules.Odor(StoolKind.Loose), Is.GreaterThan(StoolRules.Odor(StoolKind.Hard)));
            Assert.That(StoolRules.DirtWeight(StoolKind.Loose), Is.EqualTo(1.2f)); Assert.That(StoolRules.DirtWeight(StoolKind.Hard), Is.EqualTo(.6f));
        }
        [Test] public void OnlyLooseIntoABlockedPipeBackflows()
        {
            Assert.That(StoolRules.Backflows(StoolKind.Loose, true), Is.True);
            Assert.That(StoolRules.Backflows(StoolKind.Loose, false), Is.False);
            Assert.That(StoolRules.Backflows(StoolKind.Hard, true), Is.False);
        }
        [Test] public void FloorMessMapping()
        {
            Assert.That(StoolRules.FloorMess(StoolKind.Soft), Is.EqualTo(MessKind.Dropping));
            Assert.That(StoolRules.FloorMess(StoolKind.Loose), Is.EqualTo(MessKind.LooseStool));
            Assert.That(StoolRules.FloorMess(StoolKind.Hard), Is.EqualTo(MessKind.None));
            Assert.That(StoolRules.KindOf(MessKind.LooseStool), Is.EqualTo(StoolKind.Loose));
        }
    }
}
