using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace TrelamiumIII.Core.Structures;

/// <summary>
///     Reads StructureHelper 3 <c>.shstruct</c> files without StructureHelper. Null blocks and walls become void channels,
///     chest rules become <see cref="LootRule" />s, and content saved under this mod's former name is remapped.
/// </summary>
public static class StructureHelperReader
{
    public const string Extension = ".shstruct";

    private const string Header = "STRUCTURE_HELPER_STRUCTURE";
    private const string NullBlock = "StructureHelper/Content/Tiles/NullBlock";
    private const string NullWall = "StructureHelper/Content/Tiles/NullWall";
    private const string ChestEntity = "StructureHelper/ChestEntity";

    /// <summary>Resolved type of a null block or null wall.</summary>
    private const int VoidType = -2;

    /// <summary>Mod names this mod's content was saved under before it was renamed.</summary>
    private static readonly string[] FormerNames = ["SubstrateMod"];

    private delegate bool TryResolveName(string name, out int type);

    public static StructureData Read(string path, out List<string> warnings)
    {
        using FileStream stream = File.OpenRead(path);
        return Read(stream, path, out warnings);
    }

    /// <param name="warnings">Content that could not be carried over; those cells load empty.</param>
    public static StructureData Read(Stream stream, string source, out List<string> warnings)
    {
        using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        using var reader = new BinaryReader(gzip);

        if (reader.ReadString() != Header)
            throw new InvalidDataException($"{source} is not a StructureHelper 3 structure.");

        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        bool hasEntries = reader.ReadBoolean();
        reader.ReadString();

        Dictionary<ushort, string> tileNames = ReadTypeTable(reader);
        Dictionary<ushort, string> wallNames = ReadTypeTable(reader);

        // Which columns hold null blocks; the cell data says the same.
        for (int x = 0; x < width; x++)
            reader.ReadBoolean();

        Dictionary<string, byte[]> planes = [];
        int planeCount = reader.ReadInt32();

        for (int i = 0; i < planeCount; i++)
        {
            string key = reader.ReadString();
            planes[key] = reader.ReadBytes(reader.ReadInt32());
        }

        var data = new StructureData(width, height);
        var problems = new SortedSet<string>(StringComparer.Ordinal);

        ReadCells(data, planes, tileNames, wallNames, problems, source);

        if (hasEntries)
            ReadEntries(data, TagIO.Read(reader), problems, source);

        AddPlainContainers(data);

        warnings = [.. problems];
        return data;
    }

    /// <summary>StructureHelper only made chests for containers with loot rules; the rest get an empty chest.</summary>
    private static void AddPlainContainers(StructureData data)
    {
        for (int x = 0; x < data.Width; x++)
        {
            for (int y = 0; y < data.Height; y++)
            {
                StructureCell cell = data[x, y];

                if (!cell.Has(CellFlags.HasTile) || !Main.tileContainer[cell.TileType] || TileObjectData.GetTileData(cell.TileType, 0) is not { } tileData)
                    continue;

                if (cell.TileFrameX % tileData.CoordinateFullWidth == 0 && cell.TileFrameY % tileData.CoordinateFullHeight == 0)
                    ChestAt(data, x, y);
            }
        }
    }

    private static Dictionary<ushort, string> ReadTypeTable(BinaryReader reader)
    {
        Dictionary<ushort, string> names = [];
        int count = reader.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            ushort type = reader.ReadUInt16();
            names[type] = Remap(reader.ReadString());
        }

