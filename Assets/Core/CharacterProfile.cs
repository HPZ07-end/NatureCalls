namespace NatureCalls.Core
{
    /// <summary>Immutable runtime gameplay parameters for a character body.</summary>
    public readonly struct CharacterProfile
    {
        public readonly float MoveSpeedScale, UrgencyRateScale, AimSteadiness, ReachHeight, Control;
        public CharacterProfile(float moveSpeedScale, float urgencyRateScale, float aimSteadiness, float reachHeight)
        { MoveSpeedScale = moveSpeedScale; UrgencyRateScale = urgencyRateScale; AimSteadiness = aimSteadiness; ReachHeight = reachHeight; Control = 1f; }
        public CharacterProfile(float moveSpeedScale, float urgencyRateScale, float aimSteadiness, float reachHeight, float control)
        { MoveSpeedScale = moveSpeedScale; UrgencyRateScale = urgencyRateScale; AimSteadiness = aimSteadiness; ReachHeight = reachHeight; Control = control; }
        public static CharacterProfile Default => new CharacterProfile(1f, 1f, 1f, 2f);
        public static CharacterProfile For(PlayerBodyType bodyType) => Default;
    }
}
