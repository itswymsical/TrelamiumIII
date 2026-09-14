using Terraria.WorldBuilding;

namespace TrelamiumIII.Common.Generation.Ores;

/// <summary> World generation layer lines, top to bottom. Low and High are the terrain extremes around a layer line. </summary>
public enum WorldLayer
{
    Top,
    SurfaceLow,
    Surface,
    SurfaceHigh,
    RockLow,
    Rock,
    RockHigh,
    Underworld,
    Bottom,
}

/// <summary> A tile row <see cref="T"/> of the way from <see cref="From"/> to <see cref="To"/>, shifted by <see cref="Offset"/> tiles. </summary>
public readonly record struct Depth(WorldLayer From, WorldLayer To, float T, int Offset)
{
    public static Depth At(WorldLayer layer, int offset = 0) => new(layer, layer, 0f, offset);

    public static Depth Between(WorldLayer from, WorldLayer to, float t) => new(from, to, t, 0);

    public int Row => (int)MathHelper.Lerp(LayerRow(From), LayerRow(To), T) + Offset;

    private static float LayerRow(WorldLayer layer) => layer switch
    {
        WorldLayer.Top => 0f,
        WorldLayer.SurfaceLow => (float)GenVars.worldSurfaceLow,
        WorldLayer.Surface => (float)Main.worldSurface,
        WorldLayer.SurfaceHigh => (float)GenVars.worldSurfaceHigh,
        WorldLayer.RockLow => (float)GenVars.rockLayerLow,
        WorldLayer.Rock => (float)Main.rockLayer,
        WorldLayer.RockHigh => (float)GenVars.rockLayerHigh,
        WorldLayer.Underworld => Main.UnderworldLayer,
        _ => Main.maxTilesY,
    };
}

/// <summary>
/// One vein distribution. <see cref="PerMillion"/> is veins per million world tiles (the vanilla Shinies rate),
/// seeded across the full world width between <see cref="Top"/> and <see cref="Bottom"/>. Strength and Steps are
/// <see cref="WorldGen.TileRunner"/> ranges, min inclusive and max exclusive.
/// </summary>
/// <param name="Hosts"> When set, a vein only starts on one of these tiles; the runner still converts what surrounds it. </param>
/// <param name="Clustering"> 0 spreads veins evenly; toward 1 they concentrate where the ore's richness noise is high. </param>
public readonly record struct OreVein(
    ushort Tile,
    Depth Top,
    Depth Bottom,
    float PerMillion,
    (int Min, int Max) Strength,
    (int Min, int Max) Steps,
    ushort[]? Hosts = null,
    float Clustering = 0f);
