using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ObjectData;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>Reads and writes one channel of a tile for the editor's tools. Callers reframe what they changed.</summary>
internal static class EditorPainter
{
    private static readonly Point[] Neighbours = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

    private static ushort VoidTile => (ushort)ModContent.TileType<StructureVoidTile>();

    private static ushort VoidWall => (ushort)ModContent.WallType<StructureVoidWall>();

    /// <summary>What <paramref name="channel" /> of tile (i, j) holds, as a swatch that would write it back.</summary>
    public static Swatch Pick(int i, int j, EditorChannel channel, PaintTarget target)
    {
        Tile tile = Main.tile[i, j];

        switch (channel)
        {
            case EditorChannel.Blocks:
                if (!tile.HasTile)
                    return Swatch.Empty;

                return tile.TileType == VoidTile ? Swatch.Void : new Swatch(SwatchKind.Tile, tile.TileType, StyleOf(tile));

            case EditorChannel.Walls:
                if (tile.WallType == WallID.None)
                    return Swatch.Empty;

                return tile.WallType == VoidWall ? Swatch.Void : new Swatch(SwatchKind.Wall, tile.WallType);

            case EditorChannel.Liquid:
                return tile.LiquidAmount == 0 ? Swatch.Empty : new Swatch(SwatchKind.Liquid, tile.LiquidType);

            default:
                byte color = target == PaintTarget.Walls || !tile.HasTile ? tile.WallColor : tile.TileColor;
                return color == 0 ? Swatch.Empty : new Swatch(SwatchKind.Paint, color);
        }
    }

    /// <summary>Writes <paramref name="swatch" /> into <paramref name="channel" /> of tile (i, j). False when nothing changed.</summary>
    public static bool Apply(int i, int j, EditorChannel channel, Swatch swatch, PaintTarget target)
    {
        Tile tile = Main.tile[i, j];

        return channel switch
        {
            EditorChannel.Blocks => ApplyBlock(i, j, tile, swatch),
            EditorChannel.Walls => ApplyWall(tile, swatch),
            EditorChannel.Liquid => ApplyLiquid(tile, swatch),
            _ => ApplyPaint(tile, swatch, target)
        };
    }

    /// <summary>Whether a tool can put <paramref name="swatch" /> in <paramref name="channel" />.</summary>
    public static bool Accepts(EditorChannel channel, Swatch swatch) => swatch.Kind switch
    {
        SwatchKind.Empty => true,
        SwatchKind.Void => channel is EditorChannel.Blocks or EditorChannel.Walls,
        SwatchKind.Tile => channel == EditorChannel.Blocks,
        SwatchKind.Wall => channel == EditorChannel.Walls,
        SwatchKind.Liquid => channel == EditorChannel.Liquid,
        _ => channel == EditorChannel.Paint
    };

