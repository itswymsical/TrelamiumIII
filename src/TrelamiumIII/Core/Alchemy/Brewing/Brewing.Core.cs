using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

namespace TrelamiumIII.Core.Alchemy.Brewing;
/// <summary>
/// A potion's principle, effects, timed modifiers and augmentation history. Reference type, so <see cref="BrewGlobalItem"/>
/// deep-clones it; equality is structural.
/// </summary>
public sealed class PotionBrew
{
    /// <summary>Principle resolved from the ingredients that produced this brew.</summary>
    public PrincipleType Principle { get; init; }

    /// <summary>The effects this potion applies on consume. Never empty for a branded item.</summary>
    public List<AlchemicalEffect> Effects { get; init; } = new();

    /// <summary>Stats and custom effects started on drink, each on its own timer under <see cref="SaturationBuff"/>.</summary>
    public List<TimedModifier> Modifiers { get; init; } = new();

    /// <summary>Flat life restored on drink, on top of the item's own (scaled) heal.</summary>
    public int HealLife { get; set; }

    /// <summary>Flat mana restored on drink, on top of the item's own (scaled) restore.</summary>
    public int HealMana { get; set; }

    /// <summary><see cref="Augmentation.Key"/> of each augmentation applied, in order. Potency steps are not recorded.</summary>
    public List<string> Augments { get; init; } = new();

    /// <summary>The headline effect drives the tooltip's potency stars and the buff-line rewrite.</summary>
    public AlchemicalEffect Primary => Effects[0];

    /// <summary>Number of augmentations applied; past <see cref="BrewDilution.FREE_STAGES"/> each one dilutes the brew.</summary>
    public int Stage => Augments.Count;

    public int TimesAugmentedBy(string key)
    {
        int count = 0;
        foreach (string applied in Augments)
            if (applied == key) count++;
        return count;
    }

    /// <summary>Deep copy (effects and modifiers are value types, so fresh lists are a complete clone).</summary>
    public PotionBrew Clone() => new()
    {
        Principle = Principle,
        Effects = new List<AlchemicalEffect>(Effects),
        Modifiers = new List<TimedModifier>(Modifiers),
        HealLife = HealLife,
        HealMana = HealMana,
        Augments = new List<string>(Augments),
    };

    public override bool Equals(object? obj) =>
        obj is PotionBrew o && Principle == o.Principle && HealLife == o.HealLife && HealMana == o.HealMana
        && Effects.SequenceEqual(o.Effects) && Modifiers.SequenceEqual(o.Modifiers) && Augments.SequenceEqual(o.Augments);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Principle);
        hash.Add(HealLife);
        hash.Add(HealMana);
        foreach (var e in Effects) hash.Add(e);
        foreach (var m in Modifiers) hash.Add(m);
        foreach (string a in Augments) hash.Add(a);
        return hash.ToHashCode();
    }
}

/// <summary>A stat or custom effect a brew starts on drink: <see cref="IBrewModifier"/> <paramref name="Key"/> at <paramref name="Value"/>.</summary>
public readonly record struct TimedModifier(string Key, float Value, int DurationTicks);

/// <summary>Diminishing returns on augmentation: the first stages are free, every later one costs the primary effect potency.</summary>
public static class BrewDilution
{
    /// <summary>Augmentations a brew takes before dilution starts.</summary>
    public const int FREE_STAGES = 2;

    /// <summary>Potency each augmentation past <see cref="FREE_STAGES"/> takes from the primary effect.</summary>
    public const int POTENCY_PER_STAGE = 1;
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
/// Edits a brew in place: boost, shift, re-gate or combine effects, and add timed modifiers or flat restores. Augmentation
/// steps (<see cref="AugmentStep"/>) are recorded as calls on this.
/// </summary>
public sealed class BrewBuilder
{
    private readonly PotionBrew _brew;
    private readonly int _baseDurationTicks;

    /// <summary>The composition's potency (1-5): the leading solute's consumed level, or the highest solute's when augmenting.</summary>
    public int Level { get; }

    /// <summary>The primary effect's current potency.</summary>
    public int CurrentPotency => _brew.Primary.Potency;

    public PotionBrew Brew => _brew;

    public BrewBuilder(PotionBrew brew, int baseDurationTicks, int level = 1)
    {
        _brew = brew;
        _baseDurationTicks = baseDurationTicks;
        Level = Math.Clamp(level, 1, 5);
    }

    /// <summary>Adds <paramref name="strengthPercent"/> points to the primary effect's magnitude (negative cuts it).</summary>
    public BrewBuilder Amplify(int strengthPercent) => Amplify(_brew.Primary.BaseBuff, strengthPercent);

