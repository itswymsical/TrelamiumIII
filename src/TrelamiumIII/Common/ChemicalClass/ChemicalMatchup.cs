namespace TrelamiumIII.Common.ChemicalClass;

[Flags]
public enum EnemyComposition
{
    None = 0,
    Flesh = 1 << 0,
    Gel = 1 << 1,
    Armored = 1 << 2,
    Metal = 1 << 3,
    Undead = 1 << 4,
    Frozen = 1 << 5,
    Incendiary = 1 << 6,
}

public enum ChemicalDamageType
{
    None,
    Thermal,
    Cryonic,
    Caustic,
    Necrotic,
    Kinetic
}

public static class ChemicalMatchup
{
    /// <summary>
    /// Multiplier applied to affliction damage per tick based on chemical type vs enemy composition.
    /// </summary>
    public static float GetAfflictionMultiplier(ChemicalDamageType type, EnemyComposition composition)
    {
        return type switch
        {
            ChemicalDamageType.Thermal =>
                composition.HasFlag(EnemyComposition.Flesh) ? 1.65f :
                composition.HasFlag(EnemyComposition.Gel) ? 1.40f :
                composition.HasFlag(EnemyComposition.Frozen) ? 1.40f : 1f,

            ChemicalDamageType.Cryonic =>
                composition.HasFlag(EnemyComposition.Metal) ? 1.75f :
                composition.HasFlag(EnemyComposition.Armored) ? 1.50f :
                composition.HasFlag(EnemyComposition.Incendiary) ? 1.40f : 1f,

            ChemicalDamageType.Caustic =>
                composition.HasFlag(EnemyComposition.Metal) ? 1.80f :
                composition.HasFlag(EnemyComposition.Armored) ? 1.60f :
                composition.HasFlag(EnemyComposition.Undead) ? 0.70f : 1f,

            ChemicalDamageType.Necrotic =>
                composition.HasFlag(EnemyComposition.None) ? 1.90f :
                composition.HasFlag(EnemyComposition.Flesh) ? 1.55f :
                composition.HasFlag(EnemyComposition.Undead) ? 0.50f : 1f,

            _ => 1f
        };
    }

    /// <summary>
    /// Flat defense reduction applied while a chemical reaction is active on this enemy.
    /// </summary>
    public static int GetReactionDefenseShred(ChemicalDamageType type, EnemyComposition composition)
    {
        return type switch
        {
            ChemicalDamageType.Caustic => composition.HasFlag(EnemyComposition.Metal) ? 16 :
                                            composition.HasFlag(EnemyComposition.Armored) ? 10 : 5,
            ChemicalDamageType.Cryonic => composition.HasFlag(EnemyComposition.Metal) ? 10 :
                                            composition.HasFlag(EnemyComposition.Armored) ? 6 : 2,
            _ => 0
        };
    }
    public static float GetReactionDamageMultiplier(ChemicalDamageType type, EnemyComposition composition)
    {
        return type switch
        {
            ChemicalDamageType.Thermal =>
                composition.HasFlag(EnemyComposition.Frozen) ? 2.50f :
                composition.HasFlag(EnemyComposition.Flesh) ? 1.50f :
                composition.HasFlag(EnemyComposition.Incendiary) ? 0.50f : 1f,

            ChemicalDamageType.Cryonic =>
                composition.HasFlag(EnemyComposition.Incendiary) ? 2.20f :
                composition.HasFlag(EnemyComposition.Metal) ? 1.60f :
                composition.HasFlag(EnemyComposition.Armored) ? 1.30f : 1f,

            ChemicalDamageType.Caustic =>
                composition.HasFlag(EnemyComposition.Metal) ? 2.00f :
                composition.HasFlag(EnemyComposition.Armored) ? 1.50f :
                composition.HasFlag(EnemyComposition.Undead) ? 0.80f : 1f,

            ChemicalDamageType.Necrotic =>
                composition.HasFlag(EnemyComposition.Flesh) ? 1.80f :
                composition.HasFlag(EnemyComposition.Undead) ? 0.40f : 1f,

            _ => 1f
        };
    }
}