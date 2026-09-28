namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     The numbers one release applies with: its source's, adjusted by its modifiers and the releasing player's stats.
///     Multipliers stack additively, the way vanilla prefixes do.
/// </summary>
public struct ChemicalStats
{
    public int ChemicalDamage;

    public int AfflictionDuration;

    /// <summary>Ticks between damage ticks.</summary>
    public float AfflictionRate;

    /// <summary>Percent. Can go below zero, which never reacts.</summary>
    public float ReactionChance;

    public int ReactionDamage;

    public int Concentration;

    public float Catalysis;

    public float DamageMultiplier;

    public float DurationMultiplier;

    public float ReactionDamageMultiplier;

    public float ClingChanceMultiplier;

    public float ClingDurationMultiplier;

    /// <summary>Lifetime of the gas nodes the release puts into the world.</summary>
    public float GasLifeMultiplier;

    /// <summary>Radius of the gas nodes the release puts into the world.</summary>
    public float GasSpreadMultiplier;

    /// <summary>Speed of the arms the release's container bursts into.</summary>
    public float BurstSpeedMultiplier;

    /// <summary>Number of arms the release's container bursts into.</summary>
    public float BurstCountMultiplier;

    /// <summary>How often the release's gas and residue expose an NPC; divides the contact interval.</summary>
    public float ContactRateMultiplier;

    public static ChemicalStats Of(IChemicalSource source) => Neutral with
    {
        ChemicalDamage = source.ChemicalDamage,
        AfflictionDuration = source.AfflictionDuration,
        AfflictionRate = source.AfflictionRate,
        ReactionChance = source.ReactionChance,
        ReactionDamage = source.ReactionDamage,
        Concentration = source.Concentration,
        Catalysis = source.Catalysis,
    };

    /// <summary>No stats and unit multipliers, with a unit affliction rate, for measuring what a modifier changes.</summary>
    public static ChemicalStats Neutral => new()
    {
        AfflictionRate = 1f,
        DamageMultiplier = 1f,
        DurationMultiplier = 1f,
        ReactionDamageMultiplier = 1f,
        ClingChanceMultiplier = 1f,
        ClingDurationMultiplier = 1f,
        GasLifeMultiplier = 1f,
        GasSpreadMultiplier = 1f,
        BurstSpeedMultiplier = 1f,
        BurstCountMultiplier = 1f,
        ContactRateMultiplier = 1f,
    };

    /// <summary>Folds in the releasing player's chemical stats for a release of <paramref name="type" />.</summary>
    public void Apply(ChemicalPlayerStats player, ChemicalDamageType type)
    {
        DamageMultiplier *= player.AfflictionDamage(type);
        DurationMultiplier *= player.AfflictionRate;
        ReactionChance += player.ReactionChance;
        ReactionDamageMultiplier *= player.ReactionDamageBonus;
    }
}
