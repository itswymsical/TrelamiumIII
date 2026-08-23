namespace TrelamiumIII.Core.Alchemy.Brewing
{
    public sealed class BuilderPotionBehavior : IEffectBehavior
    {
        private readonly float buildValue;
        private readonly int tileRange;

        public BuilderPotionBehavior(float vanillaSpeed, int vanillaRangeBoost)
        {
            buildValue = vanillaSpeed;
            tileRange = vanillaRangeBoost;
        }

        public void Apply(Player player, in AlchemicalEffect fx)
        {
            float delta = buildValue * (fx.StrengthPercent / 100f - 1f);
            int range = tileRange * fx.Potency;

            player.tileSpeed += delta;
            player.wallSpeed += delta;
            player.blockRange += range;
        }

        public string Describe(in AlchemicalEffect fx)
        {
            float value = buildValue * fx.StrengthPercent / 100f;
            int r = tileRange * fx.Potency;
            return 
                $"increases placement speed by {(int)MathF.Round(value * 100)}%" + $"\nand placement range by {MathF.Round(r)}";
        }
    }
}
