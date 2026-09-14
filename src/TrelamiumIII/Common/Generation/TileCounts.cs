using TrelamiumIII.Content.Ingress;

namespace TrelamiumIII.Common.Generation;

internal class TileCounts : ModSystem
{
    public int ingressGrassCount;
    public int ligneousTempleCount;

    public static TileCounts Instance => ModContent.GetInstance<TileCounts>();

    public override void TileCountsAvailable(ReadOnlySpan<int> tileCounts)
    {
        ingressGrassCount = tileCounts[ModContent.TileType<IngressGrass>()];
        ligneousTempleCount = tileCounts[ModContent.TileType<BramblerootTile>()];

    }
}
