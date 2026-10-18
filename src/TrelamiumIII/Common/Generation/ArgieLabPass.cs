using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.NPCs.WorldNPCs.Argie;
using TrelamiumIII.Content.Seals;
using TrelamiumIII.Core.NPCs.Housing;
using TrelamiumIII.Core.Structures;
using TrelamiumIII.Core.Tiles.Seals;

namespace TrelamiumIII.Common.Generation;

/// <summary>
///     Places Argie's lab in the glowing mushroom biome nearest the world spawn, protects it, and reserves it as her
///     home, so the housing system spawns her inside once worldgen finishes.
/// </summary>
/// <remarks>
///     <para>
///         Biomes come from <see cref="GenVars.mushroomBiomesPosition" />, which vanilla fills during Mushroom Patches,
///         or from a scan for mushroom grass when that is empty (the dev command in a world that has already
///         generated). Within a biome, the site taken has the most mushroom grass around it, a little open cave inside
///         the lab's pocket so it joins the tunnels, and no liquid, chest or dungeon or temple brick near it.
///     </para>
///     <para>Runs after Final Cleanup, like the other story structures.</para>
/// </remarks>
public class ArgieLabPass : GenPass
{
    private const string LabPath = "Assets/Structures/ArgiesLab";

    /// <summary>Open floor in front of the alchemy station, in the middle of the main room.</summary>
    private static readonly StructureAnchor StandTile = StructureAnchor.Tile(23, 28);

    /// <summary>Tiles around the footprint counted toward its biome.</summary>
    private const int BiomeMargin = 12;

    /// <summary>Mushroom grass the biome window needs for a site to count as inside the biome.</summary>
    private const int MinMushroomGrass = 60;

    /// <summary>Tiles around the footprint that must be free of liquid, chests and dungeon or temple blocks.</summary>
    private const int HazardMargin = 4;

    /// <summary>Wet tiles allowed in the hazard window; any lava refuses the site.</summary>
    private const int MaxWetTiles = 12;

    /// <summary>How far from a biome's centre, in tiles, the lab's centre is searched.</summary>
    private const int SearchX = 70;

    private const int SearchY = 45;

    private const int SearchStep = 3;

    private const int StructurePadding = 4;

    /// <summary>The scan's window, in tiles, when <see cref="GenVars.mushroomBiomesPosition" /> is empty.</summary>
    private const int ScanCell = 40;

    private static readonly HashSet<ushort> ForbiddenTiles =
    [
        TileID.Containers, TileID.Containers2, TileID.BlueDungeonBrick, TileID.GreenDungeonBrick, TileID.PinkDungeonBrick,
        TileID.LihzahrdBrick, TileID.LihzahrdAltar, TileID.ShimmerBlock
    ];

    /// <summary>
    ///     <see cref="TileID.Sets.GeneralPlacementTiles" /> with mushroom grass allowed: vanilla leaves it out, which refuses
    ///     every site inside the biome the lab belongs in.
    /// </summary>
    private static bool[] PlaceableOver = [];

    /// <summary>Why candidate sites were turned down, logged when no site is found.</summary>
    private static readonly Dictionary<string, int> Refusals = [];

    public ArgieLabPass() : base("[T3] Argie's Lab", 5f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Culturing a laboratory...";

        if (TryPlace(out PlacedStructure placed))
        {
            TrelamiumIII.Instance.Logger.Info($"Placed Argie's lab at {placed.Origin} ({placed.Area.Width}x{placed.Area.Height}).");
        }
        else
        {
            TrelamiumIII.Instance.Logger.Warn("Found no glowing mushroom biome with room for Argie's lab; she will not be in this world. "
                + $"Biomes: {GenVars.numMushroomBiomes}. Refused: {string.Join(", ", Refusals.Select(r => $"{r.Key} {r.Value}"))}.");
        }

        progress.Set(1.0);
    }

    /// <summary>
    ///     Finds a site, places the lab, seals it and reserves it for Argie. Usable at runtime in singleplayer, where the
    ///     caller spawns her (<see cref="WorldNPCHousing.SpawnMissingResidents" />).
    /// </summary>
    public static bool TryPlace(out PlacedStructure placed)
    {
        placed = default;
        Point16 size = Structures.Dimensions(LabPath);
        Refusals.Clear();
        PlaceableOver = (bool[])TileID.Sets.GeneralPlacementTiles.Clone();
        PlaceableOver[TileID.MushroomGrass] = true;

        foreach (Point biome in BiomesNearestSpawn())
        {
            if (!TryFindSite(biome, size, out Point origin))
                continue;

            placed = Structures.Place(LabPath, origin);
            FrameRing(placed.Area);

            SealSystem.Seal(placed.Area, ModContent.GetInstance<WardSeal>());

            if (WorldGen.generatingWorld)
                GenVars.structures.AddProtectedStructure(placed.Area, StructurePadding);

            placed.ReserveFor<ArgieNPC>(StandTile);

            return true;
        }

        return false;
    }

