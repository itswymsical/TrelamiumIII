using ReLogic.Graphics;
using SubworldLibrary;
using System.Collections.Generic;
using System.Reflection;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Caves;
using TrelamiumIII.Common.Generation.Fortress;
using TrelamiumIII.Common.Generation.SurfaceShaping;
using TrelamiumIII.Common.Generation.Trees;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Dryad;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Content.Ingress.HighlandsBiome;
using TrelamiumIII.Content.Ingress.Transition;
using TrelamiumIII.Content.Seals;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.FbmScreen;
using TrelamiumIII.Core.Tiles.Seals;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
/// Temperate highlands over Brambleroot, with the canopy tree in a grove of Ingress grass at the centre. Out from the grove
/// the surface runs through rolling hills, snow-capped basalt mountains, cliff-walled plateaus and river valleys, cut by up
/// to two canyons that drop into the caves. The tree's shaft ends in a hub cave, and branches of caverns and grottos grow
/// out of the hub.
/// </summary>
public class IngressRealm : SubworldRealm
{
    /// <summary> Tiles from the canopy tree's ground down to the end of its shaft. </summary>
    public const int ShaftDepth = 250;

    /// <summary>
    /// Ground depth as a fraction of the world height. The canopy tree's crown reaches ~300 tiles above its ground and
    /// has to stay below the sky zone (worldSurface * 0.35) and space gravity (70 + worldSurface / 6 at this width).
    /// The mountains' peaks stop well short of the crown's height.
    /// </summary>
    private const float SurfaceDepthRatio = 0.4f;

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

    // Region names of the landscape's segments.
    private const string HillsRegion = "Hills";
    private const string MountainRegion = "Mountain";
    private const string PlateauRegion = "Plateau";
    private const string ValleyRegion = "Valley";

    /// <summary> Rows above the base line past which trees stop growing. </summary>
    private const int TreelineRows = 95;

    /// <summary> Rows above the base line where the snow caps begin, before the noise moves the line. </summary>
    private const int SnowlineRows = 120;

    /// <summary> Columns either side of the canopy tree that keep Ingress grass, before the noise moves the grove's edge. </summary>
    private const int SiteGroveRadius = SiteFlatRadius;

    private Layout layout;
    private SurfaceTerrain? terrain;
    private CaveNetwork? caves;
    private Rectangle treeArea;

    /// <summary> Columns the canyons cut through at their rims, which the surface dressing leaves alone. </summary>
    private readonly List<(int Left, int Right)> canyons = [];

    /// <summary> Columns of the evil patch, once <see cref="IngressBlight"/> has grown it; null before or if it found no room. </summary>
    private (int Left, int Right)? blight;

    /// <summary> Floor tile of the Dryad's rift home, set by <see cref="IngressSpawn"/> and placed in <see cref="OnLoad"/>. </summary>
    private Point? returnRift;

    /// <summary> The Ligneous Fortress of the last generation; null before one and in multiplayer clients. </summary>
    internal FortressLayout? Fortress { get; private set; }

    public override int Width => 4200;
    public override string Name => "Ingress";
    public override int Height => 1480;

    /// <summary> Row the surface swings around before regions lift and sink it; known on clients too, for the sky's horizon. </summary>
    internal static int BaseSurfaceY => (int)(Main.maxTilesY * SurfaceDepthRatio);

    /// <summary> Row of the treeline: above it no trees grow, and the alpine weather of <see cref="Content.Ingress.HighlandsBiome.HighlandsWeather"/> begins. </summary>
    internal static int AlpineRow => BaseSurfaceY - TreelineRows;

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
        new IngressCanyons(this),
        new IngressBlight(this),
        new SmoothPass("Smoothing Tiles", 100f),
        new IngressHighlands(this),
        new IngressCanopyTree(this),
        new IngressCaveFeatures(this),
        new IngressFortress(this),
        new IngressCavePools(this),
        new IngressVegetation(),
        new IngressFlora(this),
        new IngressCleanup(),
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

    /// <summary> Rows above the base line the ground at row <paramref name="y"/> stands. </summary>
    private int Elevation(int y) => layout.SurfaceY - y;

    /// <summary> Whether column <paramref name="x"/> is within <paramref name="margin"/> of the canopy tree's flat ground and its blend. </summary>
    private bool NearSite(int x, int margin) => Math.Abs(x - layout.SiteX) <= SiteFlatRadius + SiteBlendWidth + margin;

    /// <summary> Whether column <paramref name="x"/> is within <paramref name="margin"/> of a canyon's rim. </summary>
    private bool InCanyon(int x, int margin) => canyons.Exists(canyon => x + margin >= canyon.Left && x - margin <= canyon.Right);

    /// <summary> Whether no landscape segment reaches column <paramref name="x"/>, leaving the base rolling hills. </summary>
    private static bool InBaseTerrain(SurfaceTerrain terrain, int x)
    {
        foreach (SurfaceRegion region in terrain.Regions)
        {
            if (region.Weight(x) > 0f)
                return false;
        }

        return true;
    }

    private static FastNoiseLite CreateNoise(float frequency)
    {
        var noise = new FastNoiseLite(WorldGen.genRand.Next());
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        return noise;
    }

    /// <summary>
    /// Lays out the landscape and fills the ground. Each side of the site is walked outward in segments, hills first and
    /// then mountains, plateaus, valleys and more hills in no repeating order, each a <see cref="SurfaceRegion"/> on the
    /// shared terrain. Mountains are basalt under a thinning skin of peat with snow over the snowline; any step tall enough
    /// to read as a cliff is bare rock; river channels are lined with clay and lakes with banded strata.
    /// </summary>
    private class IngressFloor(IngressRealm realm) : GenPass("[T3] Ingress Floor", 150f)
    {
        private const float SurfaceFrequency = 0.0041f;
        private const int WorldSurfaceMargin = 25;
        private const int DirtBottomJitter = 6;
        private const int WallDepthMin = 3;
        private const int WallDepthMax = 6;
        private const int MoundClearance = 60;
        private const int HubOffsetMax = 30;
        private const int FortressShiftMax = 60;

        // Rows under the surface the shore strata are looked up for; the strata themselves reach less.
        private const int StrataSearchDepth = 28;

        private const int LandscapeSiteGap = 40;
        private const int LandscapeEdgeMargin = 140;

        private const int HillsSpanMin = 260;
        private const int HillsSpanMax = 400;
        private const int HillsFalloff = 90;
        private const float HillsAmplitude = 46f;
        private const float HillsLift = 22f;
        private const float HillsWarp = 70f;
        private const float HillsWeight = 1.5f;

        private const int LakesMin = 1;
        private const int LakesMax = 2;
        private const int LakeRadiusMin = 18;
        private const int LakeRadiusMax = 30;
        private const int LakeDepthMin = 8;
        private const int LakeDepthMax = 14;
        private const int LakeSpacing = 110;

        // Peaks top out near 260 rows above the base line, still 100 rows short of the sky layer.
        private const int MountainSpanMin = 520;
        private const int MountainSpanMax = 700;
        private const int MountainCoreMin = 8;
        private const int MountainCoreMax = 20;
        private const int MountainLiftMin = 115;
        private const int MountainLiftMax = 160;
        private const int MountainRidgeMin = 45;
        private const int MountainRidgeMax = 70;
        private const float MountainAmplitude = 28f;
        private const float MountainWarp = 50f;

        /// <summary> Power of the mountains' pointed lift falloff; past 1 the slopes steepen toward the summit. </summary>
        private const float MountainPeak = 1.5f;
        private const float MountainWeight = 3f;

        private const int PlateauSpanMin = 260;
        private const int PlateauSpanMax = 420;
        private const int PlateauMargin = 20;
        private const int CliffFalloffMin = 5;
        private const int CliffFalloffMax = 10;
        private const int PlateauLiftMin = 45;
        private const int PlateauLiftMax = 75;
        private const float PlateauAmplitude = 10f;
        private const float PlateauWarp = 30f;
        private const float EscarpmentChance = 0.5f;
        private const float PlateauWeight = 2f;

        private const int ValleySpanMin = 380;
        private const int ValleySpanMax = 540;
        private const int ValleyFalloff = 80;
        private const int ValleyDepthMin = 14;
        private const int ValleyDepthMax = 22;
        private const float ValleyAmplitude = 5f;
        private const float ValleyWeight = 2.5f;

        private const int RiverBankMargin = 15;
        private const int RiverDepthMin = 5;
        private const int RiverDepthMax = 8;

        /// <summary> Mountain weight past which the ground is basalt, before the noise jitters the edge. </summary>
        private const float BasaltWeight = 0.2f;

        private const float BasaltEdgeJitter = 0.08f;

        /// <summary> Mountain weight past <see cref="BasaltWeight"/> over which the basalt core deepens to its full root. </summary>
        private const float BasaltDeepenWeight = 0.35f;
        private const float RockNoiseFrequency = 0.03f;

        /// <summary> Peat over the basalt at the mountain's foot, thinning to <see cref="SkinRowsHigh"/> from <see cref="SkinThinFrom"/> rows up over <see cref="SkinThinRange"/>. </summary>
        private const float SkinRowsLow = 12f;

        private const float SkinRowsHigh = 2f;
        private const float SkinThinFrom = 30f;
        private const float SkinThinRange = 90f;
        private const int BasaltRootRows = 10;
        private const float BasaltRootJitter = 6f;

        /// <summary> Rows a column's side may stand above its lower neighbour before it reads as a cliff face; less on a mountain, where it reads as a crag. </summary>
        private const int FaceRows = 4;

        private const int CragRows = 3;
        private const int FaceRockDepth = 3;

        private const float SnowlineJitter = 12f;
        private const int SnowRowsMin = 4;
        private const int SnowRowsMax = 14;
        private const int SnowRiseRows = 6;

        private static readonly (string Kind, double Weight)[] SegmentWeights =
        [
            (HillsRegion, HillsWeight),
            (MountainRegion, MountainWeight),
            (PlateauRegion, PlateauWeight),
            (ValleyRegion, ValleyWeight)
        ];

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Generating Ingress Floor";

            int surfaceY = BaseSurfaceY;
            int siteX = Main.maxTilesX / 2;
            int siteY = surfaceY;

            var terrain = new SurfaceTerrain(WorldGen.genRand.Next(), SurfaceFrequency, surfaceY, new SurfaceProfile(HeightAmplitude, 0f, 0f),
                siteX, SiteFlatRadius, SiteBlendWidth);

            PlanLandscape(terrain, siteX);
            realm.terrain = terrain;
            realm.canyons.Clear();
            realm.blight = null;

            realm.layout = new Layout(surfaceY, siteX, siteY, WorldGen.genRand.Next(-HubOffsetMax, HubOffsetMax + 1),
                WorldGen.genRand.Next(), WorldGen.genRand.NextBool(2) ? 1 : -1);

            realm.Fortress = PlanFortress(realm.layout);

            Main.worldSurface = surfaceY + terrain.LowestOffset + WorldSurfaceMargin;
            Main.rockLayer = realm.layout.RockLayerY;

            var materials = new Materials(
                Peat: (ushort)ModContent.TileType<PeatTile>(),
                Strata:
                [
                    (ushort)ModContent.TileType<TerracottaTile>(),
                    TileID.ClayBlock,
                    (ushort)ModContent.TileType<GrayTerracottaTile>()
                ],
                Clay: TileID.ClayBlock,
                Snow: TileID.SnowBlock,
                Basalt: (ushort)ModContent.TileType<BasaltTile>(),
                Slate: (ushort)ModContent.TileType<SlateTile>());

            int[] heights = new int[Main.maxTilesX];
            int[] dirtBottom = new int[Main.maxTilesX];

            for (int x = 0; x < Main.maxTilesX; x++)
                heights[x] = terrain.HeightAt(x);

            FastNoiseLite rockNoise = CreateNoise(RockNoiseFrequency);

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                progress.Set(x / (double)Main.maxTilesX * 0.7);

                int height = heights[x];
                Column column = PlanColumn(terrain, heights, x, rockNoise, materials);
                dirtBottom[x] = height + DirtThickness + WorldGen.genRand.Next(-DirtBottomJitter, DirtBottomJitter + 1);

                for (int y = height; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = column.TileAt(terrain, x, y, materials);
                }
            }

            PlaceMounds(surfaceY, siteX, terrain);
            progress.Set(0.8);
            PlaceDirtWalls(dirtBottom);
        }

        /// <summary>
        /// Walks each side of the site out to the world's edge in segments. The first is always hills; after that no kind
        /// repeats, and a side without a mountain takes one once only a mountain and one more segment would still fit, so
        /// every side gets one. Lakes and rivers go in last, since their levels read the finished terrain.
        /// </summary>
        private static void PlanLandscape(SurfaceTerrain terrain, int siteX)
        {
            int siteEdge = SiteFlatRadius + SiteBlendWidth + LandscapeSiteGap;

            foreach (int side in (int[])[-1, 1])
            {
                int cursor = siteX + side * siteEdge;
                int end = side < 0 ? LandscapeEdgeMargin : Main.maxTilesX - LandscapeEdgeMargin;
                string? previous = null;
                bool hasMountain = false;

                while (PickSegment(previous, hasMountain, Math.Abs(end - cursor)) is { } kind)
                {
                    (int min, int max) = SpanRange(kind);
                    int span = Math.Min(WorldGen.genRand.Next(min, max + 1), Math.Abs(end - cursor));
                    int far = cursor + side * span;

                    terrain.AddRegion(CreateRegion(kind, Math.Min(cursor, far), Math.Max(cursor, far)));

                    cursor = far;
                    previous = kind;
                    hasMountain |= kind == MountainRegion;
                }
            }

            foreach (SurfaceRegion region in terrain.Regions)
            {
                if (region.Name == HillsRegion)
                    terrain.AddLakes(region, WorldGen.genRand.Next(LakesMin, LakesMax + 1), LakeRadiusMin, LakeRadiusMax, LakeDepthMin, LakeDepthMax, LakeSpacing, WorldGen.genRand);
                else if (region.Name == ValleyRegion)
                    terrain.AddRiver(region, RiverBankMargin, RiverDepthMin, RiverDepthMax, WorldGen.genRand);
            }
        }

        private static string? PickSegment(string? previous, bool hasMountain, int room)
        {
            if (previous is null)
                return room >= HillsSpanMin ? HillsRegion : null;

            if (!hasMountain && room >= MountainSpanMin && room < MountainSpanMax + ValleySpanMax)
                return MountainRegion;

            var picks = new WeightedRandom<string>(WorldGen.genRand);

            foreach ((string kind, double weight) in SegmentWeights)
            {
                if (kind != previous && room >= SpanRange(kind).Min)
                    picks.Add(kind, weight);
            }

            return picks.elements.Count > 0 ? picks.Get() : null;
        }

        private static (int Min, int Max) SpanRange(string kind) => kind switch
        {
            MountainRegion => (MountainSpanMin, MountainSpanMax),
            PlateauRegion => (PlateauSpanMin, PlateauSpanMax),
            ValleyRegion => (ValleySpanMin, ValleySpanMax),
            _ => (HillsSpanMin, HillsSpanMax)
        };

        /// <summary> A region filling the columns from <paramref name="left"/> to <paramref name="right"/>, falloff included. </summary>
        private static SurfaceRegion CreateRegion(string kind, int left, int right)
        {
            int span = right - left;
            int center = (left + right) / 2;

            switch (kind)
            {
                case MountainRegion:
                {
                    int core = WorldGen.genRand.Next(MountainCoreMin, MountainCoreMax + 1);
                    var profile = new SurfaceProfile(MountainAmplitude, WorldGen.genRand.Next(MountainLiftMin, MountainLiftMax + 1), MountainWarp,
                        WorldGen.genRand.Next(MountainRidgeMin, MountainRidgeMax + 1), MountainPeak);

                    return new SurfaceRegion(kind, center, core, span / 2 - core, profile);
                }
                case PlateauRegion:
                {
                    // A mesa stands up as cliffs on both sides; an escarpment climbs gently from one and drops sheer to the other.
                    int width = span - PlateauMargin * 2;
                    int cliff = WorldGen.genRand.Next(CliffFalloffMin, CliffFalloffMax + 1);
                    var profile = new SurfaceProfile(PlateauAmplitude, WorldGen.genRand.Next(PlateauLiftMin, PlateauLiftMax + 1), PlateauWarp);

                    if (WorldGen.genRand.NextFloat() >= EscarpmentChance)
                        return new SurfaceRegion(kind, center, width / 2 - cliff, cliff, profile);

                    int core = width / 4;
                    int gentle = width - core * 2 - cliff;
                    bool cliffRight = WorldGen.genRand.NextBool();
                    int fallLeft = cliffRight ? gentle : cliff;
                    int fallRight = cliffRight ? cliff : gentle;

                    return new SurfaceRegion(kind, left + PlateauMargin + fallLeft + core, core, fallLeft, profile, fallRight);
                }
                case ValleyRegion:
                {
                    var profile = new SurfaceProfile(ValleyAmplitude, -WorldGen.genRand.Next(ValleyDepthMin, ValleyDepthMax + 1), 0f);
                    return new SurfaceRegion(kind, center, span / 2 - ValleyFalloff, ValleyFalloff, profile);
                }
                default:
                    return new SurfaceRegion(kind, center, Math.Max(span / 2 - HillsFalloff, 20), HillsFalloff, new SurfaceProfile(HillsAmplitude, HillsLift, HillsWarp));
            }
        }

        /// <summary> What column <paramref name="x"/> is made of, read off the finished heights so faces and slopes are known. </summary>
        private static Column PlanColumn(SurfaceTerrain terrain, int[] heights, int x, FastNoiseLite noise, Materials materials)
        {
            int height = heights[x];
            int elevation = terrain.BaseY - height;
            int lower = Math.Max(heights[Math.Max(x - 1, 0)], heights[Math.Min(x + 1, Main.maxTilesX - 1)]);
            int face = lower - height;
            float weight = terrain.WeightOf(MountainRegion, x) + noise.GetNoise(x, 0f) * BasaltEdgeJitter;
            bool mountain = weight > BasaltWeight;

            int rockFrom = int.MaxValue;
            int rockTo = int.MinValue;

            if (mountain)
            {
                float thin = Math.Clamp((elevation - SkinThinFrom) / SkinThinRange, 0f, 1f);
                float skin = MathHelper.Lerp(SkinRowsLow, SkinRowsHigh, thin) + noise.GetNoise(x, 300f) * 2f;
                int root = terrain.BaseY + BasaltRootRows + (int)(noise.GetNoise(x, 600f) * BasaltRootJitter);

                // The core deepens inward from the edge, so it meets the peat on a slant rather than a vertical seam.
                float depth = Math.Clamp((weight - BasaltWeight) / BasaltDeepenWeight, 0f, 1f);

                rockFrom = height + Math.Max((int)skin, 0);
                rockTo = (int)MathHelper.Lerp(rockFrom, root, depth);
            }

            if (face >= (mountain ? CragRows : FaceRows))
            {
                rockFrom = height;
                rockTo = Math.Max(rockTo, lower + FaceRockDepth);
            }

            int snowline = SnowlineRows + (int)(noise.GetNoise(x, 900f) * SnowlineJitter);
            int snow = elevation > snowline ? Math.Min(SnowRowsMin + (elevation - snowline) / SnowRiseRows, SnowRowsMax) : 0;

            return new Column(height, snow, terrain.RiverbedDepth(x), rockFrom, rockTo, mountain ? materials.Basalt : materials.Slate);
        }

        /// <summary> On <see cref="ContentBottom"/> under the canopy tree, shifted part of the way toward the spawn side. </summary>
        private static FortressLayout PlanFortress(Layout layout)
        {
            int centerX = layout.SiteX + layout.SpawnSide * WorldGen.genRand.Next(FortressShiftMax + 1);
            var origin = new Point(centerX - FortressLayout.TotalWidth / 2, ContentBottom - FortressLayout.TotalHeight);

            return FortressLayout.Create(origin, new UnifiedRandom(WorldGen.genRand.Next()));
        }

        /// <summary> Small vanilla mounds on the stretches of base terrain between the segments. </summary>
        private static void PlaceMounds(int surfaceY, int siteX, SurfaceTerrain terrain)
        {
            int x = WorldGen.genRand.Next(100, 200);

            while (x < Main.maxTilesX - 100)
            {
                if (Math.Abs(x - siteX) > SiteFlatRadius + SiteBlendWidth + MoundClearance && InBaseTerrain(terrain, x))
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

        private readonly record struct Materials(ushort Peat, ushort[] Strata, ushort Clay, ushort Snow, ushort Basalt, ushort Slate);

        /// <summary>
        /// One column of ground from its surface at <paramref name="Height"/> down: <paramref name="Snow"/> rows of snow,
        /// then <paramref name="Clay"/> rows of riverbed, then <paramref name="Rock"/> between <paramref name="RockFrom"/>
        /// and <paramref name="RockTo"/>, then shore strata or peat.
        /// </summary>
        private readonly record struct Column(int Height, int Snow, int Clay, int RockFrom, int RockTo, ushort Rock)
        {
            public ushort TileAt(SurfaceTerrain terrain, int x, int y, Materials materials)
            {
                int depth = y - Height;

                if (depth < Snow)
                    return materials.Snow;

                if (depth < Clay)
                    return materials.Clay;

                if (y >= RockFrom && y < RockTo)
                    return Rock;

                int band = depth < StrataSearchDepth ? terrain.ShoreBand(x, y, Height) : -1;
                return band >= 0 ? materials.Strata[band] : materials.Peat;
            }
        }
    }

    /// <summary>
    /// Slate through the peat: veins below the surface layer, and a few outcrops just under the ground that break the surface
    /// away from the canopy tree's site. The mountains get basalt instead: boulders half-sunk into their slopes and seams just
    /// under them. Runs before grass and caves, so caves cut through the rock, exposed rock stays bare, and smoothing rounds
    /// the boulders off.
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

        private const float BasaltWeight = 0.3f;
        private const int BasaltSpacingMin = 18;
        private const int BasaltSpacingMax = 45;
        private const float BoulderChance = 0.55f;
        private const int BoulderStrengthMin = 5;
        private const int BoulderStrengthMax = 8;
        private const int BoulderStepsMin = 2;
        private const int BoulderStepsMax = 4;
        private const int SeamStrengthMin = 6;
        private const int SeamStrengthMax = 12;
        private const int SeamStepsMin = 6;
        private const int SeamStepsMax = 16;

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
            PlaceBasalt();
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

        private void PlaceBasalt()
        {
            if (realm.terrain is not { } terrain)
                return;

            ushort basalt = (ushort)ModContent.TileType<BasaltTile>();

            for (int x = WorldEdge + BasaltSpacingMax; x < Main.maxTilesX - WorldEdge - BasaltSpacingMax; x += WorldGen.genRand.Next(BasaltSpacingMin, BasaltSpacingMax + 1))
            {
                if (terrain.WeightOf(MountainRegion, x) < BasaltWeight)
                    continue;

                int y = SurfaceRow(x);

                if (WorldGen.genRand.NextFloat() < BoulderChance)
                {
                    WorldGen.TileRunner(x, y - 1, WorldGen.genRand.Next(BoulderStrengthMin, BoulderStrengthMax + 1), WorldGen.genRand.Next(BoulderStepsMin, BoulderStepsMax + 1),
                        basalt, addTile: true);
                }
                else
                {
                    WorldGen.TileRunner(x, y + WorldGen.genRand.Next(OutcropDepthMax + 1), WorldGen.genRand.Next(SeamStrengthMin, SeamStrengthMax + 1),
                        WorldGen.genRand.Next(SeamStepsMin, SeamStepsMax + 1), basalt);
                }
            }
        }
    }

    /// <summary>
    /// Highlands grass over the whole surface, then the canopy tree's grove turned back to Ingress grass, its edge wandering
    /// with the noise.
    /// </summary>
    private class IngressGrass(IngressRealm realm) : GrassSpreadPass("[T3] Ingress Grass", 100f)
    {
        private const float GroveEdgeFrequency = 0.06f;
        private const float GroveEdgeJitter = 14f;
        private const int GroveRowsAbove = 3;
        private const int GroveRowsBelow = 14;

        protected override string GetProgressMessage() => "Spreading Grass";

        protected override ushort GetSoilTileType() => (ushort)ModContent.TileType<PeatTile>();

        protected override ushort GetGrassTileType() => (ushort)ModContent.TileType<HighlandsGrass>();

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            base.ApplyPass(progress, configuration);
            PlantGrove();
            StonePatches();
        }

        private void PlantGrove()
        {
            ushort highlands = (ushort)ModContent.TileType<HighlandsGrass>();
            ushort ingress = (ushort)ModContent.TileType<Content.Ingress.IngressGrass>();
            FastNoiseLite edge = CreateNoise(GroveEdgeFrequency);
            int siteX = realm.layout.SiteX;
            int reach = SiteGroveRadius + (int)GroveEdgeJitter;

            for (int x = siteX - reach; x <= siteX + reach; x++)
            {
                if (Math.Abs(x - siteX) > SiteGroveRadius + edge.GetNoise(x, 0f) * GroveEdgeJitter)
                    continue;

                int surface = SurfaceRow(x);

                for (int y = surface - GroveRowsAbove; y < surface + GroveRowsBelow; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && tile.TileType == highlands)
                        tile.TileType = ingress;
                }
            }
        }

        /// <summary> After grass spreads, because <see cref="WorldGen.StonePatch"/> needs grass on top and no wall behind it. </summary>
        private void StonePatches()
        {
            int x = WorldGen.genRand.Next(100, 200);

            while (x < Main.maxTilesX - 100)
            {
                if (!realm.NearSite(x, 0) && WorldGen.StonePatch(x, SurfaceRow(x)))
                {
                    x += WorldGen.genRand.Next(100, 200);
                    continue;
                }

                x += WorldGen.genRand.Next(20, 40);
            }
        }
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

    /// <summary>
    /// One or two canyons cut from the surface down into the caves: wide at the rim, narrowing and wandering as they drop,
    /// with rock shelves jutting from alternate walls. The upper part stays open to the sky; below it the canyon keeps a back
    /// wall. After the caves so each canyon's foot can tunnel to the nearest cavern, and before smoothing so the walls
    /// weather with the rest of the terrain.
    /// </summary>
    private class IngressCanyons(IngressRealm realm) : GenPass("[T3] Ingress Canyons", 60f)
    {
        private const int CountMin = 1;
        private const int CountMax = 2;
        private const int Attempts = 60;
        private const int EdgeClearance = 300;
        private const int SiteClearance = 220;
        private const int Spacing = 700;
        private const int LakeClearance = 40;

        /// <summary> Mountain weight anywhere across the cut past which a spot is rejected, so canyons stay off the slopes. </summary>
        private const float MountainLimit = 0.15f;

        private const int RimHalfWidthMin = 26;
        private const int RimHalfWidthMax = 38;
        private const int FootHalfWidthMin = 9;
        private const int FootHalfWidthMax = 14;
        private const int RimFlareRows = 22;
        private const float RimFlare = 14f;

        private const float MeanderMin = 10f;
        private const float MeanderMax = 22f;
        private const float MeanderFrequency = 0.012f;
        private const float WallJitter = 4f;
        private const float WallFrequency = 0.08f;

        private const float LedgeFrequency = 0.07f;
        private const float LedgeThreshold = 0.3f;
        private const float LedgeColumns = 11f;

        /// <summary> Columns each side of the centre line a shelf always leaves open, so a ledge never closes the canyon. </summary>
        private const float MinCutHalfWidth = 3f;

        private const int FootBelowRockLayer = 40;
        private const float FootRadius = 10f;
        private const float FootWarp = 3f;

        /// <summary> Fraction of the depth from the rim that is left without a back wall, so the sky shows down into it. </summary>
        private const float OpenSkyFraction = 0.35f;

        /// <summary> Columns either side of a canyon's centre line its cut can reach. </summary>
        private const int Reach = RimHalfWidthMax + (int)RimFlare + (int)WallJitter + (int)MeanderMax;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Cutting canyons";

            if (realm.terrain is not { } terrain)
                return;

            int count = WorldGen.genRand.Next(CountMin, CountMax + 1);
            var picked = new List<int>();

            for (int attempt = 0; attempt < Attempts && picked.Count < count; attempt++)
            {
                int x = WorldGen.genRand.Next(EdgeClearance, Main.maxTilesX - EdgeClearance);

                if (!Suitable(terrain, x, picked))
                    continue;

                picked.Add(x);
                Carve(x);
                progress.Set(picked.Count / (double)count);
            }
        }

        private bool Suitable(SurfaceTerrain terrain, int x, List<int> picked)
        {
            if (realm.NearSite(x, SiteClearance) || picked.Exists(other => Math.Abs(other - x) < Spacing))
                return false;

            foreach (SurfaceLake lake in terrain.Lakes)
            {
                if (Math.Abs(x - lake.Center) < lake.Radius + Reach + LakeClearance)
                    return false;
            }

            for (int column = x - Reach; column <= x + Reach; column++)
            {
                if (terrain.WeightOf(MountainRegion, column) > MountainLimit)
                    return false;
            }

            return true;
        }

        private void Carve(int x)
        {
            var carver = new CaveCarver(WorldGen.genRand.Next());
            FastNoiseLite meander = CreateNoise(MeanderFrequency);
            FastNoiseLite walls = CreateNoise(WallFrequency);
            FastNoiseLite ledges = CreateNoise(LedgeFrequency);

            // From just above the highest ground across the cut, so the rim opens cleanly on both banks.
            int rim = int.MaxValue;

            for (int column = x - Reach; column <= x + Reach; column++)
                rim = Math.Min(rim, SurfaceRow(column));

            rim -= 2;

            int foot = realm.layout.RockLayerY + FootBelowRockLayer;
            int openUntil = rim + (int)((foot - rim) * OpenSkyFraction);
            float meanderColumns = WorldGen.genRand.NextFloat(MeanderMin, MeanderMax);
            int rimHalfWidth = WorldGen.genRand.Next(RimHalfWidthMin, RimHalfWidthMax + 1);
            int footHalfWidth = WorldGen.genRand.Next(FootHalfWidthMin, FootHalfWidthMax + 1);
            ushort peatWall = (ushort)ModContent.WallType<PeatWall>();
            float center = x;

            for (int y = rim; y <= foot; y++)
            {
                float t = (y - rim) / (float)(foot - rim);
                float half = MathHelper.Lerp(rimHalfWidth, footHalfWidth, MathF.Pow(t, 0.7f));
                float flare = 1f - (y - rim) / (float)RimFlareRows;

                if (flare > 0f)
                    half += flare * flare * RimFlare;

                // The rim stays put and the cut wanders more the deeper it goes.
                center = x + meander.GetNoise(0f, y) * meanderColumns * MathF.Min(t * 4f, 1f);

                float leftHalf = half + walls.GetNoise(-500f, y) * WallJitter;
                float rightHalf = half + walls.GetNoise(500f, y) * WallJitter;
                float ledge = ledges.GetNoise(x, y);
                float jut = (MathF.Abs(ledge) - LedgeThreshold) / (1f - LedgeThreshold) * LedgeColumns;

                if (jut > 0f)
                {
                    if (ledge > 0f)
                        leftHalf -= jut;
                    else
                        rightHalf -= jut;
                }

                int from = (int)MathF.Round(center - MathF.Max(leftHalf, MinCutHalfWidth));
                int to = (int)MathF.Round(center + MathF.Max(rightHalf, MinCutHalfWidth));

                for (int column = from; column <= to; column++)
                {
                    carver.Clear(column, y);

                    Tile tile = Main.tile[column, y];

                    if (y < openUntil)
                        tile.WallType = WallID.None;
                    else if (tile.WallType == WallID.None)
                        tile.WallType = peatWall;
                }
            }

            var bottom = new Vector2(center, foot);
            carver.CarveBlob(bottom, FootRadius, FootWarp);
            ConnectToCaves(carver, bottom);

            Rectangle carved = carver.CarvedArea;
            realm.canyons.Add((carved.Left, carved.Right));
            GrassRim(carved);
        }

        private void ConnectToCaves(CaveCarver carver, Vector2 bottom)
        {
            if (realm.caves is null)
                return;

            CaveNode? nearest = null;

            foreach (CaveNode node in realm.caves.Nodes)
            {
                if (nearest is null || Vector2.DistanceSquared(node.Center, bottom) < Vector2.DistanceSquared(nearest.Center, bottom))
                    nearest = node;
            }

            if (nearest is not null)
                carver.CarveTunnel(bottom, nearest.Center);
        }

        /// <summary> Highlands grass on the peat the cut exposed above the surface layer; <see cref="IngressFlora"/> grows Ingress grass below it. </summary>
        private static void GrassRim(Rectangle carved)
        {
            ushort peat = (ushort)ModContent.TileType<PeatTile>();
            ushort grass = (ushort)ModContent.TileType<HighlandsGrass>();
            int bottom = Math.Min(carved.Bottom, (int)Main.worldSurface);

            for (int x = carved.Left - 1; x <= carved.Right + 1; x++)
            {
                for (int y = carved.Top; y < bottom; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && tile.TileType == peat && ExposedToAir(x, y))
                        tile.TileType = grass;
                }
            }
        }

        private static bool ExposedToAir(int x, int y)
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
    }

    /// <summary>
    /// One patch of the main world's evil out in the highlands, grown the way vanilla grows its own: corruption sinks chasms
    /// with shadow orbs at their feet, crimson digs a heart cavern with hearts in its pockets. Inside the patch the rock turns
    /// to ebonstone or crimstone, the grass to the evil highland grass and the walls to the evil's, with veins of the stone
    /// through the peat and altars standing on it. After the caves and canyons so they don't cut through it, and before
    /// smoothing, as in vanilla.
    /// </summary>
    private class IngressBlight(IngressRealm realm) : GenPass("[T3] Ingress Blight", 60f)
    {
        private const int HalfWidthMin = 110;
        private const int HalfWidthMax = 160;
        private const int SiteClearance = 320;
        private const int CanyonClearance = 40;
        private const float MountainLimit = 0.1f;
        private const int FallbackAttempts = 60;

        /// <summary> Columns at each edge over which the conversion thins out at random, as vanilla's does. </summary>
        private const int EdgeRagged = 5;

        // Vanilla converts down to between 30 and 50 rows under the surface layer, wandering a row or two a column.
        private const int DepthMin = 30;
        private const int DepthMax = 50;

        private const int MainChasmStepsMin = 150;
        private const int MainChasmStepsMax = 300;
        private const int SideChasmSpacingMin = 35;
        private const int SideChasmSpacingMax = 60;
        private const int SideChasmStepsMin = 50;
        private const int SideChasmStepsMax = 100;
        private const int SideChasmClearance = 20;

        /// <summary> Veins of evil stone per tile of the converted area. </summary>
        private const double VeinDensity = 0.0012;
        private const int VeinStrengthMin = 5;
        private const int VeinStrengthMax = 12;
        private const int VeinStepsMin = 8;
        private const int VeinStepsMax = 24;

        private const int AltarsMin = 3;
        private const int AltarsMax = 5;
        private const int AltarAttempts = 400;
        private const int AltarDepth = 90;

        private static readonly FieldInfo HeartCount = typeof(WorldGen).GetField("heartCount", BindingFlags.NonPublic | BindingFlags.Static)!;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = WorldGen.crimson ? "Growing the crimson" : "Spreading the corruption";

            if (realm.terrain is not { } terrain || !TryPlace(terrain, out int center, out int halfWidth))
                return;

            int left = center - halfWidth;
            int right = center + halfWidth;
            realm.blight = (left, right);

            if (WorldGen.crimson)
            {
                // Hearts are placed from positions CrimStart records; the main world's are still in there otherwise.
                HeartCount.SetValue(null, 0);
                WorldGen.CrimStart(center, SurfaceRow(center) - 10);
                WorldGen.CrimPlaceHearts();
            }
            else
            {
                SinkChasms(center, left, right);
            }

            progress.Set(0.5);
            Convert(left, right);
            Veins(left, right);
            progress.Set(0.8);

            // Corruption's chasms set an altar near each of their feet, as in vanilla; the crimson's are placed here.
            if (WorldGen.crimson)
                PlaceAltars(left, right);
        }

        /// <summary> A hills segment far from the canopy tree, the canyons and the mountains; failing that, any such stretch of ground. </summary>
        private bool TryPlace(SurfaceTerrain terrain, out int center, out int halfWidth)
        {
            halfWidth = WorldGen.genRand.Next(HalfWidthMin, HalfWidthMax + 1);
            var candidates = new List<int>();

            foreach (SurfaceRegion region in terrain.Regions)
            {
                if (region.Name == HillsRegion && Fits(terrain, region.Center, halfWidth))
                    candidates.Add(region.Center);
            }

            for (int attempt = 0; candidates.Count == 0 && attempt < FallbackAttempts; attempt++)
            {
                int x = WorldGen.genRand.Next(WorldEdge + halfWidth + 100, Main.maxTilesX - WorldEdge - halfWidth - 100);

                if (Fits(terrain, x, halfWidth))
                    candidates.Add(x);
            }

            center = candidates.Count > 0 ? candidates[WorldGen.genRand.Next(candidates.Count)] : 0;
            return candidates.Count > 0;
        }

        private bool Fits(SurfaceTerrain terrain, int center, int halfWidth)
        {
            if (center - halfWidth < WorldEdge + 50 || center + halfWidth > Main.maxTilesX - WorldEdge - 50)
                return false;

            if (Math.Abs(center - realm.layout.SiteX) < SiteFlatRadius + SiteBlendWidth + SiteClearance + halfWidth || realm.InCanyon(center, halfWidth + CanyonClearance))
                return false;

            for (int x = center - halfWidth; x <= center + halfWidth; x += 8)
            {
                if (terrain.WeightOf(MountainRegion, x) > MountainLimit)
                    return false;
            }

            return true;
        }

        /// <summary> Vanilla's corruption: one long chasm with orbs at the centre, and shorter ones spaced along the rest. </summary>
        private static void SinkChasms(int center, int left, int right)
        {
            WorldGen.ChasmRunner(center, SurfaceRow(center), WorldGen.genRand.Next(MainChasmStepsMin, MainChasmStepsMax + 1), makeOrb: true);

            for (int x = left + WorldGen.genRand.Next(SideChasmSpacingMin); x < right; x += WorldGen.genRand.Next(SideChasmSpacingMin, SideChasmSpacingMax + 1))
            {
                if (Math.Abs(x - center) > SideChasmClearance)
                    WorldGen.ChasmRunner(x, SurfaceRow(x), WorldGen.genRand.Next(SideChasmStepsMin, SideChasmStepsMax + 1), makeOrb: true);
            }
        }

        /// <summary>
        /// Rock to the evil's stone, the highlands' grasses to the evil highland grass and the walls to the evil's, from the
        /// surface down to a wandering depth under the surface layer. Peat is left as it is, as vanilla leaves dirt; any peat
        /// the chasms bared above the surface layer grows the evil grass.
        /// </summary>
        private static void Convert(int left, int right)
        {
            bool crimson = WorldGen.crimson;
            ushort stone = crimson ? TileID.Crimstone : TileID.Ebonstone;
            ushort grass = (ushort)(crimson ? ModContent.TileType<CrimsonHighlandsGrass>() : ModContent.TileType<CorruptHighlandsGrass>());
            ushort stoneWall = crimson ? WallID.CrimstoneUnsafe : WallID.EbonstoneUnsafe;
            ushort grassWall = crimson ? WallID.CrimsonGrassUnsafe : WallID.CorruptGrassUnsafe;

            ushort slate = (ushort)ModContent.TileType<SlateTile>();
            ushort basalt = (ushort)ModContent.TileType<BasaltTile>();
            ushort peat = (ushort)ModContent.TileType<PeatTile>();
            ushort peatWall = (ushort)ModContent.WallType<PeatWall>();
            ushort[] grasses = [(ushort)ModContent.TileType<HighlandsGrass>(), (ushort)ModContent.TileType<SnowHighlandsGrass>(), (ushort)ModContent.TileType<Content.Ingress.IngressGrass>(), TileID.Grass];

            double depth = Main.worldSurface + DepthMin;

            for (int x = left - EdgeRagged; x <= right + EdgeRagged; x++)
            {
                depth = Math.Clamp(depth + WorldGen.genRand.Next(-2, 3), Main.worldSurface + DepthMin, Main.worldSurface + DepthMax);

                // Past the edge the conversion only reaches this column some of the time.
                if (x < left + WorldGen.genRand.Next(EdgeRagged) - EdgeRagged / 2 || x > right - WorldGen.genRand.Next(EdgeRagged) + EdgeRagged / 2)
                    continue;

                for (int y = WorldEdge; y < depth; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.WallType == WallID.FlowerUnsafe)
                        tile.WallType = grassWall;
                    else if (tile.WallType == peatWall)
                        tile.WallType = stoneWall;

                    if (!tile.HasTile)
                        continue;

                    if (tile.TileType == slate || tile.TileType == basalt || tile.TileType == TileID.Stone)
                        tile.TileType = stone;
                    else if (Array.IndexOf(grasses, tile.TileType) >= 0 || (tile.TileType == peat && y < Main.worldSurface && ExposedToAir(x, y)))
                        tile.TileType = grass;
                }
            }
        }

        private static void Veins(int left, int right)
        {
            ushort stone = WorldGen.crimson ? TileID.Crimstone : TileID.Ebonstone;
            int top = (int)Main.worldSurface - DepthMax;
            int bottom = (int)Main.worldSurface + DepthMax;
            int veins = (int)((right - left) * (double)(bottom - top) * VeinDensity);

            for (int i = 0; i < veins; i++)
            {
                int x = WorldGen.genRand.Next(left, right + 1);
                int from = Math.Max(top, SurfaceRow(x) + 4);

                // A chasm column's first floor can lie below the whole band.
                if (from >= bottom)
                    continue;

                WorldGen.TileRunner(x, WorldGen.genRand.Next(from, bottom), WorldGen.genRand.Next(VeinStrengthMin, VeinStrengthMax + 1), WorldGen.genRand.Next(VeinStepsMin, VeinStepsMax + 1), stone);
            }
        }

        /// <summary> Altars on open floors of the evil stone, from the surface down into the chasms, as vanilla scatters its own. </summary>
        private static void PlaceAltars(int left, int right)
        {
            ushort stone = WorldGen.crimson ? TileID.Crimstone : TileID.Ebonstone;
            int style = WorldGen.crimson ? 1 : 0;
            int wanted = WorldGen.genRand.Next(AltarsMin, AltarsMax + 1);

            for (int attempt = 0; attempt < AltarAttempts && wanted > 0; attempt++)
            {
                int x = WorldGen.genRand.Next(left, right + 1);
                int y = WorldGen.genRand.Next(SurfaceRow(x) - 2, (int)Main.worldSurface + AltarDepth);

                // Down through open air to the first floor.
                while (y < Main.maxTilesY - WorldEdge && !Main.tile[x, y + 1].HasTile)
                    y++;

                Tile floor = Main.tile[x, y + 1];

                if (floor.TileType != stone || Main.tile[x, y].HasTile || WorldGen.IsTileNearby(x, y, TileID.DemonAltar, 30))
                    continue;

                WorldGen.Place3x2(x, y, TileID.DemonAltar, style);

                if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.DemonAltar)
                    wanted--;
            }
        }

        private static bool ExposedToAir(int x, int y)
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
    }

    /// <summary>
    /// Dresses the surface after smoothing, so slopes are final: water in the lakes and river channels, snowy grass under
    /// the snow caps, the living trees, gravel paths, spruce and birch groves, and ground plants. Before the canopy tree and
    /// vegetation passes, which see its trees as obstacles.
    /// </summary>
    private class IngressHighlands(IngressRealm realm) : GenPass("[T3] Ingress Highlands", 80f)
    {
        private const int LakeTilesMax = 20000;
        private const int LakeLevelFallbacks = 8;

        /// <summary> Rows under the snowline over which grass turns snowy, patchily at the bottom and wholly at the top. </summary>
        private const int AlpineBandRows = 35;

        private const float AlpineFrequency = 0.08f;
        private const float AlpinePatchiness = 0.35f;

        private const int LivingTreeEdge = 150;
        private const int LivingTreeSpacingMin = 380;
        private const int LivingTreeSpacingMax = 650;
        private const int LivingTreeRetryMin = 15;
        private const int LivingTreeRetryMax = 35;
        private const int LivingTreeSiteClearance = 60;
        private const int LivingTreeWaterClearance = 20;
        private const int LivingTreeTreelineMargin = 15;

        /// <summary> Rows up past which a living tree is always spruce, as on any mountain or plateau past <see cref="RuggedWeight"/>. </summary>
        private const int SpruceElevation = 45;

        private const float RuggedWeight = 0.3f;
        private const float ValleyBirchWeight = 0.5f;

        private const int TrailLengthMin = 140;
        private const int TrailLengthMax = 260;
        private const int PathGapMin = 90;
        private const int PathGapMax = 260;
        private const int PathLengthMin = 40;
        private const int PathLengthMax = 130;

        /// <summary> Rows the ground may step between neighbouring columns before a path gives up on the slope. </summary>
        private const int PathStepMax = 2;

        private const int PathMissesMax = 3;
        private const int PathFadeColumns = 6;
        private const float PathFrayDensity = 0.15f;
        private const float PathDepthChance = 0.55f;

        // Tried every few columns, since this pass alone grows the surface's forests: vanilla's AddTrees would also root in the snow caps.
        private const int TreeStepMin = 3;
        private const int TreeStepMax = 6;
        private const int TreeCanopyClearance = 2;
        private const int TreeSpacing = 4;
        private const float GroveFrequency = 0.012f;
        private const float MeadowChance = 0.08f;
        private const float GroveChance = 1f;
        private const float GroveEdgeLow = 0.35f;
        private const float GroveEdgeHigh = 0.75f;
        private const float SpruceBase = 0.42f;
        private const float SpruceBias = 0.35f;
        private const float AltitudeBias = 0.5f;
        private const float AltitudeBiasFrom = 20f;
        private const float AltitudeBiasRange = 80f;

        private const float PlantChance = 0.28f;
        private const float BushChance = 0.05f;
        private const float RockChance = 0.015f;
        private const int PlantStyles = 45;
        private const int BushStyles = 12;
        private const int RockWidth = 3;

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            if (realm.terrain is not { } terrain)
                return;

            progress.Message = "Raising the highlands";

            ushort grass = (ushort)ModContent.TileType<HighlandsGrass>();
            ushort snowGrass = (ushort)ModContent.TileType<SnowHighlandsGrass>();

            FillLakes(terrain);
            progress.Set(0.15);

            DressAlpine(grass, snowGrass);
            progress.Set(0.25);

            progress.Message = "Growing the living trees";
            GrowLivingTrees(terrain, grass, snowGrass);
            progress.Set(0.45);

            progress.Message = "Laying gravel paths";
            LayPaths((ushort)ModContent.TileType<GravelTile>());
            progress.Set(0.55);

            progress.Message = "Planting groves";
            GrowTrees(grass, snowGrass);
            progress.Set(0.85);

            PlacePlants(grass, snowGrass);
        }

        /// <summary> Fills each lake and river to its level, or to a shallower one if the basin doesn't hold that high. </summary>
        private static void FillLakes(SurfaceTerrain terrain)
        {
            var bounds = new Rectangle(WorldEdge, WorldEdge, Main.maxTilesX - WorldEdge * 2, Main.maxTilesY - WorldEdge * 2);
            var region = new List<Point>();

            foreach (SurfaceLake lake in terrain.Lakes)
            {
                for (int level = lake.Level; level <= lake.Level + LakeLevelFallbacks; level++)
                {
                    if (!LiquidBasin.TryFindRegion(new Point(lake.Center, level), level, bounds, LakeTilesMax, region))
                        continue;

                    LiquidBasin.Fill(region, LiquidID.Water);
                    break;
                }
            }
        }

        /// <summary> Grass under the snowline turns snowy in clumps, likelier the closer it stands to the snow. </summary>
        private void DressAlpine(ushort grass, ushort snowGrass)
        {
            FastNoiseLite patches = CreateNoise(AlpineFrequency);

            for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
            {
                int ground = SurfaceRow(x);
                float rise = (realm.Elevation(ground) - (SnowlineRows - AlpineBandRows)) / (float)AlpineBandRows;

                if (rise <= 0f || rise + patches.GetNoise(x, 0f) * AlpinePatchiness < 0.5f)
                    continue;

                Tile tile = Main.tile[x, ground];

                if (tile.HasTile && tile.TileType == grass)
                    tile.TileType = snowGrass;
            }
        }

        private void GrowLivingTrees(SurfaceTerrain terrain, ushort grass, ushort snowGrass)
        {
            int x = WorldEdge + LivingTreeEdge + WorldGen.genRand.Next(LivingTreeSpacingMin / 2);

            while (x < Main.maxTilesX - WorldEdge - LivingTreeEdge)
            {
                x += TryGrowLivingTree(terrain, x, grass, snowGrass)
                    ? WorldGen.genRand.Next(LivingTreeSpacingMin, LivingTreeSpacingMax + 1)
                    : WorldGen.genRand.Next(LivingTreeRetryMin, LivingTreeRetryMax + 1);
            }
        }

        /// <summary> Spruce up high and on the rugged ground, birch down in the valleys, either elsewhere. </summary>
        private bool TryGrowLivingTree(SurfaceTerrain terrain, int x, ushort grass, ushort snowGrass)
        {
            if (realm.NearSite(x, LivingTreeSiteClearance))
                return false;

            int ground = SurfaceRow(x);
            int elevation = realm.Elevation(ground);
            Tile tile = Main.tile[x, ground];

            if (elevation > TreelineRows - LivingTreeTreelineMargin || !tile.HasTile || (tile.TileType != grass && tile.TileType != snowGrass))
                return false;

            float rugged = MathF.Max(terrain.WeightOf(MountainRegion, x), terrain.WeightOf(PlateauRegion, x));
            HighlandLivingTree.Species species = rugged > RuggedWeight || elevation > SpruceElevation || tile.TileType == snowGrass ? HighlandLivingTree.Species.Spruce
                : terrain.WeightOf(ValleyRegion, x) > ValleyBirchWeight || WorldGen.genRand.NextBool() ? HighlandLivingTree.Species.Birch
                : HighlandLivingTree.Species.Spruce;

            int reach = HighlandLivingTree.Reach(species);

            if (realm.InCanyon(x, reach))
                return false;

            foreach (SurfaceLake lake in terrain.Lakes)
            {
                if (Math.Abs(x - lake.Center) <= lake.Radius + reach + LivingTreeWaterClearance)
                    return false;
            }

            return !HighlandLivingTree.Grow(new Point(x, ground), species, WorldGen.genRand).IsEmpty;
        }

        /// <summary> Trails out of the grove on both sides, then shorter paths scattered over the rest of the surface. </summary>
        private void LayPaths(ushort gravel)
        {
            int siteX = realm.layout.SiteX;

            foreach (int side in (int[])[-1, 1])
                LayPath(siteX + side * (SiteGroveRadius - PathFadeColumns), side, WorldGen.genRand.Next(TrailLengthMin, TrailLengthMax + 1), gravel);

            for (int x = WorldEdge + WorldGen.genRand.Next(PathGapMin, PathGapMax + 1); x < Main.maxTilesX - WorldEdge; x += WorldGen.genRand.Next(PathGapMin, PathGapMax + 1))
            {
                if (!realm.NearSite(x, 0) && !realm.InCanyon(x, 0))
                    x = LayPath(x, 1, WorldGen.genRand.Next(PathLengthMin, PathLengthMax + 1), gravel);
            }
        }

        /// <summary>
        /// Gravels the surface grass from <paramref name="start"/> along <paramref name="direction"/>, fraying at both ends,
        /// until the length runs out, the ground steps too steeply, or it runs into water or something other than grass.
        /// </summary>
        /// <returns> The column the path stopped at. </returns>
        private static int LayPath(int start, int direction, int length, ushort gravel)
        {
            ushort peat = (ushort)ModContent.TileType<PeatTile>();
            int previous = SurfaceRow(start);
            int misses = 0;
            int x = start;

            for (int i = 0; i < length; i++, x += direction)
            {
                if (x < WorldEdge || x >= Main.maxTilesX - WorldEdge)
                    break;

                int ground = SurfaceRow(x);

                if (Math.Abs(ground - previous) > PathStepMax)
                    break;

                previous = ground;
                Tile tile = Main.tile[x, ground];

                if (!IsSurfaceGrass(tile) || Main.tile[x, ground - 1].LiquidAmount > 0)
                {
                    if (++misses > PathMissesMax)
                        break;

                    continue;
                }

                misses = 0;

                float fray = Math.Min(Math.Min(i, length - 1 - i) / (float)PathFadeColumns, 1f);

                if (WorldGen.genRand.NextFloat() >= MathHelper.Lerp(PathFrayDensity, 1f, fray))
                    continue;

                tile.TileType = gravel;

                Tile below = Main.tile[x, ground + 1];

                if (WorldGen.genRand.NextFloat() < PathDepthChance && below.HasTile && (below.TileType == peat || IsSurfaceGrass(below)))
                    below.TileType = gravel;
            }

            return x;
        }

        private static bool IsSurfaceGrass(Tile tile) => tile.HasTile
            && (tile.TileType == ModContent.TileType<HighlandsGrass>() || tile.TileType == ModContent.TileType<SnowHighlandsGrass>() || tile.TileType == ModContent.TileType<Content.Ingress.IngressGrass>());

        /// <summary>
        /// Groves where the density noise is high and meadow between them. A second noise leans each stand to spruce or birch,
        /// and height leans it further to spruce. Snowy grass and the blight's grass grow only spruce, which birch can't root in.
        /// </summary>
        private void GrowTrees(ushort grass, ushort snowGrass)
        {
            FastNoiseLite density = CreateNoise(GroveFrequency);
            FastNoiseLite kind = CreateNoise(GroveFrequency);
            ushort corrupt = (ushort)ModContent.TileType<CorruptHighlandsGrass>();
            ushort crimson = (ushort)ModContent.TileType<CrimsonHighlandsGrass>();

            for (int x = WorldEdge + 20; x < Main.maxTilesX - WorldEdge - 20; x += WorldGen.genRand.Next(TreeStepMin, TreeStepMax + 1))
            {
                int ground = SurfaceRow(x);
                int elevation = realm.Elevation(ground);
                bool blighted = IsOpenGrass(x, ground, corrupt, crimson);

                if (elevation > TreelineRows || !(blighted || IsOpenGrass(x, ground, grass, snowGrass)))
                    continue;

                float grove = Math.Clamp(((density.GetNoise(x, 0f) + 1f) * 0.5f - GroveEdgeLow) / (GroveEdgeHigh - GroveEdgeLow), 0f, 1f);

                if (WorldGen.genRand.NextFloat() >= MeadowChance + grove * grove * (3f - 2f * grove) * GroveChance)
                    continue;

                float altitude = Math.Clamp((elevation - AltitudeBiasFrom) / AltitudeBiasRange, 0f, 1f);
                float spruceChance = SpruceBase + kind.GetNoise(x, 100f) * SpruceBias + (altitude - 0.3f) * AltitudeBias;
                bool spruce = blighted || Main.tile[x, ground].TileType == snowGrass || WorldGen.genRand.NextFloat() < spruceChance;

                // Birch grows straight off the grass, as vanilla's AddTrees grows trees: BirchTree is the ModTree highlands grass picks.
                bool grown = spruce
                    ? Core.Tiles.CustomTree.TryGrow<SpruceTree>(x, ground - 1, TreeCanopyClearance)
                    : WorldGen.GrowTree(x, ground);

                if (grown)
                    x += TreeSpacing;
            }
        }

        private static void PlacePlants(ushort grass, ushort snowGrass)
        {
            int plant = ModContent.TileType<TemperatePlants>();
            int bush = ModContent.TileType<WinterberryBushTile>();
            int rock = ModContent.TileType<TemperateRockTile>();

            for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge - RockWidth; x++)
            {
                int ground = SurfaceRow(x);

                if (!IsOpenGrass(x, ground, grass, snowGrass))
                    continue;

                float roll = WorldGen.genRand.NextFloat();

                if (roll < RockChance)
                {
                    if (RockFits(x, ground, grass) && WorldGen.PlaceTile(x + 1, ground - 1, rock, mute: true))
                        x += RockWidth;
                }
                else if (roll < RockChance + BushChance)
                {
                    WorldGen.PlaceTile(x, ground - 1, bush, mute: true, style: WorldGen.genRand.Next(BushStyles));
                }
                else if (roll < RockChance + BushChance + PlantChance)
                {
                    WorldGen.PlaceTile(x, ground - 1, plant, mute: true, style: WorldGen.genRand.Next(PlantStyles));
                }
            }
        }

        /// <summary> Flat highland or snowy grass at (<paramref name="x"/>, <paramref name="y"/>) with an empty, dry tile above it. </summary>
        private static bool IsOpenGrass(int x, int y, ushort grass, ushort snowGrass)
        {
            Tile tile = Main.tile[x, y];
            Tile above = Main.tile[x, y - 1];

            return tile.HasTile && (tile.TileType == grass || tile.TileType == snowGrass) && !tile.IsHalfBlock && tile.Slope == SlopeType.Solid
                && !above.HasTile && above.LiquidAmount == 0;
        }

        /// <summary> Three columns of flat highland grass, which the rock anchors to, with headroom over them. </summary>
        private static bool RockFits(int x, int y, ushort grass)
        {
            for (int column = x; column < x + RockWidth; column++)
            {
                if (!IsOpenGrass(column, y, grass, grass) || Main.tile[column, y - 2].HasTile)
                    return false;
            }

            return true;
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
    /// beds, slopes and trunks are final; cave trees go before vines and foliage so they get clear floor. The blight keeps its
    /// bare peat, so none of it grows there.
    /// </summary>
    private class IngressFlora(IngressRealm realm) : GenPass("[T3] Ingress Flora", 140f)
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

        private void SpreadGrass(ushort peat, ushort grass, int top, int bottom)
        {
            for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
            {
                if (realm.blight is { } blight && x >= blight.Left && x <= blight.Right)
                    continue;

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

            // No vanilla trees: on this surface they only take root in the snow caps, above the treeline.
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
    /// Last, after everything that grows or carves. Generation should leave no sapling behind, so any still standing either
    /// grows into its tree now or is cleared; saplings stand two tiles tall, and both growers find the ground from either
    /// half. Altars and orbs a later chasm or root cut into are cleared too: framing would break them as the world loads,
    /// and a broken altar smashes, and a broken orb breaks, as if the player had done it.
    /// </summary>
    private class IngressCleanup() : GenPass("[T3] Ingress Cleanup", 5f)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Tidying up";

            for (int x = WorldEdge; x < Main.maxTilesX - WorldEdge; x++)
            {
                for (int y = WorldEdge; y < Main.maxTilesY - WorldEdge; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (!tile.HasTile)
                        continue;

                    if (TileID.Sets.TreeSapling[tile.TileType])
                        ClearIfStunted(x, y, tile);
                    else if (tile.TileType == TileID.DemonAltar)
                        ClearIfBroken(x, y, tile, width: 3, height: 2);
                    else if (tile.TileType == TileID.ShadowOrbs)
                        ClearIfBroken(x, y, tile, width: 2, height: 2);
                }
            }
        }

        private static void ClearIfStunted(int x, int y, Tile tile)
        {
            bool grown = TileLoader.GetTile(tile.TileType) is Core.Tiles.SaplingTile sapling ? sapling.TryGrow(x, y) : WorldGen.GrowTree(x, y);
            Tile after = Main.tile[x, y];

            if (!grown && after.HasTile && TileID.Sets.TreeSapling[after.TileType])
                after.ClearTile();
        }

        /// <summary> Clears this tile of a <paramref name="width"/> by <paramref name="height"/> object unless every tile of the object is still there. </summary>
        private static void ClearIfBroken(int x, int y, Tile tile, int width, int height)
        {
            int left = x - tile.TileFrameX / 18 % width;
            int top = y - tile.TileFrameY / 18 % height;

            for (int column = left; column < left + width; column++)
            {
                for (int row = top; row < top + height; row++)
                {
                    if (Main.tile[column, row] is not { HasTile: true } part || part.TileType != tile.TileType)
                    {
                        tile.ClearTile();
                        return;
                    }
                }
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
