using System.Linq;
using Terraria.Utilities;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Common.Generation.Fortress;

/// <summary>
/// Writes a <see cref="FortressLayout"/> into the world: a solid Brambleroot polygon, the room interiors and the openings
/// between them, <see cref="FortressDressing"/>, then each room's template or placeholder, and the arena with the Altar
/// of Ingress. Run it after smoothing so the brickwork keeps square edges.
/// </summary>
public static class FortressBuilder
{
    private const ushort BackWall = WallID.LivingWoodUnsafe;

    /// <summary> <see cref="TileID.Platforms"/> style of the living wood platform. </summary>
    private const int PlatformStyle = 23;

    private const int DoorClearance = 8;
    private const int HatchClearance = 2;
    private const int FloorOpeningHeight = 8;

    private const int LadderFirstDrop = 5;
    private const int LadderSpacing = 6;

    private const int ChestOffset = 14;
    private const int PlinthWidth = 6;
    private const int ChestClearance = 2;
    private const int ChestHeadroom = 6;

    private const int RoomCornerSize = 5;
    private const int ArenaCornerSize = 10;

    private const int TorchSpacing = 14;
    private const int TorchHeight = 8;

    private const int DaisWidth = 18;
    private const int DaisStep = 3;
    private const int DaisStepHeight = 2;
    private const int DaisClearance = 3;
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
        string?[] picked = [.. layout.Rooms.Select(room => templates.Pick(room.Kind, random))];

        FortressDressing.Apply(layout, KeepOut(layout, picked), random);

        foreach (FortressRoom room in layout.Rooms)
        {
            if (picked[room.Index] is { } path)
                StructureWorld.Place(StructureLoader.Get(path), room.Interior.Location);
            else
                BuildPlaceholder(room, brick, random);
        }

        BuildArena(layout, brick);
        FortressProgress.Register(layout.Rooms.Select(room => room.Interior));
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

    /// <summary> Openings and the space around them, ladder columns, placeholder chest spots and the altar. </summary>
    private static FortressKeepOut KeepOut(FortressLayout layout, string?[] picked)
    {
        var keep = new FortressKeepOut();

        for (int i = 0; i < layout.Rooms.Count - 1; i++)
            keep.Add(Inflated(FortressLayout.Link(layout.Rooms[i], layout.Rooms[i + 1]), 1));

        foreach (FortressRoom room in layout.Rooms)
        {
            Rectangle interior = room.Interior;
            RoomSide[] openings = [room.Entry, room.Exit];

            foreach (RoomSide side in openings)
                keep.Add(OpeningZone(interior, side));

            if (picked[room.Index] is null)
                keep.Add(ChestZone(room));
        }

        keep.Add(Inflated(layout.Gate, 1));
        keep.Add(Inflated(layout.ArenaHatch, 1));
        keep.Add(new Rectangle(layout.ArenaHatch.Left - HatchClearance, layout.Arena.Top, FortressLayout.HatchWidth + HatchClearance * 2, layout.Arena.Height));

        Point altar = AltarOrigin(layout);
        int altarTop = altar.Y - DaisClearance;
        keep.Add(new Rectangle(layout.Arena.Center.X - DaisWidth / 2 - DaisClearance, altarTop, DaisWidth + DaisClearance * 2, layout.Arena.Bottom - altarTop));

        return keep;
    }

    /// <summary> A side door's floor corner, a ceiling hatch's whole ladder column, or the floor around a floor hatch. </summary>
    private static Rectangle OpeningZone(Rectangle interior, RoomSide side)
    {
        int hatchLeft = FortressLayout.HatchLeft(interior) - HatchClearance;
        int hatchWidth = FortressLayout.HatchWidth + HatchClearance * 2;
        int doorTop = interior.Bottom - FortressLayout.DoorHeight - 2;

        return side switch
        {
            RoomSide.Left => new Rectangle(interior.Left, doorTop, DoorClearance, interior.Bottom - doorTop),
            RoomSide.Right => new Rectangle(interior.Right - DoorClearance, doorTop, DoorClearance, interior.Bottom - doorTop),
            RoomSide.Top => new Rectangle(hatchLeft, interior.Top, hatchWidth, interior.Height),
            _ => new Rectangle(hatchLeft, interior.Bottom - FloorOpeningHeight, hatchWidth, FloorOpeningHeight)
        };
    }

    /// <summary> The plinth and the space above it, on the side of the room away from the entry. </summary>
    private static Rectangle ChestZone(FortressRoom room)
    {
        Rectangle interior = room.Interior;
        int direction = room.Entry == RoomSide.Right ? -1 : 1;
        int plinthLeft = interior.Center.X + direction * ChestOffset - PlinthWidth / 2;

        return new Rectangle(plinthLeft - ChestClearance, interior.Bottom - ChestHeadroom, PlinthWidth + ChestClearance * 2, ChestHeadroom);
    }

    private static Rectangle Inflated(Rectangle area, int amount)
    {
        area.Inflate(amount, amount);
        return area;
    }

    #region Placeholder rooms

    private static void BuildPlaceholder(FortressRoom room, ushort brick, UnifiedRandom random)
    {
        Rectangle interior = room.Interior;

        FillCorners(interior, RoomCornerSize, brick);

        if (room.Entry == RoomSide.Top)
            BuildLadder(interior);

        BuildChest(room, random);
        PlaceTorches(interior);
    }

    /// <summary> Platforms under the ceiling hatch, close enough apart that dropping down them never hurts. </summary>
    private static void BuildLadder(Rectangle interior)
    {
        int left = FortressLayout.HatchLeft(interior) - 1;
        int right = left + FortressLayout.HatchWidth + 2;

        for (int y = interior.Top + LadderFirstDrop; y < interior.Bottom - 3; y += LadderSpacing)
            PlacePlatforms(left, right, y);
    }

    /// <summary> A slate plinth in <see cref="ChestZone"/> with a sealed <see cref="LigneousChestTile"/> holding <see cref="FortressLoot"/>. </summary>
    private static void BuildChest(FortressRoom room, UnifiedRandom random)
    {
        Rectangle zone = ChestZone(room);
        int plinthLeft = zone.Left + ChestClearance;
        int plinthY = room.Interior.Bottom - 1;
        ushort slate = (ushort)ModContent.TileType<SlateTile>();

        for (int x = plinthLeft; x < plinthLeft + PlinthWidth; x++)
        {
            Tile tile = Main.tile[x, plinthY];
            tile.ClearTile();
            tile.HasTile = true;
            tile.TileType = slate;
        }

        int chest = WorldGen.PlaceChest(plinthLeft + PlinthWidth / 2 - 1, plinthY - 1, (ushort)ModContent.TileType<LigneousChestTile>(), style: LigneousChestTile.Style);

        if (chest >= 0)
            FortressLoot.Fill(Main.chest[chest], random);
    }

    /// <summary> Solid triangles in both top corners of <paramref name="area"/>. </summary>
    private static void FillCorners(Rectangle area, int size, ushort brick)
    {
        for (int dx = 0; dx < size; dx++)
        {
            for (int dy = 0; dx + dy < size; dy++)
            {
                SetBrick(area.Left + dx, area.Top + dy, brick);
                SetBrick(area.Right - 1 - dx, area.Top + dy, brick);
            }
        }
    }

    private static void SetBrick(int x, int y, ushort brick)
    {
        Tile tile = Main.tile[x, y];

        if (tile.HasTile)
            return;

        tile.HasTile = true;
        tile.TileType = brick;
    }

    /// <summary> At eye height along the area, skipping anything already built there. </summary>
    private static void PlaceTorches(Rectangle area)
    {
        int torch = ModContent.TileType<BramblewoodTorchTile>();
        int y = area.Bottom - TorchHeight;

        for (int x = area.Left + TorchSpacing / 2; x < area.Right; x += TorchSpacing)
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

        FillCorners(arena, ArenaCornerSize, brick);

        for (int step = 0; step < 2; step++)
        {
            int halfWidth = DaisWidth / 2 - step * DaisStep;
            int top = arena.Bottom - DaisStepHeight * (step + 1);

            for (int x = center - halfWidth; x < center + halfWidth; x++)
            {
                for (int y = top; y < top + DaisStepHeight; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.ClearTile();
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
