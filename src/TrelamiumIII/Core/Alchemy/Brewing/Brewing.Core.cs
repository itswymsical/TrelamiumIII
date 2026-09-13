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

    public BrewBuilder(PotionBrew brew, int baseDurationTicks)
    {
        _brew = brew;
        _baseDurationTicks = baseDurationTicks;
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

    /// <summary>Combines another effect onto the potion; <paramref name="durationTicks"/> defaults to the potion's buff time, or 60 s.</summary>
    public BrewBuilder Add(int baseBuff, int potency, int durationTicks = 0)
    {
        int baseDur = durationTicks > 0 ? durationTicks
            : _baseDurationTicks > 0 ? _baseDurationTicks
            : 60 * 60;
        _brew.Effects.Add(BrewTuning.Create(baseBuff, baseDur, potency));
        return this;
    }
}

/// <summary>Maps potency to a concrete <see cref="AlchemicalEffect"/>, scaling duration off the potion's buff time. Placeholder curve.</summary>
public static class BrewTuning
{
    public static AlchemicalEffect Create(int baseBuff, int baseDurationTicks, int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        // Strength is a percent of the vanilla effect. p1=80 .. p5=120.
        int strengthPercent = 70 + potency * 10;
        float durationMul = 0.6f + potency * 0.18f;     // p1 ~ 0.78x, p5 = 1.5x of the potion's stock time
        int duration = Math.Max(60, (int)(baseDurationTicks * durationMul));
        var pars = EffectBehaviors.Get(baseBuff)?.DefaultParams(potency) ?? EffectParams.None;
        return new AlchemicalEffect(baseBuff, potency, duration, strengthPercent, pars);
    }

    /// <summary>Restorative brew: no buff, so duration is 0 and StrengthPercent scales the instant heal (p1=80..p5=120).</summary>
    public static AlchemicalEffect CreateRestoration(int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        return new AlchemicalEffect(VirtualBuff.Restoration, potency, 0, 70 + potency * 10);
    }
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
