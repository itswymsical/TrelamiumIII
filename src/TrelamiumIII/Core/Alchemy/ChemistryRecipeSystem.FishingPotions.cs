using TrelamiumIII.Content.Alchemy.Solutions;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    private static void RegisterFishingPotions()
    {
        int magnolia = ModContent.ItemType<MagnoliaSolutionItem>();

        #region Sonar
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.SonarPotion,
            new BrewRecipe(ItemID.SpecularFish, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(8, 30) });

        AlchemyCore.AddResult(SolventID.AquaSpecularis, ItemID.SonarPotion,
            new BrewRecipe(ItemID.Coral, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(8) },
            new BrewRecipe(ItemID.SpecularFish, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(7, 30) });

        AlchemyCore.AddResult(magnolia, ItemID.SonarPotion,
            new BrewRecipe(ItemID.Coral, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11) },
            new BrewRecipe(ItemID.SpecularFish, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11, 30) });
        #endregion

        #region Fishing
        // Fortunate Tonic brews this one potency higher (see BrewFormula.SolventBonus).
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaFortunae], ItemID.FishingPotion,
            new BrewRecipe(ItemID.CrispyHoneyBlock, ItemID.Waterleaf) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) });

        AlchemyCore.AddResult(SolventID.AquaFortunae, ItemID.FishingPotion,
            new BrewRecipe(ItemID.Worm, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(7, 30) },
            new BrewRecipe(ItemID.EnchantedNightcrawler, ItemID.Waterleaf) { Potency = 4, StrengthPercent = 130, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.CrispyHoneyBlock, ItemID.Waterleaf, ItemID.Worm) { Potency = 2, StrengthPercent = 105, DurationTicks = Time(10, 30) });

        AlchemyCore.AddResult(magnolia, ItemID.FishingPotion,
            new BrewRecipe(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11) },
            new BrewRecipe(ItemID.Worm, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11, 30) },
            new BrewRecipe(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds, ItemID.Worm) { Potency = 1, StrengthPercent = 80, DurationTicks = Time(14) });
        #endregion

        #region Crate
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.CratePotion,
            new BrewRecipe(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) });

        AlchemyCore.AddResult(SolventID.AquaFortunae, ItemID.CratePotion,
            new BrewRecipe(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf) { Potency = 1, StrengthPercent = 105, DurationTicks = Time(6, 45) },
            new BrewRecipe(ItemID.Amber, ItemID.Moonglow, Gold) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(4) },
            new BrewRecipe(ItemID.Amber, ShiverthornOrWaterleaf, ItemID.Bass) { Potency = 4, StrengthPercent = 150, DurationTicks = Time(2, 30) });
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
        AlchemyCore.AddResult(ItemID.BottledWater, potion,
            new BrewRecipe(ItemID.LadyBug, ItemID.Waterleaf, pearl) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(minutes) },
            new BrewRecipe(ItemID.LadyBug, ItemID.Daybloom, pearl) { Potency = 1, StrengthPercent = 95, DurationTicks = Time(minutes + 1) });

        AlchemyCore.AddResult(SolventID.AquaFortunae, potion,
            new BrewRecipe(ItemID.Waterleaf, pearl) { Potency = 2, StrengthPercent = 108, DurationTicks = Time(minutes) },
            new BrewRecipe(ItemID.Daybloom, pearl) { Potency = 2, StrengthPercent = 102, DurationTicks = Time(minutes + 1) });
    }
}
