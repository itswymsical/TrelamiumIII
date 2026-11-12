using TrelamiumIII.Core.Books;

namespace TrelamiumIII.Content.Books;

public class IlluminanceBookItem : ModItem
{
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
        Item.UseSound = Assets.SFX.UI.PageFlip.Asset;
    }

    public override bool? UseItem(Player player)
    {
        BasicBookUI.Open("IlluminanceSeries.IlluminanceI");
        return true;
    }
}
