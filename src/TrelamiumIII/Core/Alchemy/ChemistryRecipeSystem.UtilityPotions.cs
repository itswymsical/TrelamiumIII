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
        AlchemyCore.AddResult(SolventID.AquaVitae, ItemID.RestorationPotion,
            new BrewRecipe(ItemID.Daybloom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyCore.AddResult(ItemID.RestorationPotion, ItemID.RestorationPotion,
            new BrewRecipe(Mushroom, ItemID.Daybloom)
            {
                Potency = 3,
                StrengthPercent = 105,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            });

        AlchemyCore.AddResult(ItemID.Bottle, ItemID.LesserHealingPotion,
            new BrewRecipe(Mushroom, ItemID.Gel) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyCore.AddResult(SolventID.AquaVitae, ItemID.LesserHealingPotion,
            new BrewRecipe(Mushroom) { OutputStack = 4, Potency = 1, StrengthPercent = 100 },
            new BrewRecipe(Mushroom, ItemID.Gel) { OutputStack = 4, Potency = 2, StrengthPercent = 107 });

        AlchemyCore.AddResult(ItemID.LesserHealingPotion, ItemID.LesserHealingPotion,
            new BrewRecipe(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            },
            new BrewRecipe(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 120 });

        AlchemyCore.AddResult(ItemID.LesserHealingPotion, ItemID.HealingPotion,
            new BrewRecipe(ItemID.GlowingMushroom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyCore.AddResult(ItemID.HealingPotion, ItemID.HealingPotion,
            new BrewRecipe(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            });
        #endregion

        #region Mining
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.MiningPotion,
            new BrewRecipe(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new BrewRecipe(Silver, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105 },
            new BrewRecipe(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) },
            new BrewRecipe(ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.MiningPotion,
            new BrewRecipe(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 111, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 4, StrengthPercent = 124, DurationTicks = Time(8, 30) },
            new BrewRecipe(Silver, ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = Time(4) },
            new BrewRecipe(Silver, ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 5, StrengthPercent = 175, DurationTicks = Time(4, 35) });

        AlchemyCore.AddResult(magnolia, ItemID.MiningPotion,
            new BrewRecipe(ItemID.BlinkrootSeeds, Silver) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });
        #endregion

        #region Harvest
        AlchemyCore.AddResult(SolventID.AquaFortunae, ModContent.ItemType<HarvestPotionItem>(),
            new BrewRecipe(ItemID.SiltBlock, ItemID.PlatinumOre) { Potency = 2 },
            new BrewRecipe(ItemID.SiltBlock, ItemID.PlatinumOre, ItemID.Amber) { Potency = 4 });
        #endregion

        #region Builder
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.BuilderPotion,
            new BrewRecipe(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(45) },
            new BrewRecipe(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 1, StrengthPercent = 125, DurationTicks = Time(45) },
            new BrewRecipe(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 1, StrengthPercent = 135, DurationTicks = Time(40) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.BuilderPotion,
            new BrewRecipe(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 2, StrengthPercent = 150, DurationTicks = Time(45) },
            new BrewRecipe(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 5, StrengthPercent = 225, DurationTicks = Time(8) },
            new BrewRecipe(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 4, StrengthPercent = 150, DurationTicks = Time(30) });

        AlchemyCore.AddResult(magnolia, ItemID.BuilderPotion,
            new BrewRecipe(ItemID.BlinkrootSeeds, ItemID.TinOre, ItemID.MoonglowSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(60) },
            new BrewRecipe(ItemID.BlinkrootSeeds, lodestone, ItemID.MoonglowSeeds) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(60) });
        #endregion

        #region Gills
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.GillsPotion,
            new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 105, DurationTicks = Time(4, 30) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Coral, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(6) });

        AlchemyCore.AddResult(magnolia, ItemID.GillsPotion,
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) });
        #endregion

        #region Flipper
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.FlipperPotion,
            new BrewRecipe(ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) });

        AlchemyCore.AddResult(magnolia, ItemID.FlipperPotion,
            new BrewRecipe(ItemID.ShiverthornSeeds, ItemID.WaterleafSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.FlipperPotion,
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Waterleaf) { Potency = 3, DurationTicks = Time(12) },
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
            new BrewRecipe(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Water Walking
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.WaterWalkingPotion,
            new BrewRecipe(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) });

        AlchemyCore.AddResult(magnolia, ItemID.WaterWalkingPotion,
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.SharkFin) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.WaterWalkingPotion,
            new BrewRecipe(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 3, DurationTicks = Time(14) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(12) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(12) });
        #endregion

        #region Calming
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.CalmingPotion,
            new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(12) },
            new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
            new BrewRecipe(ItemID.Waterleaf, ItemID.Daybloom, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) });

        AlchemyCore.AddResult(magnolia, ItemID.CalmingPotion,
            new BrewRecipe(ItemID.Damselfish, ItemID.DaybloomSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new BrewRecipe(ItemID.Damselfish, ItemID.DaybloomSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
            new BrewRecipe(ItemID.WaterleafSeeds, ItemID.DaybloomSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) });

        AlchemyCore.AddResult(SolventID.AquaHarmonia, ItemID.CalmingPotion,
            new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom) { Potency = 3, DurationTicks = Time(12) },
            new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
            new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Featherfall
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.FeatherfallPotion,
            new BrewRecipe(ItemID.Blinkroot, ItemID.Feather, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(10) });

        AlchemyCore.AddResult(magnolia, ItemID.FeatherfallPotion,
            new BrewRecipe(ItemID.Feather, ItemID.DaybloomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(7) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.FeatherfallPotion,
            new BrewRecipe(ItemID.Feather, ItemID.Daybloom, ItemID.Blinkroot) { Potency = 3, DurationTicks = Time(12) });
        #endregion

        #region Warmth
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.WarmthPotion,
            new BrewRecipe(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(15) },
            new BrewRecipe(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(20) });

        AlchemyCore.AddResult(magnolia, ItemID.WarmthPotion,
            new BrewRecipe(ItemID.FrostMinnow, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(10) });

        AlchemyCore.AddResult(SolventID.AquaVitae, ItemID.WarmthPotion,
            new BrewRecipe(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 3, DurationTicks = Time(18) },
            new BrewRecipe(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(22) });
        #endregion

        #region Obsidian Skin
        // Harmonious Tonic brews these one potency higher (see BrewFormula.SolventBonus).
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaHarmonia], ItemID.ObsidianSkinPotion,
            new BrewRecipe(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(6) },
            new BrewRecipe(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4, 45) });

        AlchemyCore.AddResult(magnolia, ItemID.ObsidianSkinPotion,
            new BrewRecipe(ItemID.Obsidian, ItemID.WaterleafSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.Obsidian, ItemID.ShiverthornSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(4, 30) });

        AlchemyCore.AddResult(SolventID.BottledOil, ItemID.ObsidianSkinPotion,
            new BrewRecipe(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(12) },
            new BrewRecipe(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10, 30) });
        #endregion

        #region Gravitation
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.GravitationPotion,
            new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(3) },
            new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(4) });

        AlchemyCore.AddResult(magnolia, ItemID.GravitationPotion,
            new BrewRecipe(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
            new BrewRecipe(ItemID.Feather, ItemID.FireblossomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
            new BrewRecipe(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.FireblossomSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.GravitationPotion,
            new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(5) },
            new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(6) },
            new BrewRecipe(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) });
        #endregion
    }
}
