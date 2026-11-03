using System.Collections.Generic;

namespace TrelamiumIII.Core.Residues.Types;

public enum BurnStage : byte
{
    Smouldering,
    Kindling,
    Burning,
    MandemTisPeakHeat,
    BurntOut,
}

/// <summary>
///     One kind of fire, as numbers: how hot it starts, how it climbs, how fast it blackens what it
///     is standing on, and whether it is allowed to eat it.
/// </summary>
/// <remarks>
///     <para>
///         A reference type with a registry, unlike the burst and gun profiles, because it has to stay
///         readable on the tile long after ignition and residues hold no per-tile state. The tile
///         stores an index (<see cref="ResidueInstance.Variant" />) into this registry: one
///         byte per burning tile instead of forty.
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

    public byte Id { get; private init; }

    public float Ignition { get; init; } = 0.25f;

    public float Peak { get; init; } = 1f;

    public float HeatGain { get; init; } = 0.010f;

    public float HeatDecay { get; init; } = 0.006f;

    public float FuelFraction { get; init; } = 0.45f;

    public float CharRate { get; init; } = 0.11f;

    public float SpreadChance { get; init; }

    public float SpreadFalloff { get; init; } = 0.7f;

    /// <summary>Only spreads onto tiles carrying something flammable, so it runs along a trail rather than across bare rock.</summary>
    public bool SpreadsAlongFuel { get; init; }

    public bool ConsumesTiles { get; init; }

    /// <summary>Whether <see cref="ConsumesTiles" /> takes anything an explosion could break, not just what burns.</summary>
    public bool ConsumesAnyTile { get; init; }

    public float Duration { get; init; } = 13f * 60f;

    public Vector3 HotColor { get; init; } = new(1.00f, 0.86f, 0.42f);

    public Vector3 CoolColor { get; init; } = new(0.85f, 0.24f, 0.04f);

    public static readonly BurnProfile Standard = Register(new BurnProfile());

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

    /// <summary>Gel that caught: runs along the gel beside it, and eats through the tile under it once it burns out.</summary>
    public static readonly BurnProfile Combustion = Register(new BurnProfile
    {
        Ignition = 0.7f,
        HeatGain = 0.018f,
        HeatDecay = 0.005f,
        FuelFraction = 0.6f,
        CharRate = 0.16f,
        SpreadChance = 0.45f,
        SpreadsAlongFuel = true,
        ConsumesTiles = true,
        ConsumesAnyTile = true,
        Duration = 10f * 60f,
    });

    /// <summary>A fire that's out: no heat left and none to come, only its char fading. See <see cref="BurnedResidue" />.</summary>
    public static readonly BurnProfile Doused = Register(new BurnProfile
    {
        Ignition = 0f,
        Peak = 0f,
        FuelFraction = 0f,
    });

    private static BurnProfile Register(BurnProfile profile)
    {
        var registered = profile with { Id = (byte)registry.Count };

        registry.Add(registered);

        return registered;
    }

    public static BurnProfile Get(byte id) => id < registry.Count ? registry[id] : Standard;

    public static BurnStage StageOf(float level) => level switch
    {
        >= 0.82f => BurnStage.MandemTisPeakHeat,
        >= 0.50f => BurnStage.Burning,
        >= 0.22f => BurnStage.Kindling,
        > 0.02f => BurnStage.Smouldering,
        _ => BurnStage.BurntOut,
    };
}
