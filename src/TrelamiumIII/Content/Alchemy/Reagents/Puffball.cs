namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>Dropped by Scrunglepuffs. One of the fungi Argie's first job asks for.</summary>
public class PuffballItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 20;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(copper: 40);
        Item.rare = ItemRarityID.Blue;
    }
}
