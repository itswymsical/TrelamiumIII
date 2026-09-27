namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>One universal modifier per principle, each after the almanac's description of it. All numbers provisional.</summary>
public static class PrincipleModifiers
{
    /// <summary>Reactions heal the releasing player.</summary>
    public static readonly ChemicalModifier Vital = new ReactionLifestealModifier(nameof(Vital), share: 0.08f);

    /// <summary>Hits react likelier on targets carrying more concentration.</summary>
    public static readonly ChemicalModifier Specular = new ConcentrationReactionChanceModifier(nameof(Specular), perConcentration: 0.1f);

    /// <summary>Reactions deal more damage for each other element on the target.</summary>
    public static readonly ChemicalModifier Martial = new ReactionPerAfflictionModifier(nameof(Martial), perAffliction: 0.12f);

    /// <summary>Gas and residue expose more often.</summary>
    public static readonly ChemicalModifier Brisk = new StatModifier(nameof(Brisk), static (ref ChemicalStats s) => s.ContactRateMultiplier += 0.11f);

    /// <summary>Bursts throw more arms, and hits react likelier.</summary>
    public static readonly ChemicalModifier Fortunate = new StatModifier(nameof(Fortunate), static (ref ChemicalStats s) =>
    {
        s.BurstCountMultiplier += 0.18f;
        s.ReactionChance += 2f;
    });

    /// <summary>Hits also expose the nearest other enemies.</summary>
    public static readonly ChemicalModifier Harmonious = new HitSpreadModifier(nameof(Harmonious), targets: 2, range: 160f, strength: 0.2f);

    /// <summary>Reactions lower the target's defense.</summary>
    public static readonly ChemicalModifier Baneful = new ReactionDefenseShredModifier(nameof(Baneful), defense: 5, ticks: 240);
}

/// <summary>Universal modifiers beside the temperaments and principles, best to poor. All numbers provisional.</summary>
public static class UniversalModifiers
{
    /// <summary>Reactions deal more damage for each other enemy near the target.</summary>
    public static readonly ChemicalModifier Valiant = new ReactionCrowdModifier(nameof(Valiant), perEnemy: 0.09f, maxEnemies: 5, range: 96f);

    /// <summary>Hits give the releasing player stacking defense.</summary>
    public static readonly ChemicalModifier Gallant = new HitGuardModifier(nameof(Gallant), perHit: 1, cap: 7, ticks: 300);

    public static readonly ChemicalModifier Hazy = new StatModifier(nameof(Hazy), static (ref ChemicalStats s) =>
    {
        s.GasSpreadMultiplier += 0.13f;
        s.GasLifeMultiplier += 0.15f;
    });

    public static readonly ChemicalModifier Lofty = new StatModifier(nameof(Lofty), static (ref ChemicalStats s) =>
    {
        s.BurstSpeedMultiplier += 0.13f;
        s.BurstCountMultiplier += 0.07f;
    });

    public static readonly ChemicalModifier Valorous = new StatModifier(nameof(Valorous), static (ref ChemicalStats s) =>
    {
        s.Concentration += 3;
        s.ReactionDamageMultiplier += 0.15f;
    });

    public static readonly ChemicalModifier Waning = new StatModifier(nameof(Waning), static (ref ChemicalStats s) =>
    {
        s.GasLifeMultiplier -= 0.2f;
        s.DurationMultiplier -= 0.15f;
    });

    public static readonly ChemicalModifier Suppressed = new StatModifier(nameof(Suppressed), static (ref ChemicalStats s) => s.ReactionChance -= 5f);

    public static readonly ChemicalModifier Recreant = new StatModifier(nameof(Recreant), static (ref ChemicalStats s) =>
    {
        s.ContactRateMultiplier -= 0.2f;
        s.GasSpreadMultiplier -= 0.1f;
    });

    public static readonly ChemicalModifier Craven = new StatModifier(nameof(Craven), static (ref ChemicalStats s) =>
    {
        s.ClingChanceMultiplier -= 0.25f;
        s.ClingDurationMultiplier -= 0.2f;
    });
}

