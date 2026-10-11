using TrelamiumIII.Content.Alchemy.Solutions;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    private static void RegisterFishingPotions()
    {
        int magnolia = ModContent.ItemType<MagnoliaSolutionItem>();

        #region Sonar
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.SonarPotion,
            new RecipeVariant(ItemID.SpecularFish, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(8, 30) });

        AlchemyRecipes.CreateRecipe(SolventID.SpecularTonic, ItemID.SonarPotion,
            new RecipeVariant(ItemID.Coral, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.SpecularFish, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(7, 30) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.SonarPotion,
            new RecipeVariant(ItemID.Coral, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11) },
            new RecipeVariant(ItemID.SpecularFish, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11, 30) });
        #endregion

        #region Fishing
        // Fortunate Tonic brews this one potency higher (see BrewFormula.SolventBonus).
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.FortunateTonic], ItemID.FishingPotion,
            new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) });

        AlchemyRecipes.CreateRecipe(SolventID.FortunateTonic, ItemID.FishingPotion,
            new RecipeVariant(ItemID.Worm, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(7, 30) },
            new RecipeVariant(ItemID.EnchantedNightcrawler, ItemID.Waterleaf) { Potency = 4, StrengthPercent = 130, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf, ItemID.Worm) { Potency = 2, StrengthPercent = 105, DurationTicks = Time(10, 30) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.FishingPotion,
            new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11) },
            new RecipeVariant(ItemID.Worm, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11, 30) },
            new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds, ItemID.Worm) { Potency = 1, StrengthPercent = 80, DurationTicks = Time(14) });
        #endregion

        #region Crate
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.CratePotion,
            new RecipeVariant(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) });

        AlchemyRecipes.CreateRecipe(SolventID.FortunateTonic, ItemID.CratePotion,
            new RecipeVariant(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf) { Potency = 1, StrengthPercent = 105, DurationTicks = Time(6, 45) },
            new RecipeVariant(ItemID.Amber, ItemID.Moonglow, Gold) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(4) },
            new RecipeVariant(ItemID.Amber, ShiverthornOrWaterleaf, ItemID.Bass) { Potency = 4, StrengthPercent = 150, DurationTicks = Time(2, 30) });
        #endregion
    }

    private static void RegisterLuckPotions()
    {
        RegisterLuckPotion(ItemID.LuckPotionLesser, ItemID.WhitePearl, minutes: 5);
        RegisterLuckPotion(ItemID.LuckPotion, ItemID.BlackPearl, minutes: 10);
        RegisterLuckPotion(ItemID.LuckPotionGreater, ItemID.PinkPearl, minutes: 15);
    }

    /// <summary>The luck tiers share recipes and differ by pearl and duration. Daybloom trades strength for a minute.</summary>
    private static void RegisterLuckPotion(int potion, int pearl, int minutes)
    {
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, potion,
            new RecipeVariant(ItemID.LadyBug, ItemID.Waterleaf, pearl) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(minutes) },
            new RecipeVariant(ItemID.LadyBug, ItemID.Daybloom, pearl) { Potency = 1, StrengthPercent = 95, DurationTicks = Time(minutes + 1) });

        AlchemyRecipes.CreateRecipe(SolventID.FortunateTonic, potion,
            new RecipeVariant(ItemID.Waterleaf, pearl) { Potency = 2, StrengthPercent = 108, DurationTicks = Time(minutes) },
            new RecipeVariant(ItemID.Daybloom, pearl) { Potency = 2, StrengthPercent = 102, DurationTicks = Time(minutes + 1) });
    }
}
