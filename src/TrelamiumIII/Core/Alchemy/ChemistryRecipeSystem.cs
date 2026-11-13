using System.Collections.Generic;
using TrelamiumIII.Common.Globals;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Items.Ammo;
using TrelamiumIII.Content.Items.ContortionistSet;
using TrelamiumIII.Content.Items.FarmingOverhaul;
using TrelamiumIII.Content.Items.Lodestone;
using TrelamiumIII.Content.Items.Rusted;
using TrelamiumIII.Content.Items.Weapons;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Registers recipes and recipe groups, including the vanilla potion recipe changes. Alchemy recipes are split by section
/// across the <c>ChemistryRecipeSystem.*.cs</c> partials, one <see cref="AlchemyCore.Result"/> chain per output;
/// <see cref="BrewFormula" /> derives what a variant leaves unset.
/// </summary>
public partial class ChemistryRecipeSystem : ModSystem
{
    public override void PostSetupContent()
    {
        base.PostSetupContent();

        AlmanacDefinitions.Register();

        RegisterSolvents();
        RegisterSolutes();
        RegisterSolutions();
        RegisterBuckets();
        RegisterMisc();
        RegisterWeapons();

        if (ModLoader.HasMod("SpiritReforged"))
            RegisterSpiritReforged();

        //if (ModLoader.HasMod("CalamityMod"))
        //    RegisterCalamity();

        // After every recipe, so each potion's ingredients are known.
        AlchemyCore.AddPotencySteps();

        AlchemyRecipeGroups.Populate();
    }

    public override void Unload()
    {
        base.Unload();

        AlchemyCore.Clear();
        AlchemyIngredients.Clear();
        AlmanacEntryRegistry.Clear();
        AlchemyRecipeGroups.Clear();
        EffectBehaviors.ClearRuntime();   // cross-mod entries hold another mod's types
        QuenchHook.Unhook();              // ...and this one lives on another mod's static event
        BrewModifiers.ClearEffects();     // BrewEffects are this session's mod types
    }

    /// <summary>A brew duration in ticks.</summary>
    private static int Time(int mins = 0, int secs = 0) => TimeConversion.SetTime(mins: mins, secs: secs);

    #region Registry

