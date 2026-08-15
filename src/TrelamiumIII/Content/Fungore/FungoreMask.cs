namespace TrelamiumIII.Content.Fungore;

[AutoloadEquip(EquipType.Head)]
public class FungoreMask : ModItem
{
    public override void SetDefaults()
    {
        Item.vanity = true;
        Item.width = Item.height = 28;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 75);
    }
}