using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Expanded mushroom biome generated in the cavern layer near spawn. Currently base generation and glowing
/// mushroom ambience; sub-biomes to come.
/// </summary>
public class GrottoPass : GenPass
{
    // Base biome blob
    private int mudTile = TileID.Mud;
    private const int wallTile = WallID.MudUnsafe;

    private const float BaseBlobWarpAmt = 28f;
    private const float BaseBlobWarpFreq = 0.097f;
    private const float BaseBlobRoughAmt = 10f;
    private const float BaseBlobRoughFreq = 0.09f;

    private const int AmbientPocketCount = 16;
    private const int AmbientRadiusMin = 5;
    private const int AmbientRadiusMax = 11;
    private const float AmbientInnerZone = 0.95f;

    // Node (cavern blob) settings
    private const int NodeRadiusMin = 18;
    private const int NodeRadiusMax = 28;
    private const float BlobWarpAmt = 18f;
    private const float BlobFreq = 0.04f;

    private const int GridCols = 2;
    private const int GridRows = 7;
    private const float GridJitter = 0.55f;
    private const float GridInnerZone = 0.45f;

    private const int SpineNodeCount = 9;
    private const float SpineCenterBias = 0.36f;

    private const float TunnelRadius = 4.7f;
    private const float TunnelRadiusVariation = 0.8f;
    private const float TunnelWallWarpAmt = 1.4f;
    private const float TunnelWallWarpFreq = 0.067f;

    // Turtle walker settings
    private const int StepSize = 3;
    private const float WarpStrength = 10f;
    private const float WarpFreq = 0.03f;
    private const float TargetPull = 0.08f;
    private const float RadiusNoiseFreq = 0.11f;

    private const int VineLengthMin = 3;
    private const int VineLengthMax = 6; // 5

    private const string ArgieLab = "Assets/Structures/ArgiesLab";
    private const int ArgieNodeRadius = 48;
    private const float ArgieCenterZone = 0.15f;

    // Tile-unit constants are tuned for a large world (8400 wide); WorldScale shrinks them proportionally for
    // medium and small worlds. Frequencies are already in tile space and aren't scaled.
    private static float WorldScale => Main.maxTilesX / 8400f;

    public GrottoPass() : base("[Substrate] Glowshroom Grotto", 1f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration config)
    {
        progress.Message = "Nurturing natural mycelias...";

        var originX = Main.maxTilesX / 2;
        var originY = (int)(Main.maxTilesY * 0.48f);
        var boundsW = (int)(Main.maxTilesX * 0.058f);
        var boundsH = (int)(Main.maxTilesY * 0.13f);

        var scale = WorldScale;

        var baseBlobNoise = new FastNoiseLite(WorldGen.genRand.Next());
        baseBlobNoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
        baseBlobNoise.SetFrequency(0.14f); // note: set BaseBlobWarpFreq to 0.14f

        var baseBlobRoughNoise = new FastNoiseLite(WorldGen.genRand.Next());
        baseBlobRoughNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        baseBlobRoughNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        baseBlobRoughNoise.SetFractalOctaves(4);
        baseBlobRoughNoise.SetFrequency(BaseBlobRoughFreq);

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

        var biomeMask = BiomeBlob(originX, originY, boundsW, boundsH, baseBlobNoise, baseBlobRoughNoise, progress, 0.0, 0.22, scale);

        var nodes = ScatterNodes(scale, biomeMask, out Node argieNode);

        var edges = BuildMST(nodes);
        TryAddExtraEdges(nodes, edges, extraCount: 9);

        for (var i = 0; i < nodes.Count; i++)
        {
            progress.Value = 0.22 + i / (double)nodes.Count * 0.20;
            CarveBlob(nodes[i], blobNoise, scale);
        }

        for (var i = 0; i < edges.Count; i++)
        {
            progress.Value = 0.42 + i / (double)edges.Count * 0.20;
            var (a, b) = edges[i];
            WalkTunnel(
                new Vector2(nodes[a].X, nodes[a].Y),
                new Vector2(nodes[b].X, nodes[b].Y),
                pathNoise, radiusNoise, wallWarpNoise, scale
            );
        }

        var pockets = ScatterAmbientPockets(biomeMask, scale);
        for (var i = 0; i < pockets.Count; i++)
        {
            progress.Value = 0.62 + i / (double)pockets.Count * 0.08;
            CarveBlob(pockets[i], blobNoise, scale);
        }

        progress.Message = "Tracing cave walls...";
        PlaceEdgeWalls(biomeMask, progress, 0.70, 0.78);

        progress.Message = "Spreading mold...";
        SpreadGrass(biomeMask, progress, 0.78, 0.95);

        // Last, so no carve or wall pass touches the lab.
        progress.Message = "Furnishing the lab...";
        PlaceArgieLab(argieNode);

        progress.Value = 1.0;
    }

