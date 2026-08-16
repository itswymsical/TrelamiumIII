namespace TrelamiumIII.Content.FarmingOverhaul;

public abstract class SeedBedItem : ModItem
{
    protected abstract int PlaceTileType { get; }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(PlaceTileType);
        Item.value = Item.sellPrice(copper: 78);
    }

    public override void AddRecipes() => BuildRecipe().Register();
    protected abstract Recipe BuildRecipe();
}