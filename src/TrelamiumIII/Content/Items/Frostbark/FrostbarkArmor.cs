using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Content.Items.Frostbark;

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
        recipe.AddRecipeGroup(RecipeGroupID.IronBar, 4);
        recipe.AddIngredient(ItemID.BorealWood, 16);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
    public override void UpdateEquip(Player player)
    {
        base.UpdateEquip(player);
        player.GetDamage(ModContent.GetInstance<ChemicalDamageClass>()) += 0.03f;
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
        var chemPlayer = player.GetModPlayer<ChemicalPlayerStats>();
        chemPlayer.ReactionChance += 0.05f;
    }
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.IceBlock, 8);
        recipe.AddRecipeGroup(RecipeGroupID.IronBar, 5);
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
        var chemPlayer = player.GetModPlayer<ChemicalPlayerStats>();
        chemPlayer.ElementDamage(ChemicalDamageType.Cryonic) += 0.04f;
    }

    public override bool IsArmorSet(Item head, Item body, Item legs)
    {
        return body.type == ModContent.ItemType<FrostbarkHauberk>() && legs.type == ModContent.ItemType<FrostbarkGreaves>();
    }

    public override void UpdateArmorSet(Player player)
    {
        player.setBonus = ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "SetBonuses.Frostbark");
        player.resistCold = true;
        player.buffImmune[BuffID.Chilled] = true;
    }
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.IceBlock, 12);
        recipe.AddRecipeGroup(RecipeGroupID.IronBar, 6);
        recipe.AddIngredient(ItemID.BorealWood, 8);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}