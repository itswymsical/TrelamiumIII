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
        AlchemyRecipes.CreateRecipe(SolventID.HarmoniousTonic, ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.SwiftnessPotion));

        AlchemyRecipes.CreateRecipe(SolventID.HarmoniousTonic, ItemID.ManaRegenerationPotion,
            new RecipeVariant(ItemID.SwiftnessPotion, ItemID.FallenStar));
    }
}
