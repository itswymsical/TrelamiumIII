using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Core.Alchemy.Brewing;

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
            new RecipeVariant(ComponentMatcher.Of(SoluteID.BoneAsh, minPotency: 5))
            {
                Augment = b => b.Add(BuffID.Wrath, potency: 1, durationTicks: Time(secs: 25)),
            },
            new RecipeVariant(ComponentMatcher.Of(ItemID.IronOre, minPotency: 2, maxPotency: 4))
            {
                Augment = b => b.Add(BuffID.Endurance, potency: b.Level + 1, durationTicks: Time(2)),
            },
            new RecipeVariant(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 3))
            {
                Augment = b => b.Add(BuffID.Endurance, potency: 3, durationTicks: Time(1)),
            });

        // Up to three Fallen Stars; each one raises the added Mana Regeneration's potency and duration.
        AlchemyRecipes.CreateRecipe(ItemID.RegenerationPotion, ItemID.RegenerationPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.FallenStar, minPotency: 1, maxPotency: 3))
            {
                Augment = b => b.Add(BuffID.ManaRegeneration, potency: b.Level, durationTicks: Time(secs: 22 * b.Level)),
            });

        AlchemyRecipes.CreateRecipe(ItemID.LesserHealingPotion, ItemID.LesserHealingPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 1, maxPotency: 4))
            {
                Augment = b => b.Add(VirtualBuff.Restoration, potency: b.Level),
            });

        AlchemyRecipes.CreateRecipe(ItemID.HealingPotion, ItemID.HealingPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 1, maxPotency: 4))
            {
                Augment = b => b.Add(VirtualBuff.Restoration, potency: b.Level),
            });

        AlchemyRecipes.CreateRecipe(ItemID.GreaterHealingPotion, ItemID.GreaterHealingPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 1, maxPotency: 5))
            {
                Augment = b => b.Add(VirtualBuff.Restoration, potency: b.Level),
            },
            new RecipeVariant(ComponentMatcher.Of(ItemID.PixieDust, minPotency: 2, maxPotency: 4))
            {
                Augment = b => b.Add(BuffID.Regeneration, potency: b.Level, durationTicks: Time(secs: 25 * b.Level)),
            });
    }
}
