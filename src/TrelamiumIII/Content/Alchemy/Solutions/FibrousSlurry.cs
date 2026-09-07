namespace TrelamiumIII.Content.Alchemy.Solutions;

[LegacyName("FibrousSlurry")]
public class FibrousSlurryItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.White;
        Item.maxStack = Item.CommonMaxStack;
    }    
}