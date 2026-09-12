using ReLogic.Utilities;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Core.Graphics.Metaballs;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Subworlds.Realms;

public class IngressRealm : SubworldRealm
{
    public override int Width => 4200;
    public override string Name => "Ingress";
    public override int Height => 1280;

    public override List<GenPass> Tasks => 
    [
        new IngressFloor(), 
        new IngressGrass(),
        new IngressCaves(),
        new SmoothPass("Smoothing Tiles", 100f), 
        new IngressVegetation()
    ];

    public override bool ShouldSave => false;
    public override bool NormalUpdates => true;
    public override bool NoPlayerSaving => false;

    public override void SetStaticDefaults()
    {
        try
        {
            base.SetStaticDefaults();
            OtherworldTitle = TextureAssets.Logo?.Value;
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance?.Logger.Error($"Error in IngressRealm.SetStaticDefaults: {ex.Message}");
            OtherworldTitle = null;
        }
    }
    public IngressRealm()
    {
        GenVars.rockLayer = Main.maxTilesY / 1.75;
        GenVars.rockLayerHigh = Main.maxTilesY / 2;
        GenVars.rockLayerLow = Main.maxTilesY / 1.6;

        GenVars.worldSurface = Main.maxTilesY / 2.75;
        GenVars.worldSurfaceHigh = Main.maxTilesY / 3;
        GenVars.worldSurfaceLow = Main.maxTilesY / 2.5;
    }
    public override void Update()
    {
        ManageGameConditions();
    }

    private void ManageGameConditions()
    {
        var player = Main.LocalPlayer;

        if (Main.slimeRain)
            Main.slimeRain = false;


        Main.townNPCCanSpawn[default] = false;
    }

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;
}

public class IngressFloor : GenPass
{
    public FastNoiseLite surfaceNoise;
    public static int surfaceY = Main.maxTilesY / 4;
    public IngressFloor() : base("[T3] Ingress Floor", 247.43f) 
    {
        surfaceNoise = new FastNoiseLite(WorldGen.genRand.Next());
        surfaceNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
        surfaceNoise.SetFrequency(0.0041f); // (lower = smoother/wider)
        surfaceNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        surfaceNoise.SetFractalOctaves(8);
        surfaceNoise.SetFractalGain(0.5f);
    }


    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Generating Ingress Floor";

        Main.spawnTileY = surfaceY;

        int heightAmplitude = 30;
        int dirtLayerThickness = 120;

        // 1. generate surface heightmap and tile grid
        for (int x = 0; x < Main.maxTilesX; x++)
        {
            float rawNoise = surfaceNoise.GetNoise(x, 0f); // Returns -1.0 to 1.0
            int localSurfaceY = surfaceY + (int)(rawNoise * heightAmplitude);

            int transitionY = localSurfaceY + dirtLayerThickness + WorldGen.genRand.Next(-6, 7);

            for (int y = surfaceY - heightAmplitude; y < localSurfaceY; y++)
            {
                if (WorldGen.InWorld(x, y))
                {
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = false;
                }
            }

            for (int y = localSurfaceY; y < Main.maxTilesY; y++)
            {
                if (WorldGen.InWorld(x, y))
                {
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;

                    if (y < transitionY)
                    {
                        tile.TileType = TileID.Dirt;
                        tile.WallType = WallID.FlowerUnsafe; 
                    }
                    else
                    {
                        tile.TileType = (ushort)ModContent.TileType<BramblerootTile>();
                    }
                }
            }
        }

        int surfaceX = WorldGen.genRand.Next(100, 200);

        while (surfaceX < Main.maxTilesX - 100)
        {
            // Skip spawn area so we don't overlap the guaranteed mountain
            if (Math.Abs(surfaceX - Main.spawnTileX) > 150)
            {
                WorldGen.Mountinater(surfaceX, surfaceY + 32);
            }

            surfaceX += WorldGen.genRand.Next(200, 400);
        }
        StonePatches();
    }

    private void StonePatches()
    {
        int x = WorldGen.genRand.Next(100, 200);

        while (x < Main.maxTilesX - 100)
        {
            int surfaceY2 = 0;
            for (int y = 150; y < Main.maxTilesY; y++)
            {
                if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
                {
                    surfaceY2 = y;
                    break;
                }
            }

            // StonePatch requires grass on the surface and walls beneath, so fake them temporarily.
            for (int i = x - 10; i <= x + 10; i++)
            {
                if (WorldGen.InWorld(i, surfaceY2))
                {
                    Main.tile[i, surfaceY2].TileType = TileID.Grass;

                    for (int j = surfaceY2 + 1; j <= surfaceY2 + 35; j++)
                    {
                        if (WorldGen.InWorld(i, j))
                            Main.tile[i, j].WallType = WallID.FlowerUnsafe;
                    }
                }
            }

            if (WorldGen.StonePatch(x, surfaceY2))
            {
                x += WorldGen.genRand.Next(100, 200); // Space out successful patches
                continue;
            }

            x += WorldGen.genRand.Next(20, 40); // Try nearby spot on failure
        }
    }
}

public class IngressGrass : GrassSpreadPass
{
    public IngressGrass() : base("[T3] Ingress Grass", 100f) { }

    protected override ushort GetGrassTileType() => TileID.Grass;


    protected override string GetProgressMessage() => "Spreading Grass";

    protected override ushort GetSoilTileType() => TileID.Dirt;
    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        base.ApplyPass(progress, configuration);
        LivingTrees();
    }

    private void LivingTrees()
    {
        int x = WorldGen.genRand.Next(150, 300);

        while (x < Main.maxTilesX - 150)
        {
            // Avoid placing living trees right on top of the main spawn point
            if (Math.Abs(x - Main.spawnTileX) > 120)
            {
                int surfaceY2 = 0;
                for (int y = 150; y < Main.maxTilesY; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
                    {
                        surfaceY2 = y - 1; // The air tile directly above solid ground
                        break;
                    }
                }

                if (WorldGen.GrowLivingTree(x, surfaceY2, patch: true))
                {
                    x += WorldGen.genRand.Next(250, 450);
                    continue;
                }
            }

            x += WorldGen.genRand.Next(15, 35);
        }
    }
}

public class IngressVegetation : GenPass
{
    public IngressVegetation() : base("[T3] Ingress Vegetation", 247.43f) { }
    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Growing Flora and Trees";

        WorldGen.AddTrees();
        WorldGen.GrowGlowTulips();
    }
}

public class IngressCaves : GenPass
{
    // Base biome blob
    private int mudTile = (ushort)ModContent.TileType<BramblerootTile>();
    private const int wallTile = WallID.MudUnsafe;

    private const float BaseBlobWarpAmt = 28f;
    private const float BaseBlobWarpFreq = 0.097f;
    private const float BaseBlobRoughAmt = 10f;
    private const float BaseBlobRoughFreq = 0.09f;

    private const int AmbientPocketCount = 35;
    private const int AmbientRadiusMin = 5;
    private const int AmbientRadiusMax = 11;
    private const float AmbientInnerZone = 0.95f;

    // Node (cavern blob) settings
    private const int NodeRadiusMin = 28;
    private const int NodeRadiusMax = 38;
    private const float BlobWarpAmt = 18f;
    private const float BlobFreq = 0.04f;

    private const int GridCols = 20;
    private const int GridRows = 10;
    private const float GridJitter = 0.45f;
    private const float GridInnerZone = 0.45f;

    private const int ExtraEdgeCount = 45;
    private const int SpineNodeCount = 18;
    private const float SpineCenterBias = 0.36f;

    private const float TunnelRadius = 11.7f;
    private const float TunnelRadiusVariation = 0.9f;
    private const float TunnelWallWarpAmt = 1.4f;
    private const float TunnelWallWarpFreq = 0.087f;

    // Turtle walker settings
    private const int StepSize = 4;
    private const float WarpStrength = 14f;
    private const float WarpFreq = 0.03f;
    private const float TargetPull = 0.11f;
    private const float RadiusNoiseFreq = 0.13f;

    private const int VineLengthMin = 3;
    private const int VineLengthMax = 6; // 5

    private const string ArgieLab = "Assets/Structures/ArgiesLab";
    private const int labWidth = 45;
    private const int labHeight = 32;
    private const int ArgieNodeRadius = 48;
    private const float ArgieCenterZone = 0.15f;

    // Tile-unit constants are tuned for a large world (8400 wide); WorldScale shrinks them proportionally for
    // medium and small worlds. Frequencies are already in tile space and aren't scaled.
    private static float WorldScale => Main.maxTilesX / 8400f;

