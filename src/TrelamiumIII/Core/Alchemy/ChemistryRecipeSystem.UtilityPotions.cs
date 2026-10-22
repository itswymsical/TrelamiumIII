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
            new SoluteInput(ItemID.Daybloom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyCore.AddResult(ItemID.RestorationPotion, ItemID.RestorationPotion,
            new SoluteInput(Mushroom, ItemID.Daybloom)
            {
                Potency = 3,
                StrengthPercent = 105,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            });

        AlchemyCore.AddResult(ItemID.Bottle, ItemID.LesserHealingPotion,
            new SoluteInput(Mushroom, ItemID.Gel) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyCore.AddResult(SolventID.AquaVitae, ItemID.LesserHealingPotion,
            new SoluteInput(Mushroom) { OutputStack = 4, Potency = 1, StrengthPercent = 100 },
            new SoluteInput(Mushroom, ItemID.Gel) { OutputStack = 4, Potency = 2, StrengthPercent = 107 });

        AlchemyCore.AddResult(ItemID.LesserHealingPotion, ItemID.LesserHealingPotion,
            new SoluteInput(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            },
            new SoluteInput(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 120 });

        AlchemyCore.AddResult(ItemID.LesserHealingPotion, ItemID.HealingPotion,
            new SoluteInput(ItemID.GlowingMushroom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyCore.AddResult(ItemID.HealingPotion, ItemID.HealingPotion,
            new SoluteInput(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
            });
        #endregion

        #region Mining
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.MiningPotion,
            new SoluteInput(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new SoluteInput(Silver, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105 },
            new SoluteInput(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) },
            new SoluteInput(ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.MiningPotion,
            new SoluteInput(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 111, DurationTicks = Time(10) },
            new SoluteInput(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
            new SoluteInput(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 4, StrengthPercent = 124, DurationTicks = Time(8, 30) },
            new SoluteInput(Silver, ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = Time(4) },
            new SoluteInput(Silver, ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 5, StrengthPercent = 175, DurationTicks = Time(4, 35) });

        AlchemyCore.AddResult(magnolia, ItemID.MiningPotion,
            new SoluteInput(ItemID.BlinkrootSeeds, Silver) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });
        #endregion

        #region Harvest
        AlchemyCore.AddResult(SolventID.AquaFortunae, ModContent.ItemType<HarvestPotionItem>(),
            new SoluteInput(ItemID.SiltBlock, ItemID.PlatinumOre) { Potency = 2 },
            new SoluteInput(ItemID.SiltBlock, ItemID.PlatinumOre, ItemID.Amber) { Potency = 4 });
        #endregion

        #region Builder
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.BuilderPotion,
            new SoluteInput(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(45) },
            new SoluteInput(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 1, StrengthPercent = 125, DurationTicks = Time(45) },
            new SoluteInput(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 1, StrengthPercent = 135, DurationTicks = Time(40) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.BuilderPotion,
            new SoluteInput(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 2, StrengthPercent = 150, DurationTicks = Time(45) },
            new SoluteInput(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 5, StrengthPercent = 225, DurationTicks = Time(8) },
            new SoluteInput(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 4, StrengthPercent = 150, DurationTicks = Time(30) });

        AlchemyCore.AddResult(magnolia, ItemID.BuilderPotion,
            new SoluteInput(ItemID.BlinkrootSeeds, ItemID.TinOre, ItemID.MoonglowSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(60) },
            new SoluteInput(ItemID.BlinkrootSeeds, lodestone, ItemID.MoonglowSeeds) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(60) });
        #endregion

        #region Gills
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.GillsPotion,
            new SoluteInput(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 105, DurationTicks = Time(4, 30) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Coral, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(6) });

        AlchemyCore.AddResult(magnolia, ItemID.GillsPotion,
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) },
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) });
        #endregion

        #region Flipper
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.FlipperPotion,
            new SoluteInput(ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) });

        AlchemyCore.AddResult(magnolia, ItemID.FlipperPotion,
            new SoluteInput(ItemID.ShiverthornSeeds, ItemID.WaterleafSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.FlipperPotion,
            new SoluteInput(ItemID.Shiverthorn, ItemID.Waterleaf) { Potency = 3, DurationTicks = Time(12) },
            new SoluteInput(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
            new SoluteInput(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Water Walking
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.WaterWalkingPotion,
            new SoluteInput(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) });

        AlchemyCore.AddResult(magnolia, ItemID.WaterWalkingPotion,
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.SharkFin) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) },
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.WaterWalkingPotion,
            new SoluteInput(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 3, DurationTicks = Time(14) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(12) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(12) });
        #endregion

        #region Calming
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.CalmingPotion,
            new SoluteInput(ItemID.Damselfish, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(12) },
            new SoluteInput(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
            new SoluteInput(ItemID.Waterleaf, ItemID.Daybloom, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) });

        AlchemyCore.AddResult(magnolia, ItemID.CalmingPotion,
            new SoluteInput(ItemID.Damselfish, ItemID.DaybloomSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
            new SoluteInput(ItemID.Damselfish, ItemID.DaybloomSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
            new SoluteInput(ItemID.WaterleafSeeds, ItemID.DaybloomSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) });

        AlchemyCore.AddResult(SolventID.AquaHarmonia, ItemID.CalmingPotion,
            new SoluteInput(ItemID.Damselfish, ItemID.Daybloom) { Potency = 3, DurationTicks = Time(12) },
            new SoluteInput(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
            new SoluteInput(ItemID.Damselfish, ItemID.Daybloom, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });
        #endregion

        #region Featherfall
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.FeatherfallPotion,
            new SoluteInput(ItemID.Blinkroot, ItemID.Feather, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(10) });

        AlchemyCore.AddResult(magnolia, ItemID.FeatherfallPotion,
            new SoluteInput(ItemID.Feather, ItemID.DaybloomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(7) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.FeatherfallPotion,
            new SoluteInput(ItemID.Feather, ItemID.Daybloom, ItemID.Blinkroot) { Potency = 3, DurationTicks = Time(12) });
        #endregion

        #region Warmth
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.WarmthPotion,
            new SoluteInput(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(15) },
            new SoluteInput(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(20) });

        AlchemyCore.AddResult(magnolia, ItemID.WarmthPotion,
            new SoluteInput(ItemID.FrostMinnow, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(10) });

        AlchemyCore.AddResult(SolventID.AquaVitae, ItemID.WarmthPotion,
            new SoluteInput(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 3, DurationTicks = Time(18) },
            new SoluteInput(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(22) });
        #endregion

        #region Obsidian Skin
        // Harmonious Tonic brews these one potency higher (see BrewFormula.SolventBonus).
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaHarmonia], ItemID.ObsidianSkinPotion,
            new SoluteInput(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(6) },
            new SoluteInput(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4, 45) });

        AlchemyCore.AddResult(magnolia, ItemID.ObsidianSkinPotion,
            new SoluteInput(ItemID.Obsidian, ItemID.WaterleafSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(5) },
            new SoluteInput(ItemID.Obsidian, ItemID.ShiverthornSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(4, 30) });

        AlchemyCore.AddResult(SolventID.BottledOil, ItemID.ObsidianSkinPotion,
            new SoluteInput(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(12) },
            new SoluteInput(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10, 30) });
        #endregion

        #region Gravitation
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.GravitationPotion,
            new SoluteInput(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(3) },
            new SoluteInput(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) },
            new SoluteInput(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(4) });

        AlchemyCore.AddResult(magnolia, ItemID.GravitationPotion,
            new SoluteInput(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
            new SoluteInput(ItemID.Feather, ItemID.FireblossomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
            new SoluteInput(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.FireblossomSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.GravitationPotion,
            new SoluteInput(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(5) },
            new SoluteInput(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(6) },
            new SoluteInput(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) });
        #endregion
    }
}
