using TrelamiumIII.Content.Alchemy.Solutions;
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
        AlchemyCore.AddResult(ItemID.BottledWater, zephyr,
            new SoluteInput(cloudstalk, ItemID.Cloud, ItemID.Moonglow) { Potency = 1, DurationTicks = Time(3) },
            new SoluteInput(cloudstalk, ComponentMatcher.Of(ItemID.Cloud, 2), ItemID.Moonglow) { Potency = 2, DurationTicks = Time(5) });

        AlchemyCore.AddResult(SolventID.AquaVivida, zephyr,
            new SoluteInput(cloudstalk, ItemID.Moonglow) { Potency = 2, DurationTicks = Time(4, 30) },
            new SoluteInput(cloudstalk, ItemID.Cloud, ItemID.Moonglow) { Potency = 3, DurationTicks = Time(6) });
        #endregion

        #region Soaring
        AlchemyCore.AddResult(ItemID.BottledWater, soaring,
            new SoluteInput(cloudstalk, ItemID.SoulofFlight, ItemID.Damselfish) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(4) },
            new SoluteInput(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish) { Potency = 2, StrengthPercent = 110, DurationTicks = Time(5) },
            new SoluteInput(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish) { Potency = 3, StrengthPercent = 118, DurationTicks = Time(6) });

        AlchemyCore.AddResult(SolventID.AquaVivida, soaring,
            new SoluteInput(cloudstalk, ItemID.SoulofFlight, ItemID.Damselfish) { Potency = 2, StrengthPercent = 112, DurationTicks = Time(4) },
            new SoluteInput(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish) { Potency = 3, StrengthPercent = 115, DurationTicks = Time(5) },
            new SoluteInput(cloudstalk, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish) { Potency = 5, StrengthPercent = 120, DurationTicks = Time(6) });
        #endregion

        #region Remedy
        // Vital Tonic brews these one potency higher (see BrewFormula.SolventBonus).
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaVitae], remedy,
            new SoluteInput(ItemID.Stinger, ItemID.Waterleaf, ItemID.Blinkroot) { Potency = 2, StrengthPercent = 100, DurationTicks = Time(10) });

        AlchemyCore.AddResult(ModContent.ItemType<MagnoliaSolutionItem>(), remedy,
            new SoluteInput(ItemID.Stinger, ItemID.WaterleafSeeds, ItemID.BlinkrootSeeds) { Potency = 1, StrengthPercent = 75, DurationTicks = Time(8) });
        #endregion

        #region Quench
        AlchemyCore.AddResult([ItemID.BottledWater, SolventID.AquaVitae], quench,
            new SoluteInput(gar, ItemID.Waterleaf, ItemID.Blinkroot) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(secs: 45) },
            new SoluteInput(gar, ItemID.Waterleaf, ItemID.Moonglow) { Potency = 1, StrengthPercent = 100, DurationTicks = Time(secs: 45) });

        AlchemyCore.AddResult(SolventID.AquaVitae, quench,
            new SoluteInput(gar, ItemID.Waterleaf, Gold) { Potency = 3, StrengthPercent = 125, DurationTicks = Time(secs: 20) },
            new SoluteInput(gar, ItemID.Waterleaf, ItemID.SoulofLight) { Potency = 4, StrengthPercent = 125, DurationTicks = Time(secs: 30) });
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

        AlchemyCore.AddResult(ItemID.BottledWater, photosynthesis,
            new SoluteInput(essenceOfSunlight, bloodOrb) { Potency = 3, DurationTicks = Time(10) },
            new SoluteInput(essenceOfSunlight, ItemID.Daybloom));

        AlchemyCore.AddResult(ItemID.BottledWater, anechoicCoating,
            new SoluteInput(bloodOrb));

        AlchemyCore.AddResult(ItemID.SuperManaPotion, astralInjection,
            new SoluteInput(starblightSoot, aureusCell) { OutputStack = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, bounding,
            new SoluteInput(ItemID.PinkGel, ItemID.Moonglow));

        AlchemyCore.AddResult(ItemID.BottledWater, calcium,
            new SoluteInput(ancientBoneDust),
            new SoluteInput(ancientBoneDust, bloodOrb) { Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, ceaselessHunger,
            new SoluteInput(darkPlasma),
            new SoluteInput(darkPlasma, bloodOrb) { OutputStack = 2, Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, gravityNormalizer,
            new SoluteInput(aureusCell, bloodOrb),
            new SoluteInput(aldebaranAlewife, aureusCell) { Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, shadow,
            new SoluteInput(shadowfish, ItemID.Blinkroot),
            new SoluteInput(shadowfish, bloodOrb) { OutputStack = 2, Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, soaring,
            new SoluteInput(sunkenSailfish, ItemID.SoulofFlight),
            new SoluteInput(bloodOrb, ItemID.SoulofFlight) { OutputStack = 2, Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, sulphurskin,
            new SoluteInput(sulphurousSand, ItemID.Waterleaf),
            new SoluteInput(bloodOrb, ItemID.Waterleaf) { OutputStack = 2, Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, zen,
            new SoluteInput(purifiedGel, ItemID.Daybloom),
            new SoluteInput(ComponentMatcher.Of(bloodOrb, 5), ItemID.Fireblossom) { OutputStack = 2, Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, zerg,
            new SoluteInput(ComponentMatcher.Of(purifiedGel, 2), ComponentMatcher.Of(ItemID.Fireblossom, 2)),
            new SoluteInput(ComponentMatcher.Of(bloodOrb, 5), ComponentMatcher.Of(purifiedGel, 2)) { OutputStack = 2, Potency = 3 });

        AlchemyCore.AddResult(ItemID.BottledWater, omniscience,
            new SoluteInput(ItemID.HunterPotion, ItemID.SpelunkerPotion, ItemID.TrapsightPotion));
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
