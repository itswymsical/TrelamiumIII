using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Alchemy.Solutions;

internal sealed class SunflowerOilBottleItem : BottledLiquidItem
{
    private static LiquidSurface? surface;

    public SunflowerOilBottleItem()
    {
        Rare = ItemRarityID.Blue;

        AlmanacPotency = 2;
        Temperaments = Temperament.Acidic;

        Recipes = self => self.CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            .AddCondition(OilLiquids.NearSunflower)
            .Register();
    }

    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;

    protected override BottleTemplate Template => BottleTemplate.Solution;

    protected override LiquidPalette Palette => new(new Color(255, 255, 104), new Color(254, 237, 154), new Color(255, 248, 204));

    protected override LiquidSurface Surface => surface ??= OilLiquids.Instance.Sunflower.Surface! with { Speed = 0.5f, Strength = 0.85f };
}
