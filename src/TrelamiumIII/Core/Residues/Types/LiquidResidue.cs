using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues.Types;

/// <summary>
///     A stain where liquid sits against the terrain, in that liquid's colour: the bed of a pool, the
///     bank of a river. Dries out once the liquid has gone.
/// </summary>
/// <remarks>
///     The palette is grey and the tint is the whole colour, so any liquid -- vanilla, a modded one,
///     one of oil's conditions -- stains in its own hue with no extra work. Liquid draws over the solid
///     layer, so a stain under a pool shows through the water rather than over it.
/// </remarks>
public sealed class LiquidResidue : FilmResidue
{
    /// <summary>The coat of anything wet, re-dyed by whatever tint it's laid with. Rain wears it too; see <see cref="DampResidue" />.</summary>
    internal static readonly FilmLook WetLook = new()
    {
        Threshold = 0.18f,
        NoiseScale = 0.05f,
        NoiseStrength = 0.22f,
        NoiseDrift = new Vector2(0.002f, 0.006f),
        Bands = 3f,
        ShadowColor = new Vector3(0.3f),
        BodyColor = new Vector3(0.5f),
        CoreColor = new Vector3(0.62f),
        BodyAlpha = 0.42f,
        BodyAlphaFloor = 0.5f,
        OutlineColor = new Vector3(0.36f),
        OutlineAlpha = 0.5f,
        HighlightColor = new Vector3(0.95f),
        HighlightAlpha = 0.4f,
        EmissiveEdge = 0.15f,
        TintStrength = 1f,
        TintWash = 0.75f,
    };

    protected override FilmLook Look => WetLook;

    public override float Duration => 20f * 60f;

    public override int MaxTiles => 2500;

    public override int FadeInTicks => 30;

    public override int FadeOutTicks => 60;

    /// <summary>Runs straight off.</summary>
    public override Bodies.BodyFlow BodyFlow => new(Rise: 0f, Fall: 0.12f, Creep: 0.03f, Sag: 0.07f, Decay: 0.005f, Reach: 0.3f);

    public override Bodies.BodyShed? BodyShed => new(Rate: 0.3f, Radius: 3.5f, Life: 24f, Launch: new Vector2(0f, 0.3f), Scatter: 0.2f, Pull: new Vector2(0f, 0.16f), Drag: 0.99f, Cling: 0.2f);

    public override IResidue? BodyPickup(in ResidueInstance instance, out float amount)
    {
        amount = instance.Strength * 0.04f;

        return instance.Variant == LiquidID.Water ? this : null;
    }

    /// <summary>Water soaks; anything else is only a stain. <see cref="ResidueInstance.Variant" /> holds which liquid left it.</summary>
    public override ResidueElement ElementsOf(in ResidueInstance instance)
        => instance.Variant == LiquidID.Water ? ResidueElement.Water : ResidueElement.None;

    /// <summary>Still in contact, so still wet: drying starts over.</summary>
    public override void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount)
        => instance.Life = instance.MaxLife;

    /// <summary>
    ///     The colour liquid of <paramref name="liquidType" /> stains with at (<paramref name="i" />,
    ///     <paramref name="j" />).
    /// </summary>
    /// <remarks>
    ///     A conditional liquid is drawn as its condition's tint over grey, so that tint is its colour.
    ///     Anything else takes its map colour, lifted a little where the map's is too dark to read as
    ///     a stain.
    /// </remarks>
    public static Color TintOf(int i, int j, int liquidType)
    {
        if (InternalLiquidSet.ByType(liquidType) is { } set && set.InternalLiqAt(i, j).Tint is var tint && tint != Color.White)
            return tint;

        return liquidType switch
        {
            LiquidID.Water => new Color(40, 90, 170),
            LiquidID.Lava => new Color(253, 80, 18),
            LiquidID.Honey => new Color(254, 194, 20),
            LiquidID.Shimmer => new Color(161, 127, 255),
            _ => new Color(120, 120, 130),
        };
    }

    /// <summary>Stains (<paramref name="i" />, <paramref name="j" />) with whatever liquid is at (<paramref name="liquidX" />, <paramref name="liquidY" />).</summary>
    public static bool Stain(int i, int j, int liquidX, int liquidY, float intensity = 1f)
    {
        Tile liquid = Main.tile[liquidX, liquidY];

        return ResidueSystem.Apply<LiquidResidue>(i, j, intensity, origin: ResidueOrigin.Local, variant: (byte)liquid.LiquidType, tint: TintOf(liquidX, liquidY, liquid.LiquidType));
    }

    /// <summary>Leaves (<paramref name="i" />, <paramref name="j" />) soaked with water, whether or not any is touching it.</summary>
    public static bool Soak(int i, int j, float intensity = 1f)
        => ResidueSystem.Apply<LiquidResidue>(i, j, intensity, origin: ResidueOrigin.Shared, variant: (byte)LiquidID.Water, tint: TintOf(i, j, LiquidID.Water));
}
