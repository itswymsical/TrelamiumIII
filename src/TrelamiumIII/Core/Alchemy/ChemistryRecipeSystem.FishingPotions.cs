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
            new SoluteInput(ItemID.SpecularFish, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(8, 30) });

        AlchemyCore.AddResult(SolventID.AquaSpecularis, ItemID.SonarPotion,
            new SoluteInput(ItemID.Coral, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(8) },
            new SoluteInput(ItemID.SpecularFish, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(7, 30) });

        AlchemyCore.AddResult(magnolia, ItemID.SonarPotion,
            new SoluteInput(ItemID.Coral, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11) },
            new SoluteInput(ItemID.SpecularFish, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11, 30) });
        #endregion

        #region Fishing
        // Fortunate Tonic brews this one potency higher (see BrewFormula.SolventBonus).
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaFortunae], ItemID.FishingPotion,
            new SoluteInput(ItemID.CrispyHoneyBlock, ItemID.Waterleaf) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) });

        AlchemyCore.AddResult(SolventID.AquaFortunae, ItemID.FishingPotion,
            new SoluteInput(ItemID.Worm, ItemID.Waterleaf) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(7, 30) },
            new SoluteInput(ItemID.EnchantedNightcrawler, ItemID.Waterleaf) { Potency = 4, StrengthPercent = 130, DurationTicks = Time(5) },
            new SoluteInput(ItemID.CrispyHoneyBlock, ItemID.Waterleaf, ItemID.Worm) { Potency = 2, StrengthPercent = 105, DurationTicks = Time(10, 30) });

        AlchemyCore.AddResult(magnolia, ItemID.FishingPotion,
            new SoluteInput(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11) },
            new SoluteInput(ItemID.Worm, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(11, 30) },
            new SoluteInput(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds, ItemID.Worm) { Potency = 1, StrengthPercent = 80, DurationTicks = Time(14) });
        #endregion

        #region Crate
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.CratePotion,
            new SoluteInput(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) });

        AlchemyCore.AddResult(SolventID.AquaFortunae, ItemID.CratePotion,
            new SoluteInput(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf) { Potency = 1, StrengthPercent = 105, DurationTicks = Time(6, 45) },
            new SoluteInput(ItemID.Amber, ItemID.Moonglow, Gold) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(4) },
            new SoluteInput(ItemID.Amber, ShiverthornOrWaterleaf, ItemID.Bass) { Potency = 4, StrengthPercent = 150, DurationTicks = Time(2, 30) });
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
            new SoluteInput(ItemID.LadyBug, ItemID.Waterleaf, pearl) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(minutes) },
            new SoluteInput(ItemID.LadyBug, ItemID.Daybloom, pearl) { Potency = 1, StrengthPercent = 95, DurationTicks = Time(minutes + 1) });

        AlchemyCore.AddResult(SolventID.AquaFortunae, potion,
            new SoluteInput(ItemID.Waterleaf, pearl) { Potency = 2, StrengthPercent = 108, DurationTicks = Time(minutes) },
            new SoluteInput(ItemID.Daybloom, pearl) { Potency = 2, StrengthPercent = 102, DurationTicks = Time(minutes + 1) });
    }
}
