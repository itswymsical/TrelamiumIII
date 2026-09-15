using ReLogic.Graphics;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Caves;
using TrelamiumIII.Common.Generation.Trees;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Core.Text;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
/// Rolling dirt over Brambleroot with the canopy tree at the centre. The tree's shaft ends in a hub cave, and branches
/// of caverns and grottos grow out of the hub.
/// </summary>
public class IngressRealm : SubworldRealm
{
    /// <summary> Tiles from the canopy tree's ground down to the end of its shaft. </summary>
    public const int ShaftDepth = 250;

    private const int WorldEdge = 10;
    private const int HeightAmplitude = 30;
    private const int DirtThickness = 120;
    private const int SiteFlatRadius = 110;
    private const int SiteBlendWidth = 70;

    private const float HubRadiusX = 70f;
    private const float HubRadiusY = 36f;
    private const float HubFloorOffset = 22f;
    private const float ShaftEndBelowHubCenter = 4f;

    private Layout layout;
    private CaveNetwork? caves;
    private Rectangle treeArea;

    public override int Width => 4200;
    public override string Name => "Ingress";
    public override int Height => 1280;

    /// <summary> SubworldLibrary rebuilds this list on every access, so the passes share state through the realm. </summary>
    public override List<GenPass> Tasks =>
    [
        new IngressFloor(this),
        new IngressGrass(this),
        new IngressCaves(this),
        new SmoothPass("Smoothing Tiles", 100f),
        new IngressCanopyTree(this),
        new IngressCaveFeatures(this),
        new IngressCaveFlora(),
        new IngressVegetation(),
        new IngressSpawn(this)
    ];

    public override bool ShouldSave => false;
    public override bool NormalUpdates => true;
    public override bool NoPlayerSaving => false;

    public override int LoadingMusic => Assets.Music.IngressNight.Slot;
    public override DynamicSpriteFont TipFont => base.TipFont;
    public override Color LoadingAccent => new(140, 220, 110);
    public static int GroundY => (int)(Main.maxTilesY * 0.52f);

    public override void Update()
    {
        ManageGameConditions();
    }

    private void ManageGameConditions()
    {
        if (Main.slimeRain)
            Main.slimeRain = false;


        Main.townNPCCanSpawn[default] = false;
    }

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;

    /// <summary> Carves only the hub cave for <paramref name="shaftEnd"/>, for previews outside the realm; returns the cleared area. </summary>
    public static Rectangle CarveHub(Point shaftEnd, int seed)
    {
        var carver = new CaveCarver(seed);
        CreateHub(shaftEnd).Carve(carver);
        return carver.CarvedArea;
    }

    /// <summary> Centred just above the shaft end, so the lower trunk hangs through the ceiling and stops above the floor. </summary>
    private static ChamberNode CreateHub(Point shaftEnd) =>
        new(new Vector2(shaftEnd.X, shaftEnd.Y - ShaftEndBelowHubCenter), new Vector2(HubRadiusX, HubRadiusY), HubFloorOffset);

    /// <summary> First row from the top of column <paramref name="x"/> with a solid, non-platform tile; the bottom edge if there is none. </summary>
    private static int SurfaceRow(int x)
    {
        for (int y = WorldEdge; y < Main.maxTilesY - WorldEdge; y++)
        {
            Tile tile = Main.tile[x, y];

            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                return y;
        }

        return Main.maxTilesY - WorldEdge;
    }

    /// <summary>
    /// Rolled once by <see cref="IngressFloor"/>. SubworldLibrary reseeds genRand before every pass, so later passes
    /// read these instead of rolling them again.
    /// </summary>
    private readonly record struct Layout(int SurfaceY, int SiteX, int SiteY, int HubOffset, int TreeSeed, int SpawnSide)
    {
        public int RockLayerY => SurfaceY + DirtThickness;

        public Point ShaftEnd => new(SiteX + HubOffset, SiteY + ShaftDepth);
    }

