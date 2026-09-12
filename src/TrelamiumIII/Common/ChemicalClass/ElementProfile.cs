using System.Collections.Generic;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     The world-wide definition of an element: its contact effects and settled appearance. Also an
///     <see cref="IChemicalSource" />, so weapon-less hazards can afflict via <see cref="ChemicalReaction.Trigger" />.
/// </summary>
public sealed record ElementProfile : IChemicalSource
{
    public ChemicalDamageType ChemicalType { get; init; }

    public float ChemicalDamage { get; init; } = 0.35f;

    public int AfflictionDuration { get; init; } = 60;

    public float AfflictionRate { get; init; } = 30f;

    public float ReactionChance { get; init; } = 0.01f;

    public float ReactionDamage { get; init; }

    /// <summary>Vanilla debuff applied on contact, or -1 (most elements) to rely on the affliction alone.</summary>
    public int ContactBuff { get; init; } = -1;

    public int ContactBuffTicks { get; init; } = 90;

    /// <summary>Whether the element is a hazard to the player who put it there, not just to targets.</summary>
    public bool HarmsPlayers { get; init; }

    /// <summary>The element's colour where it lies thick.</summary>
    public Vector3 BodyColor { get; init; } = new(0.35f, 0.35f, 0.38f);

    /// <summary>Its colour at the rim, where it is thin and catching the light.</summary>
    public Vector3 EdgeColor { get; init; } = new(0.72f, 0.74f, 0.80f);

    public Vector3 EmittedLight { get; init; }

    /// <summary>
    ///     Summed blob coverage a pixel needs to be inside the pool. Small because overlapping blobs sum past 1
    ///     inside a pool; higher reads thicker, lower spreads thinner.
    /// </summary>
    public float Threshold { get; init; } = 0.17f;

    public float NoiseScale { get; init; } = 5.5f;

    public Vector2 NoiseDrift { get; init; } = new(0.01f, -0.014f);

    /// <summary>Nothing by default: element-scoped reaction behaviour belongs to whatever emitted it.</summary>
    public void OnChemicalReaction(in ChemicalReactionEvent e) { }

    /// <summary>Fire and anything else too hot. Sets targets on fire through its contact debuff.</summary>
    public static readonly ElementProfile Thermal = new()
    {
        ChemicalType = ChemicalDamageType.Thermal,
        ChemicalDamage = 0.5f,
        AfflictionDuration = 90,
        ReactionChance = 0.015f,
        ReactionDamage = 1f,
        ContactBuff = BuffID.OnFire,
        HarmsPlayers = true,
        BodyColor = new Vector3(0.52f, 0.14f, 0.02f),
        EdgeColor = new Vector3(1.00f, 0.62f, 0.16f),
        EmittedLight = new Vector3(0.34f, 0.16f, 0.03f),
        NoiseDrift = new Vector2(0.014f, -0.032f),
    };

    /// <summary>Cold: controls by removing friction rather than hurting. No contact debuff on purpose.</summary>
    public static readonly ElementProfile Cryonic = new()
    {
        ChemicalType = ChemicalDamageType.Cryonic,
        ChemicalDamage = 0.28f,
        BodyColor = new Vector3(0.58f, 0.74f, 0.92f),
        EdgeColor = new Vector3(0.90f, 0.98f, 1.00f),
        EmittedLight = new Vector3(0.05f, 0.09f, 0.14f),
        Threshold = 0.20f,
        NoiseScale = 8f,
        NoiseDrift = new Vector2(0.003f, -0.004f),
    };

    /// <summary>Acids and spores: chemistry that eats what it lands on.</summary>
    public static readonly ElementProfile Caustic = new()
    {
        ChemicalType = ChemicalDamageType.Caustic,
        ChemicalDamage = 0.4f,
        ContactBuff = BuffID.Poisoned,
        BodyColor = new Vector3(0.24f, 0.34f, 0.06f),
        EdgeColor = new Vector3(0.72f, 0.92f, 0.22f),
        EmittedLight = new Vector3(0.05f, 0.10f, 0.02f),
    };

    /// <summary>Decay, rot and blood. No contact debuff; corrupt spores request bleeding themselves.</summary>
    public static readonly ElementProfile Necrotic = new()
    {
        ChemicalType = ChemicalDamageType.Necrotic,
        ChemicalDamage = 0.3f,
        BodyColor = new Vector3(0.26f, 0.04f, 0.10f),
        EdgeColor = new Vector3(0.74f, 0.12f, 0.28f),
        Threshold = 0.15f,
        NoiseScale = 4.5f,
    };

    /// <summary>Force: impact, pressure, shrapnel and starlight.</summary>
    public static readonly ElementProfile Kinetic = new()
    {
        ChemicalType = ChemicalDamageType.Kinetic,
        ChemicalDamage = 0.32f,
        BodyColor = new Vector3(0.40f, 0.38f, 0.58f),
        EdgeColor = new Vector3(0.95f, 0.94f, 1.00f),
        EmittedLight = new Vector3(0.08f, 0.08f, 0.14f),
    };

    /// <summary>Current. Registered ahead of use; nothing emits it yet.</summary>
    public static readonly ElementProfile Electric = new()
    {
        ChemicalType = ChemicalDamageType.Electric,
        ChemicalDamage = 0.45f,
        AfflictionDuration = 45,
        AfflictionRate = 20f,
        BodyColor = new Vector3(0.18f, 0.26f, 0.52f),
        EdgeColor = new Vector3(0.72f, 0.92f, 1.00f),
        EmittedLight = new Vector3(0.10f, 0.14f, 0.24f),
        Threshold = 0.22f,
        NoiseScale = 7f,
        NoiseDrift = new Vector2(0.05f, -0.01f),
    };

    /// <summary>Matter with no element (gel, powders, inert gas): afflicts but never reacts.</summary>
    public static readonly ElementProfile Solute = new()
    {
        ChemicalType = ChemicalDamageType.Solute,
        ChemicalDamage = 0.25f,
        ReactionChance = 0f,
        ContactBuff = BuffID.Slow,
        BodyColor = new Vector3(0.16f, 0.42f, 0.30f),
        EdgeColor = new Vector3(0.55f, 0.95f, 0.70f),
    };

    /// <summary>Reactive raw material, awaiting a use.</summary>
    public static readonly ElementProfile Reagent = new()
    {
        ChemicalType = ChemicalDamageType.Reagent,
        ChemicalDamage = 0.2f,
        ReactionChance = 0f,
    };

    /// <summary>The absence of chemistry, so that <see cref="For" /> always has an answer.</summary>
    public static readonly ElementProfile None = new()
    {
        ChemicalType = ChemicalDamageType.None,
        ChemicalDamage = 0f,
        ReactionChance = 0f,
    };

    private static readonly Dictionary<ChemicalDamageType, ElementProfile> registry = new()
    {
        [ChemicalDamageType.None] = None,
        [ChemicalDamageType.Solute] = Solute,
        [ChemicalDamageType.Reagent] = Reagent,
        [ChemicalDamageType.Thermal] = Thermal,
        [ChemicalDamageType.Cryonic] = Cryonic,
        [ChemicalDamageType.Caustic] = Caustic,
        [ChemicalDamageType.Necrotic] = Necrotic,
        [ChemicalDamageType.Kinetic] = Kinetic,
        [ChemicalDamageType.Electric] = Electric,
    };

    /// <summary>Never null: unregistered types return <see cref="None" />.</summary>
    public static ElementProfile For(ChemicalDamageType type)
        => registry.TryGetValue(type, out ElementProfile? profile) ? profile : None;
}