    /// <summary>
    /// Places the lab bottom-centre on the first solid tile below Argie's node, protected from digging. Reserve it
    /// with <c>placed.ReserveFor&lt;ArgieNPC&gt;()</c> once she exists.
    /// </summary>
    private static void PlaceArgieLab(Node node)
    {
        int floorY = node.Y;
        while (WorldGen.InWorld(node.X, floorY + 1) && !WorldGen.SolidTile(node.X, floorY + 1))
            floorY++;

        if (!Structures.TryPlace(ArgieLab, new Point(node.X, floorY), StructureAnchor.BottomCenter, out PlacedStructure placed))
            return;

        StructureProtectionSystem.ArgieLabArea = placed.Area;
    }

    private BiomeMask BiomeBlob(int originX, int originY, int width, int height, FastNoiseLite noise, FastNoiseLite roughNoise,
        GenerationProgress progress, double progressStart, double progressEnd, float scale)
    {
        List<FieldNode> metaballs = new();

        metaballs.Add(
        new FieldNode
        {
            Position = new(originX, originY),
            Radius = 150 * scale
        });

        for (int i = 0; i < 20; i++)
        {
            var parent = metaballs[WorldGen.genRand.Next(metaballs.Count)];

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(125f, 175f) * scale;

            metaballs.Add(new FieldNode
            {
                Position = parent.Position + angle.ToRotationVector2() * distance,
                Radius = Main.rand.NextFloat(130f, 160f) * scale
            });
        }

        float minX = originX, maxX = originX, minY = originY, maxY = originY;
        foreach (var node in metaballs)
        {
            minX = Math.Min(minX, node.Position.X - node.Radius);
            maxX = Math.Max(maxX, node.Position.X + node.Radius);
            minY = Math.Min(minY, node.Position.Y - node.Radius);
            maxY = Math.Max(maxY, node.Position.Y + node.Radius);
        }

        float warpAmt = BaseBlobWarpAmt * scale;
        float roughAmt = BaseBlobRoughAmt * scale;
        int margin = (int)(warpAmt + roughAmt + 2);

        int left = (int)Math.Floor(minX) - margin;
        int right = (int)Math.Ceiling(maxX) + margin;
        int top = (int)Math.Floor(minY) - margin;
        int bottom = (int)Math.Ceiling(maxY) + margin;

        var mask = new BiomeMask(left, top, right - left + 1, bottom - top + 1);

        int totalColumns = right - left + 1;
        int column = 0;

        for (int tileX = left; tileX <= right; tileX++)
        {
            progress.Value = progressStart + (column++ / (double)totalColumns) * (progressEnd - progressStart);
            for (int tileY = top; tileY <= bottom; tileY++)
            {
                if (!WorldGen.InWorld(tileX, tileY)) continue;

                float warpedX = tileX + noise.GetNoise(tileX, tileY) * warpAmt;
                float warpedY = tileY + noise.GetNoise(tileX + 700f, tileY + 700f) * warpAmt;
                warpedX += roughNoise.GetNoise(tileX, tileY) * roughAmt;
                warpedY += roughNoise.GetNoise(tileX + 400f, tileY + 400f) * roughAmt;

                float density = 0f;

                foreach (var node in metaballs)
                {
                    float dx = warpedX - node.Position.X;
                    float dy = warpedY - node.Position.Y;
                    float distSq = (dx * dx) + (dy * dy);

                    if (distSq == 0)
                    {
                        density += 1000f;
                    }
                    else
                    {
                        float distance = MathF.Sqrt(distSq);

                        if (distance < node.Radius)
                        {
                            density += 1f - distance / node.Radius;
                        }
                    }
                }

                if (density >= 1f)
                {
                    WorldGen.PlaceTile(tileX, tileY, mudTile, mute: true, forced: true);
                    mask.Mark(tileX, tileY);
                }
            }
        }

        return mask;
    }

    private void PlaceEdgeWalls(BiomeMask mask, GenerationProgress progress, double progressStart, double progressEnd)
    {
        const int wallThickness = 3;

        var left = mask.Left - wallThickness;
        var right = mask.Left + mask.Width + wallThickness;
        var top = mask.Top - wallThickness;
        var bottom = mask.Top + mask.Height + wallThickness;

        var total = (right - left + 1) * (bottom - top + 1);
        var processed = 0;

        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                if (processed++ % 500 == 0)
                    progress.Value = progressStart + processed / (double)total * (progressEnd - progressStart);

                if (!WorldGen.InWorld(x, y)) continue;

                var tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile) continue;

