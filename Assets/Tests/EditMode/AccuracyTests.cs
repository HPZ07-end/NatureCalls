using NatureCalls.Core;
using NUnit.Framework;
namespace NatureCalls.Tests
{
    public sealed class AccuracyTests
    {
        [TestCase(.8f, MessKind.None)] [TestCase(.799f, MessKind.DirtyToilet)]
        [TestCase(.4f, MessKind.DirtyToilet)] [TestCase(.399f, MessKind.Dropping)]
        public void DefecationThresholds(float value, MessKind expected) => Assert.That(NeedsRules.Defecation(value), Is.EqualTo(expected));
        [TestCase(.7f, MessKind.None)] [TestCase(.699f, MessKind.Puddle)]
        public void UrinalThreshold(float value, MessKind expected) => Assert.That(NeedsRules.Urination(value), Is.EqualTo(expected));
        [Test] public void AccuracyWeightsTimeAndCapsFinalSample()
        {
            var score = new Accuracy(5);
            score.Sample(1, false); score.Sample(3, true); score.Sample(2, true);
            Assert.That(score.Elapsed, Is.EqualTo(5)); Assert.That(score.Value, Is.EqualTo(.8f));
            score.Sample(10, false); Assert.That(score.Value, Is.EqualTo(.8f));
        }
        [Test] public void DriftGrowsWithUrgencyAndClamps()
        {
            Assert.That(NeedsRules.DriftAmplitude(100), Is.GreaterThan(NeedsRules.DriftAmplitude(50)));
            Assert.That(NeedsRules.DriftAmplitude(50), Is.GreaterThan(NeedsRules.DriftAmplitude(0)));
            Assert.That(NeedsRules.DriftAmplitude(-1), Is.EqualTo(NeedsRules.DriftAmplitude(0)));
        }
        [Test] public void CleaningInterruptionResetsElapsed()
        {
            float held = NeedsRules.TickCleaning(0, 2, true);
            held = NeedsRules.TickCleaning(held, 1, false);
            Assert.That(held, Is.Zero);
            Assert.That(NeedsRules.TickCleaning(held, 1, true), Is.EqualTo(1));
            Assert.That(NeedsRules.TickCleaning(0, 4, true), Is.EqualTo(3));
        }
        [Test] public void PaperAndNeedsValues()
        {
            int uses = NeedsRules.PaperUses;
            for (int i = 0; i < 3; i++) uses = NeedsRules.UsePaper(uses);
            Assert.That(uses, Is.Zero); Assert.That(NeedsRules.UsePaper(uses), Is.Zero);
            Assert.That(NeedsRules.UrgencyAfterUrination(70), Is.EqualTo(35));
            Assert.That(NeedsRules.PuddleDisgust(1, 3), Is.EqualTo(6));
            Assert.That(NeedsRules.PuddleDisgust(1.01f, 3), Is.Zero);
            Assert.That(NeedsRules.UnwipedDisgust, Is.EqualTo(15));
            Assert.That(NeedsRules.DirtyToiletDisgust, Is.EqualTo(10));
            Assert.That(NeedsRules.CleaningDisgust, Is.EqualTo(5));
            Assert.That(NeedsRules.CleaningSeconds, Is.EqualTo(3));
        }
    }
}
