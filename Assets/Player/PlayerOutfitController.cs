using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Player
{
    /// <summary>
    /// DressingRoom outfit flow: preview, confirm or cancel. Confirmed outfits are saved to <see cref="LocalProfile"/>.
    /// </summary>
    public sealed class PlayerOutfitController : MonoBehaviour
    {
        [SerializeField] private PlayerAppearance appearance;

        private OutfitSelection selection;

        public OutfitCatalog Catalog => appearance.Catalog;
        public int PreviewIndex => selection.PreviewIndex;
        public int ConfirmedIndex => selection.ConfirmedIndex;
        public string PreviewName => Catalog[selection.PreviewIndex].Name;
        public bool IsSelecting => selection.IsSelecting;

        private void Awake()
        {
            if (appearance == null)
                appearance = GetComponent<PlayerAppearance>();
            selection = new OutfitSelection(Catalog.Count, Catalog.Clamp(LocalProfile.OutfitIndex));
            Apply(selection.ConfirmedIndex);
        }

        public void BeginSelection()
        {
            selection.Begin();
            Apply(selection.PreviewIndex);
        }

        public void PreviewNext(int direction)
        {
            Apply(selection.Move(direction));
        }

        public void Preview(int index)
        {
            Apply(selection.Move(index - selection.PreviewIndex));
        }

        public void ConfirmSelection()
        {
            selection.Confirm();
            LocalProfile.OutfitIndex = selection.ConfirmedIndex;
            Apply(selection.ConfirmedIndex);
        }

        public void CancelSelection()
        {
            selection.Cancel();
            Apply(selection.ConfirmedIndex);
        }

        private void Apply(int index)
        {
            appearance.ApplyOutfit(index);
        }
    }
}