    private static readonly int[] simpleSolvents = [ItemID.BottledWater, SolventID.AquaInertis, SolventID.AquaSalis];
    /// <summary>Tonics and the other brewed solvents. Bottled Water and Inert Solution brew the same ones.</summary>
    private static void RegisterSolvents()
    {
        #region basic Solvents
        AlchemyCore.Result(SolventID.AquaInertis)
            .From(ItemID.BottledWater, new BrewRecipe(ComponentMatcher.Of(Mushroom, min: 1)));

        AlchemyCore.Result(SolventID.AquaSalis)
            .From(ItemID.BottledWater, new BrewRecipe(ComponentMatcher.Of(AnyMineralSalt, min: 1)));

        AlchemyCore.Result(ModContent.ItemType<AquaTerrae>()) // also a weapon.
            .From(ItemID.BottledWater, new BrewRecipe(Dirts));

        AlchemyCore.Result(SolventID.BottledOil)
            .From(ItemID.Bottle, new BrewRecipe(SoluteID.Pitch));

        AlchemyCore.Result(ModContent.ItemType<SunflowerOilBottleItem>())
            .From(ItemID.Bottle, new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<SunflowerSeeds>(), 2)));
        #endregion

        #region Principle solvents/tonics
        AlchemyCore.Result(SolventID.AquaHarmonia)
            .From(simpleSolvents,
                new BrewRecipe(ItemID.CopperOre),
                new BrewRecipe(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyCore.Result(SolventID.AquaVivida)
            .From(simpleSolvents, new BrewRecipe(ItemID.TinOre));

        AlchemyCore.Result(SolventID.AquaVitae)
            .From(simpleSolvents, new BrewRecipe(ItemID.GoldOre));

        AlchemyCore.Result(SolventID.AquaSpecularis)
            .From(simpleSolvents,
                new BrewRecipe(ItemID.SilverOre),
                new BrewRecipe(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyCore.Result(SolventID.AquaFortis)
            .From(simpleSolvents, new BrewRecipe(ItemID.IronOre, SoluteID.Niter));

        AlchemyCore.Result(SolventID.AquaFortunae)
            .From(simpleSolvents,
                new BrewRecipe(ItemID.PlatinumOre) { OutputStack = 3 },
                new BrewRecipe(ItemID.TinOre));

        AlchemyCore.Result(SolventID.AquaPestis)
            .From(simpleSolvents,
                new BrewRecipe(AnyEvilOre),
                new BrewRecipe(ItemID.LeadOre),
                new BrewRecipe(RottenChunkVertabrae));
        #endregion

        #region special solvents
        AlchemyCore.Result(ModContent.ItemType<MagnoliaSolutionItem>())
            .From(simpleSolvents,
                new BrewRecipe(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
                new BrewRecipe(ModContent.ItemType<MagnoliaSeedsItem>()),
                new BrewRecipe(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        AlchemyCore.Result(SolventID.BloodtoothExtract)
            .From(simpleSolvents, new BrewRecipe(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyCore.Result(SolventID.AquaRegia) // gold eater
            .From(SolventID.AquaFortis, new BrewRecipe(SoluteID.SpiritOfSalt));

        AlchemyCore.Result(SolventID.OilOfVitriol)
            .From(SolventID.AquaFortis, new BrewRecipe(SoluteID.Niter, SoluteID.Pitch));
        #endregion
    }

    private static void RegisterSolutes()
    {
        #region Reagents

        AlchemyCore.Result(ModContent.ItemType<TwistedFungus>())
            .From(SolventID.AquaPestis, new BrewRecipe(SoluteID.Sulfur, ItemID.VileMushroom) { ReturnsBottle = true });

        AlchemyCore.Result(SoluteID.Niter)
            .From(SolventID.AquaFortis, new BrewRecipe(SoluteID.RockSalt, SoluteID.Potash) 
            { 
                OutputStack = 3, 
                ReturnsBottle = true 
            });

        #endregion

        #region Alkalis

        //AlchemyCore.Result(SoluteID.SlakedLime)
        //    .From(ItemID.BottledWater, new BrewRecipe(ComponentMatcher.Of(SoluteID.Quicklime, 2)) { OutputStack = 2 });

        //AlchemyCore.Result(SoluteID.CausticLye)
        //    .From(ItemID.BottledWater, new BrewRecipe(ComponentMatcher.Of(SoluteID.Potash, 2), SoluteID.Quicklime));

        AlchemyCore.Result(SoluteID.FrigorificSalt)
            .From(SolventID.AquaInertis, new BrewRecipe(SoluteID.Niter, AnyMineralSalt, ItemID.IceBlock));

        AlchemyCore.Result(SoluteID.BlueVitriol)
            .From(SolventID.AquaFortis, new BrewRecipe(ModContent.ItemType<OxidizedCopperItem>()));

        #endregion

        #region Distillates and powders

        AlchemyCore.Result(SoluteID.SpiritOfOil)
            .From(SolventID.BottledOil, new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<CharcoalItem>(), 2)));

        AlchemyCore.Result(SoluteID.Pitch)
            .From(SolventID.BottledOil, new BrewRecipe(ComponentMatcher.Of(ItemID.Cobweb, 4)) { OutputStack = 2 });

        AlchemyCore.Result(SolventID.FibrousSlurry)
            .From(ItemID.BottledWater, new BrewRecipe(ComponentMatcher.Of(ItemID.Cobweb, 3)) { OutputStack = 1 });

        AlchemyCore.Result(SoluteID.BlackPowder)
            .From(ItemID.Bottle,
                new BrewRecipe(ComponentMatcher.Of(SoluteID.Niter, 3), SoluteID.Sulfur, ModContent.ItemType<CharcoalItem>())
                {
                    OutputStack = 2,
                    ReturnsBottle = true,
                });

        AlchemyCore.Result(SoluteID.ColdFire)
            .From(SolventID.AquaInertis,
                new BrewRecipe(ComponentMatcher.Of(SoluteID.BoneAsh, 2), ModContent.ItemType<CharcoalItem>(), ItemID.SandBlock));

        #endregion
    }
    private static void RegisterBuckets()
    {
        AlchemyCore.Result(ItemID.WaterBucket)
        .From(ItemID.EmptyBucket, new BrewRecipe(ItemID.IceBlock, 5) { ReturnsBucket = true });

    }
    /// <summary>
    /// Vanilla's potion recipes, at potency 2 with a seed variant (see <see cref="HerbsOrSeeds" />), plus augmentations. Vanilla
    /// amounts become slot levels and <see cref="BrewRecipe.InputStack" />; four-ingredient recipes keep their key ingredient with
    /// any two of the herbs, since the station has three slots.
    /// </summary>
    private static void RegisterSolutions()
    {
        #region Recovery
        AlchemyCore.Result(ItemID.LesserHealingPotion)
            .From(SolventID.AquaVitae,
                new BrewRecipe(ItemID.Mushroom, ItemID.Gel) { Potency = 2 },
                new BrewRecipe(ComponentMatcher.Of(ItemID.Mushroom, 2), ItemID.Gel) { Potency = 3 })
            .From(SolventID.AquaHarmonia,
                new BrewRecipe(ItemID.Mushroom, ItemID.Gel) { Potency = 2 },
                new BrewRecipe(ComponentMatcher.Of(ItemID.Mushroom, 2), ItemID.Gel) { Potency = 3 })
            .From(ItemID.Bottle,
                new BrewRecipe(ItemID.Mushroom, ItemID.Gel) { Potency = 1 },
                new BrewRecipe(ComponentMatcher.Of(ItemID.Mushroom, 2), ItemID.Gel) { Potency = 2 })
            .Augment(ComponentMatcher.Of(ItemID.Mushroom, 2)).Amplify(+15)
            .Augment(ComponentMatcher.Of(ItemID.GoldOre, 2)).Amplify(+30)
            .Augment(ComponentMatcher.Of(ItemID.PinkGel, 2)).Buff(BuffID.Regeneration, potency: 2, durationTicks: Time(secs: 30));

        AlchemyCore.Result(ItemID.HealingPotion)
            .From(ItemID.LesserHealingPotion, new BrewRecipe(ComponentMatcher.Of(ItemID.Mushroom, 2)) { Potency = 2});

        AlchemyCore.Result(ItemID.GreaterHealingPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ComponentMatcher.Of(ItemID.PixieDust, 3), ItemID.CrystalShard) { Potency = 2, InputStack = 3, OutputStack = 3 });

        AlchemyCore.Result(ItemID.SuperHealingPotion)
            .From(ItemID.GreaterHealingPotion,
                new BrewRecipe(ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentNebula) { Potency = 2, InputStack = 4, OutputStack = 4 },
                new BrewRecipe(ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentStardust) { Potency = 2, InputStack = 4, OutputStack = 4 },
                new BrewRecipe(ItemID.FragmentSolar, ItemID.FragmentNebula, ItemID.FragmentStardust) { Potency = 2, InputStack = 4, OutputStack = 4 },
                new BrewRecipe(ItemID.FragmentVortex, ItemID.FragmentNebula, ItemID.FragmentStardust) { Potency = 2, InputStack = 4, OutputStack = 4 });

        AlchemyCore.Result(ItemID.ManaPotion)
            .From(ItemID.LesserManaPotion, new BrewRecipe(ItemID.GlowingMushroom) { Potency = 2, InputStack = 2 });

        AlchemyCore.Result(ItemID.SuperManaPotion)
            .From(ItemID.GreaterManaPotion,
                new BrewRecipe(ItemID.FallenStar, ComponentMatcher.Of(ItemID.CrystalShard, 3), ItemID.UnicornHorn)
                {
                    Potency = 2, InputStack = 15, OutputStack = 15,
                });

        AlchemyCore.Result(ItemID.RestorationPotion)
            .From(ItemID.Bottle, new BrewRecipe(ItemID.Mushroom, ItemID.GlowingMushroom, ItemID.PinkGel) { Potency = 2 });
        #endregion

        #region Attack
        AlchemyCore.Result(ItemID.AmmoReservationPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.DoubleCod, ItemID.Moonglow) { Potency = 2 }));

        AlchemyCore.Result(ItemID.ArcheryPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Daybloom, ItemID.Lens) { Potency = 2 }));

        AlchemyCore.Result(ItemID.BattlePotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Deathweed, RottenChunkVertabrae) { Potency = 2 }));

        AlchemyCore.Result(ItemID.MagicPowerPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Moonglow, ItemID.Deathweed, ItemID.FallenStar) { Potency = 2 }));

        AlchemyCore.Result(ItemID.RagePotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Hemopiranha, ItemID.Deathweed) { Potency = 2 }));

        AlchemyCore.Result(ItemID.SummoningPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.VariegatedLardfish, ItemID.Moonglow) { Potency = 2 }));

        AlchemyCore.Result(ItemID.TitanPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) { Potency = 2 }));

        AlchemyCore.Result(ItemID.WrathPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Ebonkoi, ItemID.Deathweed) { Potency = 2 }));
        #endregion

        #region Defense
        AlchemyCore.Result(ItemID.CalmingPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Damselfish, ItemID.Daybloom) { Potency = 2 }));

        AlchemyCore.Result(ItemID.EndurancePotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.ArmoredCavefish, ItemID.Blinkroot) { Potency = 2 }));

        AlchemyCore.Result(ItemID.HeartreachPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.CrimsonTigerfish, ItemID.Daybloom) { Potency = 2 }));

        AlchemyCore.Result(ItemID.InfernoPotion)
            .From(ItemID.BottledWater,
                HerbsOrSeeds(new BrewRecipe(ItemID.FlarefinKoi, ComponentMatcher.Of(ItemID.Obsidifish, 2), ItemID.Fireblossom) { Potency = 2 }));

        AlchemyCore.Result(ItemID.InvisibilityPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 2 }));

        AlchemyCore.Result(ItemID.IronskinPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(Iron, ItemID.Daybloom) { Potency = 2 }))
            .Augment(ComponentMatcher.Of(SoluteID.BoneAsh, 2)).Amplify(+15)
            .Augment(ComponentMatcher.Of(ItemID.BlinkrootSeeds, 5)).Amplify(+35)
            .Augment(ItemID.ArmoredCavefish).Buff(BuffID.Endurance, potency: 2, durationTicks: Time(secs: 30));

        AlchemyCore.Result(ItemID.LifeforcePotion)
            .From(ItemID.BottledWater, KeyAndAnyTwoHerbs(ItemID.Prismite, ItemID.Moonglow, ItemID.Shiverthorn, ItemID.Waterleaf));

        AlchemyCore.Result(ItemID.ManaRegenerationPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Moonglow, ItemID.Daybloom, ItemID.FallenStar) { Potency = 2 }));

        AlchemyCore.Result(ItemID.RegenerationPotion)
            .From(SolventID.AquaVitae, HerbsOrSeeds(new BrewRecipe(ItemID.Mushroom, ItemID.Daybloom) { Potency = 3 }))
            .From(SolventID.AquaHarmonia, HerbsOrSeeds(new BrewRecipe(ItemID.Mushroom, ItemID.Daybloom) { Potency = 2 }))
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Mushroom, ItemID.Daybloom) { Potency = 1 }))
            .Augment(ComponentMatcher.Of(ItemID.Gel, 2)).Amplify(+15)
            .Augment(ComponentMatcher.Of(ItemID.DaybloomSeeds, 5)).Amplify(+35)
            .Augment(ComponentMatcher.Of(ItemID.PinkGel, 2)).Heal(+25);

        AlchemyCore.Result(ItemID.ThornsPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Deathweed, ItemID.Cactus) { Potency = 2 }));

        AlchemyCore.Result(ItemID.WarmthPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.FrostMinnow, ItemID.Shiverthorn) { Potency = 2 }));
        #endregion

        #region Movement
        AlchemyCore.Result(ItemID.FeatherfallPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Feather) { Potency = 2 }));

        AlchemyCore.Result(ItemID.FlipperPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Shiverthorn, ItemID.Waterleaf) { Potency = 2 }));

        AlchemyCore.Result(ItemID.GillsPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 2 }));

        AlchemyCore.Result(ItemID.GravitationPotion)
            .From(ItemID.BottledWater, KeyAndAnyTwoHerbs(ItemID.Feather, ItemID.Fireblossom, ItemID.Deathweed, ItemID.Blinkroot));

        AlchemyCore.Result(ItemID.ObsidianSkinPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian) { Potency = 2 }));

        AlchemyCore.Result(ItemID.SwiftnessPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2 }));

        AlchemyCore.Result(ItemID.WaterWalkingPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Waterleaf, ItemID.SharkFin) { Potency = 2 }));
        #endregion

        #region Detection and vision
        AlchemyCore.Result(ItemID.BiomeSightPotion)
            .From(SolventID.AquaSpecularis,
                KeyAndAnyTwoHerbs(ComponentMatcher.Of(ItemID.GrassSeeds, 5), ItemID.Fireblossom, ItemID.Blinkroot, ItemID.Moonglow, potency: 2))
            .From(ItemID.BottledWater,
                KeyAndAnyTwoHerbs(ComponentMatcher.Of(ItemID.GrassSeeds, 5), ItemID.Fireblossom, ItemID.Blinkroot, ItemID.Moonglow));

        AlchemyCore.Result(ItemID.TrapsightPotion)
            .From(SolventID.AquaSpecularis, HerbsOrSeeds(new BrewRecipe(ItemID.Shiverthorn, ComponentMatcher.Of(ItemID.Cobweb, 5)) { Potency = 2 }))
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Shiverthorn, ComponentMatcher.Of(ItemID.Cobweb, 5)) { Potency = 1 }));

        AlchemyCore.Result(ItemID.HunterPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 2 }));

        AlchemyCore.Result(ItemID.NightOwlPotion)
            .From(SolventID.AquaSpecularis, HerbsOrSeeds(new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 3 }))
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 2 }));

        AlchemyCore.Result(ItemID.ShinePotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Daybloom, ItemID.GlowingMushroom) { Potency = 2 }));

        AlchemyCore.Result(ItemID.SpelunkerPotion)
            .From(SolventID.AquaSpecularis, HerbsOrSeeds(new BrewRecipe(ItemID.Moonglow, ItemID.Blinkroot, GoldOre) { Potency = 4 }))
            .From(SolventID.AquaSpecularis, new BrewRecipe(GoldOre, 2) { Potency = 2 })
            .From(SolventID.AquaInertis, HerbsOrSeeds(new BrewRecipe(ItemID.Moonglow, ItemID.Blinkroot, GoldOre) { Potency = 3 }))
             
            // Hardmode
            .From([ItemID.BottledWater, SolventID.AquaSpecularis, SolventID.AquaInertis], HerbsOrSeeds(new BrewRecipe(ItemID.Moonglow, ItemID.Blinkroot, AdamantiteOre) { Potency = 5 }))
             
            // Default
            .From([ItemID.BottledWater, SolventID.AquaInertis], HerbsOrSeeds(new BrewRecipe(ItemID.Moonglow, ItemID.Blinkroot, ItemID.PlatinumOre) { Potency = 3 }))
            .From([ItemID.BottledWater, SolventID.AquaInertis], HerbsOrSeeds(new BrewRecipe(ItemID.Blinkroot, ItemID.Moonglow, ItemID.GoldOre) { Potency = 2 }));
        #endregion

        #region Fishing
        AlchemyCore.Result(ItemID.CratePotion)
            .From(ItemID.BottledWater, KeyAndAnyTwoHerbs(ItemID.Amber, ItemID.Moonglow, ItemID.Shiverthorn, ItemID.Waterleaf));

        AlchemyCore.Result(ItemID.FishingPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.CrispyHoneyBlock, ItemID.Waterleaf) { Potency = 2 }));

        // Vanilla brews Sonar from Gills' exact ingredients, so the Specular Tonic tells them apart.
        AlchemyCore.Result(ItemID.SonarPotion)
            .From(SolventID.AquaSpecularis, HerbsOrSeeds(new BrewRecipe(ItemID.Waterleaf, ItemID.Coral) { Potency = 2 }));
        #endregion

        #region Luck
        AlchemyCore.Result(ItemID.LuckPotionLesser)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Waterleaf, ItemID.LadyBug, ItemID.WhitePearl) { Potency = 2 }));

        AlchemyCore.Result(ItemID.LuckPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Waterleaf, ItemID.LadyBug, ItemID.BlackPearl) { Potency = 2 }));

        AlchemyCore.Result(ItemID.LuckPotionGreater)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Waterleaf, ItemID.LadyBug, ItemID.PinkPearl) { Potency = 2 }));
        #endregion

        #region Other buffs
        AlchemyCore.Result(ItemID.BuilderPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { Potency = 2 }));

        AlchemyCore.Result(ItemID.MiningPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.Blinkroot, ItemID.AntlionMandible) { Potency = 2 }));
        #endregion

        #region Travel and other potions
        AlchemyCore.Result(ItemID.RecallPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.SpecularFish, ItemID.Daybloom)));

        AlchemyCore.Result(ItemID.PotionOfReturn)
            .From(ItemID.RecallPotion, new BrewRecipe(ItemID.Obsidifish));

        AlchemyCore.Result(ItemID.TeleportationPotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.ChaosFish, ItemID.Fireblossom)));

        AlchemyCore.Result(ItemID.WormholePotion)
            .From(ItemID.BottledWater, HerbsOrSeeds(new BrewRecipe(ItemID.SpecularFish, ItemID.Blinkroot) { InputStack = 3, OutputStack = 3 }));

        // Vanilla takes one of each of the seven herbs; three slots take any three.
        AlchemyCore.Result(ItemID.GenderChangePotion)
            .From(ItemID.BottledWater, new BrewRecipe(AnyHerb, AnyHerb, AnyHerb));
        #endregion
    }

    /// <summary>Miscellaneous craftable items (e.g. unholy water, love potion).</summary>
    private static void RegisterMisc()
    {
        #region Water flasks & useless potions (lol)
        AlchemyCore.Result(ItemID.UnholyWater)
            .From(ItemID.BottledWater,
                new BrewRecipe(CorruptTiles, ItemID.CorruptSeeds) { OutputStack = 4 },
                new BrewRecipe(ItemID.VileMushroom, Dirts, ItemID.Worm) { OutputStack = 4 });

        AlchemyCore.Result(ItemID.BloodWater)
            .From(ItemID.BottledWater,
                new BrewRecipe(BloodTiles, ItemID.CrimsonSeeds) { OutputStack = 4 },
                new BrewRecipe(ItemID.ViciousMushroom, Dirts, ItemID.Worm) { OutputStack = 4 });

        AlchemyCore.Result(ItemID.HolyWater)
            .From(ItemID.BottledWater, new BrewRecipe(ItemID.PixieDust, ItemID.HallowedSeeds) { OutputStack = 4 });

        AlchemyCore.Result(ItemID.LovePotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.LifeCrystal, ItemID.FallenStar) { OutputStack = 4 },
                new BrewRecipe(ItemID.PrincessFish, ItemID.Shiverthorn) { OutputStack = 8 });

        AlchemyCore.Result(ItemID.StinkPotion)
            .From(ItemID.BottledWater,
                new BrewRecipe(ItemID.Stinkfish, ItemID.Deathweed) { OutputStack = 2 },
                new BrewRecipe(ItemID.Stinkfish, ItemID.DeathweedSeeds));
        #endregion

        AlchemyCore.Result(ItemID.ViciousPowder)
            .From(ItemID.Bottle, new BrewRecipe(ItemID.ViciousMushroom) { OutputStack = 5, ReturnsBottle = true });

        AlchemyCore.Result(ItemID.VilePowder)
            .From(ItemID.Bottle, new BrewRecipe(ItemID.VileMushroom) { OutputStack = 5, ReturnsBottle = true });

        AlchemyCore.Result(ItemID.PurificationPowder)
            .From(ItemID.Bottle, new BrewRecipe(ModContent.ItemType<DewdropBonnetItem>()) { OutputStack = 5, ReturnsBottle = true });

        AlchemyCore.Result(ModContent.ItemType<MagnoliaPowderItem>())
            .From(ItemID.Bottle, new BrewRecipe(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 5, ReturnsBottle = true });
    }

    private static void RegisterWeapons()
    {
        int[] bases = [ItemID.BottledWater, SolventID.AquaInertis, SolventID.AquaSalis, ModContent.ItemType<AquaTerrae>()];

        int magnolia = ModContent.ItemType<MagnoliaItem>();
        int grassFlask = OvergrowthFlaskID.Of(VineStrain.All[0]);

        foreach (VineStrain strain in VineStrain.All)
        {
            BrewResult flask = AlchemyCore.Result(OvergrowthFlaskID.Of(strain))
                .From(bases, new BrewRecipe(strain.Seed, Dirts, magnolia) { OutputStack = 16 });

            if (!strain.IsDefault)
                flask.From(grassFlask, new BrewRecipe(strain.Seed));
        }

        #region Early game
        AlchemyCore.Result(ModContent.ItemType<ArcticFlaskItem>())
            .From(SolventID.AquaHarmonia,
                new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ItemID.CopperOre, ComponentMatcher.Of(SoluteID.Niter, 2)) { OutputStack = 16 },
                new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ComponentMatcher.Of(SoluteID.Niter, 2)) { OutputStack = 16 });

        AlchemyCore.Result(ModContent.ItemType<PrismaticStarflaskItem>())
            .From(SolventID.AquaHarmonia,
                new BrewRecipe(ComponentMatcher.Of(ItemID.FallenStar, 2), ComponentMatcher.Of(SoluteID.Niter, 1)) { OutputStack = 16 },
                new BrewRecipe(ComponentMatcher.Of(ItemID.FallenStar, 2), ComponentMatcher.Of(ItemID.Gel, 3)) { OutputStack = 16 });

        AlchemyCore.Result(ModContent.ItemType<ThermiteFlaskItem>())
            .From([SolventID.FibrousSlurry, SolventID.BottledOil],
                new BrewRecipe(ComponentMatcher.Of(BlackPowderOrRust, 1), ComponentMatcher.Of(SoluteID.Sulfur, 2), ComponentMatcher.Of(ItemID.Hellstone, 2))
                { OutputStack = 16 });

        AlchemyCore.Result(ModContent.ItemType<PhytoncideFlaskItem>())
            .From(SolventID.AquaVitae,
                new BrewRecipe(ComponentMatcher.Of(ItemID.JungleSpores, 2), ComponentMatcher.Of(SoluteID.Potash, 1)) { OutputStack = 16 },
                new BrewRecipe(ComponentMatcher.Of(ItemID.JungleSpores, 2), ComponentMatcher.Of(ItemID.Stinger, 1), ComponentMatcher.Of(SoluteID.Potash, 1))
                { OutputStack = 24 });

        AlchemyCore.Result(ModContent.ItemType<VilesporeFlaskItem>())
            .From(SolventID.AquaPestis, AnyTwoOf(ItemID.VilePowder, ItemID.Deathweed, ItemID.VileMushroom, potency: 1, stack: 16));

        AlchemyCore.Result(ModContent.ItemType<CrimsonEffusionFlaskItem>())
            .From(SolventID.AquaPestis, AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, ItemID.ViciousMushroom, potency: 1, stack: 16))
            .From(SolventID.BloodtoothExtract,
            [
                new BrewRecipe(ItemID.ViciousPowder, ItemID.Deathweed) { OutputStack = 16 },
                new BrewRecipe(ItemID.Deathweed) { OutputStack = 8 },
                .. AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, ModContent.ItemType<BloodtoothFungusItem>(), potency: 2, stack: 18),
            ]);
        #endregion

        #region Gas Canisters
        int canister = ModContent.ItemType<GasCanisterBase>();

        // Vilespore
        AlchemyCore.Result(GasCanisterAmmo.ContactVilespore)
            .From(canister,
                new BrewRecipe(ComponentMatcher.Of(ItemID.VileMushroom, 2), ComponentMatcher.Of(ItemID.Deathweed, 1), ComponentMatcher.Of(SoluteID.Niter, 1)),
                new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<VilesporeFlaskItem>(), 1)));

        AlchemyCore.Result(GasCanisterAmmo.TimedVilespore)
            .From(canister,
                new BrewRecipe(ComponentMatcher.Of(ItemID.VileMushroom, 2), ComponentMatcher.Of(ItemID.Deathweed, 1), ComponentMatcher.Of(SoluteID.Pitch, 1)));

        // Hyperborean
        AlchemyCore.Result(GasCanisterAmmo.ContactHyperborean)
            .From(canister,
                new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ComponentMatcher.Of(SoluteID.Niter, 1)),
                new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<ArcticFlaskItem>(), 1)));

        AlchemyCore.Result(GasCanisterAmmo.TimedHyperborean)
            .From(canister, new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ComponentMatcher.Of(SoluteID.Pitch, 1)));
        #endregion
    }
    #endregion

    #region Helpers
    private static readonly Dictionary<int, int> herbSeeds = new()
    {
        [ItemID.Daybloom] = ItemID.DaybloomSeeds,
        [ItemID.Moonglow] = ItemID.MoonglowSeeds,
        [ItemID.Blinkroot] = ItemID.BlinkrootSeeds,
        [ItemID.Deathweed] = ItemID.DeathweedSeeds,
        [ItemID.Waterleaf] = ItemID.WaterleafSeeds,
        [ItemID.Fireblossom] = ItemID.FireblossomSeeds,
        [ItemID.Shiverthorn] = ItemID.ShiverthornSeeds,
    };

    /// <summary>
    /// <paramref name="herbal" />, then a copy with every herb swapped for its seeds at <paramref name="potency" /> and
    /// <paramref name="strength" /> percent. A variant without herbs comes back alone.
    /// </summary>
    private static BrewRecipe[] HerbsOrSeeds(BrewRecipe herbal, int potency = 1, int strength = 50)
    {
        var seeded = new ComponentMatcher[herbal.Components.Length];
        bool swapped = false;

        for (int i = 0; i < seeded.Length; i++)
        {
            ComponentMatcher matcher = herbal.Components[i];
            if (matcher.Group == null && herbSeeds.TryGetValue(matcher.ExactType, out int seeds))
            {
                matcher = new ComponentMatcher(seeds, matcher.Infusion, matcher.MinPotency, matcher.MaxPotency);
                swapped = true;
            }
            seeded[i] = matcher;
        }

        return swapped ? [herbal, herbal with { Components = seeded, Potency = potency, StrengthPercent = strength }] : [herbal];
    }

    /// <summary>
    /// A four-ingredient vanilla recipe on three slots: <paramref name="key" /> with each pair of <paramref name="a" />,
    /// <paramref name="b" /> and <paramref name="c" />, at potency 2 with seed variants.
    /// </summary>
    private static BrewRecipe[] KeyAndAnyTwoHerbs(ComponentMatcher key, int a, int b, int c, int potency = 1) =>
    [
        .. HerbsOrSeeds(new BrewRecipe(key, a, b) { Potency = potency }),
        .. HerbsOrSeeds(new BrewRecipe(key, a, c) { Potency = potency }),
        .. HerbsOrSeeds(new BrewRecipe(key, b, c) { Potency = potency }),
    ];

    /// <summary>One variant per pair of <paramref name="a" />, <paramref name="b" /> and <paramref name="c" />, each at <paramref name="potency" />.</summary>
    private static BrewRecipe[] AnyTwoOf(int a, int b, int c, int potency, int stack) =>
    [
        new BrewRecipe(ComponentMatcher.Of(a, potency), ComponentMatcher.Of(b, potency)) { OutputStack = stack },
        new BrewRecipe(ComponentMatcher.Of(b, potency), ComponentMatcher.Of(c, potency)) { OutputStack = stack },
        new BrewRecipe(ComponentMatcher.Of(a, potency), ComponentMatcher.Of(c, potency)) { OutputStack = stack },
    ];

    /// <summary>An empty Bottle holding <paramref name="storedType" />, for recipes whose product stays in the bottle.</summary>
    private static Func<Item> BottleOf(int storedType) => () =>
    {
        var bottle = new Item();
        bottle.SetDefaults(ItemID.Bottle);
        bottle.GetGlobalItem<ContainerBottleItem>().StoredItemType = storedType;

        return bottle;
    };
    #endregion
}
