using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     A stain where liquid sits against the terrain, in that liquid's colour: the bed of a pool, the
///     bank of a river, ground soaked through by rain. Dries out once the liquid has gone.
/// </summary>
/// <remarks>
///     The palette is grey and the tint is the whole colour, so any liquid -- vanilla, a modded one,
///     one of oil's conditions -- stains in its own hue with no extra work. Liquid draws over the solid
///     layer, so a stain under a pool shows through the water rather than over it.
/// </remarks>
public sealed class LiquidResidueCondition : FilmCondition
{
    protected override TileFilmLook Look { get; } = new()
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
        AmbientLight = new Vector3(0.15f),
        TintStrength = 1f,
        TintWash = 0.75f,
    };

    public override float Duration => 20f * 60f;

    public override int MaxTiles => 2500;

    public override int FadeInTicks => 30;

    public override int FadeOutTicks => 60;

    /// <summary>Still in contact, so still wet: drying starts over.</summary>
    public override void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount)
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
        if (LiquidConditionSet.ByType(liquidType) is { } set && set.ConditionAt(i, j).Tint is var tint && tint != Color.White)
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

        return TileConditionSystem.Apply<LiquidResidueCondition>(i, j, intensity, sync: false, tint: TintOf(liquidX, liquidY, liquid.LiquidType));
    }
}
