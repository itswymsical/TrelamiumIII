using TrelamiumIII.Content.Alchemy.Reagents;

namespace TrelamiumIII.Content.Alchemy.Tiles;

/// <summary>
/// Regrows the foraged fungi on their own ground after worldgen, so the ones jobs ask for can't be picked out of a world
/// for good: fly agaric on surface grass, dewdrop bonnets on clouds, indigo caps on corrupt grass and bloodtooth on
/// crimson grass.
/// </summary>
public sealed class FungusRegrowth : GlobalTile
{
    /// <summary>One in this many random updates of a bare ground tile grows something.</summary>
    private const int GRASS_CHANCE = 1200;

    /// <summary>Clouds have far fewer open tops than grass, so they roll more often.</summary>
    private const int CLOUD_CHANCE = 150;

    /// <summary>Columns either side checked for the same fungus before growing another.</summary>
    private const int SPACING = 6;

    public override void RandomUpdate(int i, int j, int type)
    {
        switch (type)
        {
            case TileID.Grass when j < Main.worldSurface && WorldGen.genRand.NextBool(GRASS_CHANCE):
                TryGrow(i, j, ModContent.TileType<FlyAgaricTile>());
                break;

            case TileID.Cloud or TileID.RainCloud or TileID.SnowCloud when WorldGen.genRand.NextBool(CLOUD_CHANCE):
                TryGrow(i, j, ModContent.TileType<DewdropBonnetTile>());
                break;

            case TileID.CorruptGrass when WorldGen.genRand.NextBool(GRASS_CHANCE):
                TryGrow(i, j, ModContent.TileType<IndigoCapTileSmall>(), ModContent.TileType<IndigoCapTileSpores>());
                break;

            case TileID.CrimsonGrass when WorldGen.genRand.NextBool(GRASS_CHANCE):
                TryGrow(i, j, ModContent.TileType<BloodtoothFungusTileSmall>(), ModContent.TileType<BloodtoothFungusTileSpores>());
                break;
        }
    }

    /// <param name="otherForm">The fungus's other tile (its spores), grown half the time and counted toward <see cref="SPACING"/>.</param>
    private static void TryGrow(int i, int j, int growType, int otherForm = -1)
    {
        Tile ground = Main.tile[i, j];

        if (!WorldGen.InWorld(i, j - 1, SPACING + 1) || Main.tile[i, j - 1].HasTile || ground.IsHalfBlock || ground.Slope != SlopeType.Solid)
            return;

        for (int x = i - SPACING; x <= i + SPACING; x++)
        {
            for (int y = j - 3; y <= j + 1; y++)
            {
                Tile near = Main.tile[x, y];

                if (near.HasTile && (near.TileType == growType || near.TileType == otherForm))
                    return;
            }
        }

        int placed = otherForm >= 0 && WorldGen.genRand.NextBool(2) ? otherForm : growType;

        if (!WorldGen.PlaceTile(i, j - 1, placed, mute: true))
            return;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, i, j - 1);
    }
}
