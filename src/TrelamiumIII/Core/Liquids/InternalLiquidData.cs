namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     The <see cref="InternalLiquid.Index" /> of a tile's liquid. Only meaningful while the tile holds a liquid with
///     a <see cref="CustomLiquidSet" />; empty tiles keep whatever was last written.
/// </summary>
public struct InternalLiquidData : ITileData
{
    public byte Index;
}

public static class CustomLiquidTileExtensions
{
    public static ref byte InternalLiquid(this Tile tile) => ref tile.Get<InternalLiquidData>().Index;
}
