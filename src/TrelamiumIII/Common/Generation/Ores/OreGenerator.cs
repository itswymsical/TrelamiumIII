using System.Collections.Generic;
using Terraria.WorldBuilding;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation.Ores;

/// <summary> Places <see cref="OreTable"/>; runs in place of vanilla Shinies. </summary>
public static class OreGenerator
{
    private const int AttemptsPerVein = 40;
    private const float RichnessFrequency = 0.012f;

    /// <param name="includeVanillaOres"> False skips vanilla ores, for worlds where vanilla Shinies places them instead. </param>
    public static void Generate(GenerationProgress progress, bool includeVanillaOres)
    {
        progress.Message = "Laying the land with minerals...";

        List<OreVein> veins = OreTable.Build();
        var richness = new Dictionary<ushort, FastNoiseLite>();

        for (int i = 0; i < veins.Count; i++)
        {
            progress.Set(i / (double)veins.Count);

            OreVein vein = veins[i];
            if (includeVanillaOres || !IsVanillaOre(vein.Tile))
                PlaceVeins(vein, richness);
        }
    }

    private static void PlaceVeins(OreVein vein, Dictionary<ushort, FastNoiseLite> richness)
    {
        int top = Math.Max(vein.Top.Row, 0);
        int bottom = Math.Min(vein.Bottom.Row, Main.maxTilesY);
        int count = (int)(Main.maxTilesX * (double)Main.maxTilesY * 1e-6 * vein.PerMillion);

        if (count <= 0 || bottom <= top)
            return;

        FastNoiseLite? noise = vein.Clustering > 0f ? GetRichness(richness, vein.Tile) : null;
        int attempts = count * AttemptsPerVein;

        for (int placed = 0; placed < count && attempts > 0; attempts--)
        {
            int x = WorldGen.genRand.Next(Main.maxTilesX);
            int y = WorldGen.genRand.Next(top, bottom);

            if (vein.Hosts is not null && !IsHost(x, y, vein.Hosts))
                continue;

            if (noise is not null && WorldGen.genRand.NextFloat() > Richness(noise, x, y, vein.Clustering))
                continue;

            int strength = WorldGen.genRand.Next(vein.Strength.Min, vein.Strength.Max);
            int steps = WorldGen.genRand.Next(vein.Steps.Min, vein.Steps.Max);
            WorldGen.TileRunner(x, y, strength, steps, vein.Tile);
            placed++;
        }
    }

    /// <summary> One noise field per ore tile, so every row of the same ore clusters in the same regions. </summary>
    private static FastNoiseLite GetRichness(Dictionary<ushort, FastNoiseLite> richness, ushort tile)
    {
        if (!richness.TryGetValue(tile, out FastNoiseLite? noise))
        {
            noise = new FastNoiseLite(WorldGen.genRand.Next());
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            noise.SetFrequency(RichnessFrequency);
            richness[tile] = noise;
        }

        return noise;
    }

    /// <summary> Chance to keep a vein at (x, y): 1 at the richest noise, down to 1 - clustering at the poorest. </summary>
    private static float Richness(FastNoiseLite noise, int x, int y, float clustering) =>
        1f - clustering * (1f - (noise.GetNoise(x, y) + 1f) * 0.5f);

    private static bool IsHost(int x, int y, ushort[] hosts)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && Array.IndexOf(hosts, tile.TileType) >= 0;
    }

    private static bool IsVanillaOre(ushort tile) => tile is TileID.Copper or TileID.Tin or TileID.Iron or TileID.Lead
        or TileID.Silver or TileID.Tungsten or TileID.Gold or TileID.Platinum or TileID.Demonite or TileID.Crimtane;
}
