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
    /// <summary>Not chemical at all. The sentinel for a source that emits no chemistry.</summary>
    /// <remarks>
    ///     Distinct from <see cref="Solute" />, and the distinction matters: this one means "there is
    ///     no chemistry here", and the delivery path drops out on it before anything is applied.
    /// </remarks>
    None,

    /// <summary>
    ///     Chemistry with no element: matter that controls by being in the way.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Gel, powders and inert gas. A solute afflicts over time exactly as any other source
    ///         does, and that is all it does -- no composition matchup in either direction, and no
    ///         reaction, because a reaction is the elemental payoff and a solute has no element to
    ///         pay off with. What it has instead is the zone, which is the thing the whole class is
    ///         actually built around.
    ///     </para>
    ///     <para>
    ///         This is the generic membership case. A weapon does not need an element to belong to
    ///         the controller class -- it needs to control an area -- and without a case like this
    ///         one, every gel or powder weapon would either have to fake an element it does not have
    ///         or live outside the system that makes its residue and its cloud work.
    ///     </para>
    /// </remarks>
    Solute,

    Thermal,
    Cryonic,
    Caustic,
    Necrotic,
    Kinetic,
    Electric // unused for now | may become alchemical elements in the future
}

public static class ChemicalMatchup
{
    /// <summary>
    ///     Whether this type takes part in the composition matchup and can produce reactions.
    /// </summary>
    /// <remarks>
    ///     The three tables below all fall through to a neutral answer for a non-elemental type, so
    ///     nothing strictly needs this. It exists so the intent is stated once and testable, rather
    ///     than being an emergent property of three default switch arms that a later edit could
    ///     quietly change -- and so <see cref="ChemicalReaction.Trigger" /> has something to ask.
    /// </remarks>
    public static bool IsElemental(ChemicalDamageType type)
        => type is not (ChemicalDamageType.None or ChemicalDamageType.Solute);

    /// <summary>
    /// Multiplier applied to affliction damage per tick based on chemical type vs enemy composition.
    /// </summary>
    /// <remarks>
    ///     A non-elemental type lands on the default arm and takes a flat 1x against everything,
    ///     which is the whole of <see cref="ChemicalDamageType.Solute" />'s combat identity: it does
    ///     not care what it is dissolving, and nothing resists it either.
    /// </remarks>
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