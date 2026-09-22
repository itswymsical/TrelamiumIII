using TrelamiumIII.Content.Items.Alchemy.Solutions;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Combat buff potions.</summary>
    private static void RegisterCombatPotions()
    {
        int magnolia = ModContent.ItemType<MagnoliaSolutionItem>();

        #region Ironskin
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.IronskinPotion,
            new RecipeVariant(Iron, ItemID.Daybloom) { Potency = 1 });

        AlchemyRecipes.CreateRecipe(SolventID.Inert, ItemID.IronskinPotion,
            new RecipeVariant(Iron, ItemID.Daybloom) { Potency = 3, DurationTicks = Time(8) });

        AlchemyRecipes.CreateRecipe(SolventID.MartialTonic, ItemID.IronskinPotion,
            new RecipeVariant(ItemID.Daybloom) { Potency = 2, DurationTicks = Time(8) },
            new RecipeVariant(Iron, ItemID.Daybloom) { Potency = 4, DurationTicks = Time(6) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.IronskinPotion,
            new RecipeVariant(ItemID.DaybloomSeeds, Iron) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });
        #endregion

        #region Swiftness
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.BriskTonic], ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 1 });

        AlchemyRecipes.CreateRecipe(SolventID.Inert, ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2, StrengthPercent = 115 },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Feather) { Potency = 2 },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus, ItemID.Feather) { Potency = 3 });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.Feather) { Potency = 1 },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Feather) { Potency = 3 },
            new RecipeVariant(ItemID.Cactus, ItemID.Blinkroot, ItemID.Feather) { OutputStack = 3, Potency = 5 },
            new RecipeVariant(ItemID.Sunflower) { OutputStack = 2, Potency = 1, StrengthPercent = 120, DurationTicks = Time(7) },
            new RecipeVariant(ItemID.Sunflower, ItemID.Feather) { OutputStack = 2, Potency = 3, StrengthPercent = 120, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.Sunflower, ItemID.Blinkroot, ItemID.Feather)
            {
                OutputStack = 5, Potency = 5, StrengthPercent = 115, DurationTicks = Time(4),
            });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.BlinkrootSeeds, ItemID.Cactus) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = Time(10) });
        #endregion

        #region Regeneration
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.Daybloom, Mushroom) { Potency = 1 });

        AlchemyRecipes.CreateRecipe(SolventID.Inert, ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.Daybloom, Mushroom) { Potency = 2 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { Potency = 3 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 3, StrengthPercent = 130 });

        AlchemyRecipes.CreateRecipe(SolventID.VitalTonic, ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Mushroom) { Potency = 3 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { OutputStack = 2, Potency = 3 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 4, StrengthPercent = 125, DurationTicks = Time(7) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.DaybloomSeeds, ItemID.Mushroom) { OutputStack = 4, Potency = 2, StrengthPercent = 80, DurationTicks = Time(10) });
        #endregion

        #region Endurance
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.EndurancePotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 1, DurationTicks = Time(4) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 2, DurationTicks = Time(5, 30) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 2, DurationTicks = Time(4, 45) });

        AlchemyRecipes.CreateRecipe(SolventID.MartialTonic, ItemID.EndurancePotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(4) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = Time(5, 30) });
        #endregion

        #region Wrath & Rage
        RegisterEvilDamagePotion(ItemID.WrathPotion, ItemID.Ebonkoi, ItemID.VilePowder, ItemID.VileMushroom, ItemID.EbonstoneBlock);
        RegisterEvilDamagePotion(ItemID.RagePotion, ItemID.Hemopiranha, ItemID.ViciousPowder, ItemID.ViciousMushroom, ItemID.CrimstoneBlock);
        #endregion

        #region Archery
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.ArcheryPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Lens) { Potency = 1, DurationTicks = Time(8) });
        #endregion

        #region Battle
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.BattlePotion,
            new RecipeVariant(ItemID.Deathweed, RottenChunkVertabrae) { Potency = 1, DurationTicks = Time(7) },
            new RecipeVariant(RottenChunkVertabrae, ItemID.DeathweedSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(9, 30) });
        #endregion

        #region Ammo Reservation
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.AmmoReservationPotion,
            new RecipeVariant(ItemID.DoubleCod, ItemID.Moonglow) { Potency = 1, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.DoubleCod, ItemID.MoonglowSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });
        #endregion

        #region Heartreach
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.HeartreachPotion,
            new RecipeVariant(ItemID.CrimsonTigerfish, ItemID.Daybloom) { Potency = 1, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.CrimsonTigerfish, ItemID.DaybloomSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });
        #endregion

        #region Inferno
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.InfernoPotion,
            new RecipeVariant(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.Fireblossom) { Potency = 1, DurationTicks = Time(4) },
            new RecipeVariant(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.FireblossomSeeds)
            {
                Potency = 1, StrengthPercent = 75, DurationTicks = Time(6),
            });
        #endregion

        #region Lifeforce
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.LifeforcePotion,
            new RecipeVariant(ItemID.Prismite, ItemID.Moonglow, ItemID.Shiverthorn) { Potency = 1, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.Prismite, ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.Prismite, ItemID.Moonglow, ItemID.Waterleaf) { Potency = 1, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.WaterleafSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) },
            new RecipeVariant(ItemID.Prismite, ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(10) });
        #endregion

        #region Thorns
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.ThornsPotion,
            new RecipeVariant(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) });

        AlchemyRecipes.CreateRecipe(SolventID.MartialTonic, ItemID.ThornsPotion,
            new RecipeVariant(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 110, DurationTicks = Time(8) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.ThornsPotion,
            new RecipeVariant(ItemID.DeathweedSeeds, ItemID.Cactus) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(12) });
        #endregion

        #region Titan
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.TitanPotion,
            new RecipeVariant(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 1, StrengthPercent = 90, DurationTicks = Time(7) });

        AlchemyRecipes.CreateRecipe(SolventID.MartialTonic, ItemID.TitanPotion,
            new RecipeVariant(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(8) },
            new RecipeVariant(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(6, 30) });

        AlchemyRecipes.CreateRecipe(magnolia, ItemID.TitanPotion,
            new RecipeVariant(ItemID.Bone, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 85, DurationTicks = Time(6) },
            new RecipeVariant(ItemID.IronOre, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(5) });
        #endregion
    }

    /// <summary>Wrath and Rage: the same recipes with each evil biome's fish, powder, mushroom and stone.</summary>
    private static void RegisterEvilDamagePotion(int potion, int fish, int powder, int mushroom, int stone)
    {
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, potion,
            new RecipeVariant(fish, ItemID.Deathweed) { Potency = 1, DurationTicks = Time(4) },
            new RecipeVariant(fish, ItemID.Deathweed, powder) { Potency = 2, DurationTicks = Time(6) },
            new RecipeVariant(fish, ItemID.Deathweed, mushroom) { Potency = 1, DurationTicks = Time(5) },

            // Pre-hardmode versions.
            new RecipeVariant(stone, mushroom, ItemID.Deathweed) { Potency = 1, StrengthPercent = 50, DurationTicks = Time(4) },
            new RecipeVariant(stone, powder, ItemID.Deathweed) { Potency = 1, StrengthPercent = 60, DurationTicks = Time(4) });

        AlchemyRecipes.CreateRecipe(SolventID.MartialTonic, potion,
            new RecipeVariant(fish, ItemID.Deathweed) { Potency = 2, DurationTicks = Time(4) },
            new RecipeVariant(fish, ItemID.Deathweed, powder) { Potency = 3, DurationTicks = Time(5, 30) },
            new RecipeVariant(fish, ItemID.Deathweed, mushroom) { Potency = 4, DurationTicks = Time(4, 45) });
    }
}
