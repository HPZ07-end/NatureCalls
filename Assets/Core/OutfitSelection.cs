using System;

namespace NatureCalls.Core
{
    public sealed class OutfitSelection
    {
        public OutfitSelection(int outfitCount, int initialIndex = 0)
        {
            if (outfitCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(outfitCount));
            if (initialIndex < 0 || initialIndex >= outfitCount)
                throw new ArgumentOutOfRangeException(nameof(initialIndex));

            OutfitCount = outfitCount;
            ConfirmedIndex = initialIndex;
            PreviewIndex = initialIndex;
        }

        public int OutfitCount { get; }
        public int ConfirmedIndex { get; private set; }
        public int PreviewIndex { get; private set; }
        public bool IsSelecting { get; private set; }

        public void Begin()
        {
            PreviewIndex = ConfirmedIndex;
            IsSelecting = true;
        }

        public int Move(int offset)
        {
            if (!IsSelecting)
                throw new InvalidOperationException("Begin a selection before moving the preview.");

            PreviewIndex = (PreviewIndex + offset) % OutfitCount;
            if (PreviewIndex < 0)
                PreviewIndex += OutfitCount;
            return PreviewIndex;
        }

        public void Confirm()
        {
            if (!IsSelecting)
                return;
            ConfirmedIndex = PreviewIndex;
            IsSelecting = false;
        }

        public void Cancel()
        {
            PreviewIndex = ConfirmedIndex;
            IsSelecting = false;
        }
    }
}
