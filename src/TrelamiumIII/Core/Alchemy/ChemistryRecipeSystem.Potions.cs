using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Solutions;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Combat buff potions.</summary>
    private static void RegisterCombatPotions()
    {
        int magnolia = ModContent.ItemType<MagnoliaSolutionItem>();

        AlchemyCore.Result(ItemID.IronskinPotion)
            .From(ItemID.BottledWater, new BrewRecipe(Iron, ItemID.Daybloom) { Potency = 1 })
            .From(SolventID.AquaInertis, new BrewRecipe(Iron, ItemID.Daybloom) { Potency = 3, DurationTicks = Time(8) })
            .From(SolventID.AquaFortis,
                new BrewRecipe(ItemID.Daybloom) { Potency = 2, DurationTicks = Time(8) },
                new BrewRecipe(Iron, ItemID.Daybloom) { Potency = 4, DurationTicks = Time(6) })
            .From(magnolia,
                new BrewRecipe(ItemID.DaybloomSeeds, Iron) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) })
            .Augment(
                new BrewRecipe(ComponentMatcher.Of(SoluteID.BoneAsh, minPotency: 5))
                {
                    Augment = b => b.Add(BuffID.Wrath, potency: 1, durationTicks: Time(secs: 25)),
                },
                new BrewRecipe(ComponentMatcher.Of(ItemID.IronOre, minPotency: 2, maxPotency: 4))
                {
                    Augment = b => b.Add(BuffID.Endurance, potency: b.Level + 1, durationTicks: Time(2)),
                },
                new BrewRecipe(ComponentMatcher.Of(ItemID.GoldOre, minPotency: 3))
                {
                    Augment = b => b.Add(BuffID.Endurance, potency: 3, durationTicks: Time(1)),
                });

        AlchemyCore.Result(ItemID.SwiftnessPotion)
            .From([ItemID.BottledWater, SolventID.AquaVivida], new BrewRecipe(ItemID.Blinkroot, ItemID.Cactus) { Potency = 1 })
            .From(SolventID.AquaInertis,
                new BrewRecipe(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2, StrengthPercent = 115 },
                new BrewRecipe(ItemID.Blinkroot, ItemID.Feather) { Potency = 2 },
                new BrewRecipe(ItemID.Blinkroot, ItemID.Cactus, ItemID.Feather) { Potency = 3 })
            .From(SolventID.AquaVivida,
                new BrewRecipe(ItemID.Feather) { Potency = 1 },
                new BrewRecipe(ItemID.Blinkroot, ItemID.Feather) { Potency = 3 },
                new BrewRecipe(ItemID.Cactus, ItemID.Blinkroot, ItemID.Feather) { OutputStack = 3, Potency = 5 },
                new BrewRecipe(ItemID.Sunflower) { OutputStack = 2, Potency = 1, StrengthPercent = 120, DurationTicks = Time(7) },
                new BrewRecipe(ItemID.Sunflower, ItemID.Feather) { OutputStack = 2, Potency = 3, StrengthPercent = 120, DurationTicks = Time(6) },
                new BrewRecipe(ItemID.Sunflower, ItemID.Blinkroot, ItemID.Feather)
                {
                    OutputStack = 5, Potency = 5, StrengthPercent = 115, DurationTicks = Time(4),
                })
            .From(magnolia,
                new BrewRecipe(ItemID.BlinkrootSeeds, ItemID.Cactus) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });

        AlchemyCore.Result(ItemID.RegenerationPotion)
            .From(ItemID.BottledWater, new BrewRecipe(ItemID.Daybloom, Mushroom) { Potency = 1 })
            .From(SolventID.AquaInertis,
                new BrewRecipe(ItemID.Daybloom, Mushroom) { Potency = 2 },
                new BrewRecipe(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { Potency = 3 },
                new BrewRecipe(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 3, StrengthPercent = 130 })
            .From(SolventID.AquaVitae,
                new BrewRecipe(ItemID.Daybloom, ItemID.Mushroom) { Potency = 3 },
                new BrewRecipe(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { OutputStack = 2, Potency = 3 },
                new BrewRecipe(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 4, StrengthPercent = 125, DurationTicks = Time(7) })
            .From(magnolia,
                new BrewRecipe(ItemID.DaybloomSeeds, ItemID.Mushroom) { OutputStack = 4, Potency = 2, StrengthPercent = 80, DurationTicks = Time(10) })
            // Up to three Fallen Stars; each one raises the added Mana Regeneration's potency and duration.
            .Augment(
                new BrewRecipe(ComponentMatcher.Of(ItemID.FallenStar, minPotency: 1, maxPotency: 3))
                {
                    Augment = b => b.Add(BuffID.ManaRegeneration, potency: b.Level, durationTicks: Time(secs: 22 * b.Level)),
                });

        AlchemyCore.Result(ItemID.EndurancePotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 1, DurationTicks = Time(4) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 2, DurationTicks = Time(5, 30) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 2, DurationTicks = Time(4, 45) })
            .From(SolventID.AquaFortis,
                new BrewRecipe(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(4) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = Time(6) },
                new BrewRecipe(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = Time(5, 30) });

        RegisterEvilDamagePotion(ItemID.WrathPotion, ItemID.Ebonkoi, ItemID.VilePowder, ItemID.VileMushroom, ItemID.EbonstoneBlock);
        RegisterEvilDamagePotion(ItemID.RagePotion, ItemID.Hemopiranha, ItemID.ViciousPowder, ItemID.ViciousMushroom, ItemID.CrimstoneBlock);

        AlchemyCore.Result(ItemID.ArcheryPotion)
            .From(ItemID.BottledWater, new BrewRecipe(ItemID.Daybloom, ItemID.Lens) { Potency = 1, DurationTicks = Time(8) });

        AlchemyCore.Result(ItemID.BattlePotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Deathweed, RottenChunkVertabrae) { Potency = 1, DurationTicks = Time(7) },
                new BrewRecipe(RottenChunkVertabrae, ItemID.DeathweedSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(9, 30) });

        AlchemyCore.Result(ItemID.AmmoReservationPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.DoubleCod, ItemID.Moonglow) { Potency = 1, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.DoubleCod, ItemID.MoonglowSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });

        AlchemyCore.Result(ItemID.HeartreachPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.CrimsonTigerfish, ItemID.Daybloom) { Potency = 1, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.CrimsonTigerfish, ItemID.DaybloomSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });

        AlchemyCore.Result(ItemID.InfernoPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.Fireblossom) { Potency = 1, DurationTicks = Time(4) },
                new BrewRecipe(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.FireblossomSeeds)
                {
                    Potency = 1, StrengthPercent = 75, DurationTicks = Time(6),
                });

        AlchemyCore.Result(ItemID.LifeforcePotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Prismite, ItemID.Moonglow, ItemID.Shiverthorn) { Potency = 1, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.Prismite, ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.Prismite, ItemID.Moonglow, ItemID.Waterleaf) { Potency = 1, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) },
                new BrewRecipe(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) },
                new BrewRecipe(ItemID.Prismite, ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });

        AlchemyCore.Result(ItemID.ThornsPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) })
            .From(SolventID.AquaFortis,
                new BrewRecipe(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 110, DurationTicks = Time(8) })
            .From(magnolia,
                new BrewRecipe(ItemID.DeathweedSeeds, ItemID.Cactus) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(12) });

        AlchemyCore.Result(ItemID.TitanPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(7) })
            .From(SolventID.AquaFortis,
                new BrewRecipe(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(8) },
                new BrewRecipe(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(6, 30) })
            .From(magnolia,
                new BrewRecipe(ItemID.Bone, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 85, DurationTicks = Time(6) },
                new BrewRecipe(ItemID.IronOre, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(5) });
    }

    /// <summary>Wrath and Rage: the same recipes with each evil biome's fish, powder, mushroom and stone.</summary>
    private static void RegisterEvilDamagePotion(int potion, int fish, int powder, int mushroom, int stone)
    {
        AlchemyCore.Result(potion)
            .From(ItemID.BottledWater,
                new BrewRecipe(fish, ItemID.Deathweed) { Potency = 1, DurationTicks = Time(4) },
                new BrewRecipe(fish, ItemID.Deathweed, powder) { Potency = 2, DurationTicks = Time(6) },
                new BrewRecipe(fish, ItemID.Deathweed, mushroom) { Potency = 1, DurationTicks = Time(5) },

                // Pre-hardmode versions.
                new BrewRecipe(stone, mushroom, ItemID.Deathweed) { Potency = 1, StrengthPercent = 50, DurationTicks = Time(4) },
                new BrewRecipe(stone, powder, ItemID.Deathweed) { Potency = 1, StrengthPercent = 60, DurationTicks = Time(4) })
            .From(SolventID.AquaFortis,
                new BrewRecipe(fish, ItemID.Deathweed) { Potency = 2, DurationTicks = Time(4) },
                new BrewRecipe(fish, ItemID.Deathweed, powder) { Potency = 3, DurationTicks = Time(5, 30) },
                new BrewRecipe(fish, ItemID.Deathweed, mushroom) { Potency = 4, DurationTicks = Time(4, 45) });
    }
}
