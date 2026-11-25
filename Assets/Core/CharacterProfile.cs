namespace NatureCalls.Core
{
    /// <summary>Immutable runtime gameplay parameters for a character body.</summary>
    public readonly struct CharacterProfile
    {
        public readonly float MoveSpeedScale, UrgencyRateScale, AimSteadiness, ReachHeight, Control, EnduranceBonus;
        public CharacterProfile(float moveSpeedScale, float urgencyRateScale, float aimSteadiness, float reachHeight)
        { MoveSpeedScale = moveSpeedScale; UrgencyRateScale = urgencyRateScale; AimSteadiness = aimSteadiness; ReachHeight = reachHeight; Control = 1f; EnduranceBonus = 0f; }
        public CharacterProfile(float moveSpeedScale, float urgencyRateScale, float aimSteadiness, float reachHeight, float control)
        { MoveSpeedScale = moveSpeedScale; UrgencyRateScale = urgencyRateScale; AimSteadiness = aimSteadiness; ReachHeight = reachHeight; Control = control; EnduranceBonus = 0f; }
        public CharacterProfile(float moveSpeedScale, float urgencyRateScale, float aimSteadiness, float reachHeight, float control, float enduranceBonus)
        { MoveSpeedScale = moveSpeedScale; UrgencyRateScale = urgencyRateScale; AimSteadiness = aimSteadiness; ReachHeight = reachHeight; Control = control; EnduranceBonus = enduranceBonus; }
        public static CharacterProfile Default => new CharacterProfile(1f, 1f, 1f, 2f);
        public static CharacterProfile For(PlayerBodyType bodyType) => Default;
    }
}
