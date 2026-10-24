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
        AlchemyCore.Result(ItemID.RegenerationPotion)
            .From(ItemID.SwiftnessPotion, new BrewRecipe(SolventID.AquaHarmonia));

        AlchemyCore.Result(ItemID.ManaRegenerationPotion)
            .From(ItemID.SwiftnessPotion, new BrewRecipe(SolventID.AquaHarmonia, ItemID.FallenStar));

        AlchemyCore.Result(ItemID.HealingPotion)
            .From(ItemID.RestorationPotion,
                new BrewRecipe(SolventID.AquaVitae, ItemID.Mushroom),
                new BrewRecipe([
                    ComponentMatcher.Of(SolventID.AquaVitae, minPotency: 1, maxPotency: 4),
                    ComponentMatcher.Of(ItemID.Mushroom, minPotency: 1, maxPotency: 4)
                    ]));
    }
}
