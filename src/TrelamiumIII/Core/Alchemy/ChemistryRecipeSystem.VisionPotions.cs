using TrelamiumIII.Content.Alchemy.Solutions;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Potions that reveal: traps, creatures, light, ore and biomes.</summary>
    private static void RegisterVisionPotions()
    {
        #region Dangersense
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.TrapsightPotion,
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 1, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 2, DurationTicks = Time(7, 30) },
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 3 });

        AlchemyCore.AddResult(SolventID.AquaSpecularis, ItemID.TrapsightPotion,
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 2, DurationTicks = Time(6) },
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 3, DurationTicks = Time(7, 30) },
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 5 });
        #endregion

        #region Hunter
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.HunterPotion,
            new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 2, DurationTicks = Time(8) },
            new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 1, DurationTicks = Time(7, 30) },
            new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 2, DurationTicks = Time(8) });

        AlchemyCore.AddResult(SolventID.AquaSpecularis, ItemID.HunterPotion,
            new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 3, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 2, DurationTicks = Time(9, 30) },
            new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 4, DurationTicks = Time(10) });
        #endregion

        #region Shine
        // Specular Tonic brews these one potency higher (see BrewFormula.SolventBonus).
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaSpecularis], ItemID.ShinePotion,
            new BrewRecipe(ItemID.Daybloom, ItemID.GlowingMushroom) { Potency = 1, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 2, DurationTicks = Time(10, 30) },
            new BrewRecipe(ItemID.Daybloom, ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Spelunker
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.SpelunkerPotion,
            new BrewRecipe(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new BrewRecipe(Gold, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 4, StrengthPercent = 115 },
            new BrewRecipe(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) });

        AlchemyCore.AddResult(SolventID.AquaSpecularis, ItemID.SpelunkerPotion,
            new BrewRecipe(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 110 },
            new BrewRecipe(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 3, StrengthPercent = 105, DurationTicks = Time(9) },
            new BrewRecipe(Gold, ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = Time(4) });
        #endregion

        #region Night Owl
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaSpecularis], ItemID.NightOwlPotion,
            new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 1, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Sunflower, ItemID.Blinkroot) { Potency = 2, DurationTicks = Time(10, 30) },
            new BrewRecipe(ItemID.Daybloom, ItemID.Sunflower, ItemID.Blinkroot) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Invisibility
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.InvisibilityPotion,
            new BrewRecipe(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 1, DurationTicks = Time(3) },
            new BrewRecipe(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 2, DurationTicks = Time(5) });

        AlchemyCore.AddResult(SolventID.AquaSpecularis, ItemID.InvisibilityPotion,
            new BrewRecipe(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 2, DurationTicks = Time(6) },
            new BrewRecipe(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 3, DurationTicks = Time(8, 30) });
        #endregion

        #region Biome Sight
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaSpecularis], ItemID.BiomeSightPotion,
            new BrewRecipe(ItemID.Blinkroot, ItemID.Moonglow, ItemID.GrassSeeds) { Potency = 1, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.Blinkroot, ItemID.Fireblossom, ItemID.GrassSeeds) { Potency = 2, DurationTicks = Time(5, 30) });
        #endregion
    }
}
