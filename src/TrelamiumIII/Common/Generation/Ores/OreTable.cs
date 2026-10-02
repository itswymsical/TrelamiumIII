using System.Collections.Generic;
using TrelamiumIII.Content.Items.Alchemy.Reagents;
using TrelamiumIII.Content.Items.Lodestone;
using TrelamiumIII.Content.Items.Rusted;
using static TrelamiumIII.Common.Generation.Ores.Depth;
using static TrelamiumIII.Common.Generation.Ores.WorldLayer;

namespace TrelamiumIII.Common.Generation.Ores;

/// <summary>
/// Every ore vein generated in place of vanilla Shinies. Both variants of every vanilla tier, and both evil ores,
/// generate in every world. Unhosted vanilla rows match Shinies' per-variant rates and ranges; hosted rows add
/// biome veins on top.
/// </summary>
public static class OreTable
{
    private static readonly ushort[] StoneTiles = [TileID.Stone];
    private static readonly ushort[] DesertTiles = [TileID.Sand, TileID.HardenedSand, TileID.Sandstone];

    public static List<OreVein> Build()
    {
        var veins = new List<OreVein>();

        foreach (ushort ore in new[] { TileID.Copper, TileID.Tin })
        {
            veins.Add(new(ore, At(SurfaceLow), At(SurfaceHigh), 60f, (3, 6), (2, 6)));
            veins.Add(new(ore, At(SurfaceHigh), At(RockHigh), 80f, (3, 7), (3, 7)));
            veins.Add(new(ore, At(RockLow), At(Bottom), 200f, (4, 9), (4, 8)));
            veins.Add(new(ore, At(Surface), At(RockHigh), 20f, (4, 7), (4, 7), DesertTiles));
        }

        foreach (ushort ore in new[] { TileID.Iron, TileID.Lead })
        {
            veins.Add(new(ore, At(SurfaceLow), At(SurfaceHigh), 30f, (3, 7), (2, 5)));
            veins.Add(new(ore, At(SurfaceHigh), At(RockHigh), 80f, (3, 6), (3, 6)));
            veins.Add(new(ore, At(RockLow), At(Bottom), 200f, (4, 9), (4, 8)));
            veins.Add(new(ore, At(Surface), At(Underworld), 25f, (4, 8), (4, 8), [TileID.Mud]));
        }

        ushort rustedIron = (ushort)ModContent.TileType<RustedIronOre>();
        veins.Add(new(rustedIron, Between(SurfaceLow, SurfaceHigh, 0.6f), Between(RockLow, Rock, 0.85f), 80f, (4, 7), (6, 9), StoneTiles, Clustering: 0.5f));

        foreach (ushort ore in new[] { TileID.Silver, TileID.Tungsten })
        {
            veins.Add(new(ore, At(Top), At(SurfaceLow), 170f, (4, 9), (4, 8)));
            veins.Add(new(ore, At(SurfaceHigh), At(RockHigh), 26f, (3, 6), (3, 6)));
            veins.Add(new(ore, At(RockLow), At(Bottom), 150f, (4, 9), (4, 8)));
            veins.Add(new(ore, At(Surface), At(Underworld), 15f, (4, 8), (4, 8), [TileID.IceBlock]));
        }

        veins.Add(new(TileID.Silver, At(Surface), At(Underworld), 25f, (4, 8), (4, 8), [TileID.Marble]));
        veins.Add(new(TileID.Tungsten, At(Surface), At(Underworld), 25f, (4, 8), (4, 8), [TileID.Granite]));

        foreach (ushort ore in new[] { TileID.Gold, TileID.Platinum })
        {
            veins.Add(new(ore, At(Top), At(SurfaceLow, -20), 120f, (4, 8), (4, 8)));
            veins.Add(new(ore, At(RockLow), At(Bottom), 120f, (4, 8), (4, 8)));
        }

        veins.Add(new(TileID.Gold, At(Rock), At(Underworld), 15f, (4, 8), (4, 8), [TileID.Granite, .. DesertTiles]));
        veins.Add(new(TileID.Platinum, At(Rock), At(Underworld), 15f, (4, 8), (4, 8), [TileID.Marble, TileID.SnowBlock, TileID.IceBlock]));

        foreach (ushort ore in new[] { TileID.Demonite, TileID.Crimtane })
            veins.Add(new(ore, At(Rock), At(Bottom), 22.5f, (3, 6), (4, 8)));

        ushort lodestone = (ushort)ModContent.TileType<LodestoneTile>();
        veins.Add(new(lodestone, At(SurfaceHigh), Between(Rock, Underworld, 0.6f), 90f, (5, 8), (6, 10), StoneTiles, Clustering: 0.6f));
        veins.Add(new(lodestone, At(Surface), At(Underworld), 30f, (6, 9), (7, 10), [TileID.Granite, TileID.Marble]));

        ushort sulfur = (ushort)ModContent.TileType<SulfurTile>();
        veins.Add(new(sulfur, Between(Rock, Underworld, 0.2f), Between(Rock, Underworld, 0.85f), 80f, (4, 7), (6, 9), StoneTiles, Clustering: 0.5f));

        ushort rockSalt = (ushort)ModContent.TileType<RockSaltTile>();
        veins.Add(new(rockSalt, Between(SurfaceLow, RockLow, 0.17f), Between(SurfaceLow, RockLow, 0.85f), 80f, (3, 5), (4, 7), StoneTiles, Clustering: 0.45f));

        return veins;
    }
}
