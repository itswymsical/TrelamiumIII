using System.Collections.Generic;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>A stat or custom effect a brew runs for a duration (see <see cref="TimedModifier"/>), registered by <see cref="Key"/>.</summary>
public interface IBrewModifier
{
    /// <summary>Saved and synced in place of a runtime id.</summary>
    string Key { get; }

    /// <summary>Called each tick the modifier is active, after equips.</summary>
    void Apply(Player player, float value);

    /// <summary>Runs after buffs tick but before any UpdateEquips; only for stats consumed there.</summary>
    void ApplyPreEquips(Player player, float value) { }

    /// <summary>Tooltip and buff text line, e.g. "+7% life regeneration".</summary>
    string Describe(float value);
}

/// <summary>A player stat changed by <c>value</c>: percent stats read 7 as 7%, flat stats add it as is.</summary>
public sealed class BrewStat : IBrewModifier
{
    public string Key { get; }

    private readonly string _noun;
    private readonly bool _percent;
    private readonly bool _preEquips;
    private readonly Action<Player, float> _apply;

    public BrewStat(string name, string noun, bool percent, Action<Player, float> apply, bool preEquips = false)
    {
        Key = "stat:" + name;
        _noun = noun;
        _percent = percent;
        _preEquips = preEquips;
        _apply = apply;
    }

    public void Apply(Player player, float value)
    {
        if (!_preEquips) _apply(player, value);
    }

    public void ApplyPreEquips(Player player, float value)
    {
        if (_preEquips) _apply(player, value);
    }

    public string Describe(float value) =>
        $"{(value >= 0 ? "+" : "-")}{MathF.Abs(value):0.#}{(_percent ? "%" : "")} {_noun}";
}

/// <summary>Stats a brew can start with <c>.Stat(...)</c>.</summary>
public static class BrewStats
{
    public static readonly BrewStat Defense = new("Defense", "defense", percent: false,
        (p, v) => p.statDefense += (int)MathF.Round(v));

    public static readonly BrewStat DamageReduction = new("DamageReduction", "damage reduction", percent: true,
        (p, v) => p.endurance += v / 100f);

    public static readonly BrewStat LifeRegen = new("LifeRegen", "life regeneration", percent: true,
        (p, v) => p.GetModPlayer<AlchemyEffectPlayer>().LifeRegenPercent += v);

    public static readonly BrewStat ManaRegen = new("ManaRegen", "mana regeneration", percent: false,
        (p, v) => p.manaRegenBonus += (int)MathF.Round(v));

    public static readonly BrewStat MaxLife = new("MaxLife", "max life", percent: false,
        (p, v) => p.statLifeMax2 += (int)MathF.Round(v));

    public static readonly BrewStat MaxMana = new("MaxMana", "max mana", percent: false,
        (p, v) => p.statManaMax2 += (int)MathF.Round(v));

    public static readonly BrewStat MoveSpeed = new("MoveSpeed", "movement speed", percent: true,
        (p, v) => p.moveSpeed += v / 100f);

    public static readonly BrewStat Damage = new("Damage", "damage", percent: true,
        (p, v) => p.GetDamage(DamageClass.Generic) += v / 100f);

    public static readonly BrewStat Crit = new("Crit", "critical strike chance", percent: true,
        (p, v) => p.GetCritChance(DamageClass.Generic) += v);

    public static readonly BrewStat AttackSpeed = new("AttackSpeed", "attack speed", percent: true,
        (p, v) => p.GetAttackSpeed(DamageClass.Generic) += v / 100f);

    public static readonly BrewStat Knockback = new("Knockback", "knockback", percent: true,
        (p, v) => p.GetKnockback(DamageClass.Generic) += v / 100f);

    public static readonly BrewStat MiningSpeed = new("MiningSpeed", "mining speed", percent: true,
        (p, v) => p.pickSpeed -= v / 100f);

    public static readonly BrewStat FishingPower = new("FishingPower", "fishing power", percent: false,
        (p, v) => p.fishingSkill += (int)MathF.Round(v));

    public static readonly BrewStat MaxMinions = new("MaxMinions", "max minions", percent: false,
        (p, v) => p.maxMinions += (int)MathF.Round(v));

    // Declared after the stats: static initializers run in textual order.
    internal static readonly BrewStat[] All =
    [
        Defense, DamageReduction, LifeRegen, ManaRegen, MaxLife, MaxMana, MoveSpeed, Damage, Crit, AttackSpeed,
        Knockback, MiningSpeed, FishingPower, MaxMinions,
    ];
}

/// <summary>
/// A custom timed brew effect, e.g. a ModPlayer flag set each tick it's active. Autoloaded and keyed by its full name;
/// added to a potion with <c>.Effect&lt;T&gt;(durationTicks)</c>.
/// </summary>
public abstract class BrewEffect : ModType, IBrewModifier
{
    public string Key => FullName;

    public virtual void Apply(Player player, float value) { }

    public virtual void ApplyPreEquips(Player player, float value) { }

    public abstract string Describe(float value);

    protected sealed override void Register()
    {
        ModTypeLookup<BrewEffect>.Register(this);
        BrewModifiers.Register(this);
    }
}

/// <summary>Every <see cref="IBrewModifier"/> by key: the <see cref="BrewStats"/> plus loaded <see cref="BrewEffect"/>s.</summary>
public static class BrewModifiers
{
    private static readonly Dictionary<string, IBrewModifier> _modifiers = new();
    private static readonly HashSet<string> _effects = new();

    static BrewModifiers()
    {
        foreach (BrewStat stat in BrewStats.All)
            _modifiers[stat.Key] = stat;
    }

    internal static void Register(BrewEffect effect)
    {
        _modifiers[effect.Key] = effect;
        _effects.Add(effect.Key);
    }

    /// <summary>Drops the effects, which are this session's mod types; the stats stay.</summary>
    public static void ClearEffects()
    {
        foreach (string key in _effects) _modifiers.Remove(key);
        _effects.Clear();
    }

    public static IBrewModifier? Get(string key) => _modifiers.GetValueOrDefault(key);

    public static string? Describe(in TimedModifier modifier) => Get(modifier.Key)?.Describe(modifier.Value);
}
