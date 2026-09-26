using TrelamiumIII.Core;
using TrelamiumIII.Core.Books.Lore;

namespace TrelamiumIII.Content.Lore;

public class EmancipationProclamation : ModItem
{
    public override string Texture => AssetReferences.Content.Lore.ArchaicBook1.KEY;
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 3;
    }

    public override void SetDefaults()
    {
        Item.width = Item.height = 28;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 20;
        Item.rare = ItemRarityID.Quest;
        Item.UseSound = Assets.SFX.PageFlip.Asset;
    }

    public override bool? UseItem(Player player)
    {
        BasicBookUI.Open("Misc.EProclamation");
        return true;
    }
}
