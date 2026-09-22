using TrelamiumIII.Content.Items.Alchemy.Solutions;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Spirit Reforged recipes, buffs and effect behaviours.</summary>
    private static void RegisterSpiritReforged()
    {
        const string MOD = "SpiritReforged";
        bool missing = false;

        int remedy = CrossModItem(MOD, "RemedyPotion", ref missing);
        int zephyr = CrossModItem(MOD, "DoubleJumpPotion", ref missing);
        int soaring = CrossModItem(MOD, "FlightPotion", ref missing);
        int quench = CrossModItem(MOD, "QuenchPotion", ref missing);
        int gar = CrossModItem(MOD, "GarItem", ref missing);
        int cloudstalk = CrossModItem(MOD, "Cloudstalk", ref missing);

        if (missing)
            return;

        #region Effect behaviours
        // Soaring rides SpiritReforged's FlightPotionBuff, whose id only exists at runtime. Their buff adds 0.25 to
        // FlightPlayer.wingTimeMult; the brew adds its delta on top.
        if (ModContent.TryFind(MOD, "FlightPotionBuff", out ModBuff flightBuff))
        {
            EffectBehaviors.Register(flightBuff.Type,
                new CrossModStatBehavior(MOD, "FlightPlayer", "wingTimeMult", vanillaValue: 0.25f, noun: "wing flight time"));
        }

        // Quench has no accumulator to add to: their +25% buff duration is a literal inside a private
        // handler on BuffPlayer.ModifyBuffTime. QuenchHook subscribes alongside it and rescales.
        if (ModContent.TryFind(MOD, "QuenchPotion_Buff", out ModBuff quenchBuff)
            && QuenchHook.TryHook(quenchBuff.Type, vanillaBonus: 0.25f))
        {
            EffectBehaviors.Register(quenchBuff.Type, new QuenchBehavior(vanillaDurationBonus: 0.25f));
        }
        #endregion

        #region Zephyr
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, zephyr,
            new RecipeVariant(cloudstalk, ItemID.Cloud, ItemID.Moonglow) { Potency = 1, DurationTicks = Time(3) },
            new RecipeVariant(cloudstalk, ComponentMatcher.Of(ItemID.Cloud, 2), ItemID.Moonglow) { Potency = 2, DurationTicks = Time(5) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, zephyr,
            new RecipeVariant(cloudstalk, ItemID.Moonglow) { Potency = 2, DurationTicks = Time(4, 30) },
            new RecipeVariant(cloudstalk, ItemID.Cloud, ItemID.Moonglow) { Potency = 3, DurationTicks = Time(6) });
        #endregion

        #region Soaring
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, soaring,
            new RecipeVariant(cloudstalk, ItemID.SoulofFlight, ItemID.Damselfish) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) },
            new RecipeVariant(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(5) },
            new RecipeVariant(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish) { Potency = 3, StrengthPercent = 118, DurationTicks = Time(6) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, soaring,
            new RecipeVariant(cloudstalk, ItemID.SoulofFlight, ItemID.Damselfish) { Potency = 2, StrengthPercent = 112, DurationTicks = Time(4) },
            new RecipeVariant(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish) { Potency = 3, StrengthPercent = 115, DurationTicks = Time(5) },
            new RecipeVariant(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish) { Potency = 5, StrengthPercent = 120, DurationTicks = Time(6) });
        #endregion

        #region Remedy
        // Vital Tonic brews these one potency higher (see BrewFormula.SolventBonus).
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.VitalTonic], remedy,
            new RecipeVariant(ItemID.Stinger, ItemID.Waterleaf, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) });

        AlchemyRecipes.CreateRecipe(ModContent.ItemType<MagnoliaSolutionItem>(), remedy,
            new RecipeVariant(ItemID.Stinger, ItemID.WaterleafSeeds, ItemID.BlinkrootSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(8) });
        #endregion

        #region Quench
        AlchemyRecipes.CreateRecipe([ItemID.BottledWater, SolventID.VitalTonic], quench,
            new RecipeVariant(gar, ItemID.Waterleaf, ItemID.Blinkroot) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(secs: 45) },
            new RecipeVariant(gar, ItemID.Waterleaf, ItemID.Moonglow) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(secs: 45) });

        AlchemyRecipes.CreateRecipe(SolventID.VitalTonic, quench,
            new RecipeVariant(gar, ItemID.Waterleaf, Gold) { Potency = 3, StrengthPercent = 125, DurationTicks = Time(secs: 20) },
            new RecipeVariant(gar, ItemID.Waterleaf, ItemID.SoulofLight) { Potency = 4, StrengthPercent = 125, DurationTicks = Time(secs: 30) });
        #endregion
    }

    /// <summary>Calamity Mod recipes.</summary>
    private static void RegisterCalamity()
    {
        const string MOD = "CalamityMod";
        bool missing = false;

        int bloodOrb = CrossModItem(MOD, "BloodOrb", ref missing);
        int essenceOfSunlight = CrossModItem(MOD, "EssenceofSunlight", ref missing);
        int photosynthesis = CrossModItem(MOD, "PhotosynthesisPotion", ref missing);
        int anechoicCoating = CrossModItem(MOD, "AnechoicCoating", ref missing);
        int aureusCell = CrossModItem(MOD, "AureusCell", ref missing);
        int starblightSoot = CrossModItem(MOD, "StarblightSoot", ref missing);
        int astralInjection = CrossModItem(MOD, "AstralInjection", ref missing);
        int bounding = CrossModItem(MOD, "BoundingPotion", ref missing);
        int ancientBoneDust = CrossModItem(MOD, "AncientBoneDust", ref missing);
        int calcium = CrossModItem(MOD, "CalciumPotion", ref missing);
        int darkPlasma = CrossModItem(MOD, "DarkPlasma", ref missing);
        int ceaselessHunger = CrossModItem(MOD, "CeaselessHungerPotion", ref missing);
        int gravityNormalizer = CrossModItem(MOD, "GravityNormalizerPotion", ref missing);
        int aldebaranAlewife = CrossModItem(MOD, "AldebaranAlewife", ref missing);
        int shadow = CrossModItem(MOD, "ShadowPotion", ref missing);
        int shadowfish = CrossModItem(MOD, "Shadowfish", ref missing);
        int sunkenSailfish = CrossModItem(MOD, "SunkenSailfish", ref missing);
        int soaring = CrossModItem(MOD, "SoaringPotion", ref missing);
        int sulphurousSand = CrossModItem(MOD, "SulphurousSand", ref missing);
        int sulphurskin = CrossModItem(MOD, "SulphurskinPotion", ref missing);
        int purifiedGel = CrossModItem(MOD, "PurifiedGel", ref missing);
        int omniscience = CrossModItem(MOD, "PotionofOmniscience", ref missing);
        int zerg = CrossModItem(MOD, "ZergPotion", ref missing);
        int zen = CrossModItem(MOD, "ZenPotion", ref missing);

        if (missing)
            return;

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, photosynthesis,
            new RecipeVariant(essenceOfSunlight, bloodOrb) { Potency = 3, DurationTicks = Time(10) },
            new RecipeVariant(essenceOfSunlight, ItemID.Daybloom));

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, anechoicCoating,
            new RecipeVariant(bloodOrb));

        AlchemyRecipes.CreateRecipe(ItemID.SuperManaPotion, astralInjection,
            new RecipeVariant(starblightSoot, aureusCell) { OutputStack = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, bounding,
            new RecipeVariant(ItemID.PinkGel, ItemID.Moonglow));

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, calcium,
            new RecipeVariant(ancientBoneDust),
            new RecipeVariant(ancientBoneDust, bloodOrb) { Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ceaselessHunger,
            new RecipeVariant(darkPlasma),
            new RecipeVariant(darkPlasma, bloodOrb) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, gravityNormalizer,
            new RecipeVariant(aureusCell, bloodOrb),
            new RecipeVariant(aldebaranAlewife, aureusCell) { Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, shadow,
            new RecipeVariant(shadowfish, ItemID.Blinkroot),
            new RecipeVariant(shadowfish, bloodOrb) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, soaring,
            new RecipeVariant(sunkenSailfish, ItemID.SoulofFlight),
            new RecipeVariant(bloodOrb, ItemID.SoulofFlight) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, sulphurskin,
            new RecipeVariant(sulphurousSand, ItemID.Waterleaf),
            new RecipeVariant(bloodOrb, ItemID.Waterleaf) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, zen,
            new RecipeVariant(purifiedGel, ItemID.Daybloom),
            new RecipeVariant(ComponentMatcher.Of(bloodOrb, 5), ItemID.Fireblossom) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, zerg,
            new RecipeVariant(ComponentMatcher.Of(purifiedGel, 2), ComponentMatcher.Of(ItemID.Fireblossom, 2)),
            new RecipeVariant(ComponentMatcher.Of(bloodOrb, 5), ComponentMatcher.Of(purifiedGel, 2)) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, omniscience,
            new RecipeVariant(ItemID.HunterPotion, ItemID.SpelunkerPotion, ItemID.TrapsightPotion));
    }

    /// <summary>A cross-mod item's type, or 0 with a logged warning and <paramref name="missing" /> set.</summary>
    private static int CrossModItem(string mod, string name, ref bool missing)
    {
        if (ModContent.TryFind(mod, name, out ModItem item))
            return item.Type;

        TrelamiumIII.Instance.Logger.Warn($"{mod} alchemy support skipped: item {name} was not found.");
        missing = true;

        return 0;
    }
}
