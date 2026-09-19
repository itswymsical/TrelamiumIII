using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

namespace TrelamiumIII.Core.Alchemy.Brewing;
/// <summary>
/// A potion's principle and its list of effects. Reference type, so <see cref="BrewGlobalItem"/> deep-clones it; equality is structural.
/// </summary>
public sealed class PotionBrew
{
    /// <summary>Principle resolved from the ingredients that produced this brew.</summary>
    public PrincipleType Principle { get; init; }

    /// <summary>The effects this potion applies on consume. Never empty for a branded item.</summary>
    public List<AlchemicalEffect> Effects { get; init; } = new();

    /// <summary>The headline effect drives the tooltip's potency stars and the buff-line rewrite.</summary>
    public AlchemicalEffect Primary => Effects[0];

    /// <summary>Deep copy (effects are value types, so a fresh list is a complete clone).</summary>
    public PotionBrew Clone() => new() { Principle = Principle, Effects = new List<AlchemicalEffect>(Effects) };

    public override bool Equals(object? obj) =>
        obj is PotionBrew o && Principle == o.Principle && Effects.SequenceEqual(o.Effects);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Principle);
        foreach (var e in Effects) hash.Add(e);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Effect ids that don't ride a vanilla buff; negative so they never collide. Consumed item-side at use, e.g. Restoration.
/// </summary>
public static class VirtualBuff
{
    /// <summary>Instant heal scaling: StrengthPercent scales the item's healLife/healMana.</summary>
    public const int Restoration = -1;
}

/// <summary>
/// Per-effect knobs outside potency/duration/strength; a small value type so effects copy, compare and serialize cheaply.
/// </summary>
public readonly record struct EffectParams(int MaxOreTier = 0)
{
    /// <summary>All-default params (no gate). Same as <c>default</c>.</summary>
    public static readonly EffectParams None = default;
}

/// <summary>
/// One brewed effect: rides <see cref="BaseBuff"/> for icon and timer, while its <see cref="IEffectBehavior"/> owns the behaviour.
/// </summary>
public readonly record struct AlchemicalEffect(
    int BaseBuff,          // vanilla BuffID this rides on (icon + timer)
    int Potency,           // 1-5, the "level"
    int DurationTicks,     // brewed duration
    int StrengthPercent,   // magnitude as a percent of the vanilla effect (100 = standard)
    EffectParams Params = default);

/// <summary>
/// Fluent brew authoring: boost, re-gate or combine effects onto a recipe's base effect. Only the resolved list is stored.
/// </summary>
public sealed class BrewBuilder
{
    private readonly PotionBrew _brew;
    private readonly int _baseDurationTicks;

    /// <summary>The composition's potency (1-5): the leading solute's consumed level, or the highest solute's when augmenting.</summary>
    public int Level { get; }

    public BrewBuilder(PotionBrew brew, int baseDurationTicks, int level = 1)
    {
        _brew = brew;
        _baseDurationTicks = baseDurationTicks;
        Level = Math.Clamp(level, 1, 5);
    }

    /// <summary>Boost (or cut) the primary effect's magnitude, as a percent of the vanilla effect.</summary>
    public BrewBuilder Amplify(int strengthPercent)
    {
        _brew.Effects[0] = _brew.Effects[0] with { StrengthPercent = strengthPercent };
        return this;
    }

    /// <summary>Rewrite the primary effect's params: e.g. <c>Detour(p =&gt; p with { MaxOreTier = 2 })</c>.</summary>
    public BrewBuilder Detour(Func<EffectParams, EffectParams> edit)
    {
        _brew.Effects[0] = _brew.Effects[0] with { Params = edit(_brew.Effects[0].Params) };
        return this;
    }

    /// <summary>
    /// Combines another effect onto the potion, replacing one on the same buff; <paramref name="durationTicks"/> defaults to
    /// the potion's buff time, or 60 s.
    /// </summary>
    public BrewBuilder Add(int baseBuff, int potency, int durationTicks = 0)
    {
        int baseDur = durationTicks > 0 ? durationTicks
            : _baseDurationTicks > 0 ? _baseDurationTicks
            : 60 * 60;

        AlchemicalEffect effect = BrewTuning.Create(baseBuff, baseDur, potency);
        int existing = _brew.Effects.FindIndex(e => e.BaseBuff == baseBuff);

        if (existing >= 0)
            _brew.Effects[existing] = effect;
        else
            _brew.Effects.Add(effect);

        return this;
    }
}

/// <summary>Maps potency to a concrete <see cref="AlchemicalEffect"/>, scaling duration off the potion's buff time. Placeholder curve.</summary>
public static class BrewTuning
{
    /// <summary>Strength percent gained per potency level.</summary>
    public const int STRENGTH_PER_LEVEL = 10;

    public static AlchemicalEffect Create(int baseBuff, int baseDurationTicks, int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        int duration = Math.Max(60, (int)(baseDurationTicks * DurationScale(potency)));
        var pars = EffectBehaviors.Get(baseBuff)?.DefaultParams(potency) ?? EffectParams.None;
        return new AlchemicalEffect(baseBuff, potency, duration, StrengthAt(potency), pars);
    }

    /// <summary>Restorative brew: no buff, so duration is 0 and StrengthPercent scales the instant heal (p1=80..p5=120).</summary>
    public static AlchemicalEffect CreateRestoration(int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        return new AlchemicalEffect(VirtualBuff.Restoration, potency, 0, StrengthAt(potency));
    }

    /// <summary>
    /// Moves an effect to <paramref name="potency"/> along the curve: strength changes by <see cref="STRENGTH_PER_LEVEL"/> per
    /// level, duration by the ratio of the duration scales, and params reset to the new potency's defaults.
    /// </summary>
    public static AlchemicalEffect Shift(AlchemicalEffect effect, int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        int levels = potency - effect.Potency;

        if (levels == 0)
            return effect;

        int duration = effect.DurationTicks <= 0 ? 0
            : Math.Max(60, (int)(effect.DurationTicks * DurationScale(potency) / DurationScale(effect.Potency)));

        return effect with
        {
            Potency = potency,
            StrengthPercent = Math.Max(1, effect.StrengthPercent + levels * STRENGTH_PER_LEVEL),
            DurationTicks = duration,
            Params = EffectBehaviors.Get(effect.BaseBuff)?.DefaultParams(potency) ?? EffectParams.None,
        };
    }

    /// <summary>Percent of the vanilla effect: p1=80 .. p5=120.</summary>
    private static int StrengthAt(int potency) => 70 + potency * STRENGTH_PER_LEVEL;

    /// <summary>Multiplier on the potion's stock duration: p1 ~ 0.78x .. p5 = 1.5x.</summary>
    private static float DurationScale(int potency) => 0.6f + potency * 0.18f;
}

/// <summary>Rewrites a buff's hover text for the brewed instance via <see cref="GlobalBuff.ModifyBuffText"/>, no IL edit needed.</summary>
public class BrewBuffText : GlobalBuff
{
    public override void ModifyBuffText(int type, ref string buffName, ref string buffTip, ref int rare)
    {
        var player = Main.LocalPlayer;
        if (player is null || !player.active) return;
        if (!player.GetModPlayer<AlchemyEffectPlayer>().TryGetActiveBrew(type, out var fx)) return;

        string? desc = EffectBehaviors.Describe(fx);
        if (desc != null) buffTip = desc;
        buffTip += $"\nPotency {fx.Potency}/5";
    }
}
