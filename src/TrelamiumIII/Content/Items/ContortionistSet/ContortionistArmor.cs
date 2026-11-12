using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Content.Items.ContortionistSet;

[AutoloadEquip(EquipType.Head)]
public class ContortionistsHat : ModItem
{
    public override void SetDefaults()
    {
        Item.defense = 4;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(copper: 42);
        Item.width = Item.height = 30;
    }

    public override void UpdateEquip(Player player)
    {
        base.UpdateEquip(player);

        var chemPlayer = player.GetModPlayer<ChemicalPlayerStats>();
        chemPlayer[ChemicalDamageType.Necrotic].Damage += 0.07f;
    }

    public override bool IsArmorSet(Item head, Item body, Item legs) => body.type == ModContent.ItemType<ContortionistsRobe>();

    public override void UpdateArmorSet(Player player)
    {
        player.setBonus = LangHelper.LocalizationValue(TrelamiumIII.Instance, "SetBonuses.Contortionist");
        var chemPlayer = player.GetModPlayer<ChemicalPlayerStats>();
        chemPlayer[ChemicalDamageType.Necrotic].AfflictionRate += 0.22f;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ModContent.ItemType<TwistedFungus>(), 5)
        .AddIngredient(ItemID.Silk, 8)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

[AutoloadEquip(EquipType.Body)]
public class ContortionistsRobe : ModItem
{
    public override void Load()
    {
        if (Main.netMode == NetmodeID.Server)
            return;
        
        EquipLoader.AddEquipTexture(Mod, $"{Texture}_{EquipType.Legs}", EquipType.Legs, this);
    }
    public override void SetDefaults()
    {
        Item.defense = 6;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(copper: 48);
        Item.width = Item.height = 30;
    }

    public override void UpdateEquip(Player player)
    {
        base.UpdateEquip(player);

        var chemPlayer = player.GetModPlayer<ChemicalPlayerStats>();
        chemPlayer[ChemicalDamageType.Necrotic].ReactionChance += 4f;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ModContent.ItemType<TwistedFungus>(), 8)
        .AddIngredient(ItemID.Silk, 16)
        .AddTile(TileID.Anvils)
        .Register();
    }

    public override void SetMatch(bool male, ref int equipSlot, ref bool robes)
    {
        robes = true;

        equipSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Legs);
    }
}