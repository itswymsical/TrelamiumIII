using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// Potency-scaled Crate potion. Vanilla's chance is an unscaled local (10, +15 with <see cref="Player.cratePotion"/>), so extra points
/// bank on <see cref="AlchemyEffectPlayer.CrateChanceBonus"/> and <see cref="AlchemyEffectPlayer.ModifyFishingAttempt"/> re-rolls.
/// </summary>
public sealed class CrateBehavior : IEffectBehavior
{
    private readonly int baseChance;      // vanilla chance with no potion up
    private readonly int vanillaBonus;    // percentage points the stock potion adds

    public CrateBehavior(int vanillaBaseChance, int vanillaPotionBonus)
    {
        baseChance = vanillaBaseChance;
        vanillaBonus = vanillaPotionBonus;
    }

    public void Apply(Player player, in AlchemicalEffect fx)
    {
        // Vanilla already applied its own +vanillaBonus via cratePotion this tick, so only the excess is
        // banked. A 100% brew banks 0 and ModifyFishingAttempt leaves vanilla's roll alone.
        float delta = vanillaBonus * (fx.StrengthPercent / 100f - 1f);
        player.GetModPlayer<AlchemyEffectPlayer>().CrateChanceBonus += (int)MathF.Round(delta);
    }

    public string Describe(in AlchemicalEffect fx) =>
        $"{ChanceAt(fx.StrengthPercent)}% chance to fish up crates";

    /// <summary>Resulting absolute crate chance (percent) for a brew of this strength.</summary>
    private int ChanceAt(int strengthPercent) =>
        Math.Clamp(baseChance + (int)MathF.Round(vanillaBonus * strengthPercent / 100f), 0, 100);
}
