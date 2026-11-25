namespace NatureCalls.Core
{
    /// <summary>Immutable runtime gameplay parameters for a character body.</summary>
    public readonly struct CharacterProfile
    {
        public readonly float MoveSpeedScale, UrgencyRateScale, AimSteadiness, ReachHeight;
        public CharacterProfile(float moveSpeedScale, float urgencyRateScale, float aimSteadiness, float reachHeight)
        { MoveSpeedScale = moveSpeedScale; UrgencyRateScale = urgencyRateScale; AimSteadiness = aimSteadiness; ReachHeight = reachHeight; }
        public static CharacterProfile Default => new CharacterProfile(1f, 1f, 1f, 2f);
        public static CharacterProfile For(PlayerBodyType bodyType) => Default;
    }
}
