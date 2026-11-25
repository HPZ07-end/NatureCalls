using NatureCalls.Core;
using NUnit.Framework;

namespace NatureCalls.Tests
{
    public sealed class IdleExitTests
    {
        [Test]
        public void EmptyRoomReachesLimit()
        {
            var timer = new IdleExitTimer(3);
            Assert.IsFalse(timer.Tick(2, 0));
            Assert.IsTrue(timer.Tick(1, 0));
        }

        [Test]
        public void AConnectedClientResetsTimer()
        {
            var timer = new IdleExitTimer(3);
            timer.Tick(2, 0);
            Assert.IsFalse(timer.Tick(0.1f, 1));
            Assert.IsFalse(timer.Tick(2.9f, 0));
        }

        [Test]
        public void DisabledTimerNeverExits()
        {
            var timer = new IdleExitTimer(-1);
            Assert.IsFalse(timer.Tick(100, 0));
        }
    }
}
