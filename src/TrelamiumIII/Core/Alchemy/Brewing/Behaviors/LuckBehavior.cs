namespace TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

/// <summary>
/// Potency-scaled Lucky buff. Vanilla stores a tier (<see cref="Player.luckPotion"/>) that RecalculateLuck turns into luck, so the rescale
/// happens in <see cref="AlchemyEffectPlayer.ModifyLuck"/>. Duration still picks the tier and decays it; strength scales magnitude.
/// </summary>
public sealed class LuckBehavior : IEffectBehavior
{
    /// <summary>What one <see cref="Player.luckPotion"/> tier is worth in <c>Player.RecalculateLuck</c>.</summary>
    public const float PerTier = 0.1f;

    private const int Tier3Ticks = 36000;   // more than 10 minutes left
    private const int Tier2Ticks = 18000;   // more than 5 minutes left

    /// <summary>Vanilla's ladder: the <see cref="Player.luckPotion"/> tier this much time remaining gives.</summary>
    public static int TierFor(int ticksRemaining) =>
        ticksRemaining > Tier3Ticks ? 3 : ticksRemaining > Tier2Ticks ? 2 : 1;

    public void Apply(Player player, in AlchemicalEffect fx) { }

    public string Describe(in AlchemicalEffect fx)
    {
        // Tooltips are read before drinking, so quote the opening tier and say that it falls off. Luck is
        // an opaque scalar (vanilla tops out at 0.3), so it reads as a raw value rather than a percent.
        float peak = TierFor(fx.DurationTicks) * PerTier * (fx.StrengthPercent / 100f);
        return $"+{peak:0.##} luck, decaying as the buff runs out";
    }
}