    private static IEnumerable<Point> BiomesNearestSpawn()
    {
        List<Point> biomes = GenVars.numMushroomBiomes > 0
            ? GenVars.mushroomBiomesPosition.Take(GenVars.numMushroomBiomes).ToList()
            : ScanForBiomes();

        return biomes.OrderBy(biome => Math.Abs(biome.X - Main.spawnTileX));
    }

    /// <summary>Centres of the <see cref="ScanCell" />-sized windows with the most mushroom grass, below the surface.</summary>
    private static List<Point> ScanForBiomes()
    {
        var found = new List<(Point Center, int Count)>();

        for (int left = 50; left + ScanCell < Main.maxTilesX - 50; left += ScanCell)
        {
            for (int top = (int)Main.worldSurface; top + ScanCell < Main.maxTilesY - 50; top += ScanCell)
            {
                int count = CountMushroomGrass(new Rectangle(left, top, ScanCell, ScanCell));

                if (count >= MinMushroomGrass)
                    found.Add((new Point(left + ScanCell / 2, top + ScanCell / 2), count));
            }
        }

        return found.OrderByDescending(cell => cell.Count).Select(cell => cell.Center).ToList();
    }

    private static bool TryFindSite(Point biome, Point16 size, out Point origin)
    {
        origin = default;
        int bestScore = int.MinValue;

        for (int dx = -SearchX; dx <= SearchX; dx += SearchStep)
        {
            for (int dy = -SearchY; dy <= SearchY; dy += SearchStep)
            {
                var area = new Rectangle(biome.X + dx - size.X / 2, biome.Y + dy - size.Y / 2, size.X, size.Y);

                if (area.Top < Main.worldSurface + 20 || !StructureWorld.InWorld(area))
                {
                    Refuse("bounds");
                    continue;
                }

                if (!Accepts(area))
                    continue;

                int mushroomGrass = CountMushroomGrass(Inflate(area, BiomeMargin));

                if (mushroomGrass < MinMushroomGrass)
                {
                    Refuse("biome");
                    continue;
                }

                int score = mushroomGrass * 2 + CountOpen(area) - (Math.Abs(dx) + Math.Abs(dy)) / 2;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                origin = area.Location;
            }
        }

        return bestScore > int.MinValue;
    }

    private static bool Accepts(Rectangle area)
    {
        if (WorldGen.generatingWorld && !GenVars.structures.CanPlace(area, PlaceableOver, StructurePadding))
            return Refuse("structure");

        if (WorldNPCHousing.HomeIntersecting(Inflate(area, StructurePadding)) is not null)
            return Refuse("home");

        Rectangle hazards = Inflate(area, HazardMargin);
        int wet = 0;

        for (int x = hazards.Left; x < hazards.Right; x++)
        {
            for (int y = hazards.Top; y < hazards.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if ((tile.HasTile && ForbiddenTiles.Contains(tile.TileType)) || tile.WallType == WallID.LihzahrdBrickUnsafe)
                    return Refuse("forbidden");

                if (tile.LiquidAmount == 0)
                    continue;

                if (tile.LiquidType != LiquidID.Water)
                    return Refuse("liquid:" + tile.LiquidType);

                if (++wet > MaxWetTiles)
                    return Refuse("water");
            }
        }

        return true;
    }

    private static bool Refuse(string reason)
    {
        Refusals[reason] = Refusals.GetValueOrDefault(reason) + 1;
        return false;
    }

    private static int CountMushroomGrass(Rectangle area)
    {
        area = Rectangle.Intersect(area, new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY));
        int count = 0;

        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && tile.TileType == TileID.MushroomGrass)
                    count++;
            }
        }

        return count;
    }

    /// <summary>Non-solid tiles in <paramref name="area" />: cave the lab's pocket will open onto.</summary>
    private static int CountOpen(Rectangle area)
    {
        int count = 0;

        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
            {
                if (!WorldGen.SolidTile(x, y))
                    count++;
            }
        }

        return count;
    }

    private static Rectangle Inflate(Rectangle area, int by)
    {
        area.Inflate(by, by);
        return area;
    }

    /// <summary>The ring just outside <paramref name="area" />, whose tiles now border the lab's pocket.</summary>
    private static void FrameRing(Rectangle area)
    {
        WorldGen.RangeFrame(area.Left - 2, area.Top - 2, area.Left - 1, area.Bottom + 1);
        WorldGen.RangeFrame(area.Right, area.Top - 2, area.Right + 1, area.Bottom + 1);
        WorldGen.RangeFrame(area.Left, area.Top - 2, area.Right - 1, area.Top - 1);
        WorldGen.RangeFrame(area.Left, area.Bottom, area.Right - 1, area.Bottom + 1);
    }
}
