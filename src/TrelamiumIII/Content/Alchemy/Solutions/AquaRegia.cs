using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Alchemy.Solutions;

internal sealed class AquaRegia : BottledLiquidItem
{
    private static LiquidSurface? surface;

    public AquaRegia()
    {
        Rare = ItemRarityID.Blue;

        AlmanacPotency = 2;
        Temperaments = Temperament.Acidic;
        Hidden = false;

        Recipes = self => self.CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            //.AddCondition(AcidicLiquids.NearRegia)
            .Register();
    }

    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;

    protected override BottleTemplate Template => BottleTemplate.Solution;

    protected override LiquidPalette Palette => new(new Color(124, 46, 3), new Color(247, 163, 100), new Color(254, 218, 186));

    protected override LiquidSurface Surface => surface ??= WaterLiquids.Instance.Surface with { Speed = 0.9f, Strength = 0.85f };
}