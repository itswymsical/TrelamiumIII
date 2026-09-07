using System.Collections.Generic;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     How far along a fire is, in bands.
/// </summary>
/// <remarks>
///     Bands rather than a raw number because rules should not flicker. A fire that spreads above
///     some heat and stops below it would, on a threshold, be switching on and off every tick as the
///     level wobbled across the line; stepping the level into a stage and branching on the stage
///     means a fire that has caught stays caught until it has genuinely fallen a band.
/// </remarks>
public enum BurnStage : byte
{
    /// <summary>Barely alight. Embers and a little light; no spread, no flame.</summary>
    Smouldering,

    /// <summary>Taking hold. Visible heat, still climbing.</summary>
    Kindling,

    /// <summary>Established and spreading.</summary>
    Burning,

    /// <summary>
    ///     Peak heat. The band at which a fire is hot enough to host a flame of its own.
    /// </summary>
    /// <remarks>
    ///     Nothing spawns one yet -- the element entities are a later step -- but this is the
    ///     threshold they will attach at, and it is why the stage exists as its own band rather than
    ///     as the top of <see cref="Burning" />.
    /// </remarks>
    Raging,

    /// <summary>Burnt out. Heat gone, char left, waiting for the mark to lift.</summary>
    Spent,
}

/// <summary>
///     One kind of fire, as numbers: how hot it starts, how it climbs, how fast it blackens what it
///     is standing on, and whether it is allowed to eat it.
/// </summary>
/// <remarks>
///     <para>
///         A reference type with a registry, unlike the burst and gun profiles, and the difference is
///         forced by where it has to live. Those are read once at the moment something is spawned; a
///         burn profile has to still be readable on the tile six seconds later, and conditions are
///         flyweights that hold no per-tile state. So the tile stores an index --
///         <see cref="TileConditionInstance.Variant" /> -- and this is what the index points into.
///         One byte per burning tile instead of forty.
///     </para>
///     <para>
///         Registration is by static field initialiser, so the index a profile gets is its
///         declaration order here. That is stable across clients, which matters because the variant
///         rides along with a synced application.
///     </para>
/// </remarks>
public sealed record BurnProfile
{
    private static readonly List<BurnProfile> registry = [];

    /// <summary>This profile's index, and what a tile stores to find its way back here.</summary>
    public byte Id { get; private init; }

    /// <summary>Heat a freshly lit tile starts at, 0..1.</summary>
    public float Ignition { get; init; } = 0.25f;

    /// <summary>Highest heat this fire can climb to. Below 1 for something that never really rages.</summary>
    public float Peak { get; init; } = 1f;

    /// <summary>Heat gained per tick while the fire is still being fed.</summary>
    public float HeatGain { get; init; } = 0.010f;

    /// <summary>Heat lost per tick once it is past its fuel.</summary>
    public float HeatDecay { get; init; } = 0.006f;

    /// <summary>
    ///     Fraction of its life the fire spends climbing before it starts to die.
    /// </summary>
    /// <remarks>
    ///     What makes the heat curve a curve rather than a ramp. Fuel is not modelled as a quantity
    ///     -- there is nothing on a tile to spend -- so the fire's own remaining life stands in for
    ///     it, and this is where the turn happens.
    /// </remarks>
    public float FuelFraction { get; init; } = 0.45f;

    /// <summary>
    ///     How fast the black char creeps over the tile, in progress per second.
    /// </summary>
    /// <remarks>
    ///     The speed knob asked for by name. Char is derived from the instance's age rather than
    ///     stored, which is what lets it be a rate here instead of a second float on every burning
    ///     tile: it only ever advances, so age times rate is the whole of it. Scorch first, black
    ///     afterwards, at whatever pace the content wants.
    /// </remarks>
    public float CharRate { get; init; } = 0.11f;

    /// <summary>Per-tick chance a <see cref="BurnStage.Burning" /> tile lights a neighbour.</summary>
    /// <remarks>
    ///     Rolled once per tile per tick and scaled by heat, so a wall of fire spreads faster than a
    ///     single lit block -- which is the behaviour that makes spreading fire feel like fire rather
    ///     than like a flood fill with a delay.
    /// </remarks>
    public float SpreadChance { get; init; }

