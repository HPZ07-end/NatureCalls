using System;

namespace NatureCalls.Core
{
    public enum PlayerBodyType : byte { Balanced, Tall, Swift }

    public sealed class OutfitSelection
    {
        public OutfitSelection(int outfitCount, int initialIndex = 0, PlayerBodyType initialBodyType = PlayerBodyType.Balanced)
        {
            if (outfitCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(outfitCount));
            if (initialIndex < 0 || initialIndex >= outfitCount)
                throw new ArgumentOutOfRangeException(nameof(initialIndex));

            OutfitCount = outfitCount;
            ConfirmedIndex = initialIndex;
            PreviewIndex = initialIndex;
            ConfirmedBodyType = PreviewBodyType = IsValidBodyType(initialBodyType) ? initialBodyType : PlayerBodyType.Balanced;
        }

        public int OutfitCount { get; }
        public int ConfirmedIndex { get; private set; }
        public int PreviewIndex { get; private set; }
        public PlayerBodyType ConfirmedBodyType { get; private set; }
        public PlayerBodyType PreviewBodyType { get; private set; }
        public bool IsSelecting { get; private set; }

        public static bool IsValidBodyType(PlayerBodyType value) =>
            value is PlayerBodyType.Balanced or PlayerBodyType.Tall or PlayerBodyType.Swift;

        public void Begin()
        {
            PreviewIndex = ConfirmedIndex;
            PreviewBodyType = ConfirmedBodyType;
            IsSelecting = true;
        }

        public void ChooseBodyType(PlayerBodyType value)
        {
            if (IsSelecting && IsValidBodyType(value)) PreviewBodyType = value;
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
            ConfirmedBodyType = PreviewBodyType;
            IsSelecting = false;
        }

        public void Cancel()
        {
            PreviewIndex = ConfirmedIndex;
            PreviewBodyType = ConfirmedBodyType;
            IsSelecting = false;
        }
    }
}
