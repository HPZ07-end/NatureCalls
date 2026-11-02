using UnityEngine;

namespace NatureCalls.Player
{
    /// <summary>
    /// The local player's own choices that outlive a scene: currently the confirmed outfit.
    /// </summary>
    public static class LocalProfile
    {
        private const string OutfitKey = "NatureCalls.OutfitIndex";

        public static int OutfitIndex
        {
            get => PlayerPrefs.GetInt(OutfitKey, 0);
            set { PlayerPrefs.SetInt(OutfitKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }
    }
}
