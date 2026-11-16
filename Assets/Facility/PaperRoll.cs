using NatureCalls.Core;
namespace NatureCalls.Facility
{
    public sealed class PaperRoll : Item
    {
        private int remainingUses = NeedsRules.PaperUses;
        public override int RemainingUses => remainingUses;
        public bool Use()
        {
            if (remainingUses <= 0) return false;
            ApplyUses(NeedsRules.UsePaper(remainingUses));
            return true;
        }
        public void ApplyUses(int uses)
        {
            remainingUses = uses;
            if (uses == 0) { transform.SetParent(null, true); gameObject.SetActive(false); }
        }
    }
}
