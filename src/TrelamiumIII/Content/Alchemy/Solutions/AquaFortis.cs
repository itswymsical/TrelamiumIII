using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Alchemy.Solutions;

internal sealed class AquaFortis : BottledLiquidItem
{
    private static LiquidSurface? surface;

    public AquaFortis()
    {
        Rare = ItemRarityID.Blue;

        AlmanacPotency = 2;
        Temperaments = Temperament.Acidic;
        Hidden = false;

        Recipes = self => self.CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            .AddCondition(AcidicLiquids.NearNitricAcid)
            .Register();
    }

    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;

    protected override BottleTemplate Template => BottleTemplate.Solution;

    protected override LiquidPalette Palette => new(new Color(124, 79, 3), new Color(242, 203, 111), new Color(254, 232, 186));

    protected override LiquidSurface Surface => surface ??= AcidicLiquids.Instance.Surface with { Speed = 0.5f, Strength = 0.85f };
}
