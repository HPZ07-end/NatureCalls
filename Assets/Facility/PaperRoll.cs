using NatureCalls.Core;
namespace NatureCalls.Facility
{
    public sealed class PaperRoll : Item
    {
        private int remainingUses = NeedsRules.PaperUses;
        public override int RemainingUses => remainingUses;
        public bool Use()
        {
            return Use(1);
        }
        public bool Use(int units) { if (remainingUses <= 0 || units <= 0) return false; ApplyUses(System.Math.Max(0, remainingUses - units)); return true; }
        public void ApplyUses(int uses)
        {
            remainingUses = uses;
            if (uses == 0) { transform.SetParent(null, true); gameObject.SetActive(false); }
        }
    }
}