        return names;
    }

    #region Cells
    private static void ReadCells(StructureData data, Dictionary<string, byte[]> planes, Dictionary<ushort, string> tileNames,
        Dictionary<ushort, string> wallNames, ISet<string> problems, string source)
    {
        int count = data.Cells.Length;
        ReadOnlySpan<TileTypeData> tiles = Plane<TileTypeData>(planes, "Terraria/TileTypeData", count, source);
        ReadOnlySpan<WallTypeData> walls = Plane<WallTypeData>(planes, "Terraria/WallTypeData", count, source);
        ReadOnlySpan<LiquidData> liquids = Plane<LiquidData>(planes, "Terraria/LiquidData", count, source);
        ReadOnlySpan<TileWallWireStateData> states = Plane<TileWallWireStateData>(planes, "Terraria/TileWallWireStateData", count, source);
        ReadOnlySpan<TileWallBrightnessInvisibilityData> coatings =
            Plane<TileWallBrightnessInvisibilityData>(planes, "Terraria/TileWallBrightnessInvisibilityData", count, source);

        for (int i = 0; i < count; i++)
        {
            ref StructureCell cell = ref data.Cells[i];
            TileWallWireStateData state = states[i];

            ReadTile(ref cell, tiles[i].Type, state, coatings[i], tileNames, problems);

            if (!cell.Has(CellFlags.TileVoid))
            {
                cell.Flags |= Flag(CellFlags.HasActuator, state.HasActuator) | Flag(CellFlags.RedWire, state.RedWire)
                    | Flag(CellFlags.BlueWire, state.BlueWire) | Flag(CellFlags.GreenWire, state.GreenWire)
                    | Flag(CellFlags.YellowWire, state.YellowWire);

                ReadLiquid(ref cell, liquids[i], problems);
            }

            ReadWall(ref cell, walls[i].Type, state, coatings[i], wallNames, problems);
        }
    }

    private static void ReadTile(ref StructureCell cell, ushort type, TileWallWireStateData state, TileWallBrightnessInvisibilityData coating,
        Dictionary<ushort, string> names, ISet<string> problems)
    {
        if (!state.HasTile)
            return;

        if (!TryResolve(type, TileID.Count, names, StructureFile.TryResolveTile, NullBlock, problems, out int resolved))
        {
            if (resolved == VoidType)
                cell.VoidTile();

            return;
        }

        cell.Flags |= CellFlags.HasTile | Flag(CellFlags.Actuated, state.IsActuated)
            | Flag(CellFlags.TileInvisible, coating.IsTileInvisible) | Flag(CellFlags.TileFullbright, coating.IsTileFullbright);

        cell.TileType = (ushort)resolved;
        cell.TileFrameX = state.TileFrameX;
        cell.TileFrameY = state.TileFrameY;
        cell.TileFrameNumber = (byte)state.TileFrameNumber;
        cell.BlockType = state.BlockType;
        cell.TileColor = state.TileColor;
    }

    private static void ReadWall(ref StructureCell cell, ushort type, TileWallWireStateData state, TileWallBrightnessInvisibilityData coating,
        Dictionary<ushort, string> names, ISet<string> problems)
    {
        if (type == WallID.None)
            return;

        if (!TryResolve(type, WallID.Count, names, StructureFile.TryResolveWall, NullWall, problems, out int resolved))
        {
            if (resolved == VoidType)
                cell.VoidWall();

            return;
        }

        cell.Flags |= Flag(CellFlags.WallInvisible, coating.IsWallInvisible) | Flag(CellFlags.WallFullbright, coating.IsWallFullbright);
        cell.WallType = (ushort)resolved;
        cell.WallColor = state.WallColor;
        cell.WallFrameNumber = (byte)state.WallFrameNumber;
    }

    /// <summary>StructureHelper saved no liquid names, so only vanilla liquids carry over.</summary>
    private static void ReadLiquid(ref StructureCell cell, LiquidData liquid, ISet<string> problems)
    {
        if (liquid.Amount == 0)
            return;

        if (liquid.LiquidType >= LiquidID.Count)
        {
            problems.Add($"modded liquid #{liquid.LiquidType}");
            return;
        }

        cell.LiquidAmount = liquid.Amount;
        cell.LiquidType = (byte)liquid.LiquidType;
    }

    /// <summary>
    ///     Maps a saved type to a runtime one. False for the null marker (<paramref name="resolved" /> is
    ///     <see cref="VoidType" />) and for types that no longer exist.
    /// </summary>
    private static bool TryResolve(ushort saved, int vanillaCount, Dictionary<ushort, string> names, TryResolveName resolve,
        string nullMarker, ISet<string> problems, out int resolved)
    {
        if (!names.TryGetValue(saved, out string? name))
        {
            resolved = saved < vanillaCount ? saved : -1;

            if (resolved < 0)
                problems.Add($"unnamed modded type #{saved}");

            return resolved >= 0;
        }

        if (name == nullMarker)
        {
            resolved = VoidType;
            return false;
        }

        if (resolve(name, out resolved))
            return true;

        problems.Add(name);
        resolved = -1;
        return false;
    }

    private static ReadOnlySpan<T> Plane<T>(Dictionary<string, byte[]> planes, string key, int cells, string source) where T : unmanaged
    {
        if (!planes.TryGetValue(key, out byte[]? bytes))
            throw new InvalidDataException($"{source} has no {key}.");

        if (bytes.Length != cells * Unsafe.SizeOf<T>())
            throw new InvalidDataException($"{source}: {key} is {bytes.Length} bytes; {cells} cells of {typeof(T).Name} are {cells * Unsafe.SizeOf<T>()}.");

        return MemoryMarshal.Cast<byte, T>(bytes);
    }

    private static CellFlags Flag(CellFlags flag, bool value) => value ? flag : CellFlags.None;
    #endregion

    #region Entries
    private static void ReadEntries(StructureData data, TagCompound tag, ISet<string> problems, string source)
    {
        foreach (TagCompound entry in tag.GetList<TagCompound>("nbtEntries"))
        {
            int x = entry.GetInt("x");
            int y = entry.GetInt("y");

            if (!data.Contains(x, y))
                continue;

            if (entry.ContainsKey("text"))
            {
                data.Signs.Add(new StructureSign(x, y, entry.GetString("text")));
                continue;
            }

            string type = Remap(entry.GetString("tileEntityType"));
            TagCompound entityData = entry.GetCompound("tileEntityData");

            RemapItems(entityData);

            if (type == ChestEntity)
            {
                ChestAt(data, x, y).Loot = ReadRules(entityData, problems, source);
                continue;
            }

            if (!StructureWorld.TryResolveEntity(type, out _))
            {
                problems.Add(type);
                continue;
            }

            data.Entities.Add(new StructureEntity(x, y, type, entityData));
        }
    }

    private static StructureChest ChestAt(StructureData data, int x, int y)
    {
        StructureChest? chest = data.Chests.Find(chest => chest.X == x && chest.Y == y);

        if (chest is null)
        {
            chest = new StructureChest { X = x, Y = y };
            data.Chests.Add(chest);
        }

        return chest;
    }

    /// <summary>
    ///     StructureHelper's four rule kinds. Its stack range excluded the maximum and its pools used
    ///     <see cref="Main.rand" />; these take the range as written and roll with the world seed.
    /// </summary>
    private static List<LootRule> ReadRules(TagCompound tag, ISet<string> problems, string source)
    {
        List<LootRule> rules = [];
        int count = tag.GetInt("Count");

        for (int i = 0; i < count; i++)
        {
            TagCompound saved = tag.GetCompound("Rule" + i);
            string kind = saved.GetString("Type");

            LootRule? rule = kind switch
            {
                "Guaranteed" => new LootRule { Mode = LootMode.All },
                "Chance" => new LootRule { Mode = LootMode.All, Chance = saved.GetFloat("Chance") },
                "Pool" => new LootRule { Mode = LootMode.Pick, Count = saved.GetInt("ToGenerate") },
                "PoolChance" => new LootRule { Mode = LootMode.Pick, Count = saved.GetInt("ToGenerate"), Chance = saved.GetFloat("Chance") },
                _ => null
            };

            if (rule is null)
            {
                problems.Add($"chest rule '{kind}'");
                continue;
            }

            TagCompound pool = saved.GetCompound("Pool");
            int entries = pool.GetInt("Count");

            for (int j = 0; j < entries; j++)
            {
                TagCompound loot = pool.GetCompound("Pool" + j);

                if (StructureFile.LoadItem(loot.GetCompound("Item"), source) is { } item)
                    rule.Entries.Add(new LootEntry(item, loot.GetInt("Min"), loot.GetInt("Max"), loot.GetInt("Weight")));
                else
                    problems.Add($"loot item {loot.GetCompound("Item").GetString("mod")}/{loot.GetCompound("Item").GetString("name")}");
            }

            rules.Add(rule);
        }

        return rules;
    }

    private static string Remap(string name)
    {
        foreach (string former in FormerNames)
        {
            if (name.StartsWith(former + "/", StringComparison.Ordinal))
                return TrelamiumIII.Instance.Name + name[former.Length..];
        }

        return name;
    }

    /// <summary>Renames former mod names inside saved items, and drops global item data whose global item is gone.</summary>
    private static void RemapItems(TagCompound tag)
    {
        foreach (string key in tag.Select(pair => pair.Key).ToList())
        {
            switch (tag[key])
            {
                case string mod when key == "mod" && FormerNames.Contains(mod):
                    tag[key] = TrelamiumIII.Instance.Name;
                    break;

                case TagCompound child:
                    RemapItems(child);
                    break;

                case List<TagCompound> children:
                    children.ForEach(RemapItems);

                    if (key == "globalData")
                        children.RemoveAll(global => !ModContent.TryFind<GlobalItem>(global.GetString("mod") + "/" + global.GetString("name"), out _));

                    break;
            }
        }
    }
    #endregion
}
