using Terraria.Map;

namespace TrelamiumIII.Utilities;

/// <summary>
///     Reads a representative colour off a tile or wall, for effects that take their palette from
///     whatever the player is pointed at.
/// </summary>
/// <remarks>
///     The map's own colour is the first choice, and not as a shortcut: it is the one colour Terraria
///     itself already publishes per tile type, so an effect keyed off it agrees with the minimap and
///     picks up modded tiles for free rather than needing an entry adding for each one.
/// </remarks>
internal static class TileColorUtils
{
    /// <summary>Where the fallback table gives up. Mid grey reads as unremarkable stone.</summary>
    private static readonly Color Unknown = new(150, 150, 150);

    /// <summary>
    ///     The colour of whatever occupies a tile coordinate: its block, else its wall, else
    ///     <paramref name="empty" />.
    /// </summary>
    /// <param name="enhance">
    ///     Floors saturation and value, so a dim tile still hands the effect a colour it can carry.
    ///     Leave off when the true colour matters more than legibility.
    /// </param>
    public static Color Sample(int tileX, int tileY, Color empty, bool enhance = true)
    {
        if (!WorldGen.InWorld(tileX, tileY, 1))
            return empty;

        Tile tile = Main.tile[tileX, tileY];

        Color colour;

        if (tile.HasTile)
            colour = TileColor(tile.TileType, tileX, tileY);
        else if (tile.WallType != WallID.None)
            colour = WallColor(tile.WallType);
        else
            return empty;

        return enhance ? Enhance(colour) : colour;
    }

    public static Color TileColor(int tileType, int x, int y)
    {
        try
        {
            MapTile mapTile = Main.Map[x, y];
            Color mapColor = MapHelper.GetMapTileXnaColor(mapTile, x, y);

            // An unrevealed tile maps to black, which is not a colour so much as an absence of one.
            if (mapColor.R > 0 || mapColor.G > 0 || mapColor.B > 0)
                return mapColor;
        }
        catch
        {
            // Map lookups touch an array the map thread also writes. A fallback colour is a far
            // better outcome here than taking a draw loop down.
        }

        return FallbackTileColor(tileType);
    }

    /// <summary>
    ///     Colours for the ores and terrain an effect is most likely to be aimed at, for when the map
    ///     cannot answer -- most often because that chunk has never been revealed.
    /// </summary>
    private static Color FallbackTileColor(int tileType) => tileType switch
    {
        TileID.Dirt => new Color(139, 91, 71),
        TileID.Grass => new Color(71, 133, 71),
        TileID.Iron => new Color(205, 127, 50),
        TileID.Copper => new Color(184, 115, 51),
        TileID.Gold => new Color(255, 215, 0),
        TileID.Silver => new Color(192, 192, 192),
        TileID.Sand => new Color(255, 218, 143),
        TileID.WoodBlock => new Color(150, 111, 51),
        TileID.CorruptGrass => new Color(104, 86, 164),
        TileID.CrimsonGrass => new Color(200, 45, 85),
        TileID.HallowedGrass => new Color(120, 185, 225),
        TileID.Tin => new Color(145, 145, 105),
        TileID.Lead => new Color(85, 89, 118),
        TileID.Tungsten => new Color(132, 220, 85),
        TileID.Platinum => new Color(154, 192, 220),
        TileID.Demonite => new Color(123, 97, 163),
        TileID.Crimtane => new Color(200, 45, 85),
        TileID.Meteorite => new Color(95, 61, 53),
        TileID.Obsidian => new Color(49, 40, 58),
        TileID.Hellstone => new Color(200, 89, 89),
        TileID.Cobalt => new Color(43, 109, 167),
        TileID.Mythril => new Color(89, 140, 89),
        TileID.Adamantite => new Color(214, 109, 109),
        TileID.Chlorophyte => new Color(128, 200, 89),
        _ => Unknown,
    };

    public static Color WallColor(int wallType) => wallType switch
    {
        WallID.DirtUnsafe => new Color(139, 91, 71),
        WallID.Stone => new Color(100, 100, 100),
        WallID.Wood => new Color(120, 91, 51),
        WallID.Grass => new Color(51, 113, 51),
        WallID.EbonstoneUnsafe => new Color(84, 66, 144),
        WallID.CrimstoneUnsafe => new Color(180, 25, 65),
        WallID.PearlstoneBrickUnsafe => new Color(100, 165, 205),
        _ => new Color(64, 64, 64),
    };

    /// <summary>Floors saturation and value so a dim tile still gives an effect a colour to carry.</summary>
    public static Color Enhance(Color original)
    {
        Vector3 hsl = Main.rgbToHsl(original);

        hsl.Y = Math.Max(hsl.Y, 0.6f);
        hsl.Z = Math.Max(hsl.Z, 0.7f);

        Color enhanced = Main.hslToRgb(hsl);
        enhanced.A = 255;

        return enhanced;
    }
}
