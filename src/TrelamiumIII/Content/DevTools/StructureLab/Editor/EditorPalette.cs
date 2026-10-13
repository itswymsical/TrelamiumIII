using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using Terraria.Map;
using Terraria.ObjectData;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Content.Tiles.Construction.Spruce;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <param name="Item">Drawn as the slot icon; 0 draws the tile or wall texture instead.</param>
/// <param name="AlsoIn">A second category that lists the entry; sections show it under <paramref name="Category" /> only.</param>
public sealed record PaletteEntry(Swatch Swatch, int Item, string Name, string Category, string? AlsoIn = null);

/// <summary>
///     Everything the tools can write, per channel and sorted into categories. Built from the items that place things,
///     plus the blocks and walls no item places, such as moss, vines and natural cave walls.
/// </summary>
internal static partial class EditorPalette
{
    public const string Basics = "Basics";

    private static readonly Dictionary<EditorChannel, string[]> CategoryOrder = new()
    {
        [EditorChannel.Blocks] = [Basics, "Natural", "Ores & Gems", "Wood", "Bricks", "Building", "Platforms", "Lights", "Objects", "Trelamium"],
        [EditorChannel.Walls] = [Basics, "Natural", "Built", "Trelamium"],
        [EditorChannel.Liquid] = [Basics, "Liquids"],
        [EditorChannel.Paint] = [Basics, "Paints"]
    };

    private static Dictionary<EditorChannel, List<PaletteEntry>>? entries;

    public static IReadOnlyList<string> Categories(EditorChannel channel) => CategoryOrder[channel];

