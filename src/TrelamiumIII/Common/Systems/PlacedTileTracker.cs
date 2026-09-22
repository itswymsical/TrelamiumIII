using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
///     Remembers which tiles were put down by hand, so anything that pays out on mining can refuse to
///     pay for a block the player carried in and placed themselves.
/// </summary>
public sealed class PlacedTileTracker : ModSystem
{
    private static readonly Dictionary<Point16, ushort> PlacedValuables = [];

    /// <summary>How many ordinary placements are remembered before the oldest are evicted.</summary>
    public const int OrdinaryCapacity = 8192;

    private static readonly HashSet<Point16> PlacedOrdinary = [];
    private static readonly Queue<Point16> OrdinaryOrder = new();

    /// <summary>
    ///     Tile types remembered precisely and persistently. Any payout condition reading the persistent grade must be
    ///     a <i>subset</i> of this, or it could pay out on tiles that were never tracked.
    /// </summary>
    public static bool IsValuable(int type) => Main.tileSpelunker[type] || TileID.Sets.Ore[type];

    public override void Load()
    {
        On_WorldGen.PlaceTile += MarkPlacedTile;
    }

    public override void Unload()
    {
        On_WorldGen.PlaceTile -= MarkPlacedTile;
        ClearAll();
    }

    private static bool MarkPlacedTile(On_WorldGen.orig_PlaceTile orig, int i, int j, int type,
        bool mute, bool forced, int plr, int style)
    {
        bool result = orig(i, j, type, mute, forced, plr, style);

        if (WorldGen.isGeneratingOrLoadingWorld || !WorldGen.InWorld(i, j))
            return result;

        // PlaceTile's return value is documented as unreliable, so confirm against the tile itself.
        Tile tile = Framing.GetTileSafely(i, j);
        if (tile.HasTile && tile.TileType == type)
            Mark(i, j, type);

        return result;
    }

    public static bool WasPlacedByPlayer(int i, int j)
    {
        var point = new Point16(i, j);

        if (PlacedValuables.TryGetValue(point, out ushort placedType))
        {
            Tile tile = Framing.GetTileSafely(i, j);
            if (tile.HasTile && tile.TileType == placedType)
                return true;
        }

        return PlacedOrdinary.Contains(point);
    }

    /// <summary>Records a coordinate as hand-placed, at whichever grade its tile type calls for.</summary>
    public static void Mark(int i, int j, int type)
    {
        if (!WorldGen.InWorld(i, j))
            return;

        var point = new Point16(i, j);

        if (IsValuable(type))
        {
            PlacedValuables[point] = (ushort)type;
            return;
        }

        // Add-only, so the set and eviction queue never disagree: ordinary marks expire only by eviction, never on
        // read or Forget.
        if (!PlacedOrdinary.Add(point))
            return;

        OrdinaryOrder.Enqueue(point);

        if (OrdinaryOrder.Count > OrdinaryCapacity)
            PlacedOrdinary.Remove(OrdinaryOrder.Dequeue());
    }

    /// <summary>
    ///     Drops a coordinate's persistent mark. Ordinary marks are left alone on purpose: they self-expire, and
    ///     clearing them here would let a coordinate enter the eviction queue twice.
    /// </summary>
    public static void Forget(int i, int j) => PlacedValuables.Remove(new Point16(i, j));

    public override void ClearWorld()
    {
        // Otherwise marks leak into the next world loaded this session.
        ClearAll();
    }

    private static void ClearAll()
    {
        PlacedValuables.Clear();
        PlacedOrdinary.Clear();
        OrdinaryOrder.Clear();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (PlacedValuables.Count == 0)
            return;

        int[] positions = new int[PlacedValuables.Count];
        int[] types = new int[PlacedValuables.Count];

        int index = 0;
        foreach ((Point16 position, ushort type) in PlacedValuables)
        {
            // Max world is 8400x2400, so both axes fit well inside 16 bits. Y is masked because
            // Point16 stores it signed, and sign extension would smear it over the packed X.
            positions[index] = (position.X << 16) | (ushort)position.Y;
            types[index] = type;
            index++;
        }

        tag[nameof(positions)] = positions;
        tag[nameof(types)] = types;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        ClearAll();

        if (!tag.ContainsKey("positions") || !tag.ContainsKey("types"))
            return;

        int[] positions = tag.GetIntArray("positions");
        int[] types = tag.GetIntArray("types");

        // A truncated or hand-edited save should not throw during world load.
        int count = Math.Min(positions.Length, types.Length);

        for (int index = 0; index < count; index++)
        {
            int i = positions[index] >> 16;
            int j = positions[index] & 0xFFFF;

            if (!WorldGen.InWorld(i, j))
                continue;

            // Not verified against the tile: tile data may not be loaded yet, and WasPlacedByPlayer revalidates on read.
            PlacedValuables[new Point16(i, j)] = (ushort)types[index];
        }
    }
}

/// <summary>Catches block swaps, the one placement path that never reaches <see cref="WorldGen.PlaceTile" />.</summary>
public sealed class PlacedTileSwapTracker : GlobalTile
{
    public override void ReplaceTile(int i, int j, int type, int targetType, int targetStyle)
    {
        // Block swap writes the tile directly, so swapped-in ore would otherwise read as natural. Runs on every client.
        // Forget first: swapping a non-valuable tile over placed ore must clear the ore's mark.
        PlacedTileTracker.Forget(i, j);
        PlacedTileTracker.Mark(i, j, targetType);
    }
}
