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
        private const string BodyTypeKey = "NatureCalls.PlayerBodyType";
        private const string LegacyGenderKey = "NatureCalls.PlayerGender";

        public static int OutfitIndex
        {
            get => PlayerPrefs.GetInt(OutfitKey, 0);
            set { PlayerPrefs.SetInt(OutfitKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static PlayerBodyType BodyType
        {
            get
            {
                var index = PlayerPrefs.GetInt(BodyTypeKey,
                    PlayerPrefs.GetInt(LegacyGenderKey) == 1 ? (int)PlayerBodyType.Tall : (int)PlayerBodyType.Balanced);
                return index is >= 0 and <= byte.MaxValue && OutfitSelection.IsValidBodyType((PlayerBodyType)index)
                    ? (PlayerBodyType)index : PlayerBodyType.Balanced;
            }
        }

        public static void SetAppearance(PlayerBodyType bodyType, int outfit)
        {
            if (!OutfitSelection.IsValidBodyType(bodyType)) return;
            PlayerPrefs.SetInt(BodyTypeKey, (int)bodyType);
            PlayerPrefs.SetInt(OutfitKey, Mathf.Max(0, outfit));
            PlayerPrefs.Save();
        }
    }
}
