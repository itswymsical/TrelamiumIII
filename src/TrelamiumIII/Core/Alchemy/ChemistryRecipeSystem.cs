using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Items.ContortionistSet;
using TrelamiumIII.Content.Items.FarmingOverhaul;
using TrelamiumIII.Content.Items.Lodestone;
using TrelamiumIII.Content.Items.Rusted;
using TrelamiumIII.Content.Items.Weapons;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Registers recipes and recipe groups, including the vanilla potion recipe changes. Alchemy recipes are split by section
/// across the <c>ChemistryRecipeSystem.*.cs</c> partials; <see cref="BrewFormula" /> derives what a variant leaves unset.
/// </summary>
public partial class ChemistryRecipeSystem : ModSystem
{
    public override void PostSetupContent()
    {
        base.PostSetupContent();

        AlmanacDefinitions.Register();

        RegisterSolvents();
        RegisterSolutes();

        RegisterMisc();

        //RegisterCombatPotions();
        //RegisterUtilityPotions();
        //RegisterVisionPotions();
        //RegisterFishingPotions();
        //RegisterLuckPotions();
        //RegisterAugmentations();
        //RegisterTransmutations();

        //RegisterWeapons();

        if (ModLoader.HasMod("SpiritReforged"))
            RegisterSpiritReforged();

        //if (ModLoader.HasMod("CalamityMod"))
        //    RegisterCalamity();

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
    }

    /// <summary>A brew duration in ticks.</summary>
    private static int Time(int mins = 0, int secs = 0) => TimeConversion.SetTime(mins: mins, secs: secs);

