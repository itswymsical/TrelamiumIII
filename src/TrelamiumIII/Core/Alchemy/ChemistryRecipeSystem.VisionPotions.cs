using TrelamiumIII.Content.Items.Alchemy.Solutions;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Potions that reveal: traps, creatures, light, ore and biomes.</summary>
    private static void RegisterVisionPotions()
    {
        #region Dangersense
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.TrapsightPotion,
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 1, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 2, DurationTicks = Time(7, 30) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 3 });

        AlchemyRecipes.CreateRecipe(SolventID.SpecularTonic, ItemID.TrapsightPotion,
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 2, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 3, DurationTicks = Time(7, 30) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 5 });
        #endregion

        #region Hunter
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.HunterPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 2, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 1, DurationTicks = Time(7, 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 2, DurationTicks = Time(8) });

        AlchemyRecipes.CreateRecipe(SolventID.SpecularTonic, ItemID.HunterPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 3, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 2, DurationTicks = Time(9, 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 4, DurationTicks = Time(10) });
        #endregion

        #region Shine
        // Specular Tonic brews these one potency higher (see BrewFormula.SolventBonus).
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.SpecularTonic], ItemID.ShinePotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.GlowingMushroom) { Potency = 1, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 2, DurationTicks = Time(10, 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Spelunker
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.SpelunkerPotion,
            new RecipeVariant(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new RecipeVariant(Gold, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 4, StrengthPercent = 115 },
            new RecipeVariant(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) });

        AlchemyRecipes.CreateRecipe(SolventID.SpecularTonic, ItemID.SpelunkerPotion,
            new RecipeVariant(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 110 },
            new RecipeVariant(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 3, StrengthPercent = 105, DurationTicks = Time(9) },
            new RecipeVariant(Gold, ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = Time(4) });
        #endregion

        #region Night Owl
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.SpecularTonic], ItemID.NightOwlPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 1, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Sunflower, ItemID.Blinkroot) { Potency = 2, DurationTicks = Time(10, 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.Blinkroot) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Invisibility
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.InvisibilityPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 1, DurationTicks = Time(3) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 2, DurationTicks = Time(5) });

        AlchemyRecipes.CreateRecipe(SolventID.SpecularTonic, ItemID.InvisibilityPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 2, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 3, DurationTicks = Time(8, 30) });
        #endregion

        #region Biome Sight
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.SpecularTonic], ItemID.BiomeSightPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.GrassSeeds) { Potency = 1, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Fireblossom, ItemID.GrassSeeds) { Potency = 2, DurationTicks = Time(5, 30) });
        #endregion
    }
}
