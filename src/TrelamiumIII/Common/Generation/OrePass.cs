using TrelamiumIII.Content.Lodestone;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Utilities;
using TrelamiumIII.Content.Alchemy.Reagents;

namespace TrelamiumIII.Common.Generation;

public class OrePass : GenPass
{
    public OrePass() : base("Ores", 150f) { }

 
    /// <summary>
    /// what defines an ore chunk for the gen pass
    /// </summary>
    /// <param name="TileType"></param>
    /// <param name="DepthPeak">the Y fraction (0=top, 1=bottom) where this ore is most common</param>
    /// <param name="DepthRange">how far above/below the peak the ore still generates (falloff width)</param>
    /// <param name="BaseThreshold">noise value the tile must exceed to place ore (higher = rarer)</param>
    /// <param name="NoiseFreq">how tightly the noise clusters (higher = smaller, more frequent pockets)</param>
    /// <param name="Density">denisty, duh</param>
    /// <param name="VeinSize">passed to WorldGen.TileRunner as the strength/size argument</param>
    /// <param name="VeinSteps">TileRunner step count (length of each vein)</param>
    /// <param name="HostTiles">which tile types this ore can replace</param>
    /// <param name="BiomeEnrich">pairs of (hostTile, thresholdReduction). When a biome tile is nearby, the threshold drops by that amount, making ore richer there.</param>
    private readonly record struct OreProfile(
        int TileType,
        float DepthPeak,
        float DepthRange,
        float BaseThreshold,
        float NoiseFreq,
        float Density,        // 
                              // 0.003 = very sparse, 0.012 = moderately common
        int VeinSize,
        int VeinSteps,
        int[] HostTiles,
        (int tile, float reduction)[] BiomeEnrich
    );

    private static IEnumerable<OreProfile> BuildOreTable()
    {
        yield return new OreProfile(
            TileType: TileID.Copper,
            DepthPeak: 0.10f, DepthRange: 0.20f,
            BaseThreshold: 0.72f, NoiseFreq: 0.065f,
            Density: 0.08f,
            VeinSize: 5, VeinSteps: 6,
            HostTiles: [TileID.Stone, TileID.Dirt],
            BiomeEnrich: [(TileID.Sand, 0.08f), (TileID.Sandstone, 0.08f)]
        );
        yield return new OreProfile(
            TileType: TileID.Tin,
            DepthPeak: 0.10f, DepthRange: 0.20f,
            BaseThreshold: 0.72f, NoiseFreq: 0.065f,
            Density: 0.08f,
            VeinSize: 5, VeinSteps: 6,
            HostTiles: [TileID.Stone, TileID.Dirt],
            BiomeEnrich: [(TileID.Sand, 0.08f), (TileID.Sandstone, 0.08f)]
        );

        yield return new OreProfile(
            TileType: TileID.Iron,
            DepthPeak: 0.28f, DepthRange: 0.20f,
            BaseThreshold: 0.74f, NoiseFreq: 0.055f,
            Density: 0.09f,
            VeinSize: 6, VeinSteps: 7,
            HostTiles: [TileID.Stone],
            BiomeEnrich: [(TileID.Mud, 0.06f)]
        );
        yield return new OreProfile(
            TileType: TileID.Lead,
            DepthPeak: 0.28f, DepthRange: 0.20f,
            BaseThreshold: 0.74f, NoiseFreq: 0.055f,
            Density: 0.09f,
            VeinSize: 6, VeinSteps: 7,
            HostTiles: [TileID.Stone],
            BiomeEnrich: [(TileID.Mud, 0.06f)]
        );

        yield return new OreProfile(
            TileType: TileID.Silver,
            DepthPeak: 0.45f, DepthRange: 0.22f,
            BaseThreshold: 0.76f, NoiseFreq: 0.048f,
            Density: 0.08f,
            VeinSize: 6, VeinSteps: 7,
            HostTiles: [TileID.Stone],
            BiomeEnrich: [(TileID.Marble, 0.12f), (TileID.IceBlock, 0.07f)]
        );
        yield return new OreProfile(
            TileType: TileID.Tungsten,
            DepthPeak: 0.45f, DepthRange: 0.22f,
            BaseThreshold: 0.76f, NoiseFreq: 0.048f,
            Density: 0.07f,
            VeinSize: 6, VeinSteps: 7,
            HostTiles: [TileID.Stone],
            BiomeEnrich: [(TileID.Granite, 0.12f), (TileID.IceBlock, 0.07f)]
        );

        yield return new OreProfile(
            TileType: TileID.Gold,
            DepthPeak: 0.62f, DepthRange: 0.22f,
            BaseThreshold: 0.78f, NoiseFreq: 0.042f,
            Density: 0.06f,
            VeinSize: 7, VeinSteps: 8,
            HostTiles: [TileID.Stone],
            BiomeEnrich: [(TileID.Granite, 0.10f), (TileID.Sand, 0.08f)]
        );
        yield return new OreProfile(
            TileType: TileID.Platinum,
            DepthPeak: 0.62f, DepthRange: 0.22f,
            BaseThreshold: 0.78f, NoiseFreq: 0.042f,
            Density: 0.06f,
            VeinSize: 7, VeinSteps: 8,
            HostTiles: [TileID.Stone],
            BiomeEnrich: [(TileID.Marble, 0.10f), (TileID.SnowBlock, 0.08f)]
        );

        yield return new OreProfile(
            TileType: TileID.Demonite,
            DepthPeak: 0.55f, DepthRange: 0.25f,
            BaseThreshold: 0.80f, NoiseFreq: 0.038f,
            Density: 0.03f,
            VeinSize: 6, VeinSteps: 7,
            HostTiles: [TileID.Stone, TileID.Ebonstone],
            BiomeEnrich: [(TileID.Ebonstone, 0.16f)]
        );
        yield return new OreProfile(
            TileType: TileID.Crimtane,
            DepthPeak: 0.55f, DepthRange: 0.25f,
            BaseThreshold: 0.80f, NoiseFreq: 0.038f,
            Density: 0.03f,
            VeinSize: 6, VeinSteps: 7,
            HostTiles: [TileID.Stone, TileID.Crimstone],
            BiomeEnrich: [(TileID.Crimstone, 0.16f)]
        );

        yield return new OreProfile(
            TileType: TileID.Hellstone,
            DepthPeak: 0.97f, DepthRange: 0.06f,
            BaseThreshold: 0.70f, NoiseFreq: 0.055f,
            Density: 0.010f,
            VeinSize: 10, VeinSteps: 12,
            HostTiles: [TileID.Ash, TileID.Stone],
            BiomeEnrich: []
        );

        yield return new OreProfile(
            TileType: ModContent.TileType<LodestoneTile>(),
            DepthPeak: 0.40f, DepthRange: 0.35f,
            BaseThreshold: 0.76f, NoiseFreq: 0.044f,
            Density: 0.08f,
            VeinSize: 7, VeinSteps: 9,
            HostTiles: [TileID.Stone, TileID.Granite, TileID.Marble],
            BiomeEnrich: [(TileID.Granite, 0.14f), (TileID.Marble, 0.14f)]
        );

        yield return new OreProfile(
            TileType: ModContent.TileType<SulfurTile>(),
            DepthPeak: 0.40f, DepthRange: 0.35f,
            BaseThreshold: 0.76f, NoiseFreq: 0.044f,
            Density: 0.11f,
            VeinSize: 5, VeinSteps: 9,
            HostTiles: [TileID.Stone, TileID.Ash],
            BiomeEnrich: [(TileID.Stone, 0.14f), (TileID.Ash, 0.14f)]
        );
    }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "laying the land with minerals...";

