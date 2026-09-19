using TrelamiumIII.Content.Alchemy.Reagents;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>
    /// A potion augmented into its own type keeps its brew and gains the effects added here (see
    /// <see cref="BrewFormula" />). Healing-line augmentations live with the healing potions.
    /// </summary>
    private static void RegisterAugmentations()
    {
        AlchemyRecipes.CreateRecipe(ItemID.IronskinPotion, ItemID.IronskinPotion,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.BoneAsh, 5))
            {
                Augment = b => b.Add(BuffID.Wrath, potency: 1, durationTicks: Time(secs: 25)),
            },
            new RecipeVariant(ComponentMatcher.Of(ItemID.IronOre, 3))
            {
                Augment = b => b.Add(BuffID.Endurance, potency: 1, durationTicks: Time(2)),
            },
            new RecipeVariant(ComponentMatcher.Of(ItemID.GoldOre, 3))
            {
                Augment = b => b.Add(BuffID.Endurance, potency: 3, durationTicks: Time(1)),
            });

        // Up to three Fallen Stars; each one raises the added Mana Regeneration's potency and duration.
        AlchemyRecipes.CreateRecipe(ItemID.RegenerationPotion, ItemID.RegenerationPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.FallenStar, 1, maxPotency: 3))
            {
                Augment = b => b.Add(BuffID.ManaRegeneration, potency: b.Level, durationTicks: Time(secs: 40 * b.Level)),
            });
    }
}
