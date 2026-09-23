namespace TrelamiumIII.Content.Items.GemPendants;

[AutoloadEquip(EquipType.Neck)]
public class AmberPendantItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.accessory = true;
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Green;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.maxMinions++;
        player.maxTurrets++;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.Amber, 2)
            .AddIngredient(ItemID.FossilOre, 5)
            .AddIngredient(ItemID.Chain, 2)
            .AddTile(TileID.Anvils)
            .Register();
    }
}