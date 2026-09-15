// Credits: Original code by Spirit Reforged (GabeHasWon)

using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Common.Globals;

/// <summary> Grows any <see cref="SaplingTile"/> the fertilizer passes over. Runs on the server, which syncs the tree. </summary>
internal class FertilizerGlobalProjectile : GlobalProjectile
{
    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.Fertilizer;

    public override void AI(Projectile projectile)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Point start = projectile.TopLeft.ToTileCoordinates();
        Point end = projectile.BottomRight.ToTileCoordinates();

        for (int x = start.X; x <= end.X; x++)
        {
            for (int y = start.Y; y <= end.Y; y++)
            {
                if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && TileLoader.GetTile(Main.tile[x, y].TileType) is SaplingTile sapling)
                    sapling.TryGrow(x, y);
            }
        }
    }
}
