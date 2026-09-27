using System.Collections.Generic;
using System.Globalization;
using Terraria.Localization;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>A stat a <see cref="ChemicalModifier" /> can change. Each name is its line's key under <c>ChemicalModifiers.Stats</c>.</summary>
public enum ChemicalStat
{
    AfflictionDamageFlat,
    AfflictionDamage,
    AfflictionSpeed,
    AfflictionDurationFlat,
    AfflictionDuration,
    ReactionChance,
    ReactionDamageFlat,
    ReactionDamage,
    Concentration,
    Catalysis,
    ClingChance,
    ClingDuration,
    GasLifetime,
    GasSpread,
    BurstSpeed,
    BurstCount,
    ContactRate,
}

/// <summary>
///     How far a modifier moves one stat, in the unit its line shows: flat, seconds or percent. Every stat is better
///     higher, so the sign says whether the change helps.
/// </summary>
public readonly record struct ChemicalStatChange(ChemicalStat Stat, float Delta)
{
    public bool Helps => Delta > 0f;

    /// <summary>The signed line, such as "+2 concentration".</summary>
    public string Text
    {
        get
        {
            string number = Delta.ToString("0.#", CultureInfo.InvariantCulture);
            string key = TrelamiumIII.Instance.GetLocalizationKey("ChemicalModifiers.Stats." + Stat);

            return Language.GetTextValue(key, Delta > 0f ? "+" + number : number);
        }
    }

    /// <summary>
    ///     What <paramref name="modifier" /> changes about releases of <paramref name="source" />, leaving out what does
    ///     nothing for it: affliction stats on a source that deals none, reaction stats on one that cannot react, cling
    ///     stats on one that builds no concentration. With no source, as for a weapon its ammo supplies, all count.
    /// </summary>
    public static List<ChemicalStatChange> Measure(ChemicalModifier modifier, IChemicalSource? source = null)
    {
        ChemicalStats before = source is null ? ChemicalStats.Neutral : ChemicalStats.Of(source);
        ChemicalStats after = before;

        modifier.ModifyStats(ref after);

        bool unknown = source is null;
        bool afflicts = unknown || before.ChemicalDamage > 0 || after.ChemicalDamage > 0;
        bool reacts = unknown || ChemicalMatchup.IsElemental(source!.ChemicalType);
        bool concentrates = unknown || before.Concentration > 0 || after.Concentration > 0;

        var changes = new List<ChemicalStatChange>();

        void Add(ChemicalStat stat, float delta, bool counts)
        {
            float rounded = MathF.Round(delta, 1);

            if (counts && rounded != 0f)
                changes.Add(new ChemicalStatChange(stat, rounded));
        }

        float speed = after.AfflictionRate > 0f ? before.AfflictionRate / after.AfflictionRate - 1f : 0f;

        Add(ChemicalStat.AfflictionDamageFlat, after.ChemicalDamage - before.ChemicalDamage, afflicts);
        Add(ChemicalStat.AfflictionDamage, (after.DamageMultiplier - before.DamageMultiplier) * 100f, afflicts);
        Add(ChemicalStat.AfflictionSpeed, speed * 100f, afflicts);
        Add(ChemicalStat.AfflictionDurationFlat, (after.AfflictionDuration - before.AfflictionDuration) / 60f, afflicts);
        Add(ChemicalStat.AfflictionDuration, (after.DurationMultiplier - before.DurationMultiplier) * 100f, afflicts);
        Add(ChemicalStat.ReactionChance, after.ReactionChance - before.ReactionChance, reacts);
        Add(ChemicalStat.ReactionDamageFlat, after.ReactionDamage - before.ReactionDamage, reacts);
        Add(ChemicalStat.ReactionDamage, (after.ReactionDamageMultiplier - before.ReactionDamageMultiplier) * 100f, reacts);
        Add(ChemicalStat.Concentration, after.Concentration - before.Concentration, concentrates);
        Add(ChemicalStat.Catalysis, (after.Catalysis - before.Catalysis) * 100f, true);
        Add(ChemicalStat.ClingChance, (after.ClingChanceMultiplier - before.ClingChanceMultiplier) * 100f, concentrates);
        Add(ChemicalStat.ClingDuration, (after.ClingDurationMultiplier - before.ClingDurationMultiplier) * 100f, concentrates);
        Add(ChemicalStat.GasLifetime, (after.GasLifeMultiplier - before.GasLifeMultiplier) * 100f, true);
        Add(ChemicalStat.GasSpread, (after.GasSpreadMultiplier - before.GasSpreadMultiplier) * 100f, true);
        Add(ChemicalStat.BurstSpeed, (after.BurstSpeedMultiplier - before.BurstSpeedMultiplier) * 100f, true);
        Add(ChemicalStat.BurstCount, (after.BurstCountMultiplier - before.BurstCountMultiplier) * 100f, true);
        Add(ChemicalStat.ContactRate, (after.ContactRateMultiplier - before.ContactRateMultiplier) * 100f, true);

        return changes;
    }
}