                if (HasBiomeNeighbor(mask, x, y, wallThickness))
                    WorldGen.PlaceWall(x, y, wallTile, mute: true);
            }
        }
    }

    private static bool HasBiomeNeighbor(BiomeMask mask, int x, int y, int radius)
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

                if (mask.Contains(nx, ny))
                {
                    if (Framing.GetTileSafely(nx, ny).HasTile)
                        return true;
                }
            }
        }

        return false;
    }

    private void SpreadGrass(BiomeMask biomeMask, GenerationProgress progress, double progressStart, double progressEnd)
    {
        var left = biomeMask.Left;
        var right = biomeMask.Left + biomeMask.Width - 1;
        var top = biomeMask.Top;
        var bottom = biomeMask.Top + biomeMask.Height - 1;

        var total = (right - left + 1) * (bottom - top + 1);
        var processed = 0;
        var grassType = TileID.MushroomGrass;

        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                if (biomeMask.Contains(x, y))
                {
                    if (processed++ % 500 == 0)
                        progress.Value = progressStart + processed / (double)total * (progressEnd - progressStart);

                    if (!WorldGen.InWorld(x, y)) continue;

                    var tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || tile.TileType != TileID.Mud) continue;

                    if (IsAdjacentToCarvedSpace(x, y))
                    {
                        tile.TileType = grassType;
                        WorldGen.SquareTileFrame(x, y, true);
                    }
                }
            }
        }
    }

    private bool IsAdjacentToCarvedSpace(int x, int y)
    {
        for (var nx = x - 1; nx <= x + 1; nx++)
        {
            for (var ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;
                if (!WorldGen.InWorld(nx, ny)) continue;

                var neighbor = Framing.GetTileSafely(nx, ny);
                if (!neighbor.HasTile) return true;
                if (neighbor.WallType == wallTile) return true;
            }
        }

        return false;
    }


    private List<Node> ScatterAmbientPockets(BiomeMask mask, float scale)
    {
        var pockets = new List<Node>();

        for (var i = 0; i < AmbientPocketCount; i++)
        {
            var point = mask.RandomPoint();
            var r = (int)(WorldGen.genRand.Next(AmbientRadiusMin, AmbientRadiusMax) * scale);
            pockets.Add(new Node(point.X, point.Y, r));
        }

        return pockets;
    }

    private List<Node> ScatterNodes(float scale, BiomeMask mask, out Node argieNode)
    {
        var nodes = new List<Node>();

        int maskCenterX = mask.Left + mask.Width / 2;
        int maskCenterY = mask.Top + mask.Height / 2;

        // Argie is anchored to the mask's centre, independent of grid tuning, and inserted at index 0 because
        // Prim's MST roots at nodes[0].
        var argieBoxW = (int)(mask.Width * ArgieCenterZone);
        var argieBoxH = (int)(mask.Height * ArgieCenterZone);
        var argiePoint = PlaceWithinMask(mask, maskCenterX, maskCenterY, argieBoxW / 2, argieBoxH / 2);
        argieNode = new Node(argiePoint.X, argiePoint.Y, (int)(ArgieNodeRadius * scale));
        nodes.Add(argieNode);

        var innerW = (int)(mask.Width * GridInnerZone);
        var innerH = (int)(mask.Height * GridInnerZone);
        var cellW = innerW / GridCols;
        var cellH = innerH / GridRows;
        var gridLeft = maskCenterX - innerW / 2;
        var gridTop = maskCenterY - innerH / 2;

        for (var col = 0; col < GridCols; col++)
        {
            for (var row = 0; row < GridRows; row++)
            {
                var cellCx = gridLeft + col * cellW + cellW / 2;
                var cellCy = gridTop + row * cellH + cellH / 2;
                var jitterX = (int)(cellW * 0.52f);
                var jitterY = (int)(cellH * 0.72f);

                var point = PlaceWithinMask(mask, cellCx, cellCy, jitterX, jitterY);
                var r = (int)(WorldGen.genRand.Next(NodeRadiusMin, NodeRadiusMax) * scale);
                nodes.Add(new Node(point.X, point.Y, r));
            }
        }

        var spineTop = maskCenterY - innerH / 2;
        var spineBottom = maskCenterY + innerH / 2;
        var maxDrift = (int)(mask.Width * SpineCenterBias);

        for (var i = 0; i < SpineNodeCount; i++)
        {
            var t = i / (float)(SpineNodeCount - 1);
            var targetY = spineTop + (int)(t * (spineBottom - spineTop));
            var point = PlaceWithinMask(mask, maskCenterX, targetY, maxDrift, 0);
            var r = (int)(WorldGen.genRand.Next(NodeRadiusMin, NodeRadiusMax) * scale);

            nodes.Add(new Node(point.X, point.Y, r));
        }

        return nodes;
    }

    private static Point PlaceWithinMask(BiomeMask mask, int cx, int cy, int jitterX, int jitterY)
    {
        const int attemptsPerRadius = 12;
        const int maxExpansions = 5;

        var jx = jitterX;
        var jy = jitterY;

        for (var expansion = 0; expansion < maxExpansions; expansion++)
        {
            for (var attempt = 0; attempt < attemptsPerRadius; attempt++)
            {
                var x = cx + WorldGen.genRand.Next(-jx, jx + 1);
                var y = cy + WorldGen.genRand.Next(-jy, jy + 1);

                if (mask.Contains(x, y))
                    return new Point(x, y);
            }

            // Neighborhood missed the blob; widen the search before giving up on it.
            jx = jx * 3 / 2 + 1;
            jy = jy * 3 / 2 + 1;
        }

        // Fallback when the whole neighbourhood is outside the biome: any valid tile beats uncarved rock.
        return mask.RandomPoint();
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
    private void WalkTunnel(
        Vector2 from, Vector2 to,
        FastNoiseLite pathNoise,
        FastNoiseLite radiusNoise,
        FastNoiseLite wallWarpNoise,
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