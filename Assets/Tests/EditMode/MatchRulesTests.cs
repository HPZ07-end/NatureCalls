using NUnit.Framework;
using NatureCalls.Core;

namespace NatureCalls.Tests
{
    public sealed class MatchRulesTests
    {
        [Test] public void DefaultScheduleHasThreeRisingWaves()
        {
            var schedule = MatchRules.Schedule();
            Assert.That(schedule.Centers, Is.EqualTo(new[] { 120f, 264f, 408f }));
            Assert.That(schedule.Amplitudes, Is.EqualTo(new[] { .6f, 1.3f, 2f }));
            Assert.That(schedule.Width, Is.EqualTo(20f));
        }
        [Test] public void TidinessUsesClampedKernel() { Assert.That(MatchRules.Tidiness(2), Is.EqualTo(88)); Assert.That(MatchRules.Tidiness(100), Is.Zero); Assert.That(MatchRules.Tidiness(-1), Is.EqualTo(100)); }
        [Test] public void OutcomeUsesTidinessThenCleanup() { Assert.That(MatchRules.Resolve(0, false).Winner, Is.EqualTo(MatchWinner.Saboteur)); Assert.That(MatchRules.Resolve(10, true).Winner, Is.EqualTo(MatchWinner.Keepers)); Assert.That(MatchRules.Resolve(10, false).Winner, Is.EqualTo(MatchWinner.None)); }
    }
}
