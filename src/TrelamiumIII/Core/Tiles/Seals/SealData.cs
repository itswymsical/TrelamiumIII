namespace TrelamiumIII.Core.Tiles.Seals;

/// <summary>
///     The <see cref="SealType.Index" /> laid on a tile position, or 0 for none. Marks the position, not the block, so
///     open tiles inside a sealed structure refuse building too.
/// </summary>
public struct SealData : ITileData
{
    public byte Index;
}

public static class SealTileExtensions
{
    public static ref byte Seal(this Tile tile) => ref tile.Get<SealData>().Index;
}
