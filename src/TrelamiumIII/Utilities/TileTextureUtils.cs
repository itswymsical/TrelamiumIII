using Terraria.GameContent;

namespace TrelamiumIII.Utilities;

/// <summary>
///     Cuts a crop out of a tile's own sprite sheet, so debris broken off a block is drawn in the
///     block's actual pixels rather than in a stand-in.
/// </summary>
/// <remarks>
///     One shared implementation because the alternative is a copy per effect, and every copy has to
///     re-derive the two things that are easy to get wrong: which frames of a sheet are safe to
///     quarter, and what to do when the sheet is not laid out the way it was assumed to be.
/// </remarks>
internal static class TileTextureUtils
{
    /// <summary>Pixel pitch of a tile sheet. Accounts for the 2px of padding between frames.</summary>
    public const int TILE_PITCH = 18;

    /// <summary>
    ///     Side of the crop taken out of a frame. A quarter block reads as a shard; a full one reads
    ///     as a dropped block.
    /// </summary>
    public const int CHUNK = 8;

    /// <summary>
    ///     How many distinct crops <see cref="ResolveChunk" /> can produce. Callers pick a seed in
    ///     <c>[0, CHUNK_SEEDS)</c>; the frame it lands on is derived from it rather than rolled
    ///     separately, so a seed synced across the network cuts the same shard on every client.
    /// </summary>
    public const int CHUNK_SEEDS = 36;

    /// <summary>
    ///     Picks the crop a piece of debris is cut from.
    /// </summary>
    /// <remarks>
    ///     The frames at the top-left of a standard tile sheet are all fully opaque blocks whichever
    ///     way the tile blends, so any of them is safe to quarter. Anything that does not fit that
    ///     layout -- a sheet too small for the frame the seed picked, a tile with no sheet at all --
    ///     falls back to a stone block, which is wrong but never invisible and never reads past the
    ///     end of a texture.
    /// </remarks>
    public static Texture2D ResolveChunk(ushort tileType, int seed, out Rectangle source)
    {
        if (tileType < TileLoader.TileCount)
        {
            Main.instance.LoadTiles(tileType);

            if (TextureAssets.Tile[tileType] is { Value: { } sheet })
            {
                source = new Rectangle(
                    seed % 3 * TILE_PITCH + seed / 9 % 2 * CHUNK,
                    seed / 3 % 3 * TILE_PITCH + seed / 18 % 2 * CHUNK,
                    CHUNK,
                    CHUNK
                );

                if (source.Right <= sheet.Width && source.Bottom <= sheet.Height)
                    return sheet;
            }
        }

        Main.instance.LoadItem(ItemID.StoneBlock);

        Texture2D fallback = TextureAssets.Item[ItemID.StoneBlock].Value;
        source = fallback.Bounds;

        return fallback;
    }
}
