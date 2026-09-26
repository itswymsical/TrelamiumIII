namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>Numbers for concentration, clings and catalysis, kept together for tuning. All provisional.</summary>
public static class ChemicalTuning
{
    public const float MaxConcentration = 100f;

    /// <summary>Ticks a contact exposure blocks further contact exposures of the same element on one NPC.</summary>
    public const int ContactInterval = 10;

    /// <summary>Fraction of a source's concentration and catalysis that one contact exposure adds.</summary>
    public const float ContactShare = 0.25f;

    /// <summary>Ticks concentration holds after its last gain before it starts to decay.</summary>
    public const int ConcentrationHold = 60;

    public const float ConcentrationDecayPerTick = 0.5f;

    /// <summary>Cling chance per exposure at full concentration; scales linearly with concentration.</summary>
    public const float ClingChanceAtMax = 0.2f;

    /// <summary>Ticks catalysis holds after its last gain before it starts to decay.</summary>
    public const int CatalysisHold = 180;

    public const float CatalysisDecayPerTick = 1f / 120f;

    /// <summary>Extra effect at full catalysis: 1 doubles other chemistry's damage, concentration and reaction odds.</summary>
    public const float CatalysisStrength = 1f;

    public static float ClingChance(float concentration) => concentration / MaxConcentration * ClingChanceAtMax;

    /// <summary>1.5 seconds at no concentration, 6 at full.</summary>
    public static int ClingTicks(float concentration) => 90 + (int)(concentration * 2.7f);
}
