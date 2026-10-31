using UnityEngine;

namespace NatureCalls.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        [SerializeField] private Transform body;

        public Transform Body => body;
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

        public void ApplyOutfit(int index, Color color)
        {
            OutfitIndex = index;
            ApplyColor(color);
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
