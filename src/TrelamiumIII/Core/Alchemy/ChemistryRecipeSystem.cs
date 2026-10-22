using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Items.ContortionistSet;
using TrelamiumIII.Content.Items.FarmingOverhaul;
using TrelamiumIII.Content.Items.Lodestone;
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
        //RegisterMisc();

        //RegisterCombatPotions();
        //RegisterUtilityPotions();
        //RegisterVisionPotions();
        //RegisterFishingPotions();
        //RegisterLuckPotions();
        //RegisterAugmentations();
        //RegisterTransmutations();

        //RegisterWeapons();
        //RegisterSolutes();

        //if (ModLoader.HasMod("SpiritReforged"))
        //    RegisterSpiritReforged();

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
        AlchemyCore.AddResult(
            _base: ItemID.BottledWater, 
            recipe: SolventID.AquaInertis, 
            new SoluteInput(ComponentMatcher.Of(Mushroom, min: 1)));
        
        AlchemyCore.AddResult(
            _base: ItemID.BottledWater,
            recipe: SolventID.AquaSalis,
            new SoluteInput(ComponentMatcher.Of(AnyMineralSalt, min: 1)));

        AlchemyCore.AddResult(
            _base: ItemID.BottledWater,
            recipe: ModContent.ItemType<AquaTerrae>(),
            new SoluteInput(Dirts));

        AlchemyCore.AddResult(
            _base: ItemID.Bottle,
            recipe: SolventID.BottledOil,
            new SoluteInput(SoluteID.Pitch));

        AlchemyCore.AddResult(
            _base: ItemID.Bottle,
            recipe: ModContent.ItemType<SunflowerOilBottleItem>(),
            new SoluteInput(ComponentMatcher.Of(ModContent.ItemType<SunflowerSeeds>(), 2)));

        int[] bases = [ItemID.BottledWater, SolventID.AquaInertis, SolventID.AquaSalis];

        AlchemyCore.AddResult(bases, SolventID.AquaHarmonia,
            new SoluteInput(ItemID.CopperOre),
            new SoluteInput(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyCore.AddResult(bases, SolventID.AquaVivida,
            new SoluteInput(ItemID.TinOre));

        AlchemyCore.AddResult(bases, SolventID.AquaVitae,
            new SoluteInput(ItemID.GoldOre));

        AlchemyCore.AddResult(bases, SolventID.AquaSpecularis,
            new SoluteInput(ItemID.SilverOre),
            new SoluteInput(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyCore.AddResult(bases, SolventID.AquaFortis,
            new SoluteInput(ItemID.IronOre, SoluteID.Niter));

        AlchemyCore.AddResult(bases, SolventID.AquaFortunae,
            new SoluteInput(ItemID.PlatinumOre) { OutputStack = 3 },
            new SoluteInput(ItemID.TinOre));

        AlchemyCore.AddResult(bases, SolventID.AquaPestis,
            new SoluteInput(AnyEvilOre),
            new SoluteInput(ItemID.LeadOre));

        AlchemyCore.AddResult(bases, ModContent.ItemType<MagnoliaSolutionItem>(),
            new SoluteInput(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new SoluteInput(ModContent.ItemType<MagnoliaSeedsItem>()),
            new SoluteInput(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        //AlchemyCore.AddResult([ItemID.Bottle, ItemID.BottledWater, SolventID.Inert], SolventID.BloodtoothExtract,
        //    new SoluteInput(ModContent.ItemType<BloodtoothFungusItem>()));
    }

    /// <summary>
    /// Mineral refinement: acid solvents and alkali/distillate solutes. None of these outputs is a potion, so no
    /// <see cref="SoluteInput.Potency" /> applies; star ratings live on <see cref="AlmanacEntry.AlmanacPotency" />.
    /// </summary>
    private static void RegisterSolutes()
    {
        #region Reagents

        AlchemyCore.AddResult(SolventID.AquaPestis, ModContent.ItemType<TwistedFungus>(),
            new SoluteInput(ComponentMatcher.Of(SoluteID.RockSalt, 2), ItemID.VileMushroom) { ReturnsBottle = true });

        #endregion
        #region Acids

        AlchemyCore.AddResult(ItemID.BottledWater, SolventID.OilOfVitriol,
            new SoluteInput(ComponentMatcher.Of(SoluteID.Sulfur, 2), SoluteID.Niter));

        AlchemyCore.AddResult(SolventID.OilOfVitriol, SolventID.AquaFortis,
            new SoluteInput(ComponentMatcher.Of(SoluteID.Niter, 2)));

        AlchemyCore.AddResult(SolventID.OilOfVitriol, SoluteID.SpiritOfSalt,
            new SoluteInput(ComponentMatcher.Of(SoluteID.RockSalt, 2)));

        // The one that eats gold. Both of the strong acids, and nothing else.
        AlchemyCore.AddResult(SolventID.AquaFortis, SolventID.AquaRegia,
            new SoluteInput(SoluteID.SpiritOfSalt));

        AlchemyCore.AddResult(SolventID.OilOfVitriol, SoluteID.BlueVitriol,
            new SoluteInput(ComponentMatcher.Of(ModContent.ItemType<OxidizedCopperItem>(), 2)));

        #endregion

        #region Alkalis

        AlchemyCore.AddResult(ItemID.BottledWater, SoluteID.SlakedLime,
            new SoluteInput(ComponentMatcher.Of(SoluteID.Quicklime, 2)) { OutputStack = 2 });

        AlchemyCore.AddResult(ItemID.BottledWater, SoluteID.CausticLye,
            new SoluteInput(ComponentMatcher.Of(SoluteID.Potash, 2), SoluteID.Quicklime));

        AlchemyCore.AddResult(SolventID.AquaInertis, SoluteID.FrigorificSalt,
            new SoluteInput(SoluteID.Niter, SoluteID.RockSalt, ItemID.IceBlock));

        #endregion

        #region Distillates and powders

        // Oil distils into two outputs, registered as two recipes over the same solvent.
        AlchemyCore.AddResult(SolventID.BottledOil, SoluteID.SpiritOfOil,
            new SoluteInput(ComponentMatcher.Of(ModContent.ItemType<CharcoalItem>(), 2)));

        AlchemyCore.AddResult(SolventID.BottledOil, SoluteID.Pitch,
            new SoluteInput(ComponentMatcher.Of(ItemID.Cobweb, 4)) { OutputStack = 2 });

        AlchemyCore.AddResult(ItemID.BottledWater, SolventID.FibrousSlurry,
            new SoluteInput(ComponentMatcher.Of(ItemID.Cobweb, 3)) { OutputStack = 1 });

        // Niter at maximum potency to one part fuel.
        AlchemyCore.AddResult(SolventID.AquaInertis, SoluteID.BlackPowder,
            new SoluteInput(ComponentMatcher.Of(SoluteID.Niter, 3), SoluteID.Sulfur, ModContent.ItemType<CharcoalItem>())
            {
                OutputStack = 2,
            });

        AlchemyCore.AddResult(SolventID.AquaInertis, SoluteID.ColdFire,
            new SoluteInput(ComponentMatcher.Of(SoluteID.BoneAsh, 2), ModContent.ItemType<CharcoalItem>(), ItemID.SandBlock));

        #endregion
    }
    #endregion
}