/// <summary>Modifiers only thermal weapons roll. All numbers provisional.</summary>
public static class ThermalModifiers
{
    private const ChemicalDamageType Thermal = ChemicalDamageType.Thermal;

    /// <summary>Hits that don't react make the next one on the target likelier to.</summary>
    public static readonly ChemicalModifier Dauntless = new ReactionPityModifier(nameof(Dauntless), Thermal, step: 3f, cap: 30f)
        .WithStats(static (ref ChemicalStats s) => s.ReactionChance += 4f);

    /// <summary>Reactions burn off the releasing player's gas around the target in a blast that grows with it.</summary>
    public static readonly ChemicalModifier Comburant = new GasIgnitionModifier(nameof(Comburant), Thermal, radius: 128f, fullFuel: 25, maxShare: 2f);

    /// <summary>Reactions spend the target's concentration on a single harder burst.</summary>
    public static readonly ChemicalModifier Inflammatory = new ConcentrationBurstModifier(nameof(Inflammatory), Thermal, bonus: 1.5f);

    /// <summary>Reactions chain from enemy to enemy.</summary>
    public static readonly ChemicalModifier Provocative = new ReactionChainModifier(nameof(Provocative), Thermal, leaps: 3, range: 128f, falloff: 0.6f);

    /// <summary>Reactions blast the enemies around the target.</summary>
    public static readonly ChemicalModifier Combustible = new ReactionBlastModifier(nameof(Combustible), Thermal, radius: 96f, damageShare: 0.5f, exposure: 0.5f);

    public static readonly ChemicalModifier Igniting = new StatModifier(nameof(Igniting), static (ref ChemicalStats s) =>
    {
        s.AfflictionRate *= 0.8f;
        s.ReactionChance += 3f;
    }, Thermal);

    public static readonly ChemicalModifier Dwindling = new StatModifier(nameof(Dwindling), static (ref ChemicalStats s) => s.DurationMultiplier -= 0.1f, Thermal);

    public static readonly ChemicalModifier Warm = new StatModifier(nameof(Warm), static (ref ChemicalStats s) => s.DamageMultiplier -= 0.2f, Thermal);

    public static readonly ChemicalModifier Meek = new StatModifier(nameof(Meek), static (ref ChemicalStats s) => s.ReactionDamageMultiplier -= 0.14f, Thermal);

    public static readonly ChemicalModifier Amicable = new StatModifier(nameof(Amicable), static (ref ChemicalStats s) =>
    {
        s.Concentration -= 2;
        s.ClingChanceMultiplier -= 0.17f;
    }, Thermal);
}

/// <summary>Modifiers only cryonic weapons roll. All numbers provisional.</summary>
public static class CryonicModifiers
{
    private const ChemicalDamageType Cryonic = ChemicalDamageType.Cryonic;

    /// <summary>Everything the release exposes is <see cref="Chilled" />, and its gas lasts longer.</summary>
    public static readonly ChemicalModifier Wintry = new ExposureBuffModifier<Chilled>(nameof(Wintry), Cryonic, ticks: 60)
        .WithStats(static (ref ChemicalStats s) => s.GasLifeMultiplier += 0.14f);

    /// <summary>Reactions freeze the target; a reaction on a frozen target shatters it.</summary>
    public static readonly ChemicalModifier Hyperborean = new ReactionFreezeModifier(nameof(Hyperborean), Cryonic, ticks: 90, shatter: 1f);

    /// <summary>Reactions chill the target, and afflictions last longer.</summary>
    public static readonly ChemicalModifier Glacial = new ReactionBuffModifier<Chilled>(nameof(Glacial), Cryonic, ticks: 180)
        .WithStats(static (ref ChemicalStats s) => s.DurationMultiplier += 0.13f);

    public static readonly ChemicalModifier Brumal = new StatModifier(nameof(Brumal), static (ref ChemicalStats s) =>
    {
        s.GasLifeMultiplier += 0.35f;
        s.GasSpreadMultiplier += 0.09f;
    }, Cryonic);

    public static readonly ChemicalModifier Vaporous = new StatModifier(nameof(Vaporous), static (ref ChemicalStats s) =>
    {
        s.GasSpreadMultiplier += 0.09f;
        s.ContactRateMultiplier += 0.13f;
        s.GasLifeMultiplier -= 0.11f;
    }, Cryonic);

    public static readonly ChemicalModifier Temperate = new StatModifier(nameof(Temperate), static (ref ChemicalStats s) => s.Concentration -= 3, Cryonic);

    public static readonly ChemicalModifier Heated = new StatModifier(nameof(Heated), static (ref ChemicalStats s) =>
    {
        s.GasLifeMultiplier -= 0.18f;
        s.DurationMultiplier -= 0.09f;
    }, Cryonic);

    public static readonly ChemicalModifier Lukewarm = new StatModifier(nameof(Lukewarm), static (ref ChemicalStats s) =>
    {
        s.ReactionChance -= 4f;
        s.ReactionDamageMultiplier -= 0.1f;
    }, Cryonic);

    public static readonly ChemicalModifier Nippy = new StatModifier(nameof(Nippy), static (ref ChemicalStats s) =>
    {
        s.DurationMultiplier -= 0.15f;
        s.GasSpreadMultiplier -= 0.1f;
    }, Cryonic);
}

/// <summary>Modifiers only necrotic weapons roll. The third good slot is unnamed. All numbers provisional.</summary>
public static class NecroticModifiers
{
    private const ChemicalDamageType Necrotic = ChemicalDamageType.Necrotic;

    /// <summary>An enemy that dies afflicted bursts, afflicting everything nearby.</summary>
    public static readonly ChemicalModifier Vitiating = new DeathBurstModifier(nameof(Vitiating), Necrotic, radius: 128f, strength: 1f, maxGeneration: 3);

    /// <summary>Enemies it afflicts are <see cref="Enfeebled" /> for as long as the affliction.</summary>
    public static readonly ChemicalModifier Nullifying = new ExposureBuffModifier<Enfeebled>(nameof(Nullifying), Necrotic, ticks: 0);

    /// <summary>Reactions end every affliction on the target and deal what was left at once.</summary>
    public static readonly ChemicalModifier Abolishing = new AfflictionDetonateModifier(nameof(Abolishing), Necrotic);

    public static readonly ChemicalModifier Umbral = new StatModifier(nameof(Umbral), static (ref ChemicalStats s) =>
    {
        s.ClingChanceMultiplier += 0.13f;
        s.ClingDurationMultiplier += 0.2f;
    }, Necrotic);

    public static readonly ChemicalModifier Deterring = new StatModifier(nameof(Deterring), static (ref ChemicalStats s) => s.ClingChanceMultiplier -= 0.15f, Necrotic);

    public static readonly ChemicalModifier Halting = new StatModifier(nameof(Halting), static (ref ChemicalStats s) => s.AfflictionRate *= 1.17f, Necrotic);

    public static readonly ChemicalModifier Exhorting = new StatModifier(nameof(Exhorting), static (ref ChemicalStats s) =>
    {
        s.ReactionDamageMultiplier -= 0.11f;
        s.ReactionChance -= 2f;
    }, Necrotic);

    public static readonly ChemicalModifier Permissive = new StatModifier(nameof(Permissive), static (ref ChemicalStats s) => s.DamageMultiplier -= 0.17f, Necrotic);
}

/// <summary>Modifiers only caustic weapons roll. All numbers provisional.</summary>
public static class CausticModifiers
{
    private const ChemicalDamageType Caustic = ChemicalDamageType.Caustic;

    /// <summary>Afflictions pass to nearby enemies that don't have them yet, up to three hops out.</summary>
    public static readonly ChemicalModifier Virulent = new ContagionModifier(nameof(Virulent), Caustic, chance: 0.35f, range: 96f, strength: 0.75f, maxGeneration: 3);

    /// <summary>Each affliction tick strips more defense.</summary>
    public static readonly ChemicalModifier Acerbic = new TickShredModifier(nameof(Acerbic), Caustic, perTick: 1, cap: 20);

