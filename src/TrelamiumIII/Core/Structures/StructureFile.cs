using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModLiquidLib.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Structures;

/// <summary>
///     Reads and writes <c>.tstruct</c> files: a gzip'd <see cref="TagCompound" /> that names every tile, wall and liquid
///     type it uses, so modded IDs survive content changes. Unknown names load as empty cells and are logged.
/// </summary>
public static class StructureFile
{
    public const string Extension = ".tstruct";

    private const int Version = 1;

    /// <summary>Bytes per cell in the <c>cells</c> array; the layout is in <see cref="WriteCell" />.</summary>
    private const int CellSize = 17;

    private delegate bool TryResolve(string name, out int type);

    public static void Write(StructureData data, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside the target first, so a failed write never leaves half a structure behind.
        string temporary = path + ".tmp";

        using (FileStream stream = File.Create(temporary))
            TagIO.ToStream(ToTag(data), stream, compress: true);

        File.Move(temporary, path, overwrite: true);
    }

    public static StructureData Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Read(stream, path);
    }

    /// <param name="source">Names the file in errors and warnings.</param>
    public static StructureData Read(Stream stream, string source) => FromTag(TagIO.FromStream(stream, compressed: true), source);

    #region Cells
    private static TagCompound ToTag(StructureData data)
    {
        var tiles = new Palette(TileName);
        var walls = new Palette(WallName);
        var liquids = new Palette(LiquidName);
        var cells = new byte[data.Cells.Length * CellSize];

        for (int i = 0; i < data.Cells.Length; i++)
            WriteCell(data.Cells[i], cells.AsSpan(i * CellSize, CellSize), tiles, walls, liquids);

        return new TagCompound
        {
            ["version"] = Version,
            ["width"] = data.Width,
            ["height"] = data.Height,
            ["tiles"] = tiles.Names,
            ["walls"] = walls.Names,
            ["liquids"] = liquids.Names,
            ["cells"] = cells,
            ["chests"] = data.Chests.Select(SaveChest).ToList(),
            ["signs"] = data.Signs.Select(sign => new TagCompound { ["x"] = sign.X, ["y"] = sign.Y, ["text"] = sign.Text }).ToList(),
            ["entities"] = data.Entities.Select(entity => new TagCompound
            {
                ["x"] = entity.X,
                ["y"] = entity.Y,
                ["type"] = entity.Type,
                ["data"] = entity.Data
            }).ToList()
        };
    }

    /// <summary>
    ///     Little-endian: flags u16, tile palette index u16, frame x i16, frame y i16, frame number, block type, tile paint,
    ///     wall palette index + 1 u16 (0 = none), wall paint, wall frame number, liquid amount, liquid palette index.
    /// </summary>
    private static void WriteCell(in StructureCell cell, Span<byte> span, Palette tiles, Palette walls, Palette liquids)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)cell.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], cell.Has(CellFlags.HasTile) ? (ushort)tiles.IndexOf(cell.TileType) : (ushort)0);
        BinaryPrimitives.WriteInt16LittleEndian(span[4..], cell.TileFrameX);
        BinaryPrimitives.WriteInt16LittleEndian(span[6..], cell.TileFrameY);
        span[8] = cell.TileFrameNumber;
        span[9] = (byte)cell.BlockType;
        span[10] = cell.TileColor;
        BinaryPrimitives.WriteUInt16LittleEndian(span[11..], cell.WallType != WallID.None ? (ushort)(walls.IndexOf(cell.WallType) + 1) : (ushort)0);
        span[13] = cell.WallColor;
        span[14] = cell.WallFrameNumber;
        span[15] = cell.LiquidAmount;
        span[16] = cell.LiquidAmount > 0 ? (byte)liquids.IndexOf(cell.LiquidType) : (byte)0;
    }

    private static StructureData FromTag(TagCompound tag, string source)
    {
        int version = tag.GetInt("version");

        if (version is < 1 or > Version)
            throw new InvalidDataException($"{source} is structure format {version}; this build reads 1 to {Version}.");

        var data = new StructureData(tag.GetInt("width"), tag.GetInt("height"));
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        int[] tiles = Resolve(tag.GetList<string>("tiles"), TryResolveTile, missing);
        int[] walls = Resolve(tag.GetList<string>("walls"), TryResolveWall, missing);
        int[] liquids = Resolve(tag.GetList<string>("liquids"), TryResolveLiquid, missing);

        byte[] cells = tag.GetByteArray("cells");

        if (cells.Length != data.Cells.Length * CellSize)
            throw new InvalidDataException($"{source} has {cells.Length} bytes of cells; a {data.Width}x{data.Height} structure needs {data.Cells.Length * CellSize}.");

        for (int i = 0; i < data.Cells.Length; i++)
            data.Cells[i] = ReadCell(cells.AsSpan(i * CellSize, CellSize), tiles, walls, liquids);

        foreach (TagCompound chest in tag.GetList<TagCompound>("chests"))
            data.Chests.Add(LoadChest(chest, source));

        foreach (TagCompound sign in tag.GetList<TagCompound>("signs"))
            data.Signs.Add(new StructureSign(sign.GetInt("x"), sign.GetInt("y"), sign.GetString("text") ?? string.Empty));

        foreach (TagCompound entity in tag.GetList<TagCompound>("entities"))
            data.Entities.Add(new StructureEntity(entity.GetInt("x"), entity.GetInt("y"), entity.GetString("type"), entity.GetCompound("data")));

        if (missing.Count > 0)
            TrelamiumIII.Instance.Logger.Warn($"{source} uses types that are not loaded; those cells load without them: {string.Join(", ", missing)}");

        return data;
    }

    private static StructureCell ReadCell(ReadOnlySpan<byte> span, int[] tiles, int[] walls, int[] liquids)
    {
        var cell = new StructureCell
        {
            Flags = (CellFlags)BinaryPrimitives.ReadUInt16LittleEndian(span),
            TileFrameX = BinaryPrimitives.ReadInt16LittleEndian(span[4..]),
            TileFrameY = BinaryPrimitives.ReadInt16LittleEndian(span[6..]),
            TileFrameNumber = span[8],
            BlockType = (BlockType)span[9],
            TileColor = span[10],
            WallColor = span[13],
            WallFrameNumber = span[14],
            LiquidAmount = span[15]
        };

        if (cell.Has(CellFlags.HasTile))
        {
            int type = tiles[BinaryPrimitives.ReadUInt16LittleEndian(span[2..])];

            if (type >= 0)
                cell.TileType = (ushort)type;
            else
                cell.Flags &= ~(CellFlags.HasTile | CellFlags.Actuated | CellFlags.TileInvisible | CellFlags.TileFullbright);
        }

        int wall = BinaryPrimitives.ReadUInt16LittleEndian(span[11..]);

        if (wall > 0 && walls[wall - 1] >= 0)
        {
            cell.WallType = (ushort)walls[wall - 1];
        }
        else
        {
            cell.Flags &= ~CellFlags.WallChannel;
            cell.WallColor = cell.WallFrameNumber = 0;
        }

        if (cell.LiquidAmount > 0)
        {
            int type = liquids[span[16]];

            if (type >= 0)
                cell.LiquidType = (byte)type;
            else
                cell.LiquidAmount = 0;
        }

        return cell;
    }

    private static int[] Resolve(IList<string> names, TryResolve resolve, ISet<string> missing)
    {
        var types = new int[names.Count];

        for (int i = 0; i < names.Count; i++)
        {
            if (resolve(names[i], out types[i]))
                continue;

            types[i] = -1;
            missing.Add(names[i]);
        }

        return types;
    }

    /// <summary>First-seen order of the types a structure uses, by name.</summary>
    private sealed class Palette(Func<int, string> nameOf)
    {
        public readonly List<string> Names = [];

        private readonly Dictionary<int, int> indices = [];

        public int IndexOf(int type)
        {
            if (indices.TryGetValue(type, out int index))
                return index;

            index = Names.Count;
            Names.Add(nameOf(type));
            indices[type] = index;

            return index;
        }
    }
    #endregion

    #region Type names
    /// <summary>Vanilla types by their ID constant name, modded types by full name.</summary>
    public static string TileName(int type) => type < TileID.Count ? TileID.Search.GetName(type) : TileLoader.GetTile(type).FullName;

    public static string WallName(int type) => type < WallID.Count ? WallID.Search.GetName(type) : WallLoader.GetWall(type).FullName;

    public static string LiquidName(int type) => type < LiquidID.Count ? LiquidID.Search.GetName(type) : LiquidLoader.GetModLiquid(type).FullName;

    public static bool TryResolveTile(string name, out int type)
    {
        if (!name.Contains('/'))
            return TileID.Search.TryGetId(name, out type);

        bool found = ModContent.TryFind(name, out ModTile tile);
        type = found ? tile.Type : -1;

        return found;
    }

    public static bool TryResolveWall(string name, out int type)
    {
        if (!name.Contains('/'))
            return WallID.Search.TryGetId(name, out type);

        bool found = ModContent.TryFind(name, out ModWall wall);
        type = found ? wall.Type : -1;

        return found;
    }

    public static bool TryResolveLiquid(string name, out int type)
    {
        if (!name.Contains('/'))
            return LiquidID.Search.TryGetId(name, out type);

        for (type = LiquidID.Count; type < LiquidLoader.LiquidCount; type++)
        {
            if (LiquidLoader.GetModLiquid(type)?.FullName == name)
                return true;
        }

        type = -1;
        return false;
    }
    #endregion

    #region Chests
    private static TagCompound SaveChest(StructureChest chest)
    {
        List<TagCompound> items = [];

        for (int slot = 0; slot < chest.Items.Length; slot++)
        {
            if (chest.Items[slot] is { IsAir: false } item)
                items.Add(new TagCompound { ["slot"] = slot, ["item"] = ItemIO.Save(item) });
        }

        return new TagCompound
        {
            ["x"] = chest.X,
            ["y"] = chest.Y,
            ["name"] = chest.Name,
            ["items"] = items,
            ["loot"] = SaveRules(chest.Loot)
        };
    }

    private static StructureChest LoadChest(TagCompound tag, string source)
    {
        var chest = new StructureChest
        {
            X = tag.GetInt("x"),
            Y = tag.GetInt("y"),
            Name = tag.GetString("name") ?? string.Empty,
            Loot = LoadRules(tag.GetList<TagCompound>("loot"), source)
        };

        foreach (TagCompound entry in tag.GetList<TagCompound>("items"))
        {
            int slot = entry.GetInt("slot");

            if (slot is < 0 or >= Chest.AbsoluteMaxItemsWeCanEverReachInAChestForNow)
                continue;

            if (slot >= chest.Items.Length)
                Array.Resize(ref chest.Items, slot + 1);

            chest.Items[slot] = LoadItem(entry.GetCompound("item"), source);
        }

        return chest;
    }

    internal static List<TagCompound> SaveRules(IEnumerable<LootRule> rules) => rules.Select(rule => new TagCompound
    {
        ["mode"] = (byte)rule.Mode,
        ["chance"] = rule.Chance,
        ["count"] = rule.Count,
        ["entries"] = rule.Entries.Select(entry => new TagCompound
        {
            ["item"] = ItemIO.Save(entry.Item),
            ["min"] = entry.MinStack,
            ["max"] = entry.MaxStack,
            ["weight"] = entry.Weight
        }).ToList()
    }).ToList();

    internal static List<LootRule> LoadRules(IEnumerable<TagCompound> tags, string source)
    {
        List<LootRule> rules = [];

        foreach (TagCompound tag in tags)
        {
            var rule = new LootRule
            {
                Mode = (LootMode)tag.GetByte("mode"),
                Chance = tag.GetFloat("chance"),
                Count = tag.GetInt("count")
            };

            foreach (TagCompound entry in tag.GetList<TagCompound>("entries"))
            {
                if (LoadItem(entry.GetCompound("item"), source) is { } item)
                    rule.Entries.Add(new LootEntry(item, entry.GetInt("min"), entry.GetInt("max"), entry.GetInt("weight")));
            }

            rules.Add(rule);
        }

        return rules;
    }

    /// <summary>Null for an item that is not loaded, which would otherwise generate as an unloaded item.</summary>
    internal static Item? LoadItem(TagCompound tag, string source)
    {
        Item item = ItemIO.Load(tag);

        if (!item.IsAir && item.ModItem is not UnloadedItem)
            return item;

        TrelamiumIII.Instance.Logger.Warn($"{source} holds an item that is not loaded ({tag.GetString("mod")}/{tag.GetString("name")}); dropped.");
        return null;
    }
    #endregion
}
