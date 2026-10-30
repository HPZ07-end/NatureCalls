using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Player
{
    public sealed class PlayerOutfitController : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private string[] outfitNames = { "Default Blue", "Cleanup Green", "Repair Orange", "Party Pink" };
        [SerializeField] private Color[] outfitColors =
        {
            new Color(0.18f, 0.48f, 0.95f),
            new Color(0.16f, 0.72f, 0.42f),
            new Color(1f, 0.45f, 0.1f),
            new Color(0.95f, 0.24f, 0.58f)
        };

        private OutfitSelection selection;
        private Material runtimeMaterial;

        public int PreviewIndex => selection.PreviewIndex;
        public string PreviewName => outfitNames[selection.PreviewIndex];
        public bool IsSelecting => selection.IsSelecting;

        private void Awake()
        {
            selection = new OutfitSelection(Mathf.Min(outfitNames.Length, outfitColors.Length));
            runtimeMaterial = bodyRenderer.material;
            Apply(0);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
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

        public void ConfirmSelection()
        {
            selection.Confirm();
            Apply(selection.ConfirmedIndex);
        }

        public void CancelSelection()
        {
            selection.Cancel();
            Apply(selection.ConfirmedIndex);
        }

        private void Apply(int index)
        {
            runtimeMaterial.color = outfitColors[index];
        }
    }
}