        var ores = new List<OreProfile>(BuildOreTable());
        int total = ores.Count;

        for (int idx = 0; idx < total; idx++)
        {
            progress.Value = idx / (double)total;
            GenerateOre(ores[idx]);
        }
    }

    private static void GenerateOre(OreProfile ore)
    {
        var noise = new FastNoiseLite(WorldGen.genRand.Next());
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(3);
        noise.SetFrequency(ore.NoiseFreq);

        int worldH = Main.maxTilesY;
        int worldW = Main.maxTilesX;

        float peakY = ore.DepthPeak * worldH;
        float rangeY = ore.DepthRange * worldH;

        for (int x = 0; x < worldW; x++)
        {
            for (int y = 0; y < worldH; y++)
            {
                float dist = MathF.Abs(y - peakY);
                float depthWeight = MathF.Exp(-(dist * dist) / (2f * rangeY * rangeY));

                if (depthWeight < 0.08f) continue;

                // Cheap random cull before any tile read or noise sample.
                // This is the primary sparsity control  ore.Density is a small
                // probability (e.g. 0.004 = 0.4% of eligible depth tiles checked).
                // Multiply by depthWeight so tiles near the peak are checked more
                // often than tiles at the fringes, giving a natural gradient.
                if (WorldGen.genRand.NextDouble() > ore.Density * depthWeight) continue;

                if (!IsHostTile(x, y, ore.HostTiles)) continue;

                float n = (noise.GetNoise(x, y) + 1f) * 0.5f;
                float threshold = ore.BaseThreshold - BiomeBonus(x, y, ore.BiomeEnrich);

                if (n > threshold)
                    WorldGen.TileRunner(x, y, ore.VeinSize, ore.VeinSteps, ore.TileType);
            }
        }
    }

    // Helpers
    private static bool IsHostTile(int x, int y, int[] hostTiles)
    {
        if (!WorldGen.InWorld(x, y)) return false;
        var tile = Main.tile[x, y];
        if (!tile.HasTile) return false;

        foreach (int t in hostTiles)
            if (tile.TileType == t) return true;

        return false;
    }

    // Checks a small radius around the tile for biome-indicator tiles.
    // Returns the highest enrichment bonus found (doesn't stack).
    private static float BiomeBonus(int x, int y, (int tile, float reduction)[] enrichments)
    {
        if (enrichments.Length == 0) return 0f;

        float best = 0f;
        int r = 6; // scan radius in tiles

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!WorldGen.InWorld(nx, ny)) continue;

                int type = Main.tile[nx, ny].TileType;
                foreach (var (biomeTile, reduction) in enrichments)
                {
                    if (type == biomeTile && reduction > best)
                        best = reduction;
                }
            }
        }

        return best;
    }
}