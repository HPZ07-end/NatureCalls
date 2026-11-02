using System;
using UnityEngine;

namespace NatureCalls.Player
{
    /// <summary>
    /// The outfits a player can choose at the DressingRoom mirror. Art replaces entries here, not in code.
    /// </summary>
    [CreateAssetMenu(menuName = "NatureCalls/Outfit Catalog", fileName = "OutfitCatalog")]
    public sealed class OutfitCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Outfit
        {
            public string Name;
            public Color Color = Color.white;
        }

        [SerializeField] private Outfit[] outfits = Array.Empty<Outfit>();

        public int Count => outfits.Length;
        public Outfit this[int index] => outfits[index];
        public bool Contains(int index) => index >= 0 && index < outfits.Length;
        public int Clamp(int index) => Contains(index) ? index : 0;

        public void SetOutfits(Outfit[] value) => outfits = value ?? Array.Empty<Outfit>();
    }
}
