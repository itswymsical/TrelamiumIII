using Terraria.Localization;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     A change to a chemical release: its numbers, its burst, and what its reactions and clings do. Weapons carry them
///     innately through <see cref="ChemicalSourceComponent.Modifiers" />. Shared and stateless.
/// </summary>
[CloneByReference]
public abstract class ChemicalModifier
{
    /// <summary>The temperament this modifier expresses, or None. Colours and marks it in tooltips.</summary>
    public virtual Temperament Temperament => Temperament.None;

    /// <summary>Localization key under <c>ChemicalModifiers</c>. Unique per modifier.</summary>
    public virtual string Key => GetType().Name;

    public string DisplayName => Localize("DisplayName") ?? Key;

    /// <summary>Behaviour the stat lines cannot show, or null when the modifier has no description key.</summary>
    public string? Description => Localize("Description");

    /// <summary>Applied once, when the payload is built. Add to multipliers rather than multiplying them.</summary>
    public virtual void ModifyStats(ref ChemicalStats stats) { }

    /// <summary>Adjusts the burst the release's container opens into.</summary>
    public virtual void ModifyBurst(ref BurstProfile burst) { }

    /// <summary>Runs after the source's own reaction behaviour, on the machine that owns the release.</summary>
    public virtual void OnReaction(in ChemicalReactionEvent e) { }

    /// <summary>Replaces what the release does when it clings, over the source and the gas. Null leaves them.</summary>
    public virtual ClingBehaviour? ClingBehaviour => null;

    private string? Localize(string field)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"ChemicalModifiers.{Key}.{field}");
        string value = Language.GetTextValue(key);

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

    /// <summary>Enables and hastens reactions: faster afflictions and quicker build-up.</summary>
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
                break;

            case Temperament.Inert:
                stats.ReactionChance -= 5f;
                stats.DurationMultiplier += 0.4f;
                stats.GasLifeMultiplier += 0.3f;
                break;
        }
    }
}
