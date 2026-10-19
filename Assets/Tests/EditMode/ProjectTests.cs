using NUnit.Framework;
using UnityEditor;

namespace NatureCalls.Tests
{
    public sealed class ProjectTests
    {
        [Test]
        public void SerializationIsText()
        {
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
        }

        [Test]
        public void MetadataIsVisible()
        {
            Assert.That(VersionControlSettings.mode, Is.EqualTo("Visible Meta Files"));
        }
    }
}
