namespace TrelamiumIII.Content.Alchemy.Solutions;

[LegacyName("BloodtoothExtract")]
public class BloodtoothExtractItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.value = Item.sellPrice(copper: 40);
        Item.rare = ItemRarityID.Blue;
        Item.maxStack = Item.CommonMaxStack;
    }
}