    public IngressCaves() : base("[T3] Ingress Caves", 1f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
    {
        progress.Message = "Carving Ingress Caves";

        var scale = WorldScale;

        var blobNoise = new FastNoiseLite(WorldGen.genRand.Next());
        blobNoise.SetNoiseType(FastNoiseLite.NoiseType.Value);
        blobNoise.SetFrequency(BlobFreq);

        var pathNoise = new FastNoiseLite(WorldGen.genRand.Next());
        pathNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
        pathNoise.SetFrequency(BaseBlobWarpFreq);

        var radiusNoise = new FastNoiseLite(WorldGen.genRand.Next());
        radiusNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        radiusNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        radiusNoise.SetFrequency(RadiusNoiseFreq);

        var wallWarpNoise = new FastNoiseLite(WorldGen.genRand.Next());
        wallWarpNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        wallWarpNoise.SetFrequency(TunnelWallWarpFreq);

        var nodes = ScatterNodes(scale, out Node argieNode);

        var edges = BuildMST(nodes);

        TryAddExtraEdges(nodes, edges, extraCount: ExtraEdgeCount);

        for (var i = 0; i < nodes.Count; i++)
        {
            progress.Value = i / (double)nodes.Count * 0.40;
            CarveBlob(nodes[i], blobNoise, scale);
        }

        for (var i = 0; i < edges.Count; i++)
        {
            progress.Value = 0.40 + i / (double)edges.Count * 0.40;
            var (a, b) = edges[i];
            WalkTunnel(
                new Vector2(nodes[a].X, nodes[a].Y),
                new Vector2(nodes[b].X, nodes[b].Y),
                pathNoise, radiusNoise, wallWarpNoise, scale
            );
        }

        var pockets = ScatterAmbientPockets(scale);
        for (var i = 0; i < pockets.Count; i++)
        {
            progress.Value = 0.80 + i / (double)pockets.Count * 0.20;
            CarveBlob(pockets[i], blobNoise, scale);
        }
        progress.Message = "Tracing cave walls...";
        PlaceEdgeWalls(progress, 0.70, 0.78);

        progress.Value = 1.0;
    }

    private void PlaceEdgeWalls(GenerationProgress progress, double progressStart, double progressEnd)
    {
        const int wallThickness = 3;

        int left = 40;
        int right = Main.maxTilesX - 40;
        int top = (int)(IngressFloor.surfaceY * 1.25f);
        int bottom = Main.maxTilesY - 40;

        var total = (right - left + 1) * (bottom - top + 1);
        var processed = 0;

        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                if (processed++ % 5000 == 0) // Updated to 5000 so the progress bar doesn't stutter on the massive scale
                    progress.Value = progressStart + processed / (double)total * (progressEnd - progressStart);

                if (!WorldGen.InWorld(x, y)) continue;

                var tile = Framing.GetTileSafely(x, y);

                if (tile.HasTile) continue;

                if (HasSolidNeighbor(x, y, wallThickness))
                    WorldGen.PlaceWall(x, y, wallTile, mute: true);
            }
        }
    }

    private static bool HasSolidNeighbor(int x, int y, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    continue;

                int nx = x + dx;
                int ny = y + dy;

                if (!WorldGen.InWorld(nx, ny))
                    continue;

                if (Framing.GetTileSafely(nx, ny).HasTile)
                    return true;
            }
        }

        return false;
    }

    private List<Node> ScatterAmbientPockets(float scale)
    {
        var pockets = new List<Node>();

        int startX = 50;
        int endX = Main.maxTilesX - 50;
        int startY = IngressFloor.surfaceY + 60;
        int endY = Main.maxTilesY - 50;

        for (var i = 0; i < AmbientPocketCount; i++)
        {
            var px = WorldGen.genRand.Next(startX, endX);
            var py = WorldGen.genRand.Next(startY, endY);
            var r = (int)(WorldGen.genRand.Next(AmbientRadiusMin, AmbientRadiusMax) * scale);

            pockets.Add(new Node(px, py, r));
        }

        return pockets;
    }

    private List<Node> ScatterNodes(float scale, out Node argieNode)
    {
        var nodes = new List<Node>();

        int startX = 50;
        int endX = Main.maxTilesX - 50;
        int startY = IngressFloor.surfaceY + 60; // Start slightly below the surface
        int endY = Main.maxTilesY - 50;

        int areaWidth = endX - startX;
        int areaHeight = endY - startY;

        int cellW = areaWidth / GridCols;
        int cellH = areaHeight / GridRows;

        int centerX = startX + (areaWidth / 2);
        int centerY = startY + (areaHeight / 2);
        argieNode = new Node(centerX, centerY, (int)(ArgieNodeRadius * scale));
        nodes.Add(argieNode);

        for (var col = 0; col < GridCols; col++)
        {
            for (var row = 0; row < GridRows; row++)
            {
                var cellCx = startX + col * cellW + cellW / 2;
                var cellCy = startY + row * cellH + cellH / 2;

                var jitterX = (int)(cellW * GridJitter);
                var jitterY = (int)(cellH * GridJitter);

                var nx = cellCx + WorldGen.genRand.Next(-jitterX, jitterX + 1);
                var ny = cellCy + WorldGen.genRand.Next(-jitterY, jitterY + 1);

                var r = (int)(WorldGen.genRand.Next(NodeRadiusMin, NodeRadiusMax) * scale);
                nodes.Add(new Node(nx, ny, r));
            }
        }

        return nodes;
    }

    // Prim's MST
    private List<(int a, int b)> BuildMST(List<Node> nodes)
    {
        var n = nodes.Count;
        var inMST = new bool[n];
        var edges = new List<(int, int)>();

        inMST[0] = true;

        for (var step = 1; step < n; step++)
        {
            var best = float.MaxValue;
            int bestA = -1, bestB = -1;

            for (var i = 0; i < n; i++)
            {
                if (!inMST[i]) continue;
                for (var j = 0; j < n; j++)
                {
                    if (inMST[j]) continue;
                    var d = Dist(nodes[i].X, nodes[i].Y, nodes[j].X, nodes[j].Y);
                    if (d < best) { best = d; bestA = i; bestB = j; }
                }
            }

            inMST[bestB] = true;
            edges.Add((bestA, bestB));
        }

        return edges;
    }

    private void TryAddExtraEdges(List<Node> nodes, List<(int, int)> edges, int extraCount)
    {
        var candidates = new List<(float dist, int a, int b)>();
        var n = nodes.Count;

        for (var i = 0; i < n; i++)
            for (var j = i + 1; j < n; j++)
                if (!edges.Contains((i, j)) && !edges.Contains((j, i)))
                    candidates.Add((Dist(nodes[i].X, nodes[i].Y, nodes[j].X, nodes[j].Y), i, j));

        candidates.Sort((x, y) => x.dist.CompareTo(y.dist));

        var limit = Math.Min(extraCount, candidates.Count);
        for (var i = 0; i < limit; i++)
            edges.Add((candidates[i].a, candidates[i].b));
    }

    private void CarveBlob(Node node, FastNoiseLite noise, float scale)
    {
        var warpAmt = BlobWarpAmt * scale;
        var margin = node.Radius + (int)(14 * scale);

        for (var tx = node.X - margin; tx <= node.X + margin; tx++)
        {
            for (var ty = node.Y - margin; ty <= node.Y + margin; ty++)
            {
                if (!WorldGen.InWorld(tx, ty)) continue;

                var wx = tx + noise.GetNoise(tx, ty) * warpAmt;
                var wy = ty + noise.GetNoise(tx + 500f, ty + 500f) * warpAmt;

                if (Dist(wx, wy, node.X, node.Y) < node.Radius)
                    WorldGen.KillTile(tx, ty, noItem: true);
            }
        }
    }

    // Turtle walker
    private void WalkTunnel(Vector2 from, Vector2 to, 
        FastNoiseLite pathNoise, FastNoiseLite radiusNoise, FastNoiseLite wallWarpNoise, 
        float scale)
    {
        var pos = from;
        var angle = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var stepSize = StepSize * scale;

        var dist = Vector2.Distance(from, to);
        var maxSteps = (int)(dist / stepSize * 3f);
        var steps = 0;

        while (Vector2.Distance(pos, to) > stepSize * 3)
        {
            if (steps++ > maxSteps) break;

            var noiseWarp = pathNoise.GetNoise(pos.X, pos.Y) * WarpStrength * (MathF.PI / 180f);
            var targetAngle = MathF.Atan2(to.Y - pos.Y, to.X - pos.X);
            var angleDelta = WrapAngle(targetAngle - angle);
            var targetCorrection = angleDelta * TargetPull;

            angle += noiseWarp + targetCorrection;

            pos.X += MathF.Cos(angle) * stepSize;
            pos.Y += MathF.Sin(angle) * stepSize;

            if (!WorldGen.InWorld((int)pos.X, (int)pos.Y)) break;

            var r = TunnelRadius * scale
                  + radiusNoise.GetNoise(pos.X, pos.Y) * TunnelRadiusVariation * scale;

            CarveTunnelSection((int)pos.X, (int)pos.Y, r, wallWarpNoise, scale);
        }
    }

    private void CarveTunnelSection(int cx, int cy, float radius, FastNoiseLite wallWarpNoise, float scale)
    {
        var warpAmt = TunnelWallWarpAmt * scale;
        var bounds = (int)(radius + warpAmt + 2);

        for (var tx = cx - bounds; tx <= cx + bounds; tx++)
        {
            for (var ty = cy - bounds; ty <= cy + bounds; ty++)
            {
                if (!WorldGen.InWorld(tx, ty)) continue;

                var wx = tx + wallWarpNoise.GetNoise(tx, ty) * warpAmt;
                var wy = ty + wallWarpNoise.GetNoise(tx + 300f, ty + 300f) * warpAmt;

                if (Dist(wx, wy, cx, cy) < radius)
                    WorldGen.KillTile(tx, ty, noItem: true);
            }
        }
    }

    private static float Dist(float ax, float ay, float bx, float by)
        => MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    private static float WrapAngle(float a)
    {
        while (a > MathF.PI) a -= MathF.PI * 2f;
        while (a < -MathF.PI) a += MathF.PI * 2f;
        return a;
    }

    private readonly record struct Node(int X, int Y, int Radius, int TileOverride = -1);
}