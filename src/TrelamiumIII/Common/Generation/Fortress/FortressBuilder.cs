using Terraria.Utilities;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Common.Generation.Fortress;

/// <summary>
/// Writes a <see cref="FortressLayout"/> into the world: a solid Brambleroot polygon, then the room interiors, the
/// openings between them, the arena and the Altar of Ingress. Rooms without a template get a placeholder course.
/// Run it after smoothing so the brickwork keeps square edges.
/// </summary>
public static class FortressBuilder
{
    private const ushort BackWall = WallID.LivingWoodUnsafe;

    /// <summary> <see cref="TileID.Platforms"/> style of the living wood platform. </summary>
    private const int PlatformStyle = 23;

    private const int PitDepth = 3;
    private const int PitWidthMin = 3;
    private const int PitWidthMax = 6;
    private const int PitGapMin = 4;
    private const int PitGapMax = 8;
    private const int DoorClearance = 8;
    private const int HatchClearance = 4;

    private const int LadderFirstDrop = 5;
    private const int LadderSpacing = 6;
    private const int BaffleSpacing = 8;
    private const int BaffleThickness = 2;
    private const int BaffleOverhang = 10;
    private const int SpikeStripWidth = 3;

    private const int TorchSpacing = 14;
    private const int TorchHeight = 8;

    private const int DaisWidth = 18;
    private const int DaisStep = 3;
    private const int DaisStepHeight = 2;
    private const int ArenaLadderSpacing = 7;

    /// <summary> Liquid is drained this far around the roof opening, so cave pools don't pour into the gate. </summary>
    private const int GateDrainReach = 32;

    public static void Build(FortressLayout layout, UnifiedRandom random)
    {
        ushort brick = (ushort)ModContent.TileType<BramblerootTile>();

        FillShell(layout, brick);

        foreach (FortressRoom room in layout.Rooms)
            ClearArea(room.Interior);

        for (int i = 0; i < layout.Rooms.Count - 1; i++)
            ClearArea(FortressLayout.Link(layout.Rooms[i], layout.Rooms[i + 1]));

        ClearArea(layout.Arena);
        ClearArea(layout.Gate);
        ClearArea(layout.ArenaHatch);
        DrainAboveGate(layout);

        FortressRoomTemplates templates = FortressRoomTemplates.Load();

        foreach (FortressRoom room in layout.Rooms)
        {
            if (templates.Pick(room.Kind, random) is { } path)
                StructureWorld.Place(StructureLoader.Get(path), room.Interior.Location);
            else
                BuildPlaceholder(room, brick, random);
        }

        BuildArena(layout, brick);
    }

    /// <summary> Top-left tile of the altar on the arena's dais. </summary>
    public static Point AltarOrigin(FortressLayout layout)
    {
        Rectangle arena = layout.Arena;
        int top = arena.Bottom - DaisStepHeight * 2 - WardenAltarTile.Height;

        return new Point(arena.Center.X - WardenAltarTile.Width / 2, top);
    }

    private static void FillShell(FortressLayout layout, ushort brick)
    {
        Rectangle bounds = layout.Bounds;

        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                if (!layout.Contains(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                tile.ClearEverything();
                tile.HasTile = true;
                tile.TileType = brick;
                tile.WallType = BackWall;
            }
        }
    }

    /// <summary> Removes tiles and liquid, keeping the back wall. </summary>
    private static void ClearArea(Rectangle area)
    {
        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.LiquidAmount = 0;
            }
        }
    }

    private static void DrainAboveGate(FortressLayout layout)
    {
        Point approach = layout.GateApproach;

        for (int x = approach.X - GateDrainReach; x <= approach.X + GateDrainReach; x++)
        {
            for (int y = approach.Y - GateDrainReach; y <= approach.Y + 1; y++)
            {
                if (WorldGen.InWorld(x, y))
                    Main.tile[x, y].LiquidAmount = 0;
            }
        }
    }

    #region Placeholder rooms

    private static void BuildPlaceholder(FortressRoom room, ushort brick, UnifiedRandom random)
    {
        if (room.Entry == RoomSide.Top && room.Exit == RoomSide.Bottom)
        {
            BuildBaffles(room.Interior, brick);
        }
        else
        {
            BuildPitRun(room, random);

            if (room.Entry == RoomSide.Top)
                BuildLadder(room.Interior);
        }

        PlaceTorches(room.Interior);
    }

    /// <summary>
    /// Spike pits dug into the floor between the room's openings. The floor stays solid near side doors and under
    /// hatches, so entering and leaving never lands in a pit.
    /// </summary>
    private static void BuildPitRun(FortressRoom room, UnifiedRandom random)
    {
        Rectangle interior = room.Interior;
        int hatchLeft = FortressLayout.HatchLeft(interior);
        bool[] keep = new bool[interior.Width];
        RoomSide[] openings = [room.Entry, room.Exit];

        foreach (RoomSide side in openings)
        {
            (int from, int to) = side switch
            {
                RoomSide.Left => (0, DoorClearance),
                RoomSide.Right => (interior.Width - DoorClearance, interior.Width),
                _ => (hatchLeft - interior.X - HatchClearance, hatchLeft - interior.X + FortressLayout.HatchWidth + HatchClearance)
            };

            for (int column = Math.Max(from, 0); column < Math.Min(to, interior.Width); column++)
                keep[column] = true;
        }

        ushort spikes = TileID.WoodenSpikes;
        int floorY = interior.Bottom;
        int x = random.Next(PitGapMin, PitGapMax + 1);

        while (x < interior.Width)
        {
            int width = random.Next(PitWidthMin, PitWidthMax + 1);

            if (x + width <= interior.Width && !AnyKept(keep, x, width))
            {
                for (int column = interior.X + x; column < interior.X + x + width; column++)
                {
                    for (int y = floorY; y < floorY + PitDepth; y++)
                        Main.tile[column, y].ClearTile();

                    Tile bed = Main.tile[column, floorY + PitDepth];
                    bed.ClearTile();
                    bed.HasTile = true;
                    bed.TileType = spikes;
                }

                x += width + random.Next(PitGapMin, PitGapMax + 1);
                continue;
            }

            x++;
        }
    }

    private static bool AnyKept(bool[] keep, int start, int width)
    {
        for (int column = start; column < start + width; column++)
        {
            if (keep[column])
                return true;
        }

        return false;
    }

    /// <summary> Platforms under the ceiling hatch, close enough apart that dropping down them never hurts. </summary>
    private static void BuildLadder(Rectangle interior)
    {
        int left = FortressLayout.HatchLeft(interior) - 1;
        int right = left + FortressLayout.HatchWidth + 2;

        for (int y = interior.Top + LadderFirstDrop; y < interior.Bottom - 3; y += LadderSpacing)
            PlacePlatforms(left, right, y);
    }

    /// <summary>
    /// Ledges hanging off alternating side walls below the ceiling hatch, each reaching past the centre so the drop
    /// zig-zags down to the floor hatch. A spike strip on every ledge has to be jumped.
    /// </summary>
    private static void BuildBaffles(Rectangle interior, ushort brick)
    {
        int center = interior.Center.X;
        int ledge = 0;

        for (int y = interior.Top + LadderFirstDrop + 2; y < interior.Bottom - BaffleSpacing / 2; y += BaffleSpacing, ledge++)
        {
            bool fromLeft = ledge % 2 == 0;
            int left = fromLeft ? interior.Left : center - BaffleOverhang;
            int right = fromLeft ? center + BaffleOverhang : interior.Right;

            for (int x = left; x < right; x++)
            {
                for (int row = y; row < y + BaffleThickness; row++)
                {
                    Tile tile = Main.tile[x, row];
                    tile.HasTile = true;
                    tile.TileType = brick;
                }
            }

            // Between the landing under the previous opening and the edge this ledge drops off.
            int strip = fromLeft ? center + BaffleOverhang / 2 - 1 : center - BaffleOverhang / 2 - SpikeStripWidth + 1;

            for (int x = strip; x < strip + SpikeStripWidth; x++)
            {
                Tile tile = Main.tile[x, y - 1];
                tile.HasTile = true;
                tile.TileType = TileID.WoodenSpikes;
            }
        }
    }

    /// <summary> At eye height along the room, skipping anything already built there. </summary>
    private static void PlaceTorches(Rectangle interior)
    {
        int torch = ModContent.TileType<BramblewoodTorchTile>();
        int y = interior.Bottom - TorchHeight;

        for (int x = interior.Left + TorchSpacing / 2; x < interior.Right; x += TorchSpacing)
        {
            if (!Main.tile[x, y].HasTile)
                WorldGen.PlaceTile(x, y, torch, mute: true);
        }
    }

    #endregion

    #region Arena

    private static void BuildArena(FortressLayout layout, ushort brick)
    {
        Rectangle arena = layout.Arena;
        int center = arena.Center.X;

        for (int step = 0; step < 2; step++)
        {
            int halfWidth = DaisWidth / 2 - step * DaisStep;
            int top = arena.Bottom - DaisStepHeight * (step + 1);

            for (int x = center - halfWidth; x < center + halfWidth; x++)
            {
                for (int y = top; y < top + DaisStepHeight; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = brick;
                }
            }
        }

        WardenAltarTile.Place(AltarOrigin(layout));

        Rectangle hatch = layout.ArenaHatch;

        for (int y = arena.Top + ArenaLadderSpacing; y < arena.Bottom - 4; y += ArenaLadderSpacing)
            PlacePlatforms(hatch.Left - 1, hatch.Right + 1, y);

        PlaceTorches(arena);
    }

    #endregion

    private static void PlacePlatforms(int left, int right, int y)
    {
        for (int x = left; x < right; x++)
        {
            Tile tile = Main.tile[x, y];

            if (tile.HasTile)
                continue;

            tile.HasTile = true;
            tile.TileType = TileID.Platforms;
            tile.TileFrameX = 0;
            tile.TileFrameY = (short)(PlatformStyle * 18);
        }
    }
}
