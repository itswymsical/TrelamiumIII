using System.Collections.Generic;
using Terraria.ObjectData;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>Tile-event counts per player action, for <see cref="Act.EventsPerAction"/>.</summary>
public static class ActUnits
{
    private static readonly Dictionary<int, int> FootprintCache = [];

    /// <summary>
    /// Tiles one object of <paramref name="tileType"/> occupies (tile breaks per object destroyed), from
    /// <see cref="TileObjectData"/>, else <paramref name="fallback"/>. Cached; do not call before tile object data exists.
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

    /// <summary>Estimated tile breaks per felled tree, trunk plus branches.</summary>
    public const int AverageTreeTiles = 6;

    public const int TwoByTwo = 4;

    internal static void ClearCache() => FootprintCache.Clear();
}
