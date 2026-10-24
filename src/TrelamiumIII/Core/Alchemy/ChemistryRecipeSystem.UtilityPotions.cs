using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Items.Lodestone;
using TrelamiumIII.Core.Alchemy.Brewing;
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
        AlchemyCore.Result(ItemID.RestorationPotion)
            .From(SolventID.AquaVitae, new BrewRecipe(ItemID.Daybloom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 })
            .Augment(
                new BrewRecipe(Mushroom, ItemID.Daybloom)
                {
                    Potency = 3,
                    StrengthPercent = 105,
                    Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
                });

        AlchemyCore.Result(ItemID.LesserHealingPotion)
            .From(ItemID.Bottle, new BrewRecipe(Mushroom, ItemID.Gel) { OutputStack = 2, Potency = 1, StrengthPercent = 100 })
            .From(SolventID.AquaVitae,
                new BrewRecipe(Mushroom) { OutputStack = 4, Potency = 1, StrengthPercent = 100 },
                new BrewRecipe(Mushroom, ItemID.Gel) { OutputStack = 4, Potency = 2, StrengthPercent = 107 })
            .Augment(
                new BrewRecipe(ItemID.Daybloom)
                {
                    OutputStack = 2,
                    Potency = 1,
                    StrengthPercent = 100,
                    Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
                },
                new BrewRecipe(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 120 },
                new BrewRecipe(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 1, maxPotency: 4))
                {
                    Augment = b => b.Add(VirtualBuff.Restoration, potency: b.Level),
                });

        AlchemyCore.Result(ItemID.HealingPotion)
            .From(ItemID.LesserHealingPotion, new BrewRecipe(ItemID.GlowingMushroom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 })
            .Augment(
                new BrewRecipe(ItemID.Daybloom)
                {
                    OutputStack = 2,
                    Potency = 1,
                    StrengthPercent = 100,
                    Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 20)),
                },
                new BrewRecipe(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 1, maxPotency: 4))
                {
                    Augment = b => b.Add(VirtualBuff.Restoration, potency: b.Level),
                });

        AlchemyCore.Result(ItemID.GreaterHealingPotion)
            .Augment(
                new BrewRecipe(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 1, maxPotency: 5))
                {
                    Augment = b => b.Add(VirtualBuff.Restoration, potency: b.Level),
                },
                new BrewRecipe(ComponentMatcher.Of(ItemID.PixieDust, minPotency: 2, maxPotency: 4))
                {
                    Augment = b => b.Add(BuffID.Regeneration, potency: b.Level, durationTicks: Time(secs: 25 * b.Level)),
                });
        #endregion

        AlchemyCore.Result(ItemID.MiningPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
                new BrewRecipe(Silver, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105 },
                new BrewRecipe(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = Time(12) })
            .From(SolventID.AquaVivida,
                new BrewRecipe(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 111, DurationTicks = Time(10) },
                new BrewRecipe(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 4, StrengthPercent = 124, DurationTicks = Time(8, 30) },
                new BrewRecipe(Silver, ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = Time(4) },
                new BrewRecipe(Silver, ItemID.Blinkroot, ItemID.AntlionMandible) { OutputStack = 2, Potency = 5, StrengthPercent = 175, DurationTicks = Time(4, 35) })
            .From(magnolia,
                new BrewRecipe(ItemID.BlinkrootSeeds, Silver) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });

        AlchemyCore.Result(ModContent.ItemType<HarvestPotionItem>())
            .From(SolventID.AquaFortunae,
                new BrewRecipe(ItemID.SiltBlock, ItemID.PlatinumOre) { Potency = 2 },
                new BrewRecipe(ItemID.SiltBlock, ItemID.PlatinumOre, ItemID.Amber) { Potency = 4 });

        AlchemyCore.Result(ItemID.BuilderPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(45) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 1, StrengthPercent = 125, DurationTicks = Time(45) },
                new BrewRecipe(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 1, StrengthPercent = 135, DurationTicks = Time(40) })
            .From(SolventID.AquaVivida,
                new BrewRecipe(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 2, StrengthPercent = 150, DurationTicks = Time(45) },
                new BrewRecipe(ItemID.Blinkroot, lodestone, ItemID.Moonglow) { Potency = 5, StrengthPercent = 225, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow) { Potency = 4, StrengthPercent = 150, DurationTicks = Time(30) })
            .From(magnolia,
                new BrewRecipe(ItemID.BlinkrootSeeds, ItemID.TinOre, ItemID.MoonglowSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(60) },
                new BrewRecipe(ItemID.BlinkrootSeeds, lodestone, ItemID.MoonglowSeeds) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(60) });

        AlchemyCore.Result(ItemID.GillsPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 105, DurationTicks = Time(4, 30) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Coral, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(6) })
            .From(magnolia,
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) },
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(5) });

        AlchemyCore.Result(ItemID.FlipperPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) })
            .From(magnolia,
                new BrewRecipe(ItemID.ShiverthornSeeds, ItemID.WaterleafSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) })
            .From(SolventID.AquaVivida,
                new BrewRecipe(ItemID.Shiverthorn, ItemID.Waterleaf) { Potency = 3, DurationTicks = Time(12) },
                new BrewRecipe(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
                new BrewRecipe(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });

        AlchemyCore.Result(ItemID.WaterWalkingPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Seashell) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(7) })
            .From(magnolia,
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.SharkFin) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) },
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(5) })
            .From(SolventID.AquaVivida,
                new BrewRecipe(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 3, DurationTicks = Time(14) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Seashell) { Potency = 3, DurationTicks = Time(12) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 3, DurationTicks = Time(12) });

        AlchemyCore.Result(ItemID.CalmingPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(12) },
                new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) },
                new BrewRecipe(ItemID.Waterleaf, ItemID.Daybloom, ItemID.Coral) { Potency = 1, StrengthPercent = 115, DurationTicks = Time(10) })
            .From(magnolia,
                new BrewRecipe(ItemID.Damselfish, ItemID.DaybloomSeeds) { Potency = 2, StrengthPercent = 80, DurationTicks = Time(7) },
                new BrewRecipe(ItemID.Damselfish, ItemID.DaybloomSeeds, ItemID.Seashell) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) },
                new BrewRecipe(ItemID.WaterleafSeeds, ItemID.DaybloomSeeds, ItemID.Coral) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(6) })
            .From(SolventID.AquaHarmonia,
                new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom) { Potency = 3, DurationTicks = Time(12) },
                new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell) { Potency = 3, DurationTicks = Time(15) },
                new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom, ItemID.Coral) { Potency = 3, DurationTicks = Time(15) });

        AlchemyCore.Result(ItemID.FeatherfallPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Blinkroot, ItemID.Feather, ItemID.Daybloom) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(10) })
            .From(magnolia,
                new BrewRecipe(ItemID.Feather, ItemID.DaybloomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(7) })
            .From(SolventID.AquaVivida,
                new BrewRecipe(ItemID.Feather, ItemID.Daybloom, ItemID.Blinkroot) { Potency = 3, DurationTicks = Time(12) });

        AlchemyCore.Result(ItemID.WarmthPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(15) },
                new BrewRecipe(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(20) })
            .From(magnolia,
                new BrewRecipe(ItemID.FrostMinnow, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(10) })
            .From(SolventID.AquaVitae,
                new BrewRecipe(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 3, DurationTicks = Time(18) },
                new BrewRecipe(ItemID.FrostMinnow, ItemID.Fireblossom) { Potency = 3, StrengthPercent = 100, DurationTicks = Time(22) });

        AlchemyCore.Result(ItemID.ObsidianSkinPotion)
            // Harmonious Tonic brews these one potency higher (see BrewFormula.SolventBonus).
            .From([ItemID.BottledWater, SolventID.AquaHarmonia],
                new BrewRecipe(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(6) },
                new BrewRecipe(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4, 45) })
            .From(magnolia,
                new BrewRecipe(ItemID.Obsidian, ItemID.WaterleafSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(5) },
                new BrewRecipe(ItemID.Obsidian, ItemID.ShiverthornSeeds, ItemID.FireblossomSeeds) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(4, 30) })
            .From(SolventID.BottledOil,
                new BrewRecipe(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(12) },
                new BrewRecipe(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10, 30) });

        AlchemyCore.Result(ItemID.GravitationPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(3) },
                new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) },
                new BrewRecipe(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(4) })
            .From(magnolia,
                new BrewRecipe(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
                new BrewRecipe(ItemID.Feather, ItemID.FireblossomSeeds, ItemID.BlinkrootSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) },
                new BrewRecipe(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.FireblossomSeeds) { Potency = 2, StrengthPercent = 90, DurationTicks = Time(3) })
            .From(SolventID.AquaVivida,
                new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(5) },
                new BrewRecipe(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(6) },
                new BrewRecipe(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(5) });
    }
}
