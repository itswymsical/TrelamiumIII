using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Items.Lodestone;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Restorative and utility potions.</summary>
    private static void RegisterUtilityPotions()
    {
        int magnolia = ModContent.ItemType<MagnoliaSolutionItem>();
        int lodestone = ModContent.ItemType<LodestoneItem>();

        #region Restoration & Healing
        AlchemyRecipes.CreateRecipe(SolventID.VitalTonic, ItemID.RestorationPotion,
            new RecipeVariant(ItemID.Daybloom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipes.CreateRecipe(ItemID.RestorationPotion, ItemID.RestorationPotion,
            new RecipeVariant(Mushroom, ItemID.Daybloom)
            {
                Potency = 3,
                StrengthPercent = 105,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            });

        AlchemyRecipes.CreateRecipe(ItemID.Bottle, ItemID.LesserHealingPotion,
            new RecipeVariant(Mushroom, ItemID.Gel) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipes.CreateRecipe(SolventID.VitalTonic, ItemID.LesserHealingPotion,
            new RecipeVariant(Mushroom) { OutputStack = 4, Potency = 1, StrengthPercent = 100 },
            new RecipeVariant(Mushroom, ItemID.Gel) { OutputStack = 4, Potency = 2, StrengthPercent = 107 });

        AlchemyRecipes.CreateRecipe(ItemID.LesserHealingPotion, ItemID.LesserHealingPotion,
            new RecipeVariant(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            },
            new RecipeVariant(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 120 });

        AlchemyRecipes.CreateRecipe(ItemID.LesserHealingPotion, ItemID.HealingPotion,
            new RecipeVariant(ItemID.GlowingMushroom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipes.CreateRecipe(ItemID.HealingPotion, ItemID.HealingPotion,
            new RecipeVariant(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            });
        #endregion

        #region Mining
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.MiningPotion,
            new RecipeVariant(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new RecipeVariant(Silver, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105 },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.MiningPotion,
            new RecipeVariant(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 111, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 4, StrengthPercent = 124, DurationTicks = Time(8, 30) },
            new RecipeVariant(Silver, ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = Time(4) },
            new RecipeVariant(Silver, ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 5, StrengthPercent = 175, DurationTicks = Time(4, 35) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.MiningPotion,
            new RecipeVariant(ItemID.BlinkrootSeeds, Silver) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });
        #endregion

        #region Harvest
        AlchemyRecipes.CreateRecipe(SolventID.FortunateTonic, ModContent.ItemType<HarvestPotionItem>(),
            new RecipeVariant(ItemID.SiltBlock, ItemID.PlatinumOre) { Potency = 2 },
            new RecipeVariant(ItemID.SiltBlock, ItemID.PlatinumOre, ItemID.Amber) { Potency = 4 });
        #endregion

        #region Builder
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.BuilderPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(45) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 1, StrengthPercent = 125, DurationTicks = Time(45) },
            new RecipeVariant(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 1, StrengthPercent = 135, DurationTicks = Time(40) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.BuilderPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 2, StrengthPercent = 150, DurationTicks = Time(45) },
            new RecipeVariant(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 5, StrengthPercent = 225, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 4, StrengthPercent = 150, DurationTicks = Time(30) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.BuilderPotion,
            new RecipeVariant(ItemID.BlinkrootSeeds, ItemID.TinOre, ItemID.MoonglowSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(60) },
            new RecipeVariant(ItemID.BlinkrootSeeds, lodestone, ItemID.MoonglowSeeds) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(60) });
        #endregion

        #region Gills
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.GillsPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 105, DurationTicks = Time(4, 30) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(6) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.GillsPotion,
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) });
        #endregion

        #region Flipper
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.FlipperPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.FlipperPotion,
            new RecipeVariant(ItemID.ShiverthornSeeds, ItemID.WaterleafSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.FlipperPotion,
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Waterleaf) { Potency = 3, DurationTicks = Time(12) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Water Walking
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.WaterWalkingPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.WaterWalkingPotion,
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.SharkFin) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.WaterWalkingPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 3, DurationTicks = Time(14) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(12) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(12) });
        #endregion

        #region Calming
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.CalmingPotion,
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(12) },
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Daybloom, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.CalmingPotion,
            new RecipeVariant(ItemID.Damselfish, ItemID.DaybloomSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new RecipeVariant(ItemID.Damselfish, ItemID.DaybloomSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.DaybloomSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) });

        AlchemyRecipes.CreateRecipe(SolventID.HarmoniousTonic, ItemID.CalmingPotion,
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom) { Potency = 3, DurationTicks = Time(12) },
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Featherfall
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.FeatherfallPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Feather, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(10) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.FeatherfallPotion,
            new RecipeVariant(ItemID.Feather, ItemID.DaybloomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(7) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.FeatherfallPotion,
            new RecipeVariant(ItemID.Feather, ItemID.Daybloom, ItemID.Blinkroot) { Potency = 3, DurationTicks = Time(12) });
        #endregion

        #region Warmth
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.WarmthPotion,
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(15) },
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(20) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.WarmthPotion,
            new RecipeVariant(ItemID.FrostMinnow, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(10) });

        AlchemyRecipes.CreateRecipe(SolventID.VitalTonic, ItemID.WarmthPotion,
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 3, DurationTicks = Time(18) },
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(22) });
        #endregion

        #region Obsidian Skin
        // Harmonious Tonic brews these one potency higher (see BrewFormula.SolventBonus).
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.HarmoniousTonic], ItemID.ObsidianSkinPotion,
            new RecipeVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4, 45) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.ObsidianSkinPotion,
            new RecipeVariant(ItemID.Obsidian, ItemID.WaterleafSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.Obsidian, ItemID.ShiverthornSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(4, 30) });

        AlchemyRecipes.CreateRecipe(SolventID.BottledOil, ItemID.ObsidianSkinPotion,
            new RecipeVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(12) },
            new RecipeVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10, 30) });
        #endregion

        #region Gravitation
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.GravitationPotion,
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(3) },
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(4) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.GravitationPotion,
            new RecipeVariant(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
            new RecipeVariant(ItemID.Feather, ItemID.FireblossomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
            new RecipeVariant(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.FireblossomSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.GravitationPotion,
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(5) },
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) });
        #endregion
    }
}
