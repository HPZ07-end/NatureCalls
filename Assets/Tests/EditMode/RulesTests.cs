using NUnit.Framework;
using NatureCalls.Core;
namespace NatureCalls.Tests { public sealed class RulesTests {
[Test] public void MovementCombineDefaultsToIdentity() { var x=MovementRules.Combine(new MovementModifier(1), new MovementModifier(1)); Assert.That(x.SpeedMultiplier, Is.EqualTo(1)); Assert.That(x.Acceleration, Is.Zero); }
[Test] public void MovementCombineMultipliesSpeedAndUsesSmallestAcceleration() { var x=MovementRules.Combine(new MovementModifier(.8f,8,true), new MovementModifier(.5f,3,false,true)); Assert.That(x.SpeedMultiplier, Is.EqualTo(.4f)); Assert.That(x.Acceleration, Is.EqualTo(3)); Assert.That(x.ForceCrawl); Assert.That(x.BlocksJump); }
[Test] public void RepairSequenceIsFiveSteps() { var x=RepairStep.Blocked; x=RepairRules.Next(x,1); x=RepairRules.Next(x,2); x=RepairRules.Next(x,3); x=RepairRules.Next(x,2); x=RepairRules.Next(x,4); Assert.That(x, Is.EqualTo(RepairStep.Confirmed)); }
[Test] public void VendingRequiresCoinAndCapacity() { Assert.That(RepairRules.CanVend(1,0,1)); Assert.That(RepairRules.CanVend(1,1,1), Is.False); Assert.That(RepairRules.CanVend(0,0,1), Is.False); }
} }