    /// <summary>Adds <paramref name="strengthPercent"/> points to the effect riding <paramref name="baseBuff"/>, if the brew has one.</summary>
    public BrewBuilder Amplify(int baseBuff, int strengthPercent)
    {
        int index = _brew.Effects.FindIndex(e => e.BaseBuff == baseBuff);

        if (index >= 0)
        {
            AlchemicalEffect effect = _brew.Effects[index];
            _brew.Effects[index] = effect with { StrengthPercent = Math.Max(1, effect.StrengthPercent + strengthPercent) };
        }

        return this;
    }

    /// <summary>Moves the primary effect <paramref name="levels"/> along the potency curve, clamped to 1-5.</summary>
    public BrewBuilder Potency(int levels)
    {
        _brew.Effects[0] = BrewTuning.Shift(_brew.Effects[0], _brew.Effects[0].Potency + levels);
        return this;
    }

    /// <summary>Rewrite the primary effect's params: e.g. <c>Detour(p =&gt; p with { MaxOreTier = 2 })</c>.</summary>
    public BrewBuilder Detour(Func<EffectParams, EffectParams> edit) => Detour(_brew.Primary.BaseBuff, edit);

    /// <summary>Rewrite the params of the effect riding <paramref name="baseBuff"/>, if the brew has one.</summary>
    public BrewBuilder Detour(int baseBuff, Func<EffectParams, EffectParams> edit)
    {
        int index = _brew.Effects.FindIndex(e => e.BaseBuff == baseBuff);

        if (index >= 0)
            _brew.Effects[index] = _brew.Effects[index] with { Params = edit(_brew.Effects[index].Params) };

        return this;
    }

    /// <summary>
    /// Combines another buff effect onto the potion, replacing one on the same buff; <paramref name="durationTicks"/> defaults
    /// to the potion's buff time, or 60 s.
    /// </summary>
    public BrewBuilder Buff(int baseBuff, int potency, int durationTicks = 0)
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

    /// <summary>Starts <paramref name="stat"/> at <paramref name="value"/> on drink for <paramref name="durationTicks"/>.</summary>
    public BrewBuilder Stat(BrewStat stat, float value, int durationTicks) => Modifier(stat.Key, value, durationTicks);

    /// <summary>Starts the custom effect <typeparamref name="T"/> on drink for <paramref name="durationTicks"/>.</summary>
    public BrewBuilder Effect<T>(int durationTicks, float value = 1f) where T : BrewEffect =>
        Modifier(ModContent.GetInstance<T>().Key, value, durationTicks);

    /// <summary>Restores <paramref name="life"/> more on drink; a potion that doesn't heal heals this much.</summary>
    public BrewBuilder Heal(int life)
    {
        _brew.HealLife = Math.Max(0, _brew.HealLife + life);
        return this;
    }

    /// <summary>Restores <paramref name="mana"/> more on drink; a potion that doesn't restore mana restores this much.</summary>
    public BrewBuilder Mana(int mana)
    {
        _brew.HealMana = Math.Max(0, _brew.HealMana + mana);
        return this;
    }

    /// <summary>A modifier already on the brew adds <paramref name="value"/> and keeps the longer duration.</summary>
    private BrewBuilder Modifier(string key, float value, int durationTicks)
    {
        int existing = _brew.Modifiers.FindIndex(m => m.Key == key);

        if (existing >= 0)
        {
            TimedModifier current = _brew.Modifiers[existing];
            _brew.Modifiers[existing] = current with
            {
                Value = current.Value + value,
                DurationTicks = Math.Max(current.DurationTicks, durationTicks),
            };
        }
        else
            _brew.Modifiers.Add(new TimedModifier(key, value, Math.Max(1, durationTicks)));

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
    /// level, duration by the ratio of the duration scales. Default params move to the new potency's defaults; detoured ones stay.
    /// </summary>
    public static AlchemicalEffect Shift(AlchemicalEffect effect, int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        int levels = potency - effect.Potency;

        if (levels == 0)
            return effect;

        int duration = effect.DurationTicks <= 0 ? 0
            : Math.Max(60, (int)(effect.DurationTicks * DurationScale(potency) / DurationScale(effect.Potency)));

        IEffectBehavior? behavior = EffectBehaviors.Get(effect.BaseBuff);
        EffectParams defaults = behavior?.DefaultParams(effect.Potency) ?? EffectParams.None;

        return effect with
        {
            Potency = potency,
            StrengthPercent = Math.Max(1, effect.StrengthPercent + levels * STRENGTH_PER_LEVEL),
            DurationTicks = duration,
            Params = effect.Params == defaults ? behavior?.DefaultParams(potency) ?? EffectParams.None : effect.Params,
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
