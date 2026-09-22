
namespace TrelamiumIII.Content.Frostbark;

[AutoloadEquip(EquipType.Legs)]
public class FrostbarkGreaves : ModItem
{
    public override void SetDefaults()
    {
        Item.defense = 1;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 30);
        Item.width = Item.height = 34;
    }
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddRecipeGroup(RecipeGroups.IronBar, 4);
        recipe.AddIngredient(ItemID.BorealWood, 16);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}

[AutoloadEquip(EquipType.Body)]
public class FrostbarkHauberk : ModItem
{
    public override void SetDefaults()
    {
        Item.defense = 3;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 45);
        Item.width = Item.height = 34;
    }
    public override void UpdateEquip(Player player)
    {
        base.UpdateEquip(player);
        player.GetDamage(DamageClass.Melee) += 0.05f;
    }
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.IceBlock, 8);
        recipe.AddRecipeGroup(RecipeGroups.IronBar, 5);
        recipe.AddIngredient(ItemID.BorealWood, 16);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}

[AutoloadEquip(EquipType.Head)]
public class FrostbarkHelm : ModItem
{
    public override void SetDefaults()
    {
        Item.defense = 3;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 35);
        Item.width = Item.height = 34;
    }

    public override void UpdateEquip(Player player)
    {
        base.UpdateEquip(player);
        player.GetCritChance(DamageClass.Melee) += 0.03f;

    }

    public override bool IsArmorSet(Item head, Item body, Item legs)
    {
        return body.type == ModContent.ItemType<FrostbarkHauberk>() && legs.type == ModContent.ItemType<FrostbarkGreaves>();
    }

    public override void UpdateArmorSet(Player player)
    {
        // Player.setBonus is gone in 1.4.5 and tML has no modded set-bonus text yet; restore when it does:
        // "You become immune to the 'Chilled' debuff and gain resistance towards cold sources."
        player.resistCold = true;
        player.buffImmune[BuffID.Chilled] = true;
    }
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.IceBlock, 12);
        recipe.AddRecipeGroup(RecipeGroups.IronBar, 6);
        recipe.AddIngredient(ItemID.BorealWood, 8);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}