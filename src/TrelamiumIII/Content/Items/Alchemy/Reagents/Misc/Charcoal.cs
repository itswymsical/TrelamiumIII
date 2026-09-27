using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Items.Alchemy.Reagents.Misc;

[LegacyName("Charcoal")]
public class CharcoalItem : ModItem, IAlmanacDefined
{
    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        AlmanacPotency = 1,
        Temperaments = Temperament.Kinetic,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.White;
        Item.maxStack = Item.CommonMaxStack;
    }

    public override void AddRecipes()
    {
        base.AddRecipes();
        CreateRecipe(4)
            .AddIngredient(ItemID.Wood, 10)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}