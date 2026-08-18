namespace TrelamiumIII.Common.Systems.Alchemy.Brewing;

/// <summary>
/// Potency-scaled Lucky buff. Vanilla keeps luck as an int <em>tier</em> in <see cref="Player.luckPotion"/>
/// (3/2/1 by remaining buff time, local player only), which <c>Player.RecalculateLuck</c> turns into
/// <c>tier * <see cref="PerTier"/></c>. <see cref="Player.luck"/> is a cache that recalculation assigns
/// wholesale, not a per-tick accumulator, so there is nothing for <see cref="Apply"/> to nudge: the
/// rescale lands in <see cref="AlchemyEffectPlayer.ModifyLuck"/>, the hook vanilla calls at the very end
/// of that recalculation.
/// <para>Vanilla encodes strength as duration -- the three Luck Potions are one buff at 5/10/15 minutes.
/// That ladder is left intact, so duration still picks the opening tier and the buff still decays;
/// StrengthPercent only scales the magnitude, letting a strong brew fall from a higher peak.</para>
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
