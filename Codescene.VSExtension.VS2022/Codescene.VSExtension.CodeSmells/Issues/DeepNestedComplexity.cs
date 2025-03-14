namespace Codescene.VSExtension.CodeSmells.Issues
{
    class DeepNestedComplexity
    {
        // Pretpostavke: boolean svojstva/polja
        public bool IsDead { get; set; }
        public bool IsSeparated { get; set; }
        public bool IsRetired { get; set; }

        // Metode za izračun
        private decimal DeadAmount() => 0m;
        private decimal SeparatedAmount() => 100m;
        private decimal RetiredAmount() => 200m;
        private decimal NormalPayAmount() => 300m;

        public decimal GetPayAmount()
        {
            decimal result;
            if (IsDead)
            {
                result = DeadAmount();
            }
            else
            {
                if (IsSeparated)
                {
                    result = SeparatedAmount();
                }
                else
                {
                    if (IsRetired)
                    {
                        result = RetiredAmount();
                    }
                    else
                    {
                        result = NormalPayAmount();
                    }
                }
            }
            return result;
        }
    }

}
