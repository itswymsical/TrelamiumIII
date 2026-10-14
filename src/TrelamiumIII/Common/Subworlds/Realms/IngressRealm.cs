using ReLogic.Graphics;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Caves;
using TrelamiumIII.Common.Generation.Fortress;
using TrelamiumIII.Common.Generation.Trees;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Dryad;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Content.Ingress.Transition;
using TrelamiumIII.Content.Seals;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.FbmScreen;
using TrelamiumIII.Core.Tiles.Seals;
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
    private const int UnderworldClearance = 20;

    private Layout layout;
    private CaveNetwork? caves;
    private Rectangle treeArea;

    /// <summary> Floor tile of the Dryad's rift home, set by <see cref="IngressSpawn"/> and placed in <see cref="OnLoad"/>. </summary>
    private Point? returnRift;

    /// <summary> The Ligneous Fortress of the last generation; null before one and in multiplayer clients. </summary>
    internal FortressLayout? Fortress { get; private set; }

    public override int Width => 4200;
    public override string Name => "Ingress";
    public override int Height => 1480;

    /// <summary>
    /// Lowest row generated content may reach. Below <see cref="Main.UnderworldLayer"/> vanilla applies hell light, hell
    /// spawns and underworld music, so the bottom of the realm is left as solid filler.
    /// </summary>
    private static int ContentBottom => Main.UnderworldLayer - UnderworldClearance;

    /// <summary> SubworldLibrary rebuilds this list on every access, so the passes share state through the realm. </summary>
    public override List<GenPass> Tasks =>
    [
        new IngressFloor(this),
        new IngressSlate(this),
        new IngressGrass(this),
        new IngressCaves(this),
        new SmoothPass("Smoothing Tiles", 100f),
        new IngressCanopyTree(this),
        new IngressCaveFeatures(this),
        new IngressFortress(this),
        new IngressCavePools(this),
        new IngressVegetation(),
        new IngressFlora(),
        new IngressSpawn(this)
    ];

    public override bool ShouldSave => true;
    public override bool NormalUpdates => true;
    public override bool NoPlayerSaving => false;

    public override int LoadingMusic => Assets.Music.IlluminantInkiness.Slot;
    public override DynamicSpriteFont TipFont => base.TipFont;
    public override Color LoadingAccent => new(140, 220, 110);
    public static int GroundY => (int)(Main.maxTilesY * 0.52f);

    internal override FbmScreenStyle? ScreenStyle => IngressScreen.Style;

    internal override void PopulateScreen(FbmScreenRenderer screen) => IngressScreen.Populate(screen);

    internal override bool StartEntryCutscene()
    {
        CutsceneSystem.PlayCutscene<IngressEntryCutscene>();

        return true;
    }

    public override void Update()
    {
        ManageGameConditions();
    }

    private void ManageGameConditions()
    {
       Main.slimeRain = false;

       Main.townNPCCanSpawn[default] = false;
    }

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;

    /// <summary>
    /// After <see cref="ModSystem.OnWorldLoad"/>, which clears protected areas; a realm loaded from file keeps its saved ones.
    /// Also stands the way home by the spawn when this load generated the realm; a realm loaded from file has it saved.
    /// </summary>
    public override void OnLoad()
    {
        ArriveAtAltar = false;

        // Multiplayer clients run this too, with nothing generated: they get the rift from the server.
        if (Main.netMode != NetmodeID.MultiplayerClient && returnRift is { } rift)
            IngressRift.PlaceReturn(rift);

        returnRift = null;

        if (Fortress is { } fortress)
        {
            foreach (Rectangle area in fortress.ProtectedAreas)
                SealSystem.Seal(area, ModContent.GetInstance<RealmSeal>());
        }
    }

    /// <summary>
    /// Set before entering through the scar from Reverie: the next generation spawns the player beside the altar rather
    /// than on the surface. Singleplayer only; a sub-server generates in another process.
    /// </summary>
    public static bool ArriveAtAltar { get; set; }

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
        private const int FortressShiftMax = 60;

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

            realm.Fortress = PlanFortress(realm.layout);

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

        /// <summary> On <see cref="ContentBottom"/> under the canopy tree, shifted part of the way toward the spawn side. </summary>
        private static FortressLayout PlanFortress(Layout layout)
        {
            int centerX = layout.SiteX + layout.SpawnSide * WorldGen.genRand.Next(FortressShiftMax + 1);
            var origin = new Point(centerX - FortressLayout.TotalWidth / 2, ContentBottom - FortressLayout.TotalHeight);

            return FortressLayout.Create(origin, new UnifiedRandom(WorldGen.genRand.Next()));
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

    /// <summary>
    /// Slate through the peat: veins below the surface layer, and a few outcrops just under the ground that break the surface
    /// away from the canopy tree's site. Runs before grass and caves, so caves cut through the slate and exposed slate stays bare.
    /// </summary>
    private class IngressSlate(IngressRealm realm) : GenPass("[T3] Ingress Slate", 40f)
    {
        /// <summary> Veins per tile of the area below the surface layer. </summary>
        private const double VeinDensity = 0.0007;
        private const int VeinStrengthMin = 6;
        private const int VeinStrengthMax = 16;
        private const int VeinStepsMin = 10;
        private const int VeinStepsMax = 40;

        private const int OutcropSpacingMin = 60;
        private const int OutcropSpacingMax = 160;
        private const int OutcropDepthMax = 3;
        private const int OutcropStrengthMin = 5;
        private const int OutcropStrengthMax = 10;
        private const int OutcropStepsMin = 6;
        private const int OutcropStepsMax = 20;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Layering slate";

            ushort slate = (ushort)ModContent.TileType<SlateTile>();
            int top = (int)Main.worldSurface;
            int bottom = Main.maxTilesY - WorldEdge;
            int veins = (int)(Main.maxTilesX * (double)(bottom - top) * VeinDensity);

            for (int i = 0; i < veins; i++)
            {
                if (i % 64 == 0)
                    progress.Set(i / (double)veins * 0.9);

                WorldGen.TileRunner(WorldGen.genRand.Next(WorldEdge, Main.maxTilesX - WorldEdge), WorldGen.genRand.Next(top, bottom),
                    WorldGen.genRand.Next(VeinStrengthMin, VeinStrengthMax + 1), WorldGen.genRand.Next(VeinStepsMin, VeinStepsMax + 1), slate);
            }

            PlaceOutcrops(slate);
        }

        private void PlaceOutcrops(ushort slate)
        {
            int siteClearance = SiteFlatRadius + SiteBlendWidth;

            for (int x = WorldGen.genRand.Next(OutcropSpacingMin, OutcropSpacingMax + 1); x < Main.maxTilesX - WorldEdge; x += WorldGen.genRand.Next(OutcropSpacingMin, OutcropSpacingMax + 1))
            {
                if (Math.Abs(x - realm.layout.SiteX) <= siteClearance)
                    continue;

                int y = SurfaceRow(x) + WorldGen.genRand.Next(OutcropDepthMax + 1);
                WorldGen.TileRunner(x, y, WorldGen.genRand.Next(OutcropStrengthMin, OutcropStrengthMax + 1), WorldGen.genRand.Next(OutcropStepsMin, OutcropStepsMax + 1), slate);
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

        private const int FortressClearance = 20;
        private const float GateOpeningRadius = 6f;
        private const float GateOpeningWarp = 2f;

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
            var area = new Rectangle(NodeMargin, top, Main.maxTilesX - NodeMargin * 2, ContentBottom - top);
            var network = new CaveNetwork(area, random) { Spacing = NodeSpacing };

            ChamberNode hub = CreateHub(layout.ShaftEnd);
            Rectangle trunkColumn = TrunkColumn(layout, hub);
            network.Nodes.Add(hub);
            network.Reserved.Add(trunkColumn);

            if (realm.Fortress is { } fortress)
            {
                Rectangle fortressArea = fortress.Bounds;
                fortressArea.Inflate(FortressClearance, FortressClearance);
                network.Reserved.Add(fortressArea);
            }

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

            if (realm.Fortress is { } gated)
                ConnectFortress(carver, network, gated);

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

        /// <summary>
        /// A tunnel from the node above the fortress nearest its gate, ending in a blob that opens onto the roof; the
        /// tunnel alone stops a few tiles short of its target.
        /// </summary>
        private static void ConnectFortress(CaveCarver carver, CaveNetwork network, FortressLayout fortress)
        {
            Vector2 gate = fortress.GateApproach.ToVector2();
            CaveNode? nearest = null;

            foreach (CaveNode node in network.Nodes)
            {
                if (node.Bounds.Bottom < fortress.Bounds.Top && (nearest is null || Vector2.DistanceSquared(node.Exit, gate) < Vector2.DistanceSquared(nearest.Exit, gate)))
                    nearest = node;
            }

            if (nearest is null)
                return;

            carver.CarveTunnel(nearest.Exit, gate);
            carver.CarveBlob(gate, GateOpeningRadius, GateOpeningWarp);
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
                int y = random.Next(top, ContentBottom - PocketRadiusMax - (int)PocketWarp);

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

    /// <summary> After smoothing so the brickwork stays square, and before the pools and flora so they see its gate. </summary>
    private class IngressFortress(IngressRealm realm) : GenPass("[T3] Ligneous Fortress", 60f)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Raising the Ligneous Fortress";

            if (realm.Fortress is { } fortress)
                FortressBuilder.Build(fortress, WorldGen.genRand);
        }
    }

    /// <summary>
    /// Water pools in cave dips below the surface layer. Generation settles no liquid, so each pool is placed full and
    /// enclosed (<see cref="LiquidBasin"/>). Runs after smoothing so slopes are final, and before the cave flora so pool beds
    /// grow grass for the water plants to anchor on. The fortress stays dry: its roots and peat form basins in mid-air.
    /// </summary>
    private class IngressCavePools(IngressRealm realm) : GenPass("[T3] Ingress Cave Pools", 40f)
    {
        private const int ColumnStep = 4;
        private const int BoundsMargin = 30;
        private const float PoolChance = 0.5f;
        private const int DepthMin = 3;
        private const int DepthMax = 8;
        private const int SurfaceWidthMin = 8;
        private const int WidthMax = 50;
        private const int TilesMax = 1000;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Filling cave pools";

            int top = (int)Main.worldSurface;
            var bounds = new Rectangle(BoundsMargin, top, Main.maxTilesX - BoundsMargin * 2, Main.maxTilesY - BoundsMargin - top);
            var rolledBasins = new HashSet<Point>();
            var region = new List<Point>();
            Rectangle fortress = realm.Fortress?.Bounds ?? Rectangle.Empty;

            for (int x = bounds.Left; x < bounds.Right; x += ColumnStep)
            {
                progress.Set((x - bounds.Left) / (double)bounds.Width);

                for (int y = bounds.Top; y < bounds.Bottom - 1; y++)
                {
                    if (!Main.tile[x, y].HasTile && Main.tile[x, y + 1].HasTile && !fortress.Contains(x, y))
                        TryFillBasin(new Point(x, y), bounds, rolledBasins, region);
                }
            }
        }

        /// <summary>
        /// Floors that drain into the same basin share its lowest, leftmost tile, so each basin is rolled once. A kept basin
        /// fills to a random depth, falling back to shallower levels while the water would drain away or the pool doesn't fit.
        /// </summary>
        private static void TryFillBasin(Point seed, Rectangle bounds, HashSet<Point> rolledBasins, List<Point> region)
        {
            if (!LiquidBasin.TryFindRegion(seed, seed.Y, bounds, TilesMax, region))
                return;

            Point bottom = Lowest(region);

            if (!rolledBasins.Add(bottom) || WorldGen.genRand.NextFloat() >= PoolChance)
                return;

            for (int depth = WorldGen.genRand.Next(DepthMin, DepthMax + 1); depth >= DepthMin; depth--)
            {
                int surfaceY = bottom.Y - depth + 1;

                if (LiquidBasin.TryFindRegion(bottom, surfaceY, bounds, TilesMax, region) && FitsWidth(region, surfaceY))
                {
                    LiquidBasin.Fill(region, LiquidID.Water);
                    return;
                }
            }
        }

        private static Point Lowest(List<Point> region)
        {
            Point lowest = region[0];

            foreach (Point point in region)
            {
                if (point.Y > lowest.Y || (point.Y == lowest.Y && point.X < lowest.X))
                    lowest = point;
            }

            return lowest;
        }

        /// <summary> At most <see cref="WidthMax"/> tiles across anywhere, so tunnels don't flood, and <see cref="SurfaceWidthMin"/> or more on the surface. </summary>
        private static bool FitsWidth(List<Point> region, int surfaceY)
        {
            int left = int.MaxValue;
            int right = int.MinValue;
            int surface = 0;

            foreach (Point point in region)
            {
                left = Math.Min(left, point.X);
                right = Math.Max(right, point.X);

                if (point.Y == surfaceY)
                    surface++;
            }

            return right - left + 1 <= WidthMax && surface >= SurfaceWidthMin;
        }
    }

    /// <summary>
    /// Below the surface layer: Ingress grass on exposed peat, Tanglewood trees, Ousia vines, and cattails and lily pads in
    /// pools. Foliage goes on flat grass both there and on the surface. Runs after the pools and the surface trees so pool
    /// beds, slopes and trunks are final; cave trees go before vines and foliage so they get clear floor.
    /// </summary>
    private class IngressFlora() : GenPass("[T3] Ingress Flora", 140f)
    {
        private const float TreeChance = 0.15f;
        private const float TreeDensityFrequency = 0.01f;
        private const float MediumTreeChance = 0.4f;
        private const int TreeCanopyClearance = 2;
        private const int TreeSpacing = 4;

        private const float VineChance = 0.45f;
        private const float VineDensityFrequency = 0.025f;
        private const float VineLengthExponent = 1.5f;
        private const int VineLengthMin = 3;
        private const int VineLengthMax = 16;

        private const float CaveFoliageChance = 0.3f;
        private const float SurfaceFoliageChance = 0.35f;
        private const float FoliageDensityFrequency = 0.04f;

        /// <summary> Frames 0-16 of the vanilla jungle plant sheet <see cref="RootFoliageTile"/> borrows. </summary>
        private const int RootFoliageStyles = 17;

        private const float WaterPlantChance = 0.2f;
        private const int NearbyWaterPlantsMax = 3;
        private const int LilyPadSpread = 5;
        private const int LilyPadDepthMin = 3;
        private const int LilyPadDepthMax = 12;
        private const int CattailSpread = 7;
        private const int CattailDepthMin = 2;
        private const int CattailDepthMax = 7;
        private const int CattailGrowthMax = 13;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            ushort peat = (ushort)ModContent.TileType<PeatTile>();
            ushort grass = (ushort)ModContent.TileType<Content.Ingress.IngressGrass>();
            int top = (int)Main.worldSurface;
            int bottom = Main.maxTilesY - WorldEdge;

            progress.Message = "Growing cave grass";
            SpreadGrass(peat, grass, top, bottom);
            progress.Set(0.3);

            progress.Message = "Growing cave trees";
            GrowTrees(grass, top, bottom);
            progress.Set(0.45);

            progress.Message = "Hanging Ousia vines";
            HangVines(grass, top, bottom);
            progress.Set(0.65);

            progress.Message = "Placing cave foliage";
            PlaceFoliage(grass, top, bottom, CaveFoliageChance);
            progress.Set(0.8);

            progress.Message = "Placing surface foliage";
            PlaceFoliage(grass, WorldEdge, top, SurfaceFoliageChance);
            progress.Set(0.9);

            progress.Message = "Growing water plants";
            PlaceWaterPlants(grass, top, bottom);
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

        /// <summary> Density noise gathers the trees into groves. Growth rejects wet, crowded and low-ceilinged spots, and the scan skips past each tree. </summary>
        private static void GrowTrees(ushort grass, int top, int bottom)
        {
            FastNoiseLite density = CreateNoise(TreeDensityFrequency);

            for (int y = top + 1; y < bottom; y++)
            {
                for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
                {
                    if (!IsOpenFloor(x, y, grass))
                        continue;

                    float weight = (density.GetNoise(x, y) + 1f) * 0.5f;

                    if (WorldGen.genRand.NextFloat() >= TreeChance * weight * weight)
                        continue;

                    bool grown = WorldGen.genRand.NextFloat() < MediumTreeChance
                        ? Core.Tiles.CustomTree.TryGrow<Content.Ingress.Trees.MediumTanglewoodTree>(x, y - 1, TreeCanopyClearance)
                        : Core.Tiles.CustomTree.TryGrow<Content.Ingress.Trees.SmallTanglewoodTree>(x, y - 1, TreeCanopyClearance);

                    if (grown)
                        x += TreeSpacing;
                }
            }
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

                        if (strand.HasTile || strand.LiquidAmount > 0)
                            break;

                        strand.HasTile = true;
                        strand.TileType = vine;
                    }
                }
            }
        }

        /// <summary> Row by row from <paramref name="top"/> up to <paramref name="bottom"/>. A plant needs its whole footprint on open floor, and the scan then skips its spacing so wide sprites don't overlap. </summary>
        private static void PlaceFoliage(ushort grass, int top, int bottom, float chance)
        {
            FoliageKind[] kinds =
            [
                new(ModContent.TileType<RootFoliageTile>(), Styles: RootFoliageStyles, Width: 1, Height: 1, OriginX: 0, Spacing: 1, Weight: 5f),
                new(ModContent.TileType<RootFernTile>(), Styles: 1, Width: 1, Height: 1, OriginX: 0, Spacing: 2, Weight: 3f),
                new(ModContent.TileType<RootRainplantTile>(), Styles: 1, Width: 1, Height: 1, OriginX: 0, Spacing: 2, Weight: 2f),
                new(ModContent.TileType<RootMushroomTile>(), Styles: RootMushroomTile.Styles, Width: 1, Height: 1, OriginX: 0, Spacing: 1, Weight: 2f),
                new(ModContent.TileType<RootRockTile>(), Styles: 1, Width: RootRockTile.Width, Height: RootRockTile.Height, OriginX: 1, Spacing: RootRockTile.Width, Weight: 0.6f)
            ];

            float totalWeight = 0f;
            foreach (FoliageKind kind in kinds)
                totalWeight += kind.Weight;

            FastNoiseLite density = CreateNoise(FoliageDensityFrequency);

            for (int y = top; y < bottom; y++)
            {
                for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
                {
                    if (!IsOpenFloor(x, y, grass))
                        continue;

                    float weight = (density.GetNoise(x, y) + 1f) * 0.5f;

                    if (WorldGen.genRand.NextFloat() >= chance * weight)
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

        /// <summary>
        /// Cattails and lily pads on pool surfaces over Ingress grass, following vanilla's "Water Plants" pass. Placed directly
        /// because <see cref="WorldGen.PlaceCatTail"/> and <see cref="WorldGen.PlaceLilyPad"/> reject modded anchors and walls;
        /// the plants survive framing through <see cref="Systems.WaterPlantAnchorSystem"/>.
        /// </summary>
        private static void PlaceWaterPlants(ushort grass, int top, int bottom)
        {
            for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
            {
                for (int y = top + 1; y < bottom; y++)
                {
                    Tile tile = Main.tile[x, y];
                    Tile above = Main.tile[x, y - 1];

                    if (tile.HasTile || tile.LiquidAmount == 0 || tile.LiquidType != LiquidID.Water || above.HasTile || above.LiquidAmount > 0)
                        continue;

                    if (WorldGen.genRand.NextFloat() >= WaterPlantChance)
                        continue;

                    if (WorldGen.genRand.NextBool(2))
                        TryPlaceLilyPad(x, y, grass);
                    else
                        TryPlaceCattail(x, y, grass);
                }
            }
        }

        /// <summary> Vanilla's limits: 3-12 tiles of water and at most three pads nearby. Frames 0-2 are bare pads and 3-17 are flowers, three per colour, with the colour picked by region as vanilla does. </summary>
        private static void TryPlaceLilyPad(int x, int surfaceY, ushort grass)
        {
            if (CountColumnsWith(x, surfaceY, LilyPadSpread, TileID.LilyPad) > NearbyWaterPlantsMax || !TryFindPoolBed(x, surfaceY, grass, out int bedY))
                return;

            int depth = bedY - surfaceY;

            if (depth < LilyPadDepthMin || depth > LilyPadDepthMax)
                return;

            int frame = WorldGen.genRand.NextBool(2) ? WorldGen.genRand.Next(3) : 3 + x * 5 / Main.maxTilesX * 3 + WorldGen.genRand.Next(3);

            Tile tile = Main.tile[x, surfaceY];
            tile.HasTile = true;
            tile.TileType = TileID.LilyPad;
            tile.TileFrameX = (short)(frame * 18);
            tile.TileFrameY = 0;
            WorldGen.SquareTileFrame(x, surfaceY);
        }

        /// <summary>
        /// Vanilla's limits: 2-7 tiles of water and at most three cattails nearby. The base goes on the bed, whose top slope is
        /// cleared as vanilla does during generation, and <see cref="WorldGen.GrowCatTail"/> grows it up through the surface.
        /// </summary>
        private static void TryPlaceCattail(int x, int surfaceY, ushort grass)
        {
            if (CountColumnsWith(x, surfaceY, CattailSpread, TileID.Cattail) > NearbyWaterPlantsMax || !TryFindPoolBed(x, surfaceY, grass, out int bedY))
                return;

            int depth = bedY - surfaceY;
            Tile bed = Main.tile[x, bedY];

            if (depth < CattailDepthMin || depth > CattailDepthMax || bed.IsHalfBlock)
                return;

            if (bed.TopSlope)
                bed.Slope = SlopeType.Solid;

            Tile tile = Main.tile[x, bedY - 1];
            tile.HasTile = true;
            tile.TileType = TileID.Cattail;
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;

            for (int i = WorldGen.genRand.Next(CattailGrowthMax + 1); i > 0; i--)
                WorldGen.GrowCatTail(x, bedY - 1);

            WorldGen.SquareTileFrame(x, bedY - 1);
        }

        /// <summary> Row of the first tile under the water column at <paramref name="x"/>; false if anything but water comes before it or it isn't Ingress grass. </summary>
        private static bool TryFindPoolBed(int x, int surfaceY, ushort grass, out int bedY)
        {
            for (bedY = surfaceY; bedY < Main.maxTilesY - WorldEdge; bedY++)
            {
                Tile tile = Main.tile[x, bedY];

                if (tile.HasTile)
                    return tile.TileType == grass;

                if (tile.LiquidAmount == 0)
                    return false;
            }

            return false;
        }

        /// <summary> Columns within <paramref name="radius"/> of <paramref name="x"/> holding a <paramref name="type"/> tile within <paramref name="radius"/> rows of <paramref name="y"/>. </summary>
        private static int CountColumnsWith(int x, int y, int radius, ushort type)
        {
            int count = 0;

            for (int column = x - radius; column <= x + radius; column++)
            {
                for (int row = y - radius; row <= y + radius; row++)
                {
                    Tile tile = Main.tile[column, row];

                    if (tile.HasTile && tile.TileType == type)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
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

    /// <summary>
    /// Beside the canopy tree, on whichever side the layout picked, on the first solid tile of that column. The Dryad's
    /// rift home stands a few columns further out, so a player arriving through it steps out between it and the tree.
    /// </summary>
    private class IngressSpawn(IngressRealm realm) : GenPass("[T3] Ingress Spawn", 1f)
    {
        private const int SpawnDistance = 160;
        private const int TreeClearance = 12;

        /// <summary> Columns from the surface spawn to the rift home, away from the tree. </summary>
        private const int RiftGap = 5;

        /// <summary> Columns left of the altar, clear of its dais, where a player arriving through the scar stands. </summary>
        private const int AltarSpawnGap = 9;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Choosing the spawn point";

            Layout layout = realm.layout;
            Rectangle tree = realm.treeArea;
            int x = layout.SiteX + layout.SpawnSide * SpawnDistance;

            if (!tree.IsEmpty)
                x = layout.SpawnSide > 0 ? Math.Max(x, tree.Right + TreeClearance) : Math.Min(x, tree.Left - TreeClearance);

            int spawnX = Math.Clamp(x, WorldEdge, Main.maxTilesX - WorldEdge - 1);
            int riftX = Math.Clamp(spawnX + layout.SpawnSide * RiftGap, WorldEdge, Main.maxTilesX - WorldEdge - 1);

            realm.returnRift = new Point(riftX, SurfaceRow(riftX));

            if (ArriveAtAltar && realm.Fortress is { } fortress)
            {
                Point altar = FortressBuilder.AltarOrigin(fortress);
                Main.spawnTileX = altar.X - AltarSpawnGap;
                Main.spawnTileY = fortress.Arena.Bottom;
                return;
            }

            Main.spawnTileX = spawnX;
            Main.spawnTileY = SurfaceRow(spawnX);
        }
    }
}
