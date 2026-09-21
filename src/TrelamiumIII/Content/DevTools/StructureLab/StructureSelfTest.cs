using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab;

/// <summary>
///     Places a structure for editing, captures it back and compares the two, then restores the tiles it borrowed. Logs an
///     ASCII picture of what came back.
/// </summary>
internal static class StructureSelfTest
{
    private const int MaxReported = 12;

    /// <returns>"OK", or the first differences found.</returns>
    public static string Run(string name, StructureData expected, Point origin)
    {
        var area = new Rectangle(origin.X, origin.Y, expected.Width, expected.Height);

        if (!StructureWorld.InWorld(area))
            return "does not fit in this world";

        StructureData backup = StructureWorld.Capture(area);
        StructureData actual;

        try
        {
            StructureWorld.Place(expected, origin, editable: true);
            actual = StructureWorld.Capture(area);
        }
        finally
        {
            StructureWorld.Place(backup, origin, editable: true);
        }

        TrelamiumIII.Instance.Logger.Info($"Structure self-test, {name} as placed:\n{Picture(actual)}");

        List<string> differences = Compare(expected, actual);

        return differences.Count == 0
            ? "OK"
            : $"{differences.Count} differences: {string.Join("; ", differences.Take(MaxReported))}";
    }

    private static List<string> Compare(StructureData expected, StructureData actual)
    {
        List<string> differences = [];

        for (int x = 0; x < expected.Width; x++)
        {
            for (int y = 0; y < expected.Height; y++)
            {
                if (CompareCell(expected[x, y], actual[x, y]) is { } difference)
                    differences.Add($"({x}, {y}) {difference}");
            }
        }

        foreach (StructureChest chest in expected.Chests)
        {
            StructureChest? match = actual.Chests.Find(other => other.X == chest.X && other.Y == chest.Y);

            if (match is null)
                differences.Add($"chest at ({chest.X}, {chest.Y}) missing");
            else if (Describe(chest) != Describe(match))
                differences.Add($"chest at ({chest.X}, {chest.Y}) holds {Describe(match)}, expected {Describe(chest)}");
        }

        if (actual.Chests.Count != expected.Chests.Count)
            differences.Add($"{actual.Chests.Count} chests, expected {expected.Chests.Count}");

        if (!actual.Signs.OrderBy(sign => (sign.X, sign.Y)).SequenceEqual(expected.Signs.OrderBy(sign => (sign.X, sign.Y))))
            differences.Add("signs differ");

        var expectedEntities = expected.Entities.Select(entity => (entity.X, entity.Y, entity.Type)).Order().ToList();
        var actualEntities = actual.Entities.Select(entity => (entity.X, entity.Y, entity.Type)).Order().ToList();

        if (!actualEntities.SequenceEqual(expectedEntities))
            differences.Add($"tile entities [{string.Join(", ", actualEntities)}], expected [{string.Join(", ", expectedEntities)}]");

        return differences;
    }

    private static string? CompareCell(in StructureCell expected, in StructureCell actual)
    {
        if (expected.Flags != actual.Flags)
            return $"flags {actual.Flags}, expected {expected.Flags}";

        if (expected.Has(CellFlags.HasTile))
        {
            if (actual.TileType != expected.TileType)
                return $"tile {StructureFile.TileName(actual.TileType)}, expected {StructureFile.TileName(expected.TileType)}";

            if (actual.BlockType != expected.BlockType || actual.TileColor != expected.TileColor)
                return $"{StructureFile.TileName(actual.TileType)} shape or paint changed";

            // Framing recomputes block frames from their neighbours and re-rolls tree frames; only other multitile frames are data.
            bool framesAreData = Main.tileFrameImportant[expected.TileType] && !TileID.Sets.IsATreeTrunk[expected.TileType];

            if (framesAreData && (actual.TileFrameX != expected.TileFrameX || actual.TileFrameY != expected.TileFrameY))
                return $"{StructureFile.TileName(actual.TileType)} frame ({actual.TileFrameX}, {actual.TileFrameY}), expected ({expected.TileFrameX}, {expected.TileFrameY})";
        }

        if (actual.WallType != expected.WallType || actual.WallColor != expected.WallColor)
            return $"wall {actual.WallType}/{actual.WallColor}, expected {expected.WallType}/{expected.WallColor}";

        if (actual.LiquidAmount != expected.LiquidAmount || (expected.LiquidAmount > 0 && actual.LiquidType != expected.LiquidType))
            return $"liquid {actual.LiquidType}x{actual.LiquidAmount}, expected {expected.LiquidType}x{expected.LiquidAmount}";

        return null;
    }

    private static string Describe(StructureChest chest)
    {
        IEnumerable<string> items = chest.Items
            .Select((item, slot) => item is null ? null : $"{slot}:{item.type}x{item.stack}p{item.prefix}")
            .OfType<string>();

        return $"'{chest.Name}' [{string.Join(" ", items)}] {chest.Loot.Count} loot rules";
    }

    /// <summary>
    ///     One character per cell: <c>#</c> block, <c>=</c> platform, <c>o</c> multitile, <c>+</c> other tile,
    ///     <c>~</c> liquid, <c>:</c> wall only, <c>.</c> air, space for void.
    /// </summary>
    private static string Picture(StructureData data)
    {
        var picture = new StringBuilder();

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                StructureCell cell = data[x, y];
                ushort type = cell.TileType;

                picture.Append(cell switch
                {
                    _ when cell.Has(CellFlags.TileVoid) => ' ',
                    _ when !cell.Has(CellFlags.HasTile) => cell.LiquidAmount > 0 ? '~' : cell.WallType != WallID.None ? ':' : '.',
                    _ when Main.tileFrameImportant[type] => 'o',
                    _ when Main.tileSolidTop[type] => '=',
                    _ when Main.tileSolid[type] => '#',
                    _ => '+'
                });
            }

            picture.Append('\n');
        }

        return picture.ToString();
    }
}
