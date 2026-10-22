using TrelamiumIII.Content.Alchemy.Solutions;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Combat buff potions.</summary>
    private static void RegisterCombatPotions()
    {
        int magnolia = ModContent.ItemType<MagnoliaSolutionItem>();

        #region Ironskin
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.IronskinPotion,
            new SoluteInput(Iron, ItemID.Daybloom) { Potency = 1 });

        AlchemyCore.AddResult(SolventID.AquaInertis, ItemID.IronskinPotion,
            new SoluteInput(Iron, ItemID.Daybloom) { Potency = 3, DurationTicks = Time(8) });

        AlchemyCore.AddResult(SolventID.AquaFortis, ItemID.IronskinPotion,
            new SoluteInput(ItemID.Daybloom) { Potency = 2, DurationTicks = Time(8) },
            new SoluteInput(Iron, ItemID.Daybloom) { Potency = 4, DurationTicks = Time(6) });

        AlchemyCore.AddResult(magnolia, ItemID.IronskinPotion,
            new SoluteInput(ItemID.DaybloomSeeds, Iron) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });
        #endregion

        #region Swiftness
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaVivida], ItemID.SwiftnessPotion,
            new SoluteInput(ItemID.Blinkroot, ItemID.Cactus) { Potency = 1 });

        AlchemyCore.AddResult(SolventID.AquaInertis, ItemID.SwiftnessPotion,
            new SoluteInput(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2, StrengthPercent = 115 },
            new SoluteInput(ItemID.Blinkroot, ItemID.Feather) { Potency = 2 },
            new SoluteInput(ItemID.Blinkroot, ItemID.Cactus, ItemID.Feather) { Potency = 3 });

        AlchemyCore.AddResult(SolventID.AquaVivida, ItemID.SwiftnessPotion,
            new SoluteInput(ItemID.Feather) { Potency = 1 },
            new SoluteInput(ItemID.Blinkroot, ItemID.Feather) { Potency = 3 },
            new SoluteInput(ItemID.Cactus, ItemID.Blinkroot, ItemID.Feather) { OutputStack = 3, Potency = 5 },
            new SoluteInput(ItemID.Sunflower) { OutputStack = 2, Potency = 1, StrengthPercent = 120, DurationTicks = Time(7) },
            new SoluteInput(ItemID.Sunflower, ItemID.Feather) { OutputStack = 2, Potency = 3, StrengthPercent = 120, DurationTicks = Time(6) },
            new SoluteInput(ItemID.Sunflower, ItemID.Blinkroot, ItemID.Feather)
            {
                OutputStack = 5, Potency = 5, StrengthPercent = 115, DurationTicks = Time(4),
            });

        AlchemyCore.AddResult(magnolia, ItemID.SwiftnessPotion,
            new SoluteInput(ItemID.BlinkrootSeeds, ItemID.Cactus) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });
        #endregion

        #region Regeneration
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.RegenerationPotion,
            new SoluteInput(ItemID.Daybloom, Mushroom) { Potency = 1 });

        AlchemyCore.AddResult(SolventID.AquaInertis, ItemID.RegenerationPotion,
            new SoluteInput(ItemID.Daybloom, Mushroom) { Potency = 2 },
            new SoluteInput(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { Potency = 3 },
            new SoluteInput(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 3, StrengthPercent = 130 });

        AlchemyCore.AddResult(SolventID.AquaVitae, ItemID.RegenerationPotion,
            new SoluteInput(ItemID.Daybloom, ItemID.Mushroom) { Potency = 3 },
            new SoluteInput(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { OutputStack = 2, Potency = 3 },
            new SoluteInput(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 4, StrengthPercent = 125, DurationTicks = Time(7) });

        AlchemyCore.AddResult(magnolia, ItemID.RegenerationPotion,
            new SoluteInput(ItemID.DaybloomSeeds, ItemID.Mushroom) { OutputStack = 4, Potency = 2, StrengthPercent = 80, DurationTicks = Time(10) });
        #endregion

        #region Endurance
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.EndurancePotion,
            new SoluteInput(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 1, DurationTicks = Time(4) },
            new SoluteInput(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 2, DurationTicks = Time(5, 30) },
            new SoluteInput(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 2, DurationTicks = Time(4, 45) });

        AlchemyCore.AddResult(SolventID.AquaFortis, ItemID.EndurancePotion,
            new SoluteInput(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(4) },
            new SoluteInput(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = Time(6) },
            new SoluteInput(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = Time(5, 30) });
        #endregion

        #region Wrath & Rage
        RegisterEvilDamagePotion(ItemID.WrathPotion, ItemID.Ebonkoi, ItemID.VilePowder, ItemID.VileMushroom, ItemID.EbonstoneBlock);
        RegisterEvilDamagePotion(ItemID.RagePotion, ItemID.Hemopiranha, ItemID.ViciousPowder, ItemID.ViciousMushroom, ItemID.CrimstoneBlock);
        #endregion

        #region Archery
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.ArcheryPotion,
            new SoluteInput(ItemID.Daybloom, ItemID.Lens) { Potency = 1, DurationTicks = Time(8) });
        #endregion

        #region Battle
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.BattlePotion,
            new SoluteInput(ItemID.Deathweed, RottenChunkVertabrae) { Potency = 1, DurationTicks = Time(7) },
            new SoluteInput(RottenChunkVertabrae, ItemID.DeathweedSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(9, 30) });
        #endregion

        #region Ammo Reservation
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.AmmoReservationPotion,
            new SoluteInput(ItemID.DoubleCod, ItemID.Moonglow) { Potency = 1, DurationTicks = Time(8) },
            new SoluteInput(ItemID.DoubleCod, ItemID.MoonglowSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });
        #endregion

        #region Heartreach
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.HeartreachPotion,
            new SoluteInput(ItemID.CrimsonTigerfish, ItemID.Daybloom) { Potency = 1, DurationTicks = Time(8) },
            new SoluteInput(ItemID.CrimsonTigerfish, ItemID.DaybloomSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });
        #endregion

        #region Inferno
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.InfernoPotion,
            new SoluteInput(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.Fireblossom) { Potency = 1, DurationTicks = Time(4) },
            new SoluteInput(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.FireblossomSeeds)
            {
                Potency = 1, StrengthPercent = 75, DurationTicks = Time(6),
            });
        #endregion

        #region Lifeforce
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.LifeforcePotion,
            new SoluteInput(ItemID.Prismite, ItemID.Moonglow, ItemID.Shiverthorn) { Potency = 1, DurationTicks = Time(8) },
            new SoluteInput(ItemID.Prismite, ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, DurationTicks = Time(8) },
            new SoluteInput(ItemID.Prismite, ItemID.Moonglow, ItemID.Waterleaf) { Potency = 1, DurationTicks = Time(8) },
            new SoluteInput(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) },
            new SoluteInput(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) },
            new SoluteInput(ItemID.Prismite, ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });
        #endregion

        #region Thorns
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.ThornsPotion,
            new SoluteInput(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) });

        AlchemyCore.AddResult(SolventID.AquaFortis, ItemID.ThornsPotion,
            new SoluteInput(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 110, DurationTicks = Time(8) });

        AlchemyCore.AddResult(magnolia, ItemID.ThornsPotion,
            new SoluteInput(ItemID.DeathweedSeeds, ItemID.Cactus) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(12) });
        #endregion

        #region Titan
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.TitanPotion,
            new SoluteInput(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) },
            new SoluteInput(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(7) });

        AlchemyCore.AddResult(SolventID.AquaFortis, ItemID.TitanPotion,
            new SoluteInput(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(8) },
            new SoluteInput(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(6, 30) });

        AlchemyCore.AddResult(magnolia, ItemID.TitanPotion,
            new SoluteInput(ItemID.Bone, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 85, DurationTicks = Time(6) },
            new SoluteInput(ItemID.IronOre, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(5) });
        #endregion
    }

    /// <summary>Wrath and Rage: the same recipes with each evil biome's fish, powder, mushroom and stone.</summary>
    private static void RegisterEvilDamagePotion(int potion, int fish, int powder, int mushroom, int stone)
    {
        AlchemyCore.AddResult(ItemID.BottledWater, potion,
            new SoluteInput(fish, ItemID.Deathweed) { Potency = 1, DurationTicks = Time(4) },
            new SoluteInput(fish, ItemID.Deathweed, powder) { Potency = 2, DurationTicks = Time(6) },
            new SoluteInput(fish, ItemID.Deathweed, mushroom) { Potency = 1, DurationTicks = Time(5) },

            // Pre-hardmode versions.
            new SoluteInput(stone, mushroom, ItemID.Deathweed) { Potency = 1, StrengthPercent = 50, DurationTicks = Time(4) },
            new SoluteInput(stone, powder, ItemID.Deathweed) { Potency = 1, StrengthPercent = 60, DurationTicks = Time(4) });

        AlchemyCore.AddResult(SolventID.AquaFortis, potion,
            new SoluteInput(fish, ItemID.Deathweed) { Potency = 2, DurationTicks = Time(4) },
            new SoluteInput(fish, ItemID.Deathweed, powder) { Potency = 3, DurationTicks = Time(5, 30) },
            new SoluteInput(fish, ItemID.Deathweed, mushroom) { Potency = 4, DurationTicks = Time(4, 45) });
    }
}
