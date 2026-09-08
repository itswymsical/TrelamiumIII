using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.Alchemy.Brewing;
/// <summary>
/// A potion's brew: its guiding <see cref="PrincipleType"/> plus the list of effects it applies. v1
/// recipes stamp a single effect, but the list shape is what lets recipes "intermix" several (the
/// augmentation "add/combine" case). Reference type, so <see cref="BrewGlobalItem"/> must deep-Clone
/// it; equality is structural so only identical brews stack.
/// </summary>
public sealed class PotionBrew
{
    public PrincipleType Domain { get; init; }

    /// <summary>The effects this potion applies on consume. Never empty for a branded item.</summary>
    public List<AlchemicalEffect> Effects { get; init; } = new();

    /// <summary>The headline effect drives the tooltip's potency stars and the buff-line rewrite.</summary>
    public AlchemicalEffect Primary => Effects[0];

    /// <summary>Deep copy (effects are value types, so a fresh list is a complete clone).</summary>
    public PotionBrew Clone() => new() { Domain = Domain, Effects = new List<AlchemicalEffect>(Effects) };

    public override bool Equals(object? obj) =>
        obj is PotionBrew o && Domain == o.Domain && Effects.SequenceEqual(o.Effects);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Domain);
        foreach (var e in Effects) hash.Add(e);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Ids for brewed effects that do NOT ride a vanilla buff. Negative so they can never collide with
/// real <see cref="BuffID"/> values. They live in the same effect list
/// but are consumed item-side at use time instead of ticking on <see cref="AlchemyEffectPlayer"/>:
/// e.g. Restoration scales the item's heal in <see cref="BrewGlobalItem.GetHealLife"/>.
/// </summary>
public static class VirtualBuff
{
    /// <summary>Instant heal scaling: StrengthPercent scales the item's healLife/healMana.</summary>
    public const int Restoration = -1;
}

/// <summary>
/// Per-effect knobs that don't fit the common (potency/duration/strength) shape. 
/// Kept a small value type so effects copy by value, compare structurally (for
/// stacking) and serialize cheaply.
/// </summary>
public readonly record struct EffectParams(int MaxOreTier = 0)
{
    /// <summary>All-default params (no gate). Same as <c>default</c>.</summary>
    public static readonly EffectParams None = default;
}

/// <summary>
/// One brewed effect instance on a potion. A value type, so a <see cref="PotionBrew"/>'s list of these
/// copies/compares by value. Rides a vanilla <see cref="BaseBuff"/> for its icon + timer; the actual
/// magnitude/behaviour is owned by the matching <see cref="IEffectBehavior"/>.
/// </summary>
public readonly record struct AlchemicalEffect(
    int BaseBuff,          // vanilla BuffID this rides on (icon + timer)
    int Potency,           // 1-5, the "level"
    int DurationTicks,     // brewed duration
    int StrengthPercent,   // magnitude as a percent of the vanilla effect (100 = standard)
    EffectParams Params = default);

/// <summary>
/// Fluent authoring for a recipe's brew. A recipe stamps a base effect from
/// its potency, then optionally runs one of these to boost, re-gate, or combine further effects. The
/// resolved effect list is what gets stored on the item (augmentations aren't persisted as layers).
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

    /// <summary>
    /// Combine another brewed effect onto the potion ("intermix"). <paramref name="durationTicks"/>
    /// is the base time fed to the potency curve; omitted, it falls back to the potion's own buff
    /// time, or 60s for items with none (heal potions).
    /// </summary>
    public BrewBuilder Add(int baseBuff, int potency, int durationTicks = 0)
    {
        int baseDur = durationTicks > 0 ? durationTicks
            : _baseDurationTicks > 0 ? _baseDurationTicks
            : 60 * 60;
        _brew.Effects.Add(BrewTuning.Create(baseBuff, baseDur, potency));
        return this;
    }
}

/// <summary>
/// Maps a recipe's potency tier to a concrete brewed <see cref="AlchemicalEffect"/>.
/// Duration scales off the potion's own vanilla buff time; per-effect params
/// (e.g. Spelunker's ore-tier gate) come from the buff's <see cref="IEffectBehavior"/>. Placeholder
/// curve; tune freely.
/// </summary>
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

    /// <summary>
    /// Brewed effect for a restorative (healLife/healMana) potion: no buff to ride, so duration is 0
    /// and StrengthPercent scales the instant heal instead (same p1=80..p5=120 curve).
    /// </summary>
    public static AlchemicalEffect CreateRestoration(int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        return new AlchemicalEffect(VirtualBuff.Restoration, potency, 0, 70 + potency * 10);
    }
}

/// <summary>
/// Rewrites a buff's hover description to reflect the brewed instance.
/// <see cref="GlobalBuff.ModifyBuffText"/> wraps the
/// vanilla buff-tooltip code, so it works for vanilla buffs without an IL edit.
/// </summary>
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
