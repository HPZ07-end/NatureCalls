using NatureCalls.Core;
using UnityEngine;

namespace NatureCalls.Player
{
    /// <summary>
    /// The local player's confirmed appearance across scenes and sessions.
    /// </summary>
    public static class LocalProfile
    {
        private const string OutfitKey = "NatureCalls.OutfitIndex";
        private const string GenderKey = "NatureCalls.PlayerGender";

        public static int OutfitIndex
        {
            get => PlayerPrefs.GetInt(OutfitKey, 0);
            set { PlayerPrefs.SetInt(OutfitKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static PlayerGender Gender => PlayerPrefs.GetInt(GenderKey) == (int)PlayerGender.Female
            ? PlayerGender.Female : PlayerGender.Male;

        public static void SetAppearance(PlayerGender gender, int outfit)
        {
            if (!OutfitSelection.IsValidGender(gender)) return;
            PlayerPrefs.SetInt(GenderKey, (int)gender);
            PlayerPrefs.SetInt(OutfitKey, Mathf.Max(0, outfit));
            PlayerPrefs.Save();
        }
    }
}
