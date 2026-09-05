using System.Collections.Generic;
using Terraria.ObjectData;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>
/// Converts raw tile events into the actions a player thinks they performed, for
/// <see cref="Act.EventsPerAction"/>.
///
/// <para>
/// Terraria kills tiles, not objects. Smashing one 2x2 pot raises four tile breaks and felling one tree
/// raises a dozen or more, so an act that counts events counts the wrong thing - by a factor of four in
/// the first case and by an order of magnitude in the second.
/// </para>
/// </summary>
public static class ActUnits
{
    private static readonly Dictionary<int, int> FootprintCache = [];

    /// <summary>
    /// Tile spaces one placed object of <paramref name="tileType"/> occupies, which is how many tile
    /// breaks destroying one of them raises.
    ///
    /// <para>
    /// Read from <see cref="TileObjectData"/> where possible, so a modded 3x2 container is handled without
    /// anyone counting its tiles by hand. Not every multitile has an entry though - worldgen-only objects
    /// the player can never place are not registered as placeable objects - so <paramref name="fallback"/>
    /// carries the ones that do not.
    /// </para>
    ///
    /// <para>
    /// Must not be called before tile object data is populated. Doing so resolves to the fallback and
    /// caches it, which is exactly how pots ended up counting four actions per pot.
    /// </para>
    /// </summary>
    public static int MultiTileFootprint(int tileType, int style = 0, int fallback = 1)
    {
        if (FootprintCache.TryGetValue(tileType, out int cached))
            return cached;

        int footprint;
        string source;

        if (TileObjectData.GetTileData(tileType, style) is { } data && data.Width * data.Height > 0)
        {
            footprint = data.Width * data.Height;
            source = $"TileObjectData {data.Width}x{data.Height}";
        }
        else
        {
            footprint = Math.Max(fallback, 1);
            source = "fallback (no TileObjectData entry)";
        }

        FootprintCache[tileType] = footprint;

        EventLog.Trace($"[Act] footprint for {EventLog.TileName(tileType)} = {footprint} via {source}");

        return footprint;
    }

    /// <summary>
    /// Tile breaks an average felled tree raises: its trunk height plus its branches.
    ///
    /// <para>
    /// An estimate, and unavoidably so - vanilla trees run from roughly 5 to 20 tiles tall and carry a
    /// couple of branches each, and there is no hook that fires once per tree. The tracker carries the
    /// remainder between events, so the error is a slow drift in one act's pacing rather than lost
    /// progress. Measure a few trees in game and move this if it reads wrong.
    /// </para>
    /// </summary>
    public const int AverageTreeTiles = 12;

    /// <summary>A 2x2 object, which is what a pot is. Named so the number is not a bare 4 at the call site.</summary>
    public const int TwoByTwo = 4;

    /// <summary>Dropped on unload so a reload cannot inherit a footprint resolved against the previous session.</summary>
    internal static void ClearCache() => FootprintCache.Clear();
}