    /// <summary>The entries of <paramref name="category" /> (all of them for null) whose name contains <paramref name="search" />, in category order.</summary>
    public static IEnumerable<PaletteEntry> Query(EditorChannel channel, string? category, string search)
    {
        Build();

        return entries![channel].Where(entry => (category is null || entry.Category == category || entry.AlsoIn == category || entry.Category == Basics)
                                                && (search.Length == 0 || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The entry that writes <paramref name="swatch" />, or a bare one named after it.</summary>
    public static PaletteEntry Find(EditorChannel channel, Swatch swatch)
    {
        Build();

        return entries![channel].Find(entry => entry.Swatch == swatch) ?? new PaletteEntry(swatch, 0, Describe(swatch), Basics);
    }

    public static void Unload() => entries = null;

    #region Building
    private static void Build()
    {
        if (entries is not null)
            return;

        List<PaletteEntry> blocks = [new(Swatch.Empty, 0, "Air", Basics), new(Swatch.Void, 0, "Void", Basics)];
        List<PaletteEntry> walls = [new(Swatch.Empty, 0, "No wall", Basics), new(Swatch.Void, 0, "Void", Basics)];
        List<PaletteEntry> liquids = [new(Swatch.Empty, 0, "No liquid", Basics)];
        List<PaletteEntry> paints = [new(Swatch.Empty, 0, "No paint", Basics)];

        HashSet<Swatch> seen = [];
        HashSet<int> woodTiles = WoodTiles();
        int voidTile = ModContent.TileType<StructureVoidTile>();
        int voidWall = ModContent.WallType<StructureVoidWall>();

        foreach ((int itemType, Item item) in ContentSamples.ItemsByType)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                continue;

            if (item.createTile >= TileID.Dirt && item.createTile != voidTile && IsPaintable(item.createTile, item.placeStyle))
            {
                int style = Main.tileFrameImportant[item.createTile] ? item.placeStyle : 0;
                var swatch = new Swatch(SwatchKind.Tile, item.createTile, style);

                // Grass is placed by its seeds; the block is what gets painted, so it keeps the block's name.
                bool grass = TileID.Sets.Conversion.Grass[item.createTile] || TileID.Sets.GrassSpecial[item.createTile];

                if (seen.Add(swatch))
                    blocks.Add(BlockEntry(swatch, itemType, grass ? TileLabel(item.createTile) : item.Name, woodTiles));
            }
            else if (item.createWall > WallID.None && item.createWall != voidWall)
            {
                var swatch = new Swatch(SwatchKind.Wall, item.createWall);

                if (seen.Add(swatch))
                    walls.Add(WallEntry(swatch, itemType, item.Name));
            }
            else if (item.paint > 0)
            {
                var swatch = new Swatch(SwatchKind.Paint, item.paint);

                if (seen.Add(swatch))
                    paints.Add(new PaletteEntry(swatch, itemType, item.Name, "Paints"));
            }
        }

        AddItemlessBlocks(blocks, seen, woodTiles, voidTile);
        AddItemlessWalls(walls, seen, voidWall);
        AddLiquids(liquids);

        entries = new Dictionary<EditorChannel, List<PaletteEntry>>
        {
            [EditorChannel.Blocks] = Sorted(EditorChannel.Blocks, blocks),
            [EditorChannel.Walls] = Sorted(EditorChannel.Walls, walls),
            [EditorChannel.Liquid] = liquids,
            [EditorChannel.Paint] = paints
        };
    }

    /// <summary>Blocks and one-tile objects; larger objects are placed another way.</summary>
    private static bool IsPaintable(int type, int style)
        => !Main.tileFrameImportant[type] || TileObjectData.GetTileData(type, style) is not { } data || data.Width * data.Height == 1;

    private static void AddItemlessBlocks(List<PaletteEntry> blocks, HashSet<Swatch> seen, HashSet<int> woodTiles, int voidTile)
    {
        for (int type = 0; type < TileLoader.TileCount; type++)
        {
            if (Main.tileFrameImportant[type] || type == voidTile || TileID.Sets.IsATreeTrunk[type] || type == TileID.Cactus)
                continue;

            var swatch = new Swatch(SwatchKind.Tile, type);

            if (seen.Add(swatch))
                blocks.Add(BlockEntry(swatch, 0, TileLabel(type), woodTiles));
        }
    }

    private static void AddItemlessWalls(List<PaletteEntry> walls, HashSet<Swatch> seen, int voidWall)
    {
        for (int type = 1; type < WallLoader.WallCount; type++)
        {
            var swatch = new Swatch(SwatchKind.Wall, type);

            if (type != voidWall && seen.Add(swatch))
                walls.Add(WallEntry(swatch, 0, WallLabel(type)));
        }
    }

    /// <summary>
    ///     This mod's blocks go under Trelamium and are also listed under the category of their kind, unless that is only the
    ///     Building fallback.
    /// </summary>
    private static PaletteEntry BlockEntry(Swatch swatch, int item, string name, HashSet<int> woodTiles)
    {
        string kind = BlockCategory(swatch.Type, woodTiles);

        return IsTrelamium(TileLoader.GetTile(swatch.Type))
            ? new PaletteEntry(swatch, item, name, "Trelamium", kind == "Building" ? null : kind)
            : new PaletteEntry(swatch, item, name, kind);
    }

    private static PaletteEntry WallEntry(Swatch swatch, int item, string name)
    {
        string kind = Main.wallHouse[swatch.Type] ? "Built" : "Natural";

        return IsTrelamium(WallLoader.GetWall(swatch.Type))
            ? new PaletteEntry(swatch, item, name, "Trelamium", kind)
            : new PaletteEntry(swatch, item, name, kind);
    }

    private static bool IsTrelamium(ModType? content) => content?.Mod == TrelamiumIII.Instance;

    private static void AddLiquids(List<PaletteEntry> liquids)
    {
        int[] vanillaBuckets = [ItemID.WaterBucket, ItemID.LavaBucket, ItemID.HoneyBucket, ItemID.BottomlessShimmerBucket];

        for (int type = 0; type < LiquidLoader.LiquidCount; type++)
        {
            bool vanilla = type < LiquidID.Count;

            // ModLiquidLib's stand-in for liquids from unloaded mods.
            if (!vanilla && LiquidLoader.GetModLiquid(type)?.Mod.Name == "ModLiquidLib")
                continue;
            int bucket = vanilla ? vanillaBuckets[type] : LiquidID_TLmod.Sets.CreateLiquidBucketItem[type];
            string name = vanilla ? LiquidID.Search.GetName(type) : Pretty(LiquidLoader.GetModLiquid(type).Name.Replace("Liquid", string.Empty));

            liquids.Add(new PaletteEntry(new Swatch(SwatchKind.Liquid, type), Math.Max(0, bucket), name, "Liquids"));
        }
    }

    private static List<PaletteEntry> Sorted(EditorChannel channel, List<PaletteEntry> list)
    {
        string[] order = CategoryOrder[channel];

        // OrderBy is stable, so each category keeps item order, which groups related blocks.
        return [.. list.OrderBy(entry => Array.IndexOf(order, entry.Category))];
    }
    #endregion

    #region Categories
    private static string BlockCategory(int type, HashSet<int> woodTiles)
    {
        if (TileID.Sets.Platforms[type])
            return "Platforms";

        if (Main.tileFrameImportant[type])
            return TileID.Sets.Torch[type] || Main.tileLighted[type] ? "Lights" : "Objects";

        if (TileID.Sets.Ore[type] || Main.tileSpelunker[type])
            return "Ores & Gems";

        if (IsNatural(type))
            return "Natural";

        if (woodTiles.Contains(type))
            return "Wood";

        return Main.tileBrick[type] || TileID.Sets.CrackedBricks[type] ? "Bricks" : "Building";
    }

    private static bool IsNatural(int type)
    {
        return TileID.Sets.Conversion.Stone[type] || TileID.Sets.Conversion.Grass[type] || TileID.Sets.Conversion.Sand[type]
               || TileID.Sets.Conversion.HardenedSand[type] || TileID.Sets.Conversion.Sandstone[type] || TileID.Sets.Conversion.Ice[type]
               || TileID.Sets.Conversion.Moss[type] || TileID.Sets.Conversion.Dirt[type] || TileID.Sets.Conversion.Snow[type]
               || TileID.Sets.Conversion.Thorn[type] || TileID.Sets.CanBeDugByShovel[type] || TileID.Sets.IsVine[type]
               || TileID.Sets.Leaves[type] || TileID.Sets.Clouds[type] || Main.tileMoss[type]
               || type is TileID.Marble or TileID.Granite or TileID.Hive or TileID.Obsidian or TileID.LivingWood or TileID.LeafBlock
                   or TileID.LivingMahogany or TileID.LivingMahoganyLeaves or TileID.Ash or TileID.Hellstone;
    }

    /// <summary>Tiles placed by the items of the Wood recipe group, modded woods registered to it included, and this mod's woods.</summary>
    private static HashSet<int> WoodTiles()
    {
        HashSet<int> tiles = [ModContent.TileType<BramblewoodTile>(), ModContent.TileType<BramblerootTile>(), ModContent.TileType<SpruceWoodTile>()];

        if (RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out RecipeGroup? group))
        {
            foreach (int item in group.ValidItems)
            {
                if (ContentSamples.ItemsByType.TryGetValue(item, out Item? sample) && sample.createTile >= TileID.Dirt)
                    tiles.Add(sample.createTile);
            }
        }

        return tiles;
    }
    #endregion

    #region Names
    private static string Describe(Swatch swatch) => swatch.Kind switch
    {
        SwatchKind.Empty => "Nothing",
        SwatchKind.Void => "Void",
        SwatchKind.Tile => TileLabel(swatch.Type),
        SwatchKind.Wall => WallLabel(swatch.Type),
        SwatchKind.Liquid => LiquidID.Search.TryGetName(swatch.Type, out string? name) ? name : $"Liquid {swatch.Type}",
        _ => $"Paint {swatch.Type}"
    };

    private static string TileLabel(int type)
    {
        if (type >= TileID.Count)
            return Pretty(TileLoader.GetTile(type).Name.Replace("Tile", string.Empty));

        string map = Lang.GetMapObjectName(MapHelper.TileToLookup(type, 0));
        return string.IsNullOrWhiteSpace(map) ? Pretty(TileID.Search.GetName(type)) : map;
    }

    private static string WallLabel(int type)
    {
        if (type >= WallID.Count)
            return Pretty(WallLoader.GetWall(type).Name.Replace("Wall", string.Empty)) + " Wall";

        string name = Pretty(WallID.Search.GetName(type));
        string map = Lang.GetMapObjectName(MapHelper.wallLookup[type]);

        // Map names drop the "unsafe" that tells a natural wall from its placed twin, so the ID name is kept for those.
        return string.IsNullOrWhiteSpace(map) || !Main.wallHouse[type] ? name : map;
    }

    /// <summary>"DirtUnsafe" to "Dirt Unsafe".</summary>
    private static string Pretty(string name) => WordBoundary().Replace(name, " ").Trim();

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex WordBoundary();
    #endregion
}
