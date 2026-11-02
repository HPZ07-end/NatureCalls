using NatureCalls.Core;
using NUnit.Framework;

namespace NatureCalls.Tests
{
    public sealed class OutfitSelectionTests
    {
        [Test]
        public void CancelRestoresConfirmedOutfit()
        {
            var selection = new OutfitSelection(4, 1);
            selection.Begin();
            selection.Move(1);
            selection.Cancel();

            Assert.That(selection.PreviewIndex, Is.EqualTo(1));
            Assert.That(selection.ConfirmedIndex, Is.EqualTo(1));
        }

        [Test]
        public void MoveWrapsInBothDirections()
        {
            var selection = new OutfitSelection(4);
            selection.Begin();
            Assert.That(selection.Move(-1), Is.EqualTo(3));
            Assert.That(selection.Move(1), Is.EqualTo(0));
        }
    }
}
