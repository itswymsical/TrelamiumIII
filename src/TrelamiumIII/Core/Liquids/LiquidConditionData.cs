namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     The <see cref="LiquidCondition.Index" /> of a tile's liquid. Only meaningful while the tile holds a
///     <see cref="ConditionalLiquid" />; empty tiles keep whatever was last written.
/// </summary>
public struct LiquidConditionData : ITileData
{
    public byte Index;
}

public static class LiquidConditionTileExtensions
{
    public static ref byte LiquidCondition(this Tile tile) => ref tile.Get<LiquidConditionData>().Index;
}
