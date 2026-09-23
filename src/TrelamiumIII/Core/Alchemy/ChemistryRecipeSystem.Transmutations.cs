using TrelamiumIII.Content.Items.Alchemy.Solutions;

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
        AlchemyRecipes.CreateRecipe(ItemID.SwiftnessPotion, ItemID.RegenerationPotion,
            new RecipeVariant(SolventID.HarmoniousTonic));

        AlchemyRecipes.CreateRecipe(ItemID.SwiftnessPotion, ItemID.ManaRegenerationPotion,
            new RecipeVariant(SolventID.HarmoniousTonic, ItemID.FallenStar));

        AlchemyRecipes.CreateRecipe(ItemID.RestorationPotion, ItemID.HealingPotion,
            new RecipeVariant(SolventID.VitalTonic, ItemID.Mushroom));

        AlchemyRecipes.CreateRecipe(ItemID.RestorationPotion, ItemID.HealingPotion,
            new RecipeVariant([
                ComponentMatcher.Of(SolventID.VitalTonic, minPotency: 1, maxPotency: 4), 
                ComponentMatcher.Of(ItemID.Mushroom, minPotency: 1, maxPotency: 4)
                ]));
    }
}
