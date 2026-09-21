using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab;

public sealed class StructureVoidTileItem : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<StructureVoidTile>());
        Item.rare = ItemRarityID.Quest;
    }
}

public sealed class StructureVoidWallItem : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableWall(ModContent.WallType<StructureVoidWall>());
        Item.rare = ItemRarityID.Quest;
    }
}
