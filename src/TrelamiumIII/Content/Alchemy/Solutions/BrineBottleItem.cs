using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Alchemy.Solutions;

/// <summary>Bottled brine: a water bottle carrying the brine surface's salt strands.</summary>
[LegacyName("BrineBottle")]
internal sealed class BrineBottleItem : BottledLiquidItem
{
    private static LiquidSurface? surface;

    public BrineBottleItem()
    {
        Rare = ItemRarityID.Blue;

        AlmanacPotency = 2;
        Temperaments = Temperament.Inert | Temperament.Alkaline;
        Hidden = false;

        Recipes = self => self.CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            .AddCondition(WaterConditions.NearBrine)
            .Register();
    }

    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;

    protected override BottleTemplate Template => BottleTemplate.Solution;

    protected override LiquidPalette Palette => LiquidPalette.Water;

    /// <summary>The world brine surface, slower and stronger: the bottle shows a 12-pixel patch of it.</summary>
    protected override LiquidSurface Surface => surface ??= WaterConditions.Instance.Surface with { Speed = 0.5f, Strength = 0.85f };
}
