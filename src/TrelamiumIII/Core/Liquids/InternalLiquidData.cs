namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     The <see cref="InternalLiquidType.Index" /> of a tile's liquid. Only meaningful while the tile holds a liquid with
///     a <see cref="InternalLiquidSet" />; empty tiles keep whatever was last written.
/// </summary>
public struct InternalLiquidData : ITileData
{
    public byte Index;
}

public static class LiquidConditionTileExtensions
{
    public static ref byte InternalLiquid(this Tile tile) => ref tile.Get<InternalLiquidData>().Index;
}
