using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

/// <summary>
/// Applies and describes one kind of brewed effect. Registered per vanilla buff id in
/// <see cref="EffectBehaviors"/>. Additive/percent stat buffs share <see cref="StatDeltaBehavior"/>;
/// "binary" effects (Spelunker reveal, ...) get bespoke behaviours that suppress + reparameterise the
/// vanilla effect. Adding a brew-able buff = one registry entry; the player/item code never changes.
/// </summary>
public interface IEffectBehavior
{
    /// <summary>Called each tick the effect's buff is present on the player.</summary>
    void Apply(Player player, in AlchemicalEffect fx);

    /// <summary>
    /// Called each tick the buff is present, after every buff has ticked but before any mod's
    /// <c>UpdateEquips</c>. Only needed for stats somebody else *consumes* during UpdateEquips
    /// (wing flight time, ...). <see cref="Apply"/> runs post-equips and would miss the window.
    /// </summary>
    void ApplyPreEquips(Player player, in AlchemicalEffect fx) { }

    /// <summary>Effect line for tooltips/buff text, e.g. "[30%] increased movement speed". Null if none.</summary>
    string? Describe(in AlchemicalEffect fx);

    /// <summary>Default per-effect params for a freshly brewed effect of this potency (e.g. Spelunker's tier gate).</summary>
    EffectParams DefaultParams(int potency) => EffectParams.None;
}

/// <summary>
/// Registry mapping a vanilla buff id to the <see cref="IEffectBehavior"/> that parameterises it.
/// </summary>
public static class EffectBehaviors
{
    private static readonly Dictionary<int, IEffectBehavior> _behaviors = new()
    {
        [BuffID.Ironskin] = new DeltaBehavior((8f, "defense", asPercent: false, reductionString: false,
                                    (p, delta) => p.statDefense += (int)MathF.Round(delta))),

        [BuffID.Swiftness] = new DeltaBehavior((0.25f, "movement speed", asPercent: true, reductionString: false,
                                    (p, delta) => p.moveSpeed += delta)),

        [BuffID.Regeneration] = new DeltaBehavior((4f, "life regeneration", asPercent: false, reductionString: false,
                                    (p, delta) => p.lifeRegen += (int)MathF.Round(delta))),

        [BuffID.Mining] = new DeltaBehavior((0.25f, "mining speed", asPercent: true, reductionString: false,
                                    (p, delta) => p.pickSpeed += delta)),

        [BuffID.Rage] = new DeltaBehavior((0.1f, "critical chance", asPercent: true, reductionString: false,
                                    (p, delta) => p.allCrit += delta)),

        [BuffID.Wrath] = new DeltaBehavior(
            (0.1f, "damage", asPercent: true, reductionString: false, (p, delta) => p.allDamage += delta)),

        [BuffID.Titan] = new DeltaBehavior(
            (0.5f, "knockback", asPercent: true, reductionString: false, (p, delta) => p.allKB += delta)),

        [BuffID.Lifeforce] = new DeltaBehavior(
            (0.2f, "max life", asPercent: true, reductionString: false, (p, delta) => p.statLifeMax2 += (int)delta)),

        [BuffID.MagicPower] = new DeltaBehavior(
            (0.2f, "magic damage", asPercent: true, reductionString: false, (p, delta) => p.magicDamage += delta)),

        [BuffID.ManaRegeneration] = new DeltaBehavior(
            (4f, "mana regeneration", asPercent: false, reductionString: false, (p, delta) => p.manaRegen += (int)delta)),

        [BuffID.Endurance] = new DeltaBehavior(
            (0.1f, "damage taken", asPercent: true, reductionString: true, (p, delta) => p.endurance += (int)delta)),

        [BuffID.Fishing] = new DeltaBehavior(
            (0.15f, "fishing power", asPercent: true, reductionString: false, (p, delta) => p.fishingSkill += (int)delta)),

        [BuffID.Summoning] = new DeltaBehavior(
            (1, "your max number of minions", asPercent: false, reductionString: false, (p, delta) => p.maxMinions += (int)delta)),

        [BuffID.Archery] = new DeltaBehavior(
            (0.1f, "bow damage", asPercent: true, reductionString: false, (p, delta) => p.arrowDamage += (int)delta),
            (0.2f, "arrow speed", asPercent: true, reductionString: false, (p, delta) => p.GetAttackSpeed(DamageClass.Ranged) += delta)),

        [BuffID.Thorns] = new DeltaBehavior(
            (0.1f, "contact damage", asPercent: true, reductionString: false, (p, delta) => p.thorns += (int)delta),
            (0.10f, "knockback", asPercent: true, reductionString: false, (p, delta) => p.GetKnockback(DamageClass.Generic) += delta)),

        [BuffID.Spelunker] = new SpelunkerBehavior(),

        [BuffID.Builder] = new BuilderPotionBehavior(0.25f, 1),

        [VirtualBuff.Restoration] = new RestorationBehavior(),

        // Crate chance is an absolute probability, not a relative nudge, so it needs its own Describe
        // (the resulting chance, 25% at stock strength) rather than DeltaBehavior's "+N increased ...".
        [BuffID.Crate] = new CrateBehavior(vanillaBaseChance: 10, vanillaPotionBonus: 15),

        // Luck is a derived cache rebuilt in Player.RecalculateLuck, not a stat you add to each tick.
        [BuffID.Lucky] = new LuckBehavior(),
    };

