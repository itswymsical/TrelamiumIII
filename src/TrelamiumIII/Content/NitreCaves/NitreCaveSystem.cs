using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Content.NitreCaves;

/// <summary>
/// Where the world's nitre caves are, saved with the world. Spawns only run on the server or in singleplayer, so
/// clients never need it.
/// </summary>
public sealed class NitreCaveSystem : ModSystem
{
    private static readonly List<Rectangle> caves = [];

    public static IReadOnlyList<Rectangle> Caves => caves;

    public static void Add(Rectangle bounds) => caves.Add(bounds);

    /// <summary>Whether tile (x, y) is inside a nitre cave.</summary>
    public static bool Contains(int x, int y)
    {
        foreach (Rectangle cave in caves)
        {
            if (cave.Contains(x, y))
                return true;
        }

        return false;
    }

    public override void ClearWorld() => caves.Clear();

    public override void SaveWorldData(TagCompound tag)
    {
        var packed = new int[caves.Count * 4];

        for (int n = 0; n < caves.Count; n++)
        {
            Rectangle cave = caves[n];
            (packed[n * 4], packed[n * 4 + 1], packed[n * 4 + 2], packed[n * 4 + 3]) = (cave.X, cave.Y, cave.Width, cave.Height);
        }

        tag["caves"] = packed;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        caves.Clear();
        int[] packed = tag.GetIntArray("caves");

        for (int n = 0; n + 3 < packed.Length; n += 4)
            caves.Add(new Rectangle(packed[n], packed[n + 1], packed[n + 2], packed[n + 3]));
    }
}

/// <summary>Bats roost in nitre caves: they spawn there far more often, and spawns come faster.</summary>
public sealed class NitreCaveSpawns : GlobalNPC
{
    private const float CaveBatWeight = 1.5f;
    private const float GiantBatWeight = 0.8f;

    private const float SpawnRateMultiplier = 0.6f;
    private const int ExtraSpawns = 2;

    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        Point tile = player.Center.ToTileCoordinates();
        if (!NitreCaveSystem.Contains(tile.X, tile.Y))
            return;

        // NPC.SpawnNPC rolls rand.Next(spawnRate), so it must stay at least 1.
        spawnRate = Math.Max((int)(spawnRate * SpawnRateMultiplier), 1);
        maxSpawns += ExtraSpawns;
    }

    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (spawnInfo.Water || spawnInfo.Invasion || spawnInfo.Sky || !NitreCaveSystem.Contains(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY))
            return;

        pool[NPCID.CaveBat] = pool.TryGetValue(NPCID.CaveBat, out float bat) ? bat + CaveBatWeight : CaveBatWeight;

        if (Main.hardMode)
            pool[NPCID.GiantBat] = pool.TryGetValue(NPCID.GiantBat, out float giant) ? giant + GiantBatWeight : GiantBatWeight;
    }
}