    /// <summary>The cells joined to <paramref name="start" /> through its own four neighbours that hold the same swatch, inside <paramref name="bounds" />.</summary>
    public static List<Point> FloodRegion(Point start, Rectangle bounds, EditorChannel channel, PaintTarget target)
    {
        Swatch seed = Pick(start.X, start.Y, channel, target);
        var visited = new HashSet<Point> { start };
        var queue = new Queue<Point>();
        List<Point> region = [];

        queue.Enqueue(start);

        while (queue.TryDequeue(out Point cell))
        {
            region.Add(cell);

            foreach (Point offset in Neighbours)
            {
                var next = new Point(cell.X + offset.X, cell.Y + offset.Y);

                if (bounds.Contains(next) && !visited.Contains(next) && Pick(next.X, next.Y, channel, target) == seed)
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return region;
    }

    #region Blocks
    private static bool ApplyBlock(int i, int j, Tile tile, Swatch swatch)
    {
        if (Pick(i, j, EditorChannel.Blocks, PaintTarget.Blocks) == swatch)
            return false;

        if (tile.HasTile && IsMultitile(tile))
            RemoveMultitile(i, j);

        switch (swatch.Kind)
        {
            case SwatchKind.Empty:
                ClearBlock(tile);
                return true;

            case SwatchKind.Void:
                StructureCell.Marker(VoidTile).WriteTile(tile);
                return true;

            case SwatchKind.Tile when Main.tileFrameImportant[swatch.Type]:
                return PlaceFramedTile(i, j, tile, swatch);

            case SwatchKind.Tile:
                WriteBlock(tile, (ushort)swatch.Type);
                return true;

            default:
                return false;
        }
    }

    private static void WriteBlock(Tile tile, ushort type)
    {
        ClearBlock(tile);

        tile.HasTile = true;
        tile.TileType = type;
        tile.TileFrameNumber = WorldGen.genRand.Next(3);

        if (Main.tileSolid[type] && !Main.tileSolidTop[type])
            tile.LiquidAmount = 0;
    }

    /// <summary>One-tile objects with styles, such as platforms and torches, go through vanilla placement for their frames and anchors.</summary>
    private static bool PlaceFramedTile(int i, int j, Tile tile, Swatch swatch)
    {
        StructureCell before = StructureCell.Read(tile);
        ClearBlock(tile);

        if (WorldGen.PlaceTile(i, j, swatch.Type, mute: true, forced: true, style: swatch.Style))
            return true;

        // Refused by its placement rules, e.g. a torch with nothing to hang on.
        before.WriteTile(tile);
        return false;
    }

    private static void ClearBlock(Tile tile)
    {
        tile.ClearTile();
        tile.TileType = 0;
        tile.TileFrameX = tile.TileFrameY = 0;
        tile.TileColor = 0;
        tile.IsTileInvisible = false;
        tile.IsTileFullbright = false;
    }

    private static bool IsMultitile(Tile tile)
        => Main.tileFrameImportant[tile.TileType] && TileObjectData.GetTileData(tile) is { } data && data.Width * data.Height > 1;

    /// <summary>Removes the whole multitile at (i, j) with its chest, sign or tile entity, dropping nothing.</summary>
    private static void RemoveMultitile(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        ushort type = tile.TileType;
        TileObjectData data = TileObjectData.GetTileData(tile)!;
        Point16 topLeft = TileObjectData.TopLeft(i, j);
        var area = new Rectangle(topLeft.X, topLeft.Y, data.Width, data.Height);

        StructureWorld.RemoveAttachments(area);

        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
            {
                if (WorldGen.InWorld(x, y) && Main.tile[x, y] is { HasTile: true } part && part.TileType == type)
                    ClearBlock(part);
            }
        }
    }

    private static int StyleOf(Tile tile)
    {
        if (!Main.tileFrameImportant[tile.TileType])
            return 0;

        if (TileID.Sets.Platforms[tile.TileType])
            return tile.TileFrameY / 18;

        return Math.Max(0, TileObjectData.GetTileStyle(tile));
    }
    #endregion

    #region Walls, liquid, paint
    private static bool ApplyWall(Tile tile, Swatch swatch)
    {
        ushort wall = swatch.Kind switch
        {
            SwatchKind.Void => VoidWall,
            SwatchKind.Wall => (ushort)swatch.Type,
            _ => WallID.None
        };

        if (tile.WallType == wall)
            return false;

        tile.WallType = wall;
        tile.WallColor = 0;
        tile.WallFrameNumber = WorldGen.genRand.Next(3);
        tile.IsWallInvisible = false;
        tile.IsWallFullbright = false;

        return true;
    }

    private static bool ApplyLiquid(Tile tile, Swatch swatch)
    {
        if (swatch.Kind != SwatchKind.Liquid)
        {
            if (tile.LiquidAmount == 0)
                return false;

            tile.LiquidAmount = 0;
            return true;
        }

        // Liquid can't sit inside a solid block, void blocks included.
        if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
            return false;

        if (tile.LiquidAmount == byte.MaxValue && tile.LiquidType == swatch.Type)
            return false;

        tile.LiquidAmount = byte.MaxValue;
        tile.LiquidType = swatch.Type;

        return true;
    }

    private static bool ApplyPaint(Tile tile, Swatch swatch, PaintTarget target)
    {
        byte color = swatch.Kind == SwatchKind.Paint ? (byte)swatch.Type : (byte)0;
        bool changed = false;

        if (target != PaintTarget.Walls && tile.HasTile && tile.TileType != VoidTile && tile.TileColor != color)
        {
            tile.TileColor = color;
            changed = true;
        }

        if (target != PaintTarget.Blocks && tile.WallType != WallID.None && tile.WallType != VoidWall && tile.WallColor != color)
        {
            tile.WallColor = color;
            changed = true;
        }

        return changed;
    }
    #endregion
}
