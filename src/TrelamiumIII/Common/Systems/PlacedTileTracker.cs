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

    /// <summary>
    ///     How many ordinary placements are remembered. Roughly one large building session; beating it
    ///     means placing eight thousand tiles before breaking any of them.
    /// </summary>
    public const int OrdinaryCapacity = 8192;

    private static readonly HashSet<Point16> PlacedOrdinary = [];
    private static readonly Queue<Point16> OrdinaryOrder = new();

    /// <summary>
    ///     The set of tiles worth remembering precisely and persistently. Any payout condition that reads
    ///     the persistent grade must be a <i>subset</i> of this, otherwise a tile could be paid out on
    ///     that was never a candidate for tracking.
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

    /// <summary>
    ///     Records hand-placed tiles.
    /// </summary>
    private static bool MarkPlacedTile(On_WorldGen.orig_PlaceTile orig, int i, int j, int type,
        bool mute, bool forced, int plr, int style)
    {
        bool result = orig(i, j, type, mute, forced, plr, style);

        if (WorldGen.gen || !WorldGen.InWorld(i, j))
            return result;

        // PlaceTile's return value is documented as unreliable, so confirm against the tile itself.
        Tile tile = Framing.GetTileSafely(i, j);
        if (tile.HasTile && tile.TileType == type)
            Mark(i, j, type);

        return result;
    }

    /// <summary>True when the player put something at this coordinate rather than finding it there.</summary>
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

        // Add-only, so the set and the eviction queue can never disagree. An ordinary mark is never
        // cleared on read or by Forget; it expires by eviction alone. A coordinate that ends up in both
        // grades is still correctly "player placed", so there is nothing to reconcile.
        if (!PlacedOrdinary.Add(point))
            return;

        OrdinaryOrder.Enqueue(point);

        if (OrdinaryOrder.Count > OrdinaryCapacity)
            PlacedOrdinary.Remove(OrdinaryOrder.Dequeue());
    }

    /// <summary>
    ///     Drops a coordinate's persistent mark once the tile there is gone or replaced. Ordinary marks are
    ///     left alone on purpose - they are session-scoped and self-expiring, and clearing them here would
    ///     let the same coordinate re-enter the eviction queue twice.
    /// </summary>
    public static void Forget(int i, int j) => PlacedValuables.Remove(new Point16(i, j));

    public override void ClearWorld()
    {
        // Without this the set survives into the next world loaded this session, and coordinates from
        // the old world would deny bonuses on genuinely natural ore in the new one.
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

            // Deliberately not verified against the tile here. Whether tile data is populated by the
            // time world data loads is not something this system should bet on, and a check that ran
            // too early would silently discard every mark. WasPlacedByPlayer revalidates on read.
            PlacedValuables[new Point16(i, j)] = (ushort)types[index];
        }
    }
}

/// <summary>
///     Catches the one placement path that never reaches <see cref="WorldGen.PlaceTile" />.
/// </summary>
public sealed class PlacedTileSwapTracker : GlobalTile
{
    public override void ReplaceTile(int i, int j, int type, int targetType, int targetStyle)
    {
        // Block swap writes the new tile directly, so ore swapped into a wall of stone would other-
        // wise read as natural. This hook runs on local, server and remote clients alike.
        //
        // Forget first: swapping a non-valuable tile over a placed ore has to clear the ore's mark,
        // and Mark itself only ever writes the grade the new type belongs to.
        PlacedTileTracker.Forget(i, j);
        PlacedTileTracker.Mark(i, j, targetType);
    }
}