    private class IngressFloor(IngressRealm realm) : GenPass("[T3] Ingress Floor", 150f)
    {
        /// <summary>
        /// Ground depth as a fraction of the world height. The canopy tree's crown reaches ~300 tiles above its ground and
        /// has to stay below the sky zone (worldSurface * 0.35) and space gravity (70 + worldSurface / 6 at this width).
        /// </summary>
        private const float SurfaceDepthRatio = 0.4f;

        private const float SurfaceFrequency = 0.0041f;
        private const int WorldSurfaceMargin = 25;
        private const int DirtBottomJitter = 6;
        private const int WallDepthMin = 3;
        private const int WallDepthMax = 6;
        private const int MoundClearance = 60;
        private const int HubOffsetMax = 30;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Generating Ingress Floor";

            var surfaceNoise = new FastNoiseLite(WorldGen.genRand.Next());
            surfaceNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            surfaceNoise.SetFrequency(SurfaceFrequency);
            surfaceNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            surfaceNoise.SetFractalOctaves(8);
            surfaceNoise.SetFractalGain(0.5f);

            int surfaceY = (int)(Main.maxTilesY * SurfaceDepthRatio);
            int siteX = Main.maxTilesX / 2;
            int siteY = surfaceY;

            realm.layout = new Layout(surfaceY, siteX, siteY, WorldGen.genRand.Next(-HubOffsetMax, HubOffsetMax + 1),
                WorldGen.genRand.Next(), WorldGen.genRand.NextBool(2) ? 1 : -1);

            Main.worldSurface = surfaceY + HeightAmplitude + WorldSurfaceMargin;
            Main.rockLayer = realm.layout.RockLayerY;

            ushort peat = (ushort)ModContent.TileType<PeatTile>();
            int[] dirtBottom = new int[Main.maxTilesX];

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                progress.Set(x / (double)Main.maxTilesX * 0.7);

                int height = TerrainHeight(x);
                dirtBottom[x] = height + DirtThickness + WorldGen.genRand.Next(-DirtBottomJitter, DirtBottomJitter + 1);

                for (int y = height; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = y < dirtBottom[x] ? peat : peat;
                }
            }

            PlaceMounds(surfaceY, siteX);
            progress.Set(0.8);
            PlaceDirtWalls(dirtBottom);

            int NaturalHeight(int x) => surfaceY + (int)(surfaceNoise.GetNoise(x, 0f) * HeightAmplitude);