    public static readonly ChemicalModifier Pungent = new StatModifier(nameof(Pungent), static (ref ChemicalStats s) =>
    {
        s.GasSpreadMultiplier += 0.3f;
        s.ContactRateMultiplier += 0.2f;
    }, Caustic);

    public static readonly ChemicalModifier Erosive = new StatModifier(nameof(Erosive), static (ref ChemicalStats s) =>
    {
        s.DurationMultiplier += 0.16f;
        s.GasLifeMultiplier += 0.11f;
    }, Caustic);

    /// <summary>Hits strip defense, stacking.</summary>
    public static readonly ChemicalModifier Corrosive = new HitShredModifier(nameof(Corrosive), Caustic, perHit: 4, cap: 7, ticks: 300);

    public static readonly ChemicalModifier Tart = new StatModifier(nameof(Tart), static (ref ChemicalStats s) => s.DamageMultiplier -= 0.11f, Caustic);

    public static readonly ChemicalModifier Tickling = new StatModifier(nameof(Tickling), static (ref ChemicalStats s) => s.ReactionDamageMultiplier -= 0.13f, Caustic);

    public static readonly ChemicalModifier Balanced = new StatModifier(nameof(Balanced), static (ref ChemicalStats s) =>
    {
        s.ReactionChance -= 4f;
        s.Concentration -= 2;
    }, Caustic);

    public static readonly ChemicalModifier Flat = new StatModifier(nameof(Flat), static (ref ChemicalStats s) =>
    {
        s.GasSpreadMultiplier -= 0.09f;
        s.GasLifeMultiplier -= 0.11f;
    }, Caustic);
}

/// <summary>Modifiers only kinetic weapons roll. All numbers provisional.</summary>
public static class KineticModifiers
{
    private const ChemicalDamageType Kinetic = ChemicalDamageType.Kinetic;

    /// <summary>Reactions ignore defense and hit a little harder.</summary>
    public static readonly ChemicalModifier Stalwart = new ReactionPierceModifier(nameof(Stalwart), Kinetic)
        .WithStats(static (ref ChemicalStats s) => s.ReactionDamageMultiplier += 0.1f);

    /// <summary>Concentration holds while the target is afflicted, and clings last longer.</summary>
    public static readonly ChemicalModifier Tenacious = new ConcentrationHoldModifier(nameof(Tenacious), Kinetic)
        .WithStats(static (ref ChemicalStats s) => s.ClingDurationMultiplier += 0.13f);

    /// <summary>Reactions arc to the nearest other enemies.</summary>
    public static readonly ChemicalModifier Electrifying = new ReactionArcModifier(nameof(Electrifying), Kinetic, arcs: 2, range: 128f, scale: 0.5f);

    /// <summary>Reactions hit harder the less life the target has left.</summary>
    public static readonly ChemicalModifier Terminal = new ReactionExecuteModifier(nameof(Terminal), Kinetic, bonus: 0.6f);

    public static readonly ChemicalModifier Vitalized = new StatModifier(nameof(Vitalized), static (ref ChemicalStats s) =>
    {
        s.AfflictionRate /= 1.2f;
        s.BurstSpeedMultiplier += 0.15f;
    }, Kinetic);

    public static readonly ChemicalModifier Apathetic = new StatModifier(nameof(Apathetic), static (ref ChemicalStats s) =>
    {
        s.ReactionChance -= 4f;
        s.ClingChanceMultiplier -= 0.1f;
    }, Kinetic);

    public static readonly ChemicalModifier Inactive = new StatModifier(nameof(Inactive), static (ref ChemicalStats s) => s.AfflictionRate *= 1.11f, Kinetic);

    public static readonly ChemicalModifier Unmotivated = new StatModifier(nameof(Unmotivated), static (ref ChemicalStats s) =>
    {
        s.BurstSpeedMultiplier -= 0.21f;
        s.BurstCountMultiplier -= 0.03f;
    }, Kinetic);

    public static readonly ChemicalModifier Wimpy = new StatModifier(nameof(Wimpy), static (ref ChemicalStats s) => s.ReactionDamageMultiplier -= 0.12f, Kinetic);
}
