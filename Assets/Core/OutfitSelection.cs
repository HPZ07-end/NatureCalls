using System;

namespace NatureCalls.Core
{
    public enum PlayerGender : byte { Male, Female }

    public sealed class OutfitSelection
    {
        public OutfitSelection(int outfitCount, int initialIndex = 0, PlayerGender initialGender = PlayerGender.Male)
        {
            if (outfitCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(outfitCount));
            if (initialIndex < 0 || initialIndex >= outfitCount)
                throw new ArgumentOutOfRangeException(nameof(initialIndex));

            OutfitCount = outfitCount;
            ConfirmedIndex = initialIndex;
            PreviewIndex = initialIndex;
            ConfirmedGender = PreviewGender = IsValidGender(initialGender) ? initialGender : PlayerGender.Male;
        }

        public int OutfitCount { get; }
        public int ConfirmedIndex { get; private set; }
        public int PreviewIndex { get; private set; }
        public PlayerGender ConfirmedGender { get; private set; }
        public PlayerGender PreviewGender { get; private set; }
        public bool IsSelecting { get; private set; }

        public static bool IsValidGender(PlayerGender value) => value is PlayerGender.Male or PlayerGender.Female;

        public void Begin()
        {
            PreviewIndex = ConfirmedIndex;
            PreviewGender = ConfirmedGender;
            IsSelecting = true;
        }

        public void ChooseGender(PlayerGender value)
        {
            if (IsSelecting && IsValidGender(value)) PreviewGender = value;
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
            ConfirmedGender = PreviewGender;
            IsSelecting = false;
        }

        public void Cancel()
        {
            PreviewIndex = ConfirmedIndex;
            PreviewGender = ConfirmedGender;
            IsSelecting = false;
        }
    }
}