    /// <summary>Heat a neighbour is lit at, as a fraction of the parent's.</summary>
    public float SpreadFalloff { get; init; } = 0.7f;

    /// <summary>
    ///     Whether this fire may destroy a flammable tile it burns out on.
    /// </summary>
    /// <remarks>
    ///     Off by default and deliberately per-profile rather than global. Fire that eats terrain is
    ///     a good effect and a terrible default -- the difference between a dramatic moment and a
    ///     player's house gone -- so it is something a piece of content opts into for a specific
    ///     weapon or event, and never something the system does on its own.
    /// </remarks>
    public bool ConsumesTiles { get; init; }

    public float Duration { get; init; } = 13f * 60f;

    /// <summary>Colour of the fire at full heat, before the char takes it.</summary>
    public Vector3 HotColor { get; init; } = new(1.00f, 0.86f, 0.42f);

    /// <summary>Colour it passes through on the way down.</summary>
    public Vector3 CoolColor { get; init; } = new(0.85f, 0.24f, 0.04f);

    /// <summary>
    ///     An ordinary fire: catches slowly, does not spread, leaves char.
    /// </summary>
    /// <remarks>
    ///     What <c>Scorch</c> used before profiles existed, so every call site that has not asked for
    ///     anything in particular keeps behaving as it did.
    /// </remarks>
    public static readonly BurnProfile Standard = Register(new BurnProfile());

    /// <summary>A brief scorch mark with no life in it: the scuff left by a glancing hit.</summary>
    public static readonly BurnProfile Scuff = Register(new BurnProfile
    {
        Ignition = 0.15f,
        Peak = 0.4f,
        HeatGain = 0.004f,
        HeatDecay = 0.010f,
        FuelFraction = 0.2f,
        CharRate = 0.22f,
        Duration = 6f * 60f,
    });

    /// <summary>
    ///     Thermite: starts hot, stays hot, blackens fast.
    /// </summary>
    /// <remarks>
    ///     Does not spread, which is not an oversight. Thermite burns ferociously exactly where it
    ///     was poured and has no interest in the block next door; the area it denies is the area the
    ///     player aimed at, which is the whole contract of a controller weapon.
    /// </remarks>
    public static readonly BurnProfile Thermal = Register(new BurnProfile
    {
        Ignition = 0.75f,
        Peak = 1f,
        HeatGain = 0.020f,
        HeatDecay = 0.004f,
        FuelFraction = 0.6f,
        CharRate = 0.16f,
        Duration = 11f * 60f,
        HotColor = new Vector3(1.00f, 0.94f, 0.72f),
        CoolColor = new Vector3(0.92f, 0.30f, 0.02f),
    });

    /// <summary>
    ///     Fire that spreads and eats what it spreads across.
    /// </summary>
    /// <remarks>
    ///     The only registered profile with <see cref="ConsumesTiles" /> set. Nothing lights it yet;
    ///     it exists so that the switch has a worked example and so the content that eventually wants
    ///     it is naming a profile rather than inventing one.
    /// </remarks>
    public static readonly BurnProfile Wildfire = Register(new BurnProfile
    {
        Ignition = 0.35f,
        HeatGain = 0.014f,
        HeatDecay = 0.005f,
        FuelFraction = 0.55f,
        CharRate = 0.09f,
        SpreadChance = 0.010f,
        ConsumesTiles = true,
        Duration = 16f * 60f,
    });

    private static BurnProfile Register(BurnProfile profile)
    {
        var registered = profile with { Id = (byte)registry.Count };

        registry.Add(registered);

        return registered;
    }

    /// <summary>The profile a tile's variant refers to, falling back to <see cref="Standard" />.</summary>
    public static BurnProfile Get(byte id) => id < registry.Count ? registry[id] : Standard;

    /// <summary>The band a heat level falls in.</summary>
    /// <remarks>
    ///     Deliberately not evenly spaced. Most of a fire's visible life is spent in the middle two
    ///     bands, and the top one is narrow because raging is meant to be a moment rather than a
    ///     plateau.
    /// </remarks>
    public static BurnStage StageOf(float level) => level switch
    {
        >= 0.82f => BurnStage.Raging,
        >= 0.50f => BurnStage.Burning,
        >= 0.22f => BurnStage.Kindling,
        > 0.02f => BurnStage.Smouldering,
        _ => BurnStage.Spent,
    };
}