    #region Registry
    /// <summary>Tonics and the other brewed solvents. Bottled Water and Inert Solution brew the same ones.</summary>
    private static void RegisterSolvents()
    {
        #region basic Solvents
        AlchemyCore.AddResult(
            _base: ItemID.BottledWater, 
            recipe: SolventID.AquaInertis, 
            new BrewRecipe(ComponentMatcher.Of(Mushroom, min: 1)));
        
        AlchemyCore.AddResult(
            _base: ItemID.BottledWater,
            recipe: SolventID.AquaSalis,
            new BrewRecipe(ComponentMatcher.Of(AnyMineralSalt, min: 1)));

        AlchemyCore.AddResult(
            _base: ItemID.BottledWater,
            recipe: ModContent.ItemType<AquaTerrae>(), // also a weapon.
            new BrewRecipe(Dirts));

        AlchemyCore.AddResult(
            _base: ItemID.Bottle,
            recipe: SolventID.BottledOil,
            new BrewRecipe(SoluteID.Pitch));

        AlchemyCore.AddResult(
            _base: ItemID.Bottle,
            recipe: ModContent.ItemType<SunflowerOilBottleItem>(),
            new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<SunflowerSeeds>(), 2)));
        #endregion

        int[] simpleSolvents = [ItemID.BottledWater, SolventID.AquaInertis, SolventID.AquaSalis];

        #region Principle solvents/tonics
        AlchemyCore.AddResult(simpleSolvents, SolventID.AquaHarmonia,
            new BrewRecipe(ItemID.CopperOre),
            new BrewRecipe(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyCore.AddResult(simpleSolvents, SolventID.AquaVivida,
            new BrewRecipe(ItemID.TinOre));

        AlchemyCore.AddResult(simpleSolvents, SolventID.AquaVitae,
            new BrewRecipe(ItemID.GoldOre));

        AlchemyCore.AddResult(simpleSolvents, SolventID.AquaSpecularis,
            new BrewRecipe(ItemID.SilverOre),
            new BrewRecipe(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyCore.AddResult(simpleSolvents, SolventID.AquaFortis,
            new BrewRecipe(ItemID.IronOre, SoluteID.Niter));

        AlchemyCore.AddResult(simpleSolvents, SolventID.AquaFortunae,
            new BrewRecipe(ItemID.PlatinumOre) { OutputStack = 3 },
            new BrewRecipe(ItemID.TinOre));

        AlchemyCore.AddResult(simpleSolvents, SolventID.AquaPestis,
            new BrewRecipe(AnyEvilOre),
            new BrewRecipe(ItemID.LeadOre),
            new BrewRecipe(RottenChunkVertabrae));
        #endregion

        #region special solvents
        AlchemyCore.AddResult(simpleSolvents, ModContent.ItemType<MagnoliaSolutionItem>(),
            new BrewRecipe(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new BrewRecipe(ModContent.ItemType<MagnoliaSeedsItem>()),
            new BrewRecipe(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        AlchemyCore.AddResult(simpleSolvents, SolventID.BloodtoothExtract,
            new BrewRecipe(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyCore.AddResult(SolventID.AquaFortis, SolventID.AquaRegia, // gold eater
            new BrewRecipe(SoluteID.SpiritOfSalt));

        AlchemyCore.AddResult(SolventID.AquaFortis, SolventID.OilOfVitriol,
            new BrewRecipe(SoluteID.Niter, SoluteID.Pitch));
        #endregion
    }

    private static void RegisterSolutes()
    {
        #region Reagents

        AlchemyCore.AddResult(
            _base: SolventID.AquaPestis, 
            recipe: ModContent.ItemType<TwistedFungus>(),
            new BrewRecipe(SoluteID.Sulfur, ItemID.VileMushroom) { ReturnsBottle = true });

        #endregion

        #region Alkalis

        //AlchemyCore.AddResult(ItemID.BottledWater, SoluteID.SlakedLime,
        //    new BrewRecipe(ComponentMatcher.Of(SoluteID.Quicklime, 2)) { OutputStack = 2 });

        //AlchemyCore.AddResult(ItemID.BottledWater, SoluteID.CausticLye,
        //    new BrewRecipe(ComponentMatcher.Of(SoluteID.Potash, 2), SoluteID.Quicklime));

        AlchemyCore.AddResult(SolventID.AquaInertis, SoluteID.FrigorificSalt,
            new BrewRecipe(SoluteID.Niter, SoluteID.RockSalt, ItemID.IceBlock));

        AlchemyCore.AddResult(SolventID.AquaFortis, SoluteID.BlueVitriol,
            new BrewRecipe(ModContent.ItemType<OxidizedCopperItem>()));

        #endregion

        #region Distillates and powders

        AlchemyCore.AddResult(SolventID.BottledOil, SoluteID.SpiritOfOil,
            new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<CharcoalItem>(), 2)));

        AlchemyCore.AddResult(SolventID.BottledOil, SoluteID.Pitch,
            new BrewRecipe(ComponentMatcher.Of(ItemID.Cobweb, 4)) { OutputStack = 2 });

        AlchemyCore.AddResult(ItemID.BottledWater, SolventID.FibrousSlurry,
            new BrewRecipe(ComponentMatcher.Of(ItemID.Cobweb, 3)) { OutputStack = 1 });

        AlchemyCore.AddResult(ItemID.Bottle, SoluteID.BlackPowder,
            new BrewRecipe(ComponentMatcher.Of(SoluteID.Niter, 3), SoluteID.Sulfur, ModContent.ItemType<CharcoalItem>())
            {
                OutputStack = 2,
                ReturnsBottle = true,
            });

        AlchemyCore.AddResult(SolventID.AquaInertis, SoluteID.ColdFire,
            new BrewRecipe(ComponentMatcher.Of(SoluteID.BoneAsh, 2), ModContent.ItemType<CharcoalItem>(), ItemID.SandBlock));

        #endregion
    }

    /// <summary>Miscellaneous craftable items (e.g. unholy water, love potion).</summary>
    private static void RegisterMisc()
    {
        #region Water flasks & useless potions (lol)
        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.UnholyWater,
            new BrewRecipe(CorruptTiles, ItemID.CorruptSeeds) { OutputStack = 4 },
            new BrewRecipe(ItemID.VileMushroom, Dirts, ItemID.Worm) { OutputStack = 4 });

        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.BloodWater,
            new BrewRecipe(BloodTiles, ItemID.CrimsonSeeds) { OutputStack = 4 },
            new BrewRecipe(ItemID.ViciousMushroom, Dirts, ItemID.Worm) { OutputStack = 4 });

        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.HolyWater,
            new BrewRecipe(ItemID.PixieDust, ItemID.HallowedSeeds) { OutputStack = 4 });

        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.LovePotion,
            new BrewRecipe(ItemID.LifeCrystal, ItemID.FallenStar) { OutputStack = 4 },
            new BrewRecipe(ItemID.PrincessFish, ItemID.Shiverthorn) { OutputStack = 8 });

        AlchemyCore.AddResult(ItemID.BottledWater, ItemID.StinkPotion,
            new BrewRecipe(ItemID.Stinkfish, ItemID.Deathweed) { OutputStack = 2 },
            new BrewRecipe(ItemID.Stinkfish, ItemID.DeathweedSeeds));
        #endregion

        AlchemyCore.AddResult(ItemID.Bottle, ItemID.ViciousPowder,
            new BrewRecipe(ItemID.ViciousMushroom)
            { OutputStack = 5, ReturnsBottle = true });

        AlchemyCore.AddResult(ItemID.Bottle, ItemID.VilePowder,
            new BrewRecipe(ItemID.VileMushroom)
            { OutputStack = 5, ReturnsBottle = true });

        AlchemyCore.AddResult(ItemID.Bottle, ItemID.PurificationPowder,
            new BrewRecipe(ModContent.ItemType<DewdropBonnetItem>()) 
            { OutputStack = 5, ReturnsBottle = true });

        AlchemyCore.AddResult(ItemID.Bottle, ModContent.ItemType<MagnoliaPowderItem>(),
            new BrewRecipe(ModContent.ItemType<MagnoliaItem>())
            { OutputStack = 5, ReturnsBottle = true });
    }
    #endregion
}