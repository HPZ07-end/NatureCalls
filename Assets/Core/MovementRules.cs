namespace NatureCalls.Core
{
    public readonly struct MovementModifier
    {
        public readonly float SpeedMultiplier;
        public readonly float Acceleration;
        public readonly bool ForceCrawl;
        public readonly bool BlocksJump;
        public MovementModifier(float speedMultiplier = 1, float acceleration = 0, bool forceCrawl = false, bool blocksJump = false)
        { SpeedMultiplier = speedMultiplier; Acceleration = acceleration; ForceCrawl = forceCrawl; BlocksJump = blocksJump; }
    }
    public static class MovementRules
    {
        public static MovementModifier Combine(MovementModifier current, MovementModifier next)
        {
            var acceleration = current.Acceleration == 0 ? next.Acceleration : next.Acceleration == 0 ? current.Acceleration : System.Math.Min(current.Acceleration, next.Acceleration);
            return new MovementModifier(current.SpeedMultiplier * next.SpeedMultiplier, acceleration, current.ForceCrawl || next.ForceCrawl, current.BlocksJump || next.BlocksJump);
        }
    }
}
