using Terraria.ObjectData;
using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Content.Ingress.Trees;

/// <summary> Grows into a small Tanglewood tree, or a medium one <see cref="MediumTreeChance"/> of the time. </summary>
public class KapokSapling : SaplingTile
{
    private const float MediumTreeChance = 0.35f;

    protected override CustomTree Tree => WorldGen.genRand.NextFloat() < MediumTreeChance
        ? ModContent.GetInstance<MediumTanglewoodTree>()
        : ModContent.GetInstance<SmallTanglewoodTree>();

    public override void PreAddObjectData() => TileObjectData.newTile.AnchorValidTiles = [ModContent.TileType<IngressGrass>()];
}
