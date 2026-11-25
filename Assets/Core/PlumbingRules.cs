namespace NatureCalls.Core
{
    /// <summary>Pure rules for pipe accumulation and the paper used after defecation.</summary>
    public static class PlumbingRules
    {
        // 暂定，待用户确认。
        public const int StoolContribution = 25;
        // 暂定，待用户确认。
        public const int PaperUnitContribution = 5;
        // 暂定，待用户确认。
        public const int BlockedThreshold = 100;
        // 暂定，待用户确认。
        public const int BackflowDenominator = 45;

        public static int AddStool(int value) => Add(value, StoolContribution);
        public static int AddPaper(int value, int units) => Add(value, PaperUnitContribution * System.Math.Max(0, units));
        public static int Add(int value, int amount) => System.Math.Max(0, value) + System.Math.Max(0, amount);
        public static bool IsBlocked(int value) => value >= BlockedThreshold;
        public static int Clear() => 0;
        public static bool ShouldBackflow(int accumulation, int randomRoll) => IsBlocked(accumulation) && randomRoll == 0;
    }
}
