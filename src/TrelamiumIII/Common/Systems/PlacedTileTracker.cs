using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
///     Remembers which valuable tiles were put down by hand, so mining bonuses can refuse to pay out
///     player-placed ores.
/// </summary>
/// <remarks>
///     Only tiles passing <see cref="IsValuable" /> are recorded. That is the whole reason a plain dictionary is affordable here.
///     <para />
///     Entries carry the tile type they were placed as, and <see cref="WasPlacedByPlayer" /> only
///     honours a mark if the tile is still that type. That makes the set self-healing: a tile removed
///     through a path that never reaches <see cref="Forget" /> (explosives dropping no item, biome
///     conversion, another mod calling <see cref="WorldGen.KillTile" /> with <c>noItem</c>) leaves a
///     stale entry that simply stops matching, rather than permanently blacklisting the coordinate.
/// </remarks>
public sealed class PlacedTileTracker : ModSystem
{
    private static readonly Dictionary<Point16, ushort> PlacedTiles = [];

    /// <summary>
    ///     The set of tiles worth remembering. Any payout condition must be a <i>subset</i> of this,
    ///     otherwise a tile could be paid out on that was never a candidate for tracking.
    /// </summary>
    public static bool IsValuable(int type) => Main.tileSpelunker[type] || TileID.Sets.Ore[type];

    public override void Load()
    {
        On_WorldGen.PlaceTile += MarkPlacedTile;
    }

    public override void Unload()
    {
        PlacedTiles.Clear();
    }

    /// <summary>
    ///     Records hand-placed tiles.
    /// </summary>
    /// <remarks>
    ///     <see cref="GlobalBlockType.PlaceInWorld" /> would be the tidier hook but it only runs on the
    ///     placing client. <see cref="WorldGen.PlaceTile" /> runs on
    ///     whichever side is placing, including the server handling a client's placement packet.
    ///     <para />
    ///     Its <c>plr</c> parameter is not a usable filter as it does nothing
    ///     except affect bathtubs lmao. The <see cref="WorldGen.gen" />
    ///     guard is what separates a player from world generation, and it is reliable for ore because
    ///     worldgen builds veins through <see cref="WorldGen.TileRunner" /> and direct tile writes
    ///     rather than through PlaceTile. Meteorite landings and the like take the same direct-write
    ///     path, so they stay eligible.
    /// </remarks>
    private static bool MarkPlacedTile(On_WorldGen.orig_PlaceTile orig, int i, int j, int type,
        bool mute, bool forced, int plr, int style)
    {
        bool result = orig(i, j, type, mute, forced, plr, style);

        if (WorldGen.gen || !IsValuable(type) || !WorldGen.InWorld(i, j))
            return result;

        // PlaceTile's return value is documented as unreliable, so confirm against the tile itself.
        Tile tile = Framing.GetTileSafely(i, j);
        if (tile.HasTile && tile.TileType == type)
            PlacedTiles[new Point16(i, j)] = (ushort)type;

        return result;
    }

    public static bool WasPlacedByPlayer(int i, int j)
    {
        if (!PlacedTiles.TryGetValue(new Point16(i, j), out ushort placedType))
            return false;

        Tile tile = Framing.GetTileSafely(i, j);
        return tile.HasTile && tile.TileType == placedType;
    }

    /// <summary>Records a coordinate as hand-placed.</summary>
    public static void Mark(int i, int j, int type)
    {
        if (WorldGen.InWorld(i, j))
            PlacedTiles[new Point16(i, j)] = (ushort)type;
    }

    /// <summary>Drops a coordinate's mark once the tile there is gone or replaced.</summary>
    public static void Forget(int i, int j) => PlacedTiles.Remove(new Point16(i, j));

    public override void ClearWorld()
    {
        // Without this the set survives into the next world loaded this session, and coordinates from
        // the old world would deny bonuses on genuinely natural ore in the new one.
        PlacedTiles.Clear();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (PlacedTiles.Count == 0)
            return;

        int[] positions = new int[PlacedTiles.Count];
        int[] types = new int[PlacedTiles.Count];

        int index = 0;
        foreach ((Point16 position, ushort type) in PlacedTiles)
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
        PlacedTiles.Clear();

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
            PlacedTiles[new Point16(i, j)] = (ushort)types[index];
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
        if (PlacedTileTracker.IsValuable(targetType))
            PlacedTileTracker.Mark(i, j, targetType);
        else
            PlacedTileTracker.Forget(i, j);
    }
}
