using System;
using NatureCalls.Core;
using NUnit.Framework;

namespace NatureCalls.Tests
{
    public sealed class LobbyRulesTests
    {
        [TestCase("127.0.0.1:8000", true, "127.0.0.1", 8000)]
        [TestCase("127.0.0.1", true, "127.0.0.1", 7777)]
        [TestCase("[::1]:8001", true, "::1", 8001)]
        [TestCase("49.233.132.244:0", false, "", 0)]
        [TestCase("49.233.132.244:65536", false, "", 0)]
        [TestCase("49.233.132.244:bad", false, "", 0)]
        [TestCase("invalid:8000", false, "", 0)]
        public void DirectAddressValidatesPort(string value, bool valid, string expected, int expectedPort)
        {
            Assert.That(Network.Session.TryAddress(value, 7777, out var address, out var port), Is.EqualTo(valid));
            if (!valid) return;
            Assert.That(address, Is.EqualTo(expected));
            Assert.That(port, Is.EqualTo(expectedPort));
        }

        [Test]
        public void RoomStartsWithTwoToFourPlayersWhenEveryGuestIsReady()
        {
            Assert.That(LobbyRules.CanStart(1, 0), Is.False, "A room needs at least two players.");
            Assert.That(LobbyRules.CanStart(2, 0), Is.False, "The guest is not ready.");
            Assert.That(LobbyRules.CanStart(2, 1), Is.True);
            Assert.That(LobbyRules.CanStart(4, 2), Is.False, "One guest is not ready.");
            Assert.That(LobbyRules.CanStart(4, 3), Is.True);
            Assert.That(LobbyRules.CanStart(5, 4), Is.False, "Rooms hold at most four players.");
            Assert.That(LobbyRules.HasCapacity(3), Is.True);
            Assert.That(LobbyRules.HasCapacity(4), Is.False);
        }

        [Test]
        public void LanRoomCodesSkipSelfAssignedAddresses()
        {
            foreach (var address in NatureCalls.Network.Session.LocalAddresses())
            {
                Assert.That(System.Net.IPAddress.TryParse(address, out var parsed), Is.True, address);
                Assert.That(parsed.AddressFamily, Is.EqualTo(System.Net.Sockets.AddressFamily.InterNetwork), address);
                Assert.That(address, Does.Not.StartWith("169.254."), "Self-assigned addresses are unreachable room codes.");
            }
        }

        [Test]
        public void RoundClockCountsDownToTheEnd()
        {
            Assert.That(RoundClock.Remaining(180, 30), Is.EqualTo(150));
            Assert.That(RoundClock.Remaining(180, 200), Is.Zero);
            Assert.That(RoundClock.IsOver(180, 179.9), Is.False);
            Assert.That(RoundClock.IsOver(180, 180), Is.True);
            Assert.That(RoundClock.Format(180), Is.EqualTo("03:00"));
            Assert.That(RoundClock.Format(59.2), Is.EqualTo("01:00"), "Rounds up so 00:00 shows only at the end.");
            Assert.That(RoundClock.Format(0), Is.EqualTo("00:00"));
        }

        [Test]
        public void NeedsFillOnceAMinuteAndEitherThresholdLosesTheRound()
        {
            Assert.That(NeedsRules.TickUrgency(0, 60, false), Is.EqualTo(NeedsRules.UrgencyMax), "Full once a minute from zero.");
            Assert.That(NeedsRules.TickUrgency(30, 1, false), Is.EqualTo(30 + NeedsRules.UrgencyMax / 60).Within(1e-4));
            Assert.That(NeedsRules.TickUrgency(30, 5, true), Is.EqualTo(30), "No rise while toileting.");
            Assert.That(NeedsRules.TickUrgency(99, 10, false), Is.EqualTo(NeedsRules.UrgencyMax));
            Assert.That(NeedsRules.InitialUrgency(.5f), Is.InRange(0, NeedsRules.InitialUrgencyMax));
            Assert.That(NeedsRules.DisgustAfterToileting(5, insideStall: true), Is.EqualTo(5));
            Assert.That(NeedsRules.DisgustAfterToileting(5, insideStall: false), Is.EqualTo(15));
            Assert.That(NeedsRules.Failed(99.9f, 99.9f), Is.False);
            Assert.That(NeedsRules.Failed(NeedsRules.UrgencyMax, 0), Is.True);
            Assert.That(NeedsRules.Failed(0, NeedsRules.DisgustThreshold), Is.True);
        }

        [Test]
        public void MapSelectionKeepsExplicitChoicesAndResolvesRandom()
        {
            Assert.That(MapSelection.Resolve(1, 3, _ => throw new InvalidOperationException()), Is.EqualTo(1));
            Assert.That(MapSelection.Resolve(MapSelection.Random, 3, count => count - 1), Is.EqualTo(2));
            Assert.That(MapSelection.IsValid(3, 3), Is.False);
            Assert.That(MapSelection.IsValid(MapSelection.Random, 0), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => MapSelection.Resolve(5, 3, _ => 0));
            Assert.Throws<InvalidOperationException>(() => MapSelection.Resolve(MapSelection.Random, 3, _ => 3));
        }
    }
}