            // Level at the tree site, easing back to the natural height across the blend width.
            int TerrainHeight(int x)
            {
                float distance = Math.Abs(x - siteX) - SiteFlatRadius;

                if (distance <= 0f)
                    return siteY;

                float blend = MathF.Min(distance / SiteBlendWidth, 1f);
                return (int)MathF.Round(MathHelper.Lerp(siteY, NaturalHeight(x), blend * blend * (3f - 2f * blend)));
            }
        }

        private static void PlaceMounds(int surfaceY, int siteX)
        {
            int x = WorldGen.genRand.Next(100, 200);

            while (x < Main.maxTilesX - 100)
            {
                if (Math.Abs(x - siteX) > SiteFlatRadius + SiteBlendWidth + MoundClearance)
                    WorldGen.Mountinater(x, surfaceY + 32);

                x += WorldGen.genRand.Next(200, 400);
            }
        }

        /// <summary> Flower walls down to the Brambleroot, starting a few tiles under each column's top tile so none show above ground. </summary>
        private static void PlaceDirtWalls(int[] dirtBottom)
        {
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                int top = SurfaceRow(x) + WorldGen.genRand.Next(WallDepthMin, WallDepthMax + 1);

                for (int y = top; y < dirtBottom[x]; y++)
                    Main.tile[x, y].WallType = WallID.FlowerUnsafe;
            }
        }
    }

    private class IngressGrass(IngressRealm realm) : GrassSpreadPass("[T3] Ingress Grass", 100f)
    {
        private const int LivingTreeClearance = 60;

        protected override string GetProgressMessage() => "Spreading Grass";

        protected override ushort GetSoilTileType() => (ushort)ModContent.TileType<PeatTile>();

        protected override ushort GetGrassTileType() => (ushort)ModContent.TileType<Content.Ingress.IngressGrass>();

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            base.ApplyPass(progress, configuration);
            StonePatches();
            LivingTrees();
        }

        /// <summary> After grass spreads, because <see cref="WorldGen.StonePatch"/> needs grass on top and no wall behind it. </summary>
        private void StonePatches()
        {
            int x = WorldGen.genRand.Next(100, 200);

            while (x < Main.maxTilesX - 100)
            {
                if (!NearSite(x, 0) && WorldGen.StonePatch(x, SurfaceRow(x)))
                {
                    x += WorldGen.genRand.Next(100, 200);
                    continue;
                }

                x += WorldGen.genRand.Next(20, 40);
            }
        }

        private void LivingTrees()
        {
            int x = WorldGen.genRand.Next(150, 300);

            while (x < Main.maxTilesX - 150)
            {
                if (!NearSite(x, LivingTreeClearance) && WorldGen.GrowLivingTree(x, SurfaceRow(x) - 1, patch: true))
                {
                    x += WorldGen.genRand.Next(250, 450);
                    continue;
                }

                x += WorldGen.genRand.Next(15, 35);
            }
        }

        private bool NearSite(int x, int margin) => Math.Abs(x - realm.layout.SiteX) <= SiteFlatRadius + SiteBlendWidth + margin;
    }

    private class IngressCaves(IngressRealm realm) : GenPass("[T3] Ingress Caves", 250f)
    {
        private const int NodeMargin = 80;
        private const int NodeTopBelowRockLayer = 20;
        private const int NodeSpacing = 10;
        private const int TrunkClearance = 40;
        private const int DownwardBranchesMin = 2;
        private const int DownwardBranchesMax = 3;
        private const int HorizontalBranchLengthMin = 12;
        private const int HorizontalBranchLengthMax = 16;
        private const int DownwardBranchLengthMin = 5;
        private const int DownwardBranchLengthMax = 8;
        private const float BranchHeadingJitter = 0.2f;

        private const float CavernRadiusMin = 14f;
        private const float CavernRadiusMax = 18f;
        private const float GrottoChance = 0.3f;
        private const float GrottoMinHorizontal = 0.85f;
        private const float GrottoLengthMin = 150f;
        private const float GrottoLengthMax = 240f;
        private const float GrottoHeightMin = 14f;
        private const float GrottoHeightMax = 20f;

        private const int PocketCount = 35;
        private const int PocketTopBelowSurface = 60;
        private const int PocketRadiusMin = 2;
        private const int PocketRadiusMax = 5;
        private const float PocketWarp = 9f;
        private const int EdgeWallThickness = 3;

        private static readonly float[] DownwardHeadings = [MathHelper.PiOver4, MathHelper.PiOver2, MathHelper.Pi * 0.75f];

        private static readonly CaveBranchSettings BranchSettings = new(
            StepMin: 80f,
            StepMax: 125f,
            Jitter: 0.3f,
            DirectionPull: 0.25f,
            ForkChance: 0.3f,
            ForkTurnMin: 0.7f,
            ForkTurnMax: 1.2f,
            MaxForkDepth: 2);

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Carving Ingress Caves";

            Layout layout = realm.layout;
            UnifiedRandom random = WorldGen.genRand;

            int top = layout.RockLayerY + NodeTopBelowRockLayer;
            var area = new Rectangle(NodeMargin, top, Main.maxTilesX - NodeMargin * 2, Main.maxTilesY - NodeMargin - top);
            var network = new CaveNetwork(area, random) { Spacing = NodeSpacing };

            ChamberNode hub = CreateHub(layout.ShaftEnd);
            Rectangle trunkColumn = TrunkColumn(layout, hub);
            network.Nodes.Add(hub);
            network.Reserved.Add(trunkColumn);

            foreach ((float heading, int length) in Branches(random))
                network.GrowBranch(hub, heading, length, BranchSettings, (entry, nodeHeading) => CreateNode(random, entry, nodeHeading));

            var carver = new CaveCarver(random.Next());
            int total = network.Nodes.Count + network.Tunnels.Count;
            int done = 0;

            foreach (CaveNode node in network.Nodes)
            {
                node.Carve(carver);
                progress.Set(++done / (double)total * 0.8);
            }

            foreach ((CaveNode from, CaveNode to) in network.Tunnels)
            {
                carver.CarveTunnel(from.Exit, to.Entry);
                progress.Set(++done / (double)total * 0.8);
            }

            CarvePockets(carver, random, layout.SurfaceY + PocketTopBelowSurface, trunkColumn);

            progress.Message = "Tracing cave walls";
            PlaceEdgeWalls(carver.CarvedArea, progress, 0.85, 1.0);

            realm.caves = network;
        }

        /// <summary> Long branches left and right, plus shorter ones down a random pick of the diagonals and straight down. </summary>
        private static List<(float Heading, int Length)> Branches(UnifiedRandom random)
        {
            List<(float Heading, int Length)> branches =
            [
                (0f, random.Next(HorizontalBranchLengthMin, HorizontalBranchLengthMax + 1)),
                (MathHelper.Pi, random.Next(HorizontalBranchLengthMin, HorizontalBranchLengthMax + 1))
            ];

            List<float> downward = [.. DownwardHeadings];
            int count = random.Next(DownwardBranchesMin, DownwardBranchesMax + 1);

            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(downward.Count);
                branches.Add((downward[pick], random.Next(DownwardBranchLengthMin, DownwardBranchLengthMax + 1)));
                downward.RemoveAt(pick);
            }

            for (int i = 0; i < branches.Count; i++)
                branches[i] = (branches[i].Heading + random.NextFloat(-BranchHeadingJitter, BranchHeadingJitter), branches[i].Length);

            return branches;
        }

        /// <summary> Grottos only on near-horizontal headings, running the way the branch is going. </summary>
        private static CaveNode CreateNode(UnifiedRandom random, Vector2 entry, float heading)
        {
            float horizontal = MathF.Cos(heading);

            if (MathF.Abs(horizontal) >= GrottoMinHorizontal && random.NextFloat() < GrottoChance)
            {
                return new GrottoNode(entry, MathF.Sign(horizontal), random.NextFloat(GrottoLengthMin, GrottoLengthMax),
                    random.NextFloat(GrottoHeightMin, GrottoHeightMax), random.Next());
            }

            return new CavernNode(entry, random.NextFloat(CavernRadiusMin, CavernRadiusMax));
        }

        /// <summary> The lower trunk's column from the top of the world to the hub, widened so no node cuts into it. </summary>
        private static Rectangle TrunkColumn(Layout layout, CaveNode hub)
        {
            int left = Math.Min(layout.SiteX, layout.ShaftEnd.X) - TrunkClearance;
            int right = Math.Max(layout.SiteX, layout.ShaftEnd.X) + TrunkClearance;

            return new Rectangle(left, 0, right - left, hub.Bounds.Top);
        }

        private static void CarvePockets(CaveCarver carver, UnifiedRandom random, int top, Rectangle excluded)
        {
            for (int i = 0; i < PocketCount; i++)
            {
                int x = random.Next(NodeMargin, Main.maxTilesX - NodeMargin);
                int y = random.Next(top, Main.maxTilesY - NodeMargin);

                if (!excluded.Contains(x, y))
                    carver.CarveBlob(new Vector2(x, y), random.Next(PocketRadiusMin, PocketRadiusMax + 1), PocketWarp);
            }
        }

        /// <summary> Mud walls on empty, wall-less tiles within <see cref="EdgeWallThickness"/> steps of a tile, below the surface layer. </summary>
        private static void PlaceEdgeWalls(Rectangle carved, GenerationProgress progress, double progressStart, double progressEnd)
        {
            int left = Math.Max(carved.Left - EdgeWallThickness, WorldEdge);
            int right = Math.Min(carved.Right + EdgeWallThickness, Main.maxTilesX - WorldEdge);
            int top = Math.Max(carved.Top - EdgeWallThickness, (int)Main.worldSurface);
            int bottom = Math.Min(carved.Bottom + EdgeWallThickness, Main.maxTilesY - WorldEdge);

            for (int x = left; x < right; x++)
            {
                progress.Set(progressStart + (x - left) / (double)(right - left) * (progressEnd - progressStart));

                for (int y = top; y < bottom; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile && tile.WallType == WallID.None && HasTileNearby(x, y))
                        tile.WallType = (ushort)ModContent.WallType<PeatWall>();
                }
            }
        }

        private static bool HasTileNearby(int x, int y)
        {
            for (int dx = -EdgeWallThickness; dx <= EdgeWallThickness; dx++)
            {
                int reach = EdgeWallThickness - Math.Abs(dx);

                for (int dy = -reach; dy <= reach; dy++)
                {
                    if (Main.tile[x + dx, y + dy].HasTile)
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary> After smoothing, so the living wood keeps the generator's shape. </summary>
    private class IngressCanopyTree(IngressRealm realm) : GenPass("[T3] Ingress Canopy Tree", 60f)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Growing the canopy tree";

            Layout layout = realm.layout;
            var ground = new Point(layout.SiteX, SurfaceRow(layout.SiteX));

            realm.treeArea = CanopyTreeGenerator.Grow(ground, layout.ShaftEnd, layout.TreeSeed);
        }
    }

    private class IngressCaveFeatures(IngressRealm realm) : GenPass("[T3] Ingress Cave Features", 1f)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Decorating caves";

            if (realm.caves is null)
                return;

            foreach (CaveNode node in realm.caves.Nodes)
                node.Decorate();

            realm.caves = null;
        }
    }

    /// <summary>
    /// Below the surface layer: Ingress grass on peat that touches air, Ousia vines under ceiling grass and foliage on
    /// floor grass. Runs after smoothing and the tree so slopes and exposed tiles are final.
    /// </summary>
    private class IngressCaveFlora() : GenPass("[T3] Ingress Cave Flora", 120f)
    {
        private const float VineChance = 0.45f;
        private const float VineDensityFrequency = 0.025f;
        private const float VineLengthExponent = 1.5f;
        private const int VineLengthMin = 3;
        private const int VineLengthMax = 16;

        private const float FoliageChance = 0.3f;
        private const float FoliageDensityFrequency = 0.04f;

        /// <summary> Frames 0-16 of the vanilla jungle plant sheet <see cref="RootFoliageTile"/> borrows. </summary>
        private const int RootFoliageStyles = 17;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            ushort peat = (ushort)ModContent.TileType<PeatTile>();
            ushort grass = (ushort)ModContent.TileType<Content.Ingress.IngressGrass>();
            int top = (int)Main.worldSurface;
            int bottom = Main.maxTilesY - WorldEdge;

            progress.Message = "Growing cave grass";
            SpreadGrass(peat, grass, top, bottom);
            progress.Set(0.4);

            progress.Message = "Hanging Ousia vines";
            HangVines(grass, top, bottom);
            progress.Set(0.7);

            progress.Message = "Placing cave foliage";
            PlaceFoliage(grass, top, bottom);
        }

        private static void SpreadGrass(ushort peat, ushort grass, int top, int bottom)
        {
            for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
            {
                for (int y = top; y < bottom; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && tile.TileType == peat && TouchesAir(x, y))
                        tile.TileType = grass;
                }
            }
        }

        private static bool TouchesAir(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!Main.tile[x + dx, y + dy].HasTile)
                        return true;
                }
            }

            return false;
        }

        /// <summary> Density noise clusters the vines and lengths skew short. <see cref="OusiaVineTile"/> breaks under bottom-sloped grass, so those are skipped. </summary>
        private static void HangVines(ushort grass, int top, int bottom)
        {
            ushort vine = (ushort)ModContent.TileType<OusiaVineTile>();
            FastNoiseLite density = CreateNoise(VineDensityFrequency);

            for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
            {
                for (int y = top; y < bottom - 1; y++)
                {
                    Tile tile = Main.tile[x, y];
                    Tile below = Main.tile[x, y + 1];

                    if (!tile.HasTile || tile.TileType != grass || tile.BottomSlope || below.HasTile || below.LiquidAmount > 0)
                        continue;

                    float weight = (density.GetNoise(x, y) + 1f) * 0.5f;

                    if (WorldGen.genRand.NextFloat() >= VineChance * weight * weight)
                        continue;

                    int length = VineLengthMin + (int)(MathF.Pow(WorldGen.genRand.NextFloat(), VineLengthExponent) * (VineLengthMax - VineLengthMin + 1));

                    for (int strandY = y + 1; strandY <= y + length && strandY < bottom; strandY++)
                    {
                        Tile strand = Main.tile[x, strandY];

                        if (strand.HasTile)
                            break;

                        strand.HasTile = true;
                        strand.TileType = vine;
                    }
                }
            }
        }

        /// <summary> Row by row. A plant needs its whole footprint on open floor, and the scan then skips its spacing so wide sprites don't overlap. </summary>
        private static void PlaceFoliage(ushort grass, int top, int bottom)
        {
            FoliageKind[] kinds =
            [
                new(ModContent.TileType<RootFoliageTile>(), Styles: RootFoliageStyles, Width: 1, Height: 1, OriginX: 0, Spacing: 1, Weight: 5f),
                new(ModContent.TileType<RootFernTile>(), Styles: 1, Width: 1, Height: 1, OriginX: 0, Spacing: 2, Weight: 3f),
                new(ModContent.TileType<RootRainplantTile>(), Styles: 1, Width: 1, Height: 1, OriginX: 0, Spacing: 2, Weight: 2f),
                new(ModContent.TileType<RootRockTile>(), Styles: 1, Width: RootRockTile.Width, Height: RootRockTile.Height, OriginX: 1, Spacing: RootRockTile.Width, Weight: 0.6f)
            ];

            float totalWeight = 0f;
            foreach (FoliageKind kind in kinds)
                totalWeight += kind.Weight;

            FastNoiseLite density = CreateNoise(FoliageDensityFrequency);

            for (int y = top + 1; y < bottom; y++)
            {
                for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
                {
                    if (!IsOpenFloor(x, y, grass))
                        continue;

                    float weight = (density.GetNoise(x, y) + 1f) * 0.5f;

                    if (WorldGen.genRand.NextFloat() >= FoliageChance * weight)
                        continue;

                    FoliageKind picked = Pick(kinds, totalWeight);

                    if (FootprintFits(x, y, picked, grass) && WorldGen.PlaceTile(x + picked.OriginX, y - 1, picked.Type, mute: true, style: WorldGen.genRand.Next(picked.Styles)))
                        x += picked.Spacing;
                }
            }
        }

        /// <summary> Flat Ingress grass at (<paramref name="x"/>, <paramref name="y"/>) with an empty, dry tile above it. </summary>
        private static bool IsOpenFloor(int x, int y, ushort grass)
        {
            Tile tile = Main.tile[x, y];
            Tile above = Main.tile[x, y - 1];

            return tile.HasTile && tile.TileType == grass && !tile.IsHalfBlock && tile.Slope == SlopeType.Solid && !above.HasTile && above.LiquidAmount == 0;
        }

        /// <summary> Every footprint column from <paramref name="x"/> is open floor with <see cref="FoliageKind.Height"/> empty, dry rows above. </summary>
        private static bool FootprintFits(int x, int y, FoliageKind kind, ushort grass)
        {
            if (x + kind.Width > Main.maxTilesX - WorldEdge)
                return false;

            for (int column = x; column < x + kind.Width; column++)
            {
                if (!IsOpenFloor(column, y, grass))
                    return false;

                for (int row = y - kind.Height; row < y - 1; row++)
                {
                    Tile space = Main.tile[column, row];

                    if (space.HasTile || space.LiquidAmount > 0)
                        return false;
                }
            }

            return true;
        }

        private static FoliageKind Pick(FoliageKind[] kinds, float totalWeight)
        {
            float roll = WorldGen.genRand.NextFloat(totalWeight);

            foreach (FoliageKind kind in kinds)
            {
                roll -= kind.Weight;

                if (roll < 0f)
                    return kind;
            }

            return kinds[^1];
        }

        private static FastNoiseLite CreateNoise(float frequency)
        {
            var noise = new FastNoiseLite(WorldGen.genRand.Next());
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFrequency(frequency);
            return noise;
        }

        /// <param name="Width"> Footprint in tiles from its leftmost column; <paramref name="OriginX"/> is the TileObjectData origin column within it. </param>
        /// <param name="Spacing"> Tiles the scan skips after a placement, so wide sprites don't overlap. </param>
        private readonly record struct FoliageKind(int Type, int Styles, int Width, int Height, int OriginX, int Spacing, float Weight);
    }

    private class IngressVegetation() : GenPass("[T3] Ingress Vegetation", 80f)
    {
        private const int TreeStepMin = 9;
        private const int TreeStepMax = 18;
        private const float MediumTreeChance = 0.35f;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Growing Flora and Trees";

            WorldGen.AddTrees();
            WorldGen.GrowGlowTulips();
            GrowTanglewoodTrees();
        }

        /// <summary> Tries a tree on surface Ingress grass every few columns; growth itself rejects crowded or covered spots. </summary>
        private static void GrowTanglewoodTrees()
        {
            ushort grass = (ushort)ModContent.TileType<Content.Ingress.IngressGrass>();

            for (int x = WorldEdge + 20; x < Main.maxTilesX - WorldEdge - 20; x += WorldGen.genRand.Next(TreeStepMin, TreeStepMax + 1))
            {
                int ground = SurfaceRow(x);

                if (Main.tile[x, ground].TileType != grass)
                    continue;

                if (WorldGen.genRand.NextFloat() < MediumTreeChance)
                    Core.Tiles.CustomTree.TryGrow<Content.Ingress.Trees.MediumTanglewoodTree>(x, ground - 1);
                else
                    Core.Tiles.CustomTree.TryGrow<Content.Ingress.Trees.SmallTanglewoodTree>(x, ground - 1);
            }
        }
    }

    /// <summary> Beside the canopy tree, on whichever side the layout picked, on the first solid tile of that column. </summary>
    private class IngressSpawn(IngressRealm realm) : GenPass("[T3] Ingress Spawn", 1f)
    {
        private const int SpawnDistance = 160;
        private const int TreeClearance = 12;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Choosing the spawn point";

            Layout layout = realm.layout;
            Rectangle tree = realm.treeArea;
            int x = layout.SiteX + layout.SpawnSide * SpawnDistance;

            if (!tree.IsEmpty)
                x = layout.SpawnSide > 0 ? Math.Max(x, tree.Right + TreeClearance) : Math.Min(x, tree.Left - TreeClearance);

            Main.spawnTileX = Math.Clamp(x, WorldEdge, Main.maxTilesX - WorldEdge - 1);
            Main.spawnTileY = SurfaceRow(Main.spawnTileX);
        }
    }
}
