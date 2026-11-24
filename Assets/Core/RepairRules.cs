namespace NatureCalls.Core
{
    public enum RepairStep : byte { Blocked, Inspected, Isolated, Cleared, Running, Confirmed }
    public static class RepairRules
    {
        public static RepairStep Next(RepairStep current, int action) => (action, current) switch
        {
            (1, RepairStep.Blocked) => RepairStep.Inspected,
            (2, RepairStep.Inspected) => RepairStep.Isolated,
            (3, RepairStep.Isolated) => RepairStep.Cleared,
            (2, RepairStep.Cleared) => RepairStep.Running,
            (4, RepairStep.Running) => RepairStep.Confirmed,
            _ => current
        };
        public static bool CanVend(int wallet, int carried, int capacity) => wallet > 0 && carried < capacity;
        public static bool Vend(int wallet, int carried, int capacity) => CanVend(wallet, carried, capacity);
    }
}