    // Another mod's buff has no compile-time id, so its behaviour is added once that mod is known to
    // be loaded (PostSetupContent). Tracked separately so Unload can take exactly those back out.
    private static readonly HashSet<int> _runtimeRegistered = new();

    /// <summary>
    /// Register a behaviour for a buff id that isn't a compile-time constant: i.e. a cross-mod buff,
    /// resolved via <see cref="ModContent.TryFind{T}(string, string, out T)"/>. Replaces any existing entry.
    /// </summary>
    public static void Register(int buff, IEffectBehavior behavior)
    {
        if (buff <= 0) return;
        _behaviors[buff] = behavior;
        _runtimeRegistered.Add(buff);
    }

    /// <summary>
    /// Drop every runtime-registered behaviour. Cross-mod entries hold another mod's types and must
    /// not survive into a reload where that mod is gone.
    /// </summary>
    public static void ClearRuntime()
    {
        foreach (int buff in _runtimeRegistered) _behaviors.Remove(buff);
        _runtimeRegistered.Clear();
    }

    /// <summary>The behaviour for a buff, or null if it isn't brew-parameterised.</summary>
    public static IEffectBehavior? Get(int buff) => _behaviors.GetValueOrDefault(buff);

    /// <summary>Effect line for a brew's effect, or null if the buff has no registered behaviour.</summary>
    public static string? Describe(in AlchemicalEffect fx) => Get(fx.BaseBuff)?.Describe(fx);
}

/// <summary>
/// Allows you to apply multiple effects with the amazing and fantastical <see cref="List{T}"/>.
/// <para>This is used for potions with multiple affects such as Archery and Thorns 
/// (+10% bow damage x +20% arrow speed, +10 knockback x 10% contact damage)</para>
/// </summary>
public sealed class DeltaBehavior : IEffectBehavior
{
    private readonly List<(float vanilla, string noun, bool asPercent, bool reductionString, Action<Player, float> apply)> _deltas;

    public DeltaBehavior(params (float vanilla, string noun, bool asPercent, bool reductionString, Action<Player, float> apply)[] deltas)
    {
        _deltas = [.. deltas];
    }

    public void Apply(Player player, in AlchemicalEffect fx)
    {
        foreach (var (vanilla, _, _, _, apply) in _deltas)
        {
            float delta = vanilla * (fx.StrengthPercent / 100f - 1f);
            apply(player, delta);
        }
    }

    public string Describe(in AlchemicalEffect fx)
    {
        float strengthPercent = fx.StrengthPercent;

        var descriptions = _deltas.Select(d =>
        {
            float value = d.vanilla * strengthPercent / 100f;
            //return d.asPercent
            //    ? $"+{(int)MathF.Round(value * 100)}% increased {d.noun}"
            //    : $"+{(int)MathF.Round(value)} {d.noun}";
            if (d.reductionString)
                return d.asPercent ? $"-{(int)MathF.Round(value * 100)}% reduced {d.noun}" : $"reduces {d.noun} by {(int)MathF.Round(value)}";

            else
                return d.asPercent ? $"+{(int)MathF.Round(value * 100)}% increased {d.noun}" : $"increases {d.noun} by {(int)MathF.Round(value)}";
        });
        return string.Join(" and ", descriptions);
    }
}

/// <summary>
/// Instant heal scaling for restorative potions (<see cref="VirtualBuff.Restoration"/>). There is no
/// buff to tick, so <see cref="Apply"/> is a no-op and the actual scaling happens item-side in
/// <see cref="BrewGlobalItem.GetHealLife"/>/<c>GetHealMana</c> when the potion is drunk.
/// </summary>
public sealed class RestorationBehavior : IEffectBehavior
{
    public void Apply(Player player, in AlchemicalEffect fx) { }

    public string? Describe(in AlchemicalEffect fx)
    {
        int delta = fx.StrengthPercent - 100;
        if (delta == 0) return null;
        return $"{(delta > 0 ? "+" : "")}{delta}% restored life and mana";
    }
}