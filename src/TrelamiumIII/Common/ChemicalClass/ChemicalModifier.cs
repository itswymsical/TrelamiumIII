using System.Globalization;
using Terraria.Localization;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     A change to a chemical release: its numbers, its burst, and what its reactions and clings do. Weapons carry them
///     innately through <see cref="ChemicalSourceComponent.Modifiers" /> or through a <see cref="ChemicalPrefix" />.
///     Shared and stateless.
/// </summary>
[CloneByReference]
public abstract class ChemicalModifier
{
    /// <summary>A change to a release's numbers, for <see cref="WithStats" />.</summary>
    public delegate void StatEdit(ref ChemicalStats stats);

    private StatEdit? statEdit;

    /// <summary>The temperament this modifier expresses, or None. Colours and marks it in tooltips.</summary>
    public virtual Temperament Temperament => Temperament.None;

    /// <summary>The element whose weapons roll this modifier as a prefix, or None for any.</summary>
    public virtual ChemicalDamageType Element => ChemicalDamageType.None;

    /// <summary>Localization key under <c>ChemicalModifiers</c>. Unique per modifier.</summary>
    public virtual string Key => GetType().Name;

    public string DisplayName => Localize("DisplayName") ?? Key;

    /// <summary>The name as registered text, for names fixed at load such as a prefix's.</summary>
    public LocalizedText DisplayNameText
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"ChemicalModifiers.{Key}.DisplayName"));

    /// <summary>Behaviour the stat lines cannot show, or null when the modifier has no description key.</summary>
    public string? Description => Localize("Description", DescriptionArgs);

    /// <summary>Numbers the description formats in, so its text follows the tuning.</summary>
    protected virtual object[] DescriptionArgs => [];

    /// <summary>
    ///     Whether this changes anything for releases of <paramref name="source" />: a behaviour it can use or a stat it
    ///     measures. An element's modifier needs a source of that element; a null source (ammo decides) takes the rest.
    /// </summary>
    public bool AppliesTo(IChemicalSource? source)
        => (Element == ChemicalDamageType.None || source?.ChemicalType == Element)
           && (UsesBehaviour(source) || ChemicalStatChange.Measure(this, source).Count > 0);

    /// <summary>Whether <paramref name="source" /> can use the behaviour beyond the stats. Null means the source is unknown.</summary>
    protected virtual bool UsesBehaviour(IChemicalSource? source) => false;

    /// <summary>Adds stat changes to this modifier, for one that is a behaviour and stats both. Returns itself.</summary>
    public ChemicalModifier WithStats(StatEdit edit)
    {
        statEdit += edit;

        return this;
    }

    /// <summary>Applied once, when the payload is built. Add to multipliers rather than multiplying them.</summary>
    public virtual void ModifyStats(ref ChemicalStats stats) => statEdit?.Invoke(ref stats);

    /// <summary>Adjusts the burst the release's container opens into.</summary>
    public virtual void ModifyBurst(ref BurstProfile burst) { }

    /// <summary>Runs after a release's hit lands, on the machine that owns it. Derived payloads never hit.</summary>
    public virtual void OnHit(NPC target, ChemicalPayload payload, ChemicalGasField? medium) { }

    /// <summary>Runs after any exposure to <paramref name="payload" /> applies, on the owner's machine. Must not expose anything itself.</summary>
    public virtual void OnExpose(NPC target, ChemicalPayload payload, in ChemicalDelivery delivery) { }

    /// <summary>Runs each time an affliction credited to <paramref name="payload" /> deals a tick.</summary>
    public virtual void OnAfflictionTick(NPC npc, ChemicalPayload payload, ChemicalDamageType type) { }

    /// <summary>Runs when <paramref name="npc" /> dies with an affliction credited to <paramref name="payload" />.</summary>
    public virtual void OnAfflictedDeath(NPC npc, ChemicalPayload payload) { }

    /// <summary>Adjusts the odds, in percent, that a hit on <paramref name="target" /> reacts.</summary>
    public virtual void ModifyReactionChance(NPC target, ChemicalPayload payload, ref float chance) { }

    /// <summary>Adjusts a reaction's burst damage before it lands, chained reactions included.</summary>
    public virtual void ModifyReactionDamage(NPC target, ChemicalPayload payload, ref float damage) { }

    /// <summary>
    ///     Runs after the source's own reaction behaviour, on the machine that owns the release. A reaction set off by
    ///     another carries a derived payload; behaviours that set off more reactions skip those.
    /// </summary>
    public virtual void OnReaction(in ChemicalReactionEvent e) { }

    /// <summary>Replaces what the release does when it clings, over the source and the gas. Null leaves them.</summary>
    public virtual ClingBehaviour? ClingBehaviour => null;

    /// <summary>Whether this modifier acts on releases of <paramref name="payload" />'s element.</summary>
    protected bool Matches(ChemicalPayload payload) => Element == ChemicalDamageType.None || payload.Source.ChemicalType == Element;

    /// <summary>Whether releases of <paramref name="source" /> can react. True for a null source, whose ammo decides.</summary>
    protected static bool Reacts(IChemicalSource? source) => source is null || ChemicalMatchup.IsElemental(source.ChemicalType);

    /// <summary>A fraction as a whole percent, for <see cref="DescriptionArgs" />.</summary>
    protected static string Percent(float fraction) => Number(fraction * 100f);

    protected static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private string? Localize(string field, params object[] args)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"ChemicalModifiers.{Key}.{field}");
        string value = Language.GetTextValue(key, args);

        return value == key ? null : value;
    }
}

