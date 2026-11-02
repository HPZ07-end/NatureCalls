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
