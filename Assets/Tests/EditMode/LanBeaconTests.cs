using NatureCalls.Core;
using NUnit.Framework;

namespace NatureCalls.Tests
{
    public sealed class LanBeaconTests
    {
        [Test]
        public void RoundTripsAndMarksFullRooms()
        {
            Assert.IsTrue(LanBeacon.TryParse(new LanBeacon("主机", 8001, 4, 4, false).ToJson(), out var beacon));
            Assert.AreEqual("主机", beacon.HostName);
            Assert.IsFalse(beacon.Joinable);
        }

        [Test]
        public void RejectsWrongProtocolAndInvalidShape()
        {
            Assert.IsFalse(LanBeacon.TryParse("{\"protocol\":2,\"port\":8001,\"players\":0,\"max\":4,\"selectingMap\":false}", out _));
            Assert.IsFalse(LanBeacon.TryParse("not json", out _));
        }
    }
}
