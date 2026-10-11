using Terraria.WorldBuilding;

namespace TrelamiumIII.Common.Generation.Utils;

/// <summary>
///     Per-cell counts of liquid, protected and solid tiles over the world below <c>worldSurface - 60</c>, so placement
///     checks near the surface see oases and oceans.
/// </summary>
public sealed class PlacementScan
{
    private const int CellSize = 8;

    public readonly CellCountGrid Liquid = new(CellSize);

    public readonly CellCountGrid Protected = new(CellSize);

    public readonly CellCountGrid Solid = new(CellSize);

    /// <summary>Scans the world, reporting through the first <paramref name="progressShare" /> of <paramref name="progress" />.</summary>
    public PlacementScan(GenerationProgress progress, double progressShare = 0.5)
    {
        int top = Math.Max((int)Main.worldSurface - 60, 0);

        for (int x = 0; x < Main.maxTilesX; x++)
        {
            progress.Set(progressShare * x / Main.maxTilesX);

            for (int y = top; y < Main.maxTilesY; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.LiquidAmount > 0)
                    Liquid.Add(x, y);

                if (IsProtected(tile))
                    Protected.Add(x, y);
                else if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    Solid.Add(x, y);
            }
        }

        Liquid.Build();
        Protected.Build();
        Solid.Build();
    }

    /// <summary> Multi-tile objects that aren't breakable decor, tree trunks, and dungeon, temple or hive walls. </summary>
    public static bool IsProtected(Tile tile)
    {
        if (Main.wallDungeon[tile.WallType] || tile.WallType == WallID.LihzahrdBrickUnsafe || tile.WallType == WallID.HiveUnsafe)
            return true;

        if (!tile.HasTile)
            return false;

        int type = tile.TileType;
        return TileID.Sets.IsATreeTrunk[type] || (Main.tileFrameImportant[type] && !IsDecor(type));
    }

    private static bool IsDecor(int type) => type switch
    {
        TileID.Larva or TileID.LifeFruit or TileID.BeeHive => false,
        TileID.ExposedGems or TileID.WaterDrip or TileID.LavaDrip or TileID.HoneyDrip or TileID.SandDrip => true,
        _ => Main.tileCut[type] || TileID.Sets.BreakableWhenPlacing[type],
    };
}
