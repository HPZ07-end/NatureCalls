using UnityEngine;

namespace NatureCalls.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private OutfitCatalog catalog;

        public Transform Body => body;
        public OutfitCatalog Catalog => catalog;
        public int OutfitIndex { get; private set; } = -1;

        private void Awake()
        {
            if (body == null)
                body = transform.Find("Body");
        }

        public void ApplyIdentityColor(Color color)
        {
            ApplyColor(color);
        }

        public void ApplyOutfit(int index)
        {
            if (catalog == null || !catalog.Contains(index))
            {
                Debug.LogWarning($"OUTFIT_INVALID|player={name}|index={index}|catalog={catalog != null}");
                return;
            }
            OutfitIndex = index;
            ApplyColor(catalog[index].Color);
        }

        private void ApplyColor(Color color)
        {
            if (body == null)
                return;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
                renderer.SetPropertyBlock(block);
        }
    }
}