/// <summary>The universal modifier for each temperament, each drawn from what the almanac says the temperament does.</summary>
public sealed class TemperamentModifier : ChemicalModifier
{
    /// <summary>Breaks other compounds down: harsher afflictions.</summary>
    public static readonly TemperamentModifier Acidic = new(Temperament.Acidic);

    /// <summary>Binds compounds together: clings take more readily and hold longer.</summary>
    public static readonly TemperamentModifier Alkaline = new(Temperament.Alkaline);

    /// <summary>Unstable and quick to evaporate: likelier reactions, wider but shorter-lived gas.</summary>
    public static readonly TemperamentModifier Volatile = new(Temperament.Volatile);

    /// <summary>Reacts to release raw energy: harder reaction bursts and faster container bursts.</summary>
    public static readonly TemperamentModifier Kinetic = new(Temperament.Kinetic);

    /// <summary>Enables and hastens reactions: faster afflictions, quicker build-up, and a catalyst's catalysis strengthened.</summary>
    public static readonly TemperamentModifier Catalytic = new(Temperament.Catalytic);

    /// <summary>Refuses to react and stabilises: fewer reactions, longer afflictions and longer-lived gas.</summary>
    public static readonly TemperamentModifier Inert = new(Temperament.Inert);

    private readonly Temperament temperament;

    private TemperamentModifier(Temperament temperament) => this.temperament = temperament;

    public override Temperament Temperament => temperament;

    public override string Key => temperament.ToString();

    public override void ModifyStats(ref ChemicalStats stats)
    {
        switch (temperament)
        {
            case Temperament.Acidic:
                stats.DamageMultiplier += 0.2f;
                break;

            case Temperament.Alkaline:
                stats.ClingChanceMultiplier += 0.5f;
                stats.ClingDurationMultiplier += 0.3f;
                break;

            case Temperament.Volatile:
                stats.ReactionChance += 3f;
                stats.GasLifeMultiplier -= 0.2f;
                stats.GasSpreadMultiplier += 0.2f;
                break;

            case Temperament.Kinetic:
                stats.ReactionDamageMultiplier += 0.3f;
                stats.BurstSpeedMultiplier += 0.25f;
                break;

            case Temperament.Catalytic:
                stats.AfflictionRate *= 0.8f;
                stats.Concentration += 3;

                // Catalysis from nothing would turn the source into a catalyst, which stops it being amplified.
                if (stats.Catalysis > 0f)
                    stats.Catalysis += 0.05f;

                break;

            case Temperament.Inert:
                stats.ReactionChance -= 5f;
                stats.DurationMultiplier += 0.4f;
                stats.GasLifeMultiplier += 0.3f;
                break;
        }
    }
}

/// <summary>A flat change to concentration and nothing else: the grades of how much a weapon's release builds up.</summary>
public sealed class ConcentrationModifier : ChemicalModifier
{
    public static readonly ConcentrationModifier Diluted = new(nameof(Diluted), -2);

    public static readonly ConcentrationModifier Concentrated = new(nameof(Concentrated), 2);

    public static readonly ConcentrationModifier Saturated = new(nameof(Saturated), 4);

    private readonly string key;

    private readonly int amount;

    private ConcentrationModifier(string key, int amount)
    {
        this.key = key;
        this.amount = amount;
    }

    public override string Key => key;

    public override void ModifyStats(ref ChemicalStats stats) => stats.Concentration += amount;
}
