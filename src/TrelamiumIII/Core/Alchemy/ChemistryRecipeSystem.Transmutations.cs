using TrelamiumIII.Content.Alchemy.Solutions;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>
    /// A solvent dissolving a finished potion in a component slot into a different one, the station's
    /// <see cref="BrewCategory.Transmutation" />. The dissolved potion's own potency carries into the output
    /// (see <see cref="BrewFormula" />), and these variants leave strength and duration to the curve.
    /// </summary>
    private static void RegisterTransmutations()
    {
        AlchemyCore.AddResult(ItemID.SwiftnessPotion, ItemID.RegenerationPotion,
            new BrewRecipe(SolventID.AquaHarmonia));

        AlchemyCore.AddResult(ItemID.SwiftnessPotion, ItemID.ManaRegenerationPotion,
            new BrewRecipe(SolventID.AquaHarmonia, ItemID.FallenStar));

        AlchemyCore.AddResult(ItemID.RestorationPotion, ItemID.HealingPotion,
            new BrewRecipe(SolventID.AquaVitae, ItemID.Mushroom));

        AlchemyCore.AddResult(ItemID.RestorationPotion, ItemID.HealingPotion,
            new BrewRecipe([
                ComponentMatcher.Of(SolventID.AquaVitae, minPotency: 1, maxPotency: 4), 
                ComponentMatcher.Of(ItemID.Mushroom, minPotency: 1, maxPotency: 4)
                ]));
    }
}
