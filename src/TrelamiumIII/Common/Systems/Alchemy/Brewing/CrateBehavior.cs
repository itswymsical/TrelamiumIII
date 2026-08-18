namespace TrelamiumIII.Common.Systems.Alchemy.Brewing;

/// <summary>
/// Potency-scaled Crate potion. Vanilla's crate chance is a flat percentage local in
/// <c>Projectile.FishingCheck_RollDropLevels</c> (<c>10</c>, <c>+15</c> while <see cref="Player.cratePotion"/>),
/// rolled as <c>Main.rand.Next(100) &lt; chance</c>. Unlike every other rarity tier it is NOT scaled by
/// fishing power, and it is a local, so there is no field to nudge: the behaviour banks its extra
/// percentage points on <see cref="AlchemyEffectPlayer.CrateChanceBonus"/> and the re-roll happens in
/// <see cref="AlchemyEffectPlayer.ModifyFishingAttempt"/>, which vanilla calls immediately after its own roll.
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
