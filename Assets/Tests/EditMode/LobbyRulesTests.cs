using System;
using NatureCalls.Core;
using NUnit.Framework;

namespace NatureCalls.Tests
{
    public sealed class LobbyRulesTests
    {
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
