using System.Collections.Generic;
using Terraria.Localization;
using TrelamiumIII.Common.Globals;
using TrelamiumIII.Content.Alchemy.MagnoliaContent;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Reagents.Misc;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Alchemy.Weapons;
using TrelamiumIII.Content.FarmingOverhaul.SatchelContent;
using TrelamiumIII.Content.Lodestone;
using TrelamiumIII.Content.Rusted;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>Registers recipes and recipe groups, including the vanilla potion recipe changes.</summary>
public class ChemistryRecipeSystem : ModSystem
{
    #region Terraria Recipe Management
    public override void AddRecipes()
    {
        Recipe iceBlade = Recipe.Create(ItemID.IceBlade);
        iceBlade.AddIngredient(ItemID.IceBlock, 30)
            .AddIngredient(ItemID.FallenStar, 4)
            .AddCondition(Condition.InSnow)
            .AddTile(TileID.IceMachine)
            .Register();

        Recipe fertilizer = Recipe.Create(ItemID.Fertilizer);
        fertilizer.AddIngredient(ItemID.PoopBlock, 3)
            .AddRecipeGroup(nameof(ItemID.Bass))
            .AddRecipeGroup(nameof(ItemID.DirtBlock), 3)
            .AddTile(TileID.WorkBenches)
            .Register();

        Recipe placeableHealingPot = Recipe.Create(ItemID.PlaceableHealingPotion);
        placeableHealingPot.AddIngredient(ItemID.LesserHealingPotion)
            .AddTile(TileID.WorkBenches)
            .Register();

        Recipe torch = Recipe.Create(ItemID.Torch, 3);
        torch.AddIngredient(ItemID.Wood, 1)
            .AddIngredient(ModContent.ItemType<CharcoalItem>(), 1)
            .Register();


    }

    public override void AddRecipeGroups()
    {
        RecipeGroup CopperBarRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.CopperBar)}",
                    ItemID.CopperBar, ItemID.TinBar);
        RecipeGroup.RegisterGroup(nameof(ItemID.CopperBar), CopperBarRecipeGroup);

        RecipeGroup SilverBarRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.SilverBar)}",
            ItemID.SilverBar, ItemID.TungstenBar);
        RecipeGroup.RegisterGroup(nameof(ItemID.SilverBar), SilverBarRecipeGroup);

        RecipeGroup GoldBarRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.GoldBar)}",
            ItemID.GoldBar, ItemID.PlatinumBar);
        RecipeGroup.RegisterGroup(nameof(ItemID.GoldBar), GoldBarRecipeGroup);

        RecipeGroup FishRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.Bass)}", 2290, 2297, 2298, 2299, 2300, 2301, 2302, 2303, 2304, 2305, 2306, 2307,
            2308, 2309, 2310, 2311, 2312, 2313, 2314, 2315, 2316, 2317, 2318, 2319, 2321, 4401, 4402);
        RecipeGroup.RegisterGroup(nameof(ItemID.Bass), FishRecipeGroup);

        RecipeGroup soilRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.DirtBlock)}",
            ItemID.DirtBlock, ItemID.MudBlock, ItemID.AshBlock);
        RecipeGroup.RegisterGroup(nameof(ItemID.DirtBlock), soilRecipeGroup);
    }

    //public override void PostAddRecipes()
    //{
    //    for (int i = 0; i < Recipe.numRecipes; i++)
    //    {
    //        Recipe recipe = Main.recipe[i];

    //        // Terraria's potion recipes are removed, the alchemy station's will handle this now.
    //        // An unsupported mod's alternate route to a vanilla potion won't be removed.
    //        if (recipe.IsVanillaRecipe() && recipe.CraftsVanillaPotion())
    //        {
    //            recipe.DisableRecipe();
    //            continue;
    //        }

    //        // A supported mod is only taken over where we actually replace it. Blanket-disabling
    //        // would strand every potion of theirs we haven't written an alchemy recipe for yet, so
    //        // this widens on its own as recipes get added to RegisterSpiritReforged().
    //        if (recipe.IsFromSupportedMod()
    //            && recipe.createItem.IsSupportedModPotion()
    //            && AlchemyRecipeRegistry.Produces(recipe.createItem.type))
    //        {
    //            recipe.DisableRecipe();
    //        }
    //    }
    //}

    #endregion

    public override void PostSetupContent()
    {
        base.PostSetupContent();

        AlmanacDefinitions.Register();

        RegisterSolutions();

        RegisterMisc();

        RegisterPotions();

        RegisterWeapons();

        RegisterMinerals();

        if (ModLoader.TryGetMod("SpiritReforged", out Mod _ ))
        {
            RegisterSpiritReforged();
        }

        if (ModLoader.TryGetMod("CalamityMod", out Mod calamityMod))
        {
            RegisterCalamity();
        }
        PopulateRecipeGroups();
    }

    private static void PopulateRecipeGroups()
    {
        AnyAcid.Add(
            SolventID.OilOfVitriol, SolventID.AquaFortis,
            SoluteID.SpiritOfSalt, SolventID.AquaRegia,
            SoluteID.BlueVitriol);

        AnyAlkali.Add(
            SoluteID.SlakedLime, SoluteID.CausticLye,
            SoluteID.Quicklime, SoluteID.Potash);

        AnyMineralSalt.Add(
            SoluteID.RockSalt, SoluteID.Niter,
            SoluteID.FrigorificSalt);

        Iron.Add(ItemID.IronOre, ItemID.LeadOre);

        Silver.Add(ItemID.SilverOre, ItemID.TungstenOre);

        Gold.Add(ItemID.GoldOre, ItemID.PlatinumOre);

        Critters.Add(
            ItemID.Bunny, ItemID.Squirrel, ItemID.Mouse, ItemID.Rat, ItemID.Frog, ItemID.Worm, ItemID.Snail);

        AnyOre.Add(
            ItemID.CopperOre, ItemID.TinOre, ItemID.IronOre, ItemID.LeadOre,
            ItemID.SilverOre, ItemID.TungstenOre, ItemID.GoldOre, ItemID.PlatinumOre);

        AnyEvilOre.Add(ItemID.DemoniteOre, ItemID.CrimtaneOre);

        AnyStandardFish.Add(
            ItemID.Bass, ItemID.RedSnapper, ItemID.AtlanticCod, ItemID.NeonTetra,
            ItemID.Flounder, ItemID.Ebonkoi, ItemID.DoubleCod, ItemID.Salmon);

        Mushroom.Add(ItemID.Mushroom, ModContent.ItemType<FlyAgaricItem>());

        AnyEvilMushroom.Add(ItemID.VileMushroom, ItemID.ViciousMushroom);

        AnyHerb.Add(
            ItemID.Daybloom, ItemID.Blinkroot, ItemID.Moonglow, ItemID.Deathweed,
            ItemID.Waterleaf, ItemID.Fireblossom, ItemID.Shiverthorn);

        // Cross-mod herbs go through the flower registry, so a bad internal name is reported rather than silently missing.
        int cloudstalk = FlowerRegistry.TypeOf("SpiritReforged", "Cloudstalk");
        if (cloudstalk > 0)
            AnyHerb.Add(cloudstalk);

        RottenChunkVertabrae.Add(ItemID.RottenChunk, ItemID.Vertebrae);

        BloodTiles.Add(
            ItemID.CrimsandBlock, ItemID.CrimstoneBlock);

        CorruptTiles.Add(
            ItemID.EbonsandBlock, ItemID.EbonstoneBlock);

        ShiverthornOrWaterleaf.Add(ItemID.Shiverthorn, ItemID.Waterleaf);

        AnySoil.Add(
            ItemID.DirtBlock, ItemID.MudBlock,
            ItemID.EbonsandBlock, ItemID.CrimsandBlock, ItemID.PearlsandBlock,
            ItemID.EbonstoneBlock, ItemID.CrimstoneBlock, ItemID.PearlstoneBlock);

        AnyDirt.Add(ItemID.DirtBlock, ItemID.MudBlock);

        AnyButterfly.Add(
            ItemID.GoldButterfly, ItemID.HellButterfly, ItemID.JuliaButterfly,
            ItemID.MonarchButterfly, ItemID.PurpleEmperorButterfly, ItemID.RedAdmiralButterfly,
            ItemID.SulphurButterfly, ItemID.TreeNymphButterfly, ItemID.UlyssesButterfly,
            ItemID.ZebraSwallowtailButterfly);

        AnyBaseBottle.Add(ItemID.Bottle, ItemID.BottledWater, SolventID.Inert);

        SpecularFishOrMoonglow.Add(ItemID.SpecularFish, ItemID.Moonglow);
    }

    private static void RegisterWeapons()
    {
        AlchemyRecipes.CreateRecipe(
            SolventID.BloodtoothExtract,
            ModContent.ItemType<CrimsonEffusionFlaskItem>(),
            new RecipeVariant(ItemID.ViciousPowder, ItemID.Deathweed) { OutputStack = 8 },
            new RecipeVariant(ItemID.Deathweed) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(SolventID.Dead, ModContent.ItemType<CrimsonEffusionFlaskItem>(),
            new RecipeVariant(ComponentMatcher.Of(ItemID.ViciousPowder, 2), ComponentMatcher.Of(ItemID.Deathweed, 2)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.Deathweed, 2), ComponentMatcher.Of(ModContent.ItemType<BloodtoothFungusItem>(), 2)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.ViciousPowder, 2), ComponentMatcher.Of(ModContent.ItemType<BloodtoothFungusItem>(), 2)) { OutputStack = 8 });
        
        AlchemyRecipes.CreateRecipe(SolventID.BloodtoothExtract, ModContent.ItemType<CrimsonEffusionFlaskItem>(),
            new RecipeVariant(ComponentMatcher.Of(ItemID.ViciousPowder, 2), ComponentMatcher.Of(ItemID.Deathweed, 2)) { OutputStack = 12 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.Deathweed, 2), ComponentMatcher.Of(ModContent.ItemType<BloodtoothFungusItem>(), 2)) { OutputStack = 12 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.ViciousPowder, 2), ComponentMatcher.Of(ModContent.ItemType<BloodtoothFungusItem>(), 2)) { OutputStack = 12 });

        AlchemyRecipes.CreateRecipe(SolventID.BaneTonic, ModContent.ItemType<CrimsonEffusionFlaskItem>(),
            new RecipeVariant(ComponentMatcher.Of(ItemID.ViciousPowder, 1), ComponentMatcher.Of(ItemID.Deathweed, 1)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.Deathweed, 1), ComponentMatcher.Of(ModContent.ItemType<BloodtoothFungusItem>(), 1)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.ViciousPowder, 1), ComponentMatcher.Of(ModContent.ItemType<BloodtoothFungusItem>(), 1)) { OutputStack = 8 });

        AlchemyRecipes.CreateRecipe(SolventID.Dead, ModContent.ItemType<VilesporeFlaskItem>(),
            new RecipeVariant(ComponentMatcher.Of(ItemID.VilePowder, 2), ComponentMatcher.Of(ItemID.Deathweed, 2)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.VilePowder, 2), ComponentMatcher.Of(ItemID.RottenChunk, 2)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.VilePowder, 2), ComponentMatcher.Of(ItemID.RottenChunk, 2)) { OutputStack = 8 });

        AlchemyRecipes.CreateRecipe(SolventID.BaneTonic, ModContent.ItemType<VilesporeFlaskItem>(),
            new RecipeVariant(ComponentMatcher.Of(ItemID.VilePowder, 1), ComponentMatcher.Of(ItemID.Deathweed, 1)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.VilePowder, 1), ComponentMatcher.Of(ItemID.RottenChunk, 1)) { OutputStack = 8 },
            new RecipeVariant(ComponentMatcher.Of(ItemID.VilePowder, 1), ComponentMatcher.Of(ItemID.RottenChunk, 1)) { OutputStack = 8 });


        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ModContent.ItemType<HyperboreanFlaskItem>(),
            new RecipeVariant(
                ComponentMatcher.Of(ItemID.Shiverthorn, 2), 
                ComponentMatcher.Of(ItemID.CopperOre, 1),
                ComponentMatcher.Of(SoluteID.Niter, 2)) 
            { OutputStack = 8 },

            new RecipeVariant(
                ComponentMatcher.Of(ItemID.ShiverthornSeeds, 2),
                ComponentMatcher.Of(ItemID.CopperOre, 1),
                ComponentMatcher.Of(SoluteID.Niter, 2)) 
            { OutputStack = 8 });

        AlchemyRecipes.CreateRecipe(SolventID.HarmoniousTonic, ModContent.ItemType<HyperboreanFlaskItem>(),
            new RecipeVariant(
                ComponentMatcher.Of(ItemID.Shiverthorn, 2),
                ComponentMatcher.Of(SoluteID.Niter, 2))
            { OutputStack = 8 },

            new RecipeVariant(
                ComponentMatcher.Of(ItemID.ShiverthornSeeds, 2),
                ComponentMatcher.Of(SoluteID.Niter, 2))
            { OutputStack = 8 });
    }

    /// <summary>
    /// Mineral refinement: acid solvents (<c>Solution</c>) and alkali/distillate solutes (<c>Reagent</c>). Star ratings live on
    /// <see cref="AlmanacEntry.Potency"/>; <see cref="RecipeVariant.Potency"/> brands buff potions and must stay 0 here.
    /// </summary>
    private static void RegisterMinerals()
    {
        #region Acids

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.OilOfVitriol,
            new RecipeVariant(
                ComponentMatcher.Of(SoluteID.Sulfur, 2),
                SoluteID.Niter));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.OilOfVitriol,
            outputType: SolventID.AquaFortis,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.Niter, 2)));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.OilOfVitriol,
            outputType: SoluteID.SpiritOfSalt,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.RockSalt, 2)));

        // The one that eats gold. Both of the strong acids, and nothing else.
        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.AquaFortis,
            outputType: SolventID.AquaRegia,
            new RecipeVariant(SoluteID.SpiritOfSalt));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.OilOfVitriol,
            outputType: SoluteID.BlueVitriol,
            new RecipeVariant(ComponentMatcher.Of(ModContent.ItemType<OxidizedCopperItem>(), 2)));

        #endregion

        #region Alkalis

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SoluteID.SlakedLime,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.Quicklime, 2))
            { OutputStack = 2 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SoluteID.CausticLye,
            new RecipeVariant(
                ComponentMatcher.Of(SoluteID.Potash, 2),
                SoluteID.Quicklime));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: SoluteID.FrigorificSalt,
            new RecipeVariant(
                SoluteID.Niter,
                SoluteID.RockSalt,
                ComponentMatcher.Of(ItemID.IceBlock, 1)));

        #endregion

        #region Distillates and powders

        // Oil distils into two outputs, registered as two recipes over the same solvent.
        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BottledOil,
            outputType: SoluteID.SpiritOfOil,
            new RecipeVariant(ComponentMatcher.Of(ModContent.ItemType<CharcoalItem>(), 2)));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BottledOil,
            outputType: SoluteID.Pitch,
            new RecipeVariant(ComponentMatcher.Of(SolventID.FibrousSlurry, 2))
            { OutputStack = 2 });

        // Niter at maximum potency to one part fuel.
        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: SoluteID.BlackPowder,
            new RecipeVariant(
                ComponentMatcher.Of(SoluteID.Niter, 5),
                SoluteID.Sulfur,
                ModContent.ItemType<CharcoalItem>())
            { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: SoluteID.ColdFire,
            new RecipeVariant(
                ComponentMatcher.Of(SoluteID.BoneAsh, 2),
                ModContent.ItemType<CharcoalItem>(),
                ComponentMatcher.Of(ItemID.SandBlock, 1)));

        #endregion
    }

    private static void RegisterSolutions()
    {
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.Inert,
            new RecipeVariant(Mushroom));

        #region Inert-bases
        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: SolventID.HarmoniousTonic,
            new RecipeVariant(ItemID.CopperOre),
            new RecipeVariant(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert, 
            outputType: SolventID.BriskTonic,
            new RecipeVariant(ModContent.ItemType<LodestoneItem>()) { OutputStack = 2 },
            new RecipeVariant(ItemID.TinOre) { OutputStack = 2 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert, 
            outputType: SolventID.VitalTonic,
            new RecipeVariant(ItemID.GoldOre));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: SolventID.SpecularTonic,
            new RecipeVariant(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert, 
            outputType: SolventID.SpecularTonic,
            new RecipeVariant(ItemID.SilverOre),
            new RecipeVariant(ItemID.SpecularFish) { OutputStack = 4});

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert, 
            outputType: SolventID.MartialTonic,
            new RecipeVariant(ItemID.IronOre));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert, 
            outputType: SolventID.FortunateTonic,
            new RecipeVariant(ItemID.PlatinumOre) { OutputStack = 3 },
            new RecipeVariant(ItemID.TinOre));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: SolventID.BaneTonic,
            new RecipeVariant(AnyEvilOre),
            new RecipeVariant(ItemID.LeadOre));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert, 
            outputType: SolventID.Dead,
            new RecipeVariant(RottenChunkVertabrae));

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert, 
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new RecipeVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new RecipeVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new RecipeVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new RecipeVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new RecipeVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new RecipeVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });
        #endregion

        #region Water-bases
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.HarmoniousTonic,
            new RecipeVariant(ItemID.CopperOre),
            new RecipeVariant(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.BriskTonic,
            new RecipeVariant(ModContent.ItemType<LodestoneItem>()) { OutputStack = 2 },
            new RecipeVariant(ItemID.TinOre) { OutputStack = 2 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.VitalTonic,
            new RecipeVariant(ItemID.GoldOre));

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.SpecularTonic,
            new RecipeVariant(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.SpecularTonic,
            new RecipeVariant(ItemID.SilverOre),
            new RecipeVariant(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.MartialTonic,
            new RecipeVariant(ItemID.IronOre));

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.FortunateTonic,
            new RecipeVariant(ItemID.PlatinumOre) { OutputStack = 3 },
            new RecipeVariant(ItemID.TinOre));

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.BaneTonic,
            new RecipeVariant(AnyEvilOre),
            new RecipeVariant(ItemID.LeadOre));

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: SolventID.Dead,
            new RecipeVariant(RottenChunkVertabrae));

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new RecipeVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new RecipeVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new RecipeVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new RecipeVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new RecipeVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new RecipeVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });
        #endregion
    }

    /// <summary>Recipes for the vanilla potions, including their inert variants.</summary>
    private static void RegisterPotions()
    {
        // Actual Buff Potions

        #region Ironskin
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater, 
            outputType: ItemID.IronskinPotion,
            new RecipeVariant(Iron, ItemID.Daybloom) { Potency = 1 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: ItemID.IronskinPotion,
            new RecipeVariant(Iron, ItemID.Daybloom) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.MartialTonic,
            outputType: ItemID.IronskinPotion,
            new RecipeVariant(ItemID.Daybloom) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new RecipeVariant(Iron, ItemID.Daybloom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 6) });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.IronskinPotion,
            new RecipeVariant(ItemID.DaybloomSeeds, Iron)
            {
                OutputStack = 4,
                Potency = 2,
                StrengthPercent = 95,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });
        #endregion  

        #region Swiftness
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 1 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2, StrengthPercent = 115 },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Feather) { Potency = 2},
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus, ItemID.Feather) { Potency = 3 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BriskTonic,
            outputType: ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2 },
            new RecipeVariant(ItemID.Feather) { Potency = 1 },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Feather) { Potency = 3 },
            new RecipeVariant(ItemID.Cactus, ItemID.Blinkroot, ItemID.Feather) { OutputStack = 3, Potency = 5 },
            new RecipeVariant(ItemID.Sunflower) { OutputStack = 2, Potency = 1, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 7) },
            new RecipeVariant(ItemID.Sunflower, ItemID.Feather) { OutputStack = 2, Potency = 3, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new RecipeVariant(ItemID.Sunflower, ItemID.Blinkroot, ItemID.Feather) { OutputStack = 5, Potency = 5, StrengthPercent = 115, DurationTicks = TimeConversion.SetTime(mins: 4) });
        
        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.BlinkrootSeeds, ItemID.Cactus) 
            { 
                OutputStack = 4, 
                Potency = 2, 
                StrengthPercent = 95, 
                DurationTicks = TimeConversion.SetTime(mins: 10) 
            });
        #endregion

        #region Regeneration
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.Daybloom, Mushroom) { Potency = 1 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.Inert,
            outputType: ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.Daybloom, Mushroom) { Potency = 2 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { Potency = 3 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 3, StrengthPercent = 130 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.VitalTonic,
            outputType: ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Mushroom) { Potency = 3 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { OutputStack = 2, Potency = 3 },
            new RecipeVariant(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 4, StrengthPercent = 125, DurationTicks = TimeConversion.SetTime(mins: 7) });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.RegenerationPotion,
            new RecipeVariant(ItemID.DaybloomSeeds, ItemID.Mushroom)
            {
                OutputStack = 4,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        #endregion

        #region Endurance
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.EndurancePotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.MartialTonic,
            outputType: ItemID.EndurancePotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 2, StrengthPercent = 110, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) });
        #endregion

        #region Wrath
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.WrathPotion,
            new RecipeVariant(ItemID.Ebonkoi, ItemID.Deathweed) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new RecipeVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VilePowder) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new RecipeVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VileMushroom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },

            new RecipeVariant(ItemID.EbonstoneBlock, ItemID.VileMushroom, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 50, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            },
            new RecipeVariant(ItemID.EbonstoneBlock, ItemID.VilePowder, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 60, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            });

        AlchemyRecipes.CreateRecipe(
           inputType: SolventID.MartialTonic,
           outputType: ItemID.WrathPotion,
           new RecipeVariant(ItemID.Ebonkoi, ItemID.Deathweed) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 4) },
           new RecipeVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VilePowder) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) },
           new RecipeVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VileMushroom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45) });

        #endregion

        #region Rage
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.RagePotion,
            new RecipeVariant(ItemID.Hemopiranha, ItemID.Deathweed) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new RecipeVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousPowder) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new RecipeVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousMushroom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },

            new RecipeVariant(ItemID.CrimstoneBlock, ItemID.ViciousMushroom, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 50, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            },
            new RecipeVariant(ItemID.CrimstoneBlock, ItemID.ViciousPowder, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 60, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            });

        AlchemyRecipes.CreateRecipe(
           inputType: SolventID.MartialTonic,
           outputType: ItemID.RagePotion,
           new RecipeVariant(ItemID.Hemopiranha, ItemID.Deathweed) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 4) },
           new RecipeVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousPowder) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) },
           new RecipeVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousMushroom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45) });

        #endregion

        #region Archery
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.ArcheryPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Lens) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });
        #endregion

        #region Battle
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.BattlePotion,
            new RecipeVariant(ItemID.Deathweed, RottenChunkVertabrae) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 7) });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.BattlePotion,
            new RecipeVariant(RottenChunkVertabrae, ItemID.DeathweedSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 9, secs: 30)
            });
        #endregion

        #region Ammo Reservation
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.AmmoReservationPotion,
            new RecipeVariant(ItemID.DoubleCod, ItemID.Moonglow) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.AmmoReservationPotion,
            new RecipeVariant(ItemID.DoubleCod, ItemID.MoonglowSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });
        #endregion

        #region Heartreach
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.HeartreachPotion,
            new RecipeVariant(ItemID.CrimsonTigerfish, ItemID.Daybloom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.HeartreachPotion,
            new RecipeVariant(ItemID.CrimsonTigerfish, ItemID.DaybloomSeeds) { 
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10) 
            });
        #endregion

        #region Inferno
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.InfernoPotion,
            new RecipeVariant(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.Fireblossom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.InfernoPotion,
            new RecipeVariant(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.FireblossomSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });
        #endregion

        #region Lifeforce
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.LifeforcePotion,
            new RecipeVariant(ItemID.Prismite, ItemID.Moonglow, ItemID.Shiverthorn) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new RecipeVariant(ItemID.Prismite, ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new RecipeVariant(ItemID.Prismite, ItemID.Moonglow, ItemID.Waterleaf) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.LifeforcePotion,
            new RecipeVariant(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new RecipeVariant(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.WaterleafSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new RecipeVariant(ItemID.Prismite, ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });
        #endregion

        #region Thorns
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.ThornsPotion,
            new RecipeVariant(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 100, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.MartialTonic,
            outputType: ItemID.ThornsPotion,
            new RecipeVariant(ItemID.Deathweed, ItemID.Cactus) 
            { 
                Potency = 1, 
                StrengthPercent = 110, 
                DurationTicks = TimeConversion.SetTime(mins: 8) 
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.ThornsPotion,
            new RecipeVariant(ItemID.DeathweedSeeds, ItemID.Cactus)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            });
        #endregion

        #region Titan
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.TitanPotion,
            new RecipeVariant(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) 
            { 
                Potency = 1, 
                StrengthPercent = 100, 
                DurationTicks = TimeConversion.SetTime(mins: 8) 
            },
            new RecipeVariant(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn)
            {
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.MartialTonic,
            outputType: ItemID.TitanPotion,
            new RecipeVariant(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn)
            {
                Potency = 2,
                StrengthPercent = 110,
                DurationTicks = TimeConversion.SetTime(mins: 8)
            },
            new RecipeVariant(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn)
            {
                Potency = 2,
                StrengthPercent = 110,
                DurationTicks = TimeConversion.SetTime(mins: 6, secs: 30)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.TitanPotion,
            new RecipeVariant(ItemID.Bone, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 85,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new RecipeVariant(ItemID.IronOre, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });
        #endregion

        // Utility Potions

        #region Restoration & Healing
        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.VitalTonic,
            outputType: ItemID.RestorationPotion,
            new RecipeVariant(ItemID.Daybloom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.RestorationPotion,
            outputType: ItemID.RestorationPotion,
            new RecipeVariant(Mushroom, ItemID.Daybloom) 
            { 
                OutputStack = 1, 
                Potency = 3, 
                StrengthPercent = 105,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20)) 
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.Bottle,
            outputType: ItemID.LesserHealingPotion,
            new RecipeVariant(Mushroom, ItemID.Gel) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.VitalTonic,
            outputType: ItemID.LesserHealingPotion,
            new RecipeVariant(Mushroom) { OutputStack = 4, Potency = 1, StrengthPercent = 100 },
            new RecipeVariant(Mushroom, ItemID.Gel) { OutputStack = 4, Potency = 2, StrengthPercent = 107 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.LesserHealingPotion,
            outputType: ItemID.LesserHealingPotion,
            new RecipeVariant(ItemID.Daybloom) 
            { 
                OutputStack = 2, 
                Potency = 1, 
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20))
            },
            new RecipeVariant(ItemID.Blinkroot)
            {
                OutputStack = 2,
                Potency = 2,
                StrengthPercent = 120
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.LesserHealingPotion,
            outputType: ItemID.HealingPotion,
            new RecipeVariant(ItemID.GlowingMushroom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.HealingPotion,
            outputType: ItemID.HealingPotion,
            new RecipeVariant(ItemID.Daybloom) 
            { 
                OutputStack = 2, 
                Potency = 1, 
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20))
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.HealingPotion,
            outputType: ItemID.HealingPotion,
            new RecipeVariant(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20))
            });
        #endregion

        #region Mining
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.MiningPotion,
            new RecipeVariant(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new RecipeVariant(Silver, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105 },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 12) });

        AlchemyRecipes.CreateRecipe(SolventID.BriskTonic, ItemID.MiningPotion,
            new RecipeVariant(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 111, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new RecipeVariant(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 100, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 4, StrengthPercent = 124, DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30) },
            new RecipeVariant(Silver, ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = TimeConversion.SetTime(mins: 4) });

        AlchemyRecipes.CreateRecipe(ModContent.ItemType<MagnoliaSolutionItem>(), ItemID.MiningPotion,
            new RecipeVariant(ItemID.BlinkrootSeeds, Silver) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = TimeConversion.SetTime(mins: 10) });     
        #endregion

        #region Builder

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.BuilderPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { 
                OutputStack = 1, 
                Potency = 1, 
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 45)
            },
            new RecipeVariant(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 125,
                DurationTicks = TimeConversion.SetTime(mins: 45)
            },
            new RecipeVariant(ItemID.Blinkroot, ModContent.ItemType<LodestoneItem>(), ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 135,
                DurationTicks = TimeConversion.SetTime(mins: 40)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BriskTonic,
            outputType: ItemID.BuilderPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 150,
                DurationTicks = TimeConversion.SetTime(mins: 45)
            },
            new RecipeVariant(ItemID.Blinkroot, ModContent.ItemType<LodestoneItem>(), ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 5,
                StrengthPercent = 225,
                DurationTicks = TimeConversion.SetTime(mins: 8)
            },
            new RecipeVariant(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow)
            {
            OutputStack = 1,
            Potency = 4,
            StrengthPercent = 150,
            DurationTicks = TimeConversion.SetTime(mins: 30)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.BuilderPotion,
            new RecipeVariant(ItemID.BlinkrootSeeds, ItemID.TinOre, ItemID.MoonglowSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 60)
            },
            new RecipeVariant(ItemID.BlinkrootSeeds, ModContent.ItemType<LodestoneItem>(), ItemID.MoonglowSeeds)
            {
                OutputStack = 1,
                Potency = 3,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 60)
            });
        #endregion

        #region Gills
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.GillsPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 4)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 105,
                DurationTicks = TimeConversion.SetTime(mins: 4, secs: 30)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });


        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.GillsPotion,
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });
        #endregion

        #region Flipper
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.FlipperPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.Shiverthorn)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 8)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.FlipperPotion,
            new RecipeVariant(ItemID.ShiverthornSeeds, ItemID.WaterleafSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BriskTonic,
            outputType: ItemID.FlipperPotion,
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Waterleaf)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            });
        #endregion

        #region Water Walking
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.WaterWalkingPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.SharkFin)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.WaterWalkingPotion,
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.SharkFin)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BriskTonic,
            outputType: ItemID.WaterWalkingPotion,
            new RecipeVariant(ItemID.Waterleaf, ItemID.SharkFin)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 14)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            });
        #endregion

        #region Calming
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.CalmingPotion,
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new RecipeVariant(ItemID.Waterleaf, ItemID.Daybloom, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.CalmingPotion,
            new RecipeVariant(ItemID.Damselfish, ItemID.DaybloomSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new RecipeVariant(ItemID.Damselfish, ItemID.DaybloomSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new RecipeVariant(ItemID.WaterleafSeeds, ItemID.DaybloomSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.HarmoniousTonic,
            outputType: ItemID.CalmingPotion,
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            },
            new RecipeVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            });
        #endregion

        #region Featherfall
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.FeatherfallPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Feather, ItemID.Daybloom)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.FeatherfallPotion,
            new RecipeVariant(ItemID.Feather, ItemID.DaybloomSeeds, ItemID.BlinkrootSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BriskTonic,
            outputType: ItemID.FeatherfallPotion,
            new RecipeVariant(ItemID.Feather, ItemID.Daybloom, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            });
        #endregion

        #region Warmth
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.WarmthPotion,
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Shiverthorn)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            },
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Fireblossom)
            {
                OutputStack = 1,
                Potency = 3,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 20)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.WarmthPotion,
            new RecipeVariant(ItemID.FrostMinnow, ItemID.ShiverthornSeeds)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.VitalTonic,
            outputType: ItemID.WarmthPotion,
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Shiverthorn)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 18)
            },
            new RecipeVariant(ItemID.FrostMinnow, ItemID.Fireblossom)
            {
                OutputStack = 1,
                Potency = 3,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 22)
            });
        #endregion

        #region Obsidian Skin
        AlchemyRecipes.CreateRecipe(
        inputType: ItemID.BottledWater,
        outputType: ItemID.ObsidianSkinPotion,
        new RecipeVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        },
        new RecipeVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.HarmoniousTonic,
        outputType: ItemID.ObsidianSkinPotion,
        new RecipeVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        },
        new RecipeVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 6, secs: 30)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
        outputType: ItemID.ObsidianSkinPotion,
        new RecipeVariant(ItemID.Obsidian, ItemID.WaterleafSeeds, ItemID.FireblossomSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new RecipeVariant(ItemID.Obsidian, ItemID.ShiverthornSeeds, ItemID.FireblossomSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 4, secs: 30)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.BottledOil,
        outputType: ItemID.ObsidianSkinPotion,
        new RecipeVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 12)
        },
        new RecipeVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30)
        });
        #endregion

        #region Gravitation
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.GravitationPotion,
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            },
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new RecipeVariant(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 4)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.GravitationPotion,
            new RecipeVariant(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.BlinkrootSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            },
            new RecipeVariant(ItemID.Feather, ItemID.FireblossomSeeds, ItemID.BlinkrootSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            },
            new RecipeVariant(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.FireblossomSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.BriskTonic,
            outputType: ItemID.GravitationPotion,
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new RecipeVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new RecipeVariant(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });
        #endregion

        // Visual Potions

        #region Dangersense
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.TrapsightPotion,
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 3 });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.SpecularTonic,
            outputType: ItemID.TrapsightPotion,
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30) },
            new RecipeVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 5 });
        #endregion

        #region Hunter
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.HunterPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.SpecularTonic,
            outputType: ItemID.HunterPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 9, secs: 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 10) });
        #endregion

        #region Shine

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.ShinePotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.GlowingMushroom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new RecipeVariant(ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.SpecularTonic,
            outputType: ItemID.ShinePotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.GlowingMushroom) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 12) },
            new RecipeVariant(ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15, secs: 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 20) });

        #endregion

        #region Spelunker

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.SpelunkerPotion,
            new RecipeVariant(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new RecipeVariant(Gold, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 4, StrengthPercent = 115 },
            new RecipeVariant(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 12) });

        AlchemyRecipes.CreateRecipe(SolventID.SpecularTonic, ItemID.SpelunkerPotion,
            new RecipeVariant(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 110 },
            new RecipeVariant(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new RecipeVariant(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 3, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 9) },
            new RecipeVariant(Gold, ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = TimeConversion.SetTime(mins: 4) });

        #endregion

        #region Night Owl

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.NightOwlPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new RecipeVariant(ItemID.Sunflower, ItemID.Blinkroot) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.Blinkroot) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.SpecularTonic,
            outputType: ItemID.NightOwlPotion,
            new RecipeVariant(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 12) },
            new RecipeVariant(ItemID.Sunflower, ItemID.Blinkroot) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15, secs: 30) },
            new RecipeVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.Blinkroot) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 20) });

        #endregion

        #region Invisibilty
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.InvisibilityPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 3) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 5) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.SpecularTonic,
            outputType: ItemID.InvisibilityPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30) });
        #endregion

        #region Biome Sight
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.BiomeSightPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.GrassSeeds) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Fireblossom, ItemID.GrassSeeds) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) });

        AlchemyRecipes.CreateRecipe(
            inputType: SolventID.SpecularTonic,
            outputType: ItemID.BiomeSightPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.GrassSeeds) 
            { 
                Potency = 2, 
                DurationTicks = TimeConversion.SetTime(mins: 8) 
            },
            new RecipeVariant(ItemID.Blinkroot, ItemID.Fireblossom, ItemID.GrassSeeds) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30) });
        #endregion

        // Fishing Potions

        #region Sonar
        AlchemyRecipes.CreateRecipe(
        inputType: ItemID.BottledWater,
        outputType: ItemID.SonarPotion,
        new RecipeVariant(ItemID.SpecularFish, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.SpecularTonic,
        outputType: ItemID.SonarPotion,
        new RecipeVariant(ItemID.Coral, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        },
        new RecipeVariant(ItemID.SpecularFish, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
        outputType: ItemID.SonarPotion,
        new RecipeVariant(ItemID.Coral, ItemID.WaterleafSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 11)
        },
        new RecipeVariant(ItemID.SpecularFish, ItemID.WaterleafSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 11, secs: 30)
        });
        #endregion

        #region Fishing
        AlchemyRecipes.CreateRecipe(
        inputType: ItemID.BottledWater,
        outputType: ItemID.FishingPotion,
        new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.FortunateTonic,
        outputType: ItemID.FishingPotion,
        new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        },
        new RecipeVariant(ItemID.Worm, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30)
        },
        new RecipeVariant(ItemID.EnchantedNightcrawler, ItemID.Waterleaf)
        {
            Potency = 4,
            StrengthPercent = 130,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf, ItemID.Worm)
        {
            Potency = 2,
            StrengthPercent = 105,
            DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30)
        });

        AlchemyRecipes.CreateRecipe(
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.FishingPotion,
            new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds)
            {
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 11)
            },
            new RecipeVariant(ItemID.Worm, ItemID.WaterleafSeeds)
            {
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 11, secs: 30)
            },
            new RecipeVariant(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds, ItemID.Worm)
            {
                Potency = 1,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 14)
            });
        #endregion

        #region Crate Potion
        AlchemyRecipes.CreateRecipe(
        inputType: ItemID.BottledWater,
        outputType: ItemID.CratePotion,
        new RecipeVariant(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.FortunateTonic,
        outputType: ItemID.CratePotion,
        new RecipeVariant(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf)
        {
            Potency = 1,
            StrengthPercent = 105,
            DurationTicks = TimeConversion.SetTime(mins: 6, secs: 45)
        },
        new RecipeVariant(ItemID.Amber, ItemID.Moonglow, Gold)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        },
        new RecipeVariant(ItemID.Amber, ShiverthornOrWaterleaf, ItemID.Bass)
        {
            Potency = 4,
            StrengthPercent = 150,
            DurationTicks = TimeConversion.SetTime(mins: 2, secs: 30)
        });
        #endregion

        // Luck Potions

        #region Lesser Luck
        AlchemyRecipes.CreateRecipe(
        inputType: ItemID.BottledWater,
        outputType: ItemID.LuckPotionLesser,
        new RecipeVariant(ItemID.LadyBug, ItemID.Waterleaf, ItemID.WhitePearl)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new RecipeVariant(ItemID.LadyBug, ItemID.Daybloom, ItemID.WhitePearl)
        {
            Potency = 1,
            StrengthPercent = 95,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.FortunateTonic,
        outputType: ItemID.LuckPotionLesser,
        new RecipeVariant(ItemID.Waterleaf, ItemID.WhitePearl)
        {
            Potency = 2,
            StrengthPercent = 108,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new RecipeVariant(ItemID.Daybloom, ItemID.WhitePearl)
        {
            Potency = 2,
            StrengthPercent = 102,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });
        #endregion

        #region Luck
        AlchemyRecipes.CreateRecipe(
        inputType: ItemID.BottledWater,
        outputType: ItemID.LuckPotion,
        new RecipeVariant(ItemID.LadyBug, ItemID.Waterleaf, ItemID.BlackPearl)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        },
        new RecipeVariant(ItemID.LadyBug, ItemID.Daybloom, ItemID.BlackPearl)
        {
            Potency = 1,
            StrengthPercent = 95,
            DurationTicks = TimeConversion.SetTime(mins: 11)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.FortunateTonic,
        outputType: ItemID.LuckPotion,
        new RecipeVariant(ItemID.Waterleaf, ItemID.BlackPearl)
        {
            Potency = 2,
            StrengthPercent = 108,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        },
        new RecipeVariant(ItemID.Daybloom, ItemID.BlackPearl)
        {
            Potency = 2,
            StrengthPercent = 102,
            DurationTicks = TimeConversion.SetTime(mins: 11)
        });
        #endregion

        #region Greater Luck
        AlchemyRecipes.CreateRecipe(
        inputType: ItemID.BottledWater,
        outputType: ItemID.LuckPotionGreater,
        new RecipeVariant(ItemID.LadyBug, ItemID.Waterleaf, ItemID.PinkPearl)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 15)
        },
        new RecipeVariant(ItemID.LadyBug, ItemID.Daybloom, ItemID.PinkPearl)
        {
            Potency = 1,
            StrengthPercent = 95,
            DurationTicks = TimeConversion.SetTime(mins: 16)
        });

        AlchemyRecipes.CreateRecipe(
        inputType: SolventID.FortunateTonic,
        outputType: ItemID.LuckPotionGreater,
        new RecipeVariant(ItemID.Waterleaf, ItemID.PinkPearl)
        {
            Potency = 2,
            StrengthPercent = 108,
            DurationTicks = TimeConversion.SetTime(mins: 15)
        },
        new RecipeVariant(ItemID.Daybloom, ItemID.PinkPearl)
        {
            Potency = 2,
            StrengthPercent = 102,
            DurationTicks = TimeConversion.SetTime(mins: 16)
        });
        #endregion

        Potion_Aug();
    }

    private static void Potion_Aug()
    {
        // Ironskin
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.IronskinPotion,
            outputType: ItemID.IronskinPotion,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.BoneAsh, 5))
            {
                Augment = b => b.Add(BuffID.Wrath, potency: 1, durationTicks: TimeConversion.SetTime(secs: 25))
        });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.IronskinPotion,
            outputType: ItemID.IronskinPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.IronOre, 3))
            {
                Augment = b => b.Add(BuffID.Endurance, potency: 1, durationTicks: TimeConversion.SetTime(mins: 2))
        });

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.IronskinPotion,
            outputType: ItemID.IronskinPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.GoldOre, 3))
            {
                Augment = b => b.Add(BuffID.Endurance, potency: 3, durationTicks: TimeConversion.SetTime(mins: 1))
        });

        // Regen
        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.RegenerationPotion,
            outputType: ItemID.RegenerationPotion,
            new RecipeVariant(ComponentMatcher.Of(ItemID.FallenStar, 1))
            {
                Augment = b => b.Add(BuffID.ManaRegeneration, potency: 1, durationTicks: TimeConversion.SetTime(secs: 45))
            },
            new RecipeVariant(ComponentMatcher.Of(ItemID.FallenStar, 2))
            {
                Augment = b => b.Add(BuffID.ManaRegeneration, potency: 2, durationTicks: TimeConversion.SetTime(mins: 1, secs: 20))
            },
            new RecipeVariant(ComponentMatcher.Of(ItemID.FallenStar, 3))
            {
                Augment = b => b.Add(BuffID.ManaRegeneration, potency: 3, durationTicks: TimeConversion.SetTime(mins: 2))
        });
    }
    /// <summary>Spirit Reforged recipes, buffs and effect behaviours.</summary>
    private static void RegisterSpiritReforged()
    {
        ModContent.TryFind("SpiritReforged", "RemedyPotion", out ModItem RemedyPotion);
        ModContent.TryFind("SpiritReforged", "DoubleJumpPotion", out ModItem ZephyrPotion);
        ModContent.TryFind("SpiritReforged", "FlightPotion", out ModItem SoaringPotion);
        ModContent.TryFind("SpiritReforged", "QuenchPotion", out ModItem QuenchPotion);
        ModContent.TryFind("SpiritReforged", "GarItem", out ModItem GarFish);
        ModContent.TryFind("SpiritReforged", "Cloudstalk", out ModItem Cloudstalk);

        if (ZephyrPotion is null || SoaringPotion is null || Cloudstalk is null)
        {
            TrelamiumIII.Instance.Logger.Warn(
                "SpiritReforged alchemy support skipped: expected content was not found.");
            return;
        }

        #region Effect behaviours
        // Soaring rides SpiritReforged's FlightPotionBuff, whose id only exists at runtime. Their buff adds 0.25 to
        // FlightPlayer.wingTimeMult; the brew adds its delta on top.
        if (ModContent.TryFind("SpiritReforged", "FlightPotionBuff", out ModBuff flightBuff))
        {
            EffectBehaviors.Register(flightBuff.Type,
                new CrossModStatBehavior("SpiritReforged", "FlightPlayer", "wingTimeMult",
                    vanillaValue: 0.25f, noun: "wing flight time"));
        }

        // Quench has no accumulator to add to: their +25% buff duration is a literal inside a private
        // handler on BuffPlayer.ModifyBuffTime. QuenchHook subscribes alongside it and rescales.
        if (ModContent.TryFind("SpiritReforged", "QuenchPotion_Buff", out ModBuff quenchBuff)
            && QuenchHook.TryHook(quenchBuff.Type, vanillaBonus: 0.25f))
        {
            EffectBehaviors.Register(quenchBuff.Type, new QuenchBehavior(vanillaDurationBonus: 0.25f));
        }
        #endregion

        #region Zephyr Potion
        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        ZephyrPotion.Type,
        new RecipeVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.Cloud, 1), ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 1,
            DurationTicks = TimeConversion.SetTime(mins: 3)
        },
        new RecipeVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.Cloud, 2), ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 2,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        });

        AlchemyRecipes.CreateRecipe(
        SolventID.BriskTonic,
        ZephyrPotion.Type,
        new RecipeVariant(Cloudstalk.Type, ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 2,
            DurationTicks = TimeConversion.SetTime(mins: 4, secs: 30)
        },
        new RecipeVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.Cloud, 1), ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 3,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });
        #endregion

        #region Soaring Potion
        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        SoaringPotion.Type,
        new RecipeVariant(Cloudstalk.Type, ItemID.SoulofFlight, ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        },
        new RecipeVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new RecipeVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 118,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });

        AlchemyRecipes.CreateRecipe(
       SolventID.BriskTonic,
        SoaringPotion.Type,
        new RecipeVariant(Cloudstalk.Type, ItemID.SoulofFlight, ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 112,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        },
        new RecipeVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 115,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new RecipeVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 5,
            StrengthPercent = 120,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });
        #endregion

        #region Remedy Potion
        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        RemedyPotion.Type,
        new RecipeVariant(ItemID.Stinger, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        });

        AlchemyRecipes.CreateRecipe(
        SolventID.VitalTonic,
        RemedyPotion.Type,
        new RecipeVariant(ItemID.Stinger, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 115,
            DurationTicks = TimeConversion.SetTime(mins: 12)
        });

        AlchemyRecipes.CreateRecipe(
        ModContent.ItemType<MagnoliaSolutionItem>(),
        RemedyPotion.Type,
        new RecipeVariant(ItemID.Stinger, ItemID.WaterleafSeeds, ItemID.BlinkrootSeeds)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 75,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        });
        #endregion

        #region Quench Potion
        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        QuenchPotion.Type,
        new RecipeVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(secs: 45)
        },
        new RecipeVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(secs: 45)
        });

        AlchemyRecipes.CreateRecipe(
        SolventID.VitalTonic,
        QuenchPotion.Type,
        new RecipeVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 1)
        },
        new RecipeVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 1)
        },
        new RecipeVariant(GarFish.Type, ItemID.Waterleaf, Gold)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 125,
            DurationTicks = TimeConversion.SetTime(secs: 20)
        },
        new RecipeVariant(GarFish.Type, ItemID.Waterleaf, ItemID.SoulofLight)
        {
            OutputStack = 1,
            Potency = 4,
            StrengthPercent = 125,
            DurationTicks = TimeConversion.SetTime(secs: 30)
        });
        #endregion
    }

    /// <summary>Miscellaneous craftable items (e.g. unholy water, love potion).</summary>
    private static void RegisterMisc()
    {
        #region Synthesis: Misc
        AlchemyRecipes.CreateRecipe(
            ItemID.BottledWater,
            ItemID.UnholyWater,
            new RecipeVariant(CorruptTiles, ItemID.CorruptSeeds) { OutputStack = 4 },
            new RecipeVariant(ItemID.VileMushroom, AnyDirt, ItemID.Worm) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(
            ItemID.BottledWater,
            ItemID.BloodWater,
            new RecipeVariant(BloodTiles, ItemID.CrimsonSeeds) { OutputStack = 4 },
            new RecipeVariant(ItemID.ViciousMushroom, AnyDirt, ItemID.Worm) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(
            ItemID.BottledWater,
            ItemID.HolyWater,
            new RecipeVariant(ItemID.PixieDust, ItemID.HallowedSeeds) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(
            ItemID.BottledWater,
            ItemID.LovePotion,
            new RecipeVariant(ItemID.LifeCrystal, ItemID.FallenStar) { OutputStack = 4 },
            new RecipeVariant(ItemID.PrincessFish, ItemID.Shiverthorn) { OutputStack = 8 });

        AlchemyRecipes.CreateRecipe(
            ItemID.BottledWater,
            ItemID.StinkPotion,
            new RecipeVariant(ItemID.Stinkfish, ItemID.Deathweed) { OutputStack = 2 },
            new RecipeVariant(ItemID.Stinkfish, ItemID.DeathweedSeeds) { OutputStack = 1 });

        AlchemyRecipes.CreateRecipe(
            ItemID.Bottle,
            SolventID.BloodtoothExtract,
            new RecipeVariant(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyRecipes.CreateRecipe(
            ItemID.BottledWater,
            SolventID.BloodtoothExtract,
            new RecipeVariant(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyRecipes.CreateRecipe(
            SolventID.Inert,
            SolventID.BloodtoothExtract,
            new RecipeVariant(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyRecipes.CreateRecipe(
            ItemID.Bottle,
            ItemID.ViciousPowder,
            new RecipeVariant(ItemID.ViciousMushroom)
            {
                OutputStack = 5,

                CustomOutput = () =>
                {
                    Item bottle = new();
                    bottle.SetDefaults(ItemID.Bottle);

                    bottle.GetGlobalItem<ContainerBottleItem>().StoredItemType =
                        ItemID.ViciousPowder;

                    return bottle;
                }
            });

        AlchemyRecipes.CreateRecipe(
            ItemID.Bottle,
            ItemID.VilePowder,
            new RecipeVariant(ItemID.VileMushroom)
            {
                OutputStack = 5,

                CustomOutput = () =>
                {
                    Item bottle = new();
                    bottle.SetDefaults(ItemID.Bottle);

                    bottle.GetGlobalItem<ContainerBottleItem>().StoredItemType =
                        ItemID.VilePowder;

                    return bottle;
                }
            });

        AlchemyRecipes.CreateRecipe(ItemID.Bottle, ModContent.ItemType<RustedNugget>(),
            new RecipeVariant(ItemID.IronOre)
            {
                CustomOutput = () =>
                {
                    Item bottle = new();
                    bottle.SetDefaults(ItemID.Bottle);

                    bottle.GetGlobalItem<ContainerBottleItem>().StoredItemType =
                        ModContent.ItemType<RustedNugget>();

                    return bottle;
                }
            });

        #endregion
    }

    public override void Unload()
    {
        base.Unload();

        AlchemyRecipes.Clear();
        AlchemyIngredients.Clear();
        AlmanacEntryRegistry.Clear();
        AlchemyRecipeGroups.Clear();
        EffectBehaviors.ClearRuntime();   // cross-mod entries hold another mod's types
        QuenchHook.Unhook();              // ...and this one lives on another mod's static event

    }

    private static void InfusionRecipes()
    {
        ReadOnlySpan<ChemicalQuality> flags =
        [
            ChemicalQuality.Inert, ChemicalQuality.Catalytic,
            ChemicalQuality.Alkaline, ChemicalQuality.Acidic,
            ChemicalQuality.Volatile, ChemicalQuality.Kinetic,
        ];

        foreach (var flag in flags)
        {
            var group = new AlchemyComponentGroup($"Infuse{flag}");
            foreach (var entry in AlmanacEntryRegistry.All)
            {
                // Base containers can't infuse themselves.
                if (entry.ItemType is ItemID.BottledWater or ItemID.Bottle or ItemID.BottledHoney)
                    continue;
                if ((entry.Chemical & flag) != 0)
                    group.Add(entry.ItemType);
            }
            if (group.Count == 0) continue;

            AlchemyRecipes.CreateRecipe(
                inputType: ItemID.BottledWater,
                outputType: ItemID.BottledWater,
                new RecipeVariant(group) { OutputInfusion = flag });
        }

        AlchemyRecipes.CreateRecipe(
            inputType: ItemID.BottledWater,
            outputType: ItemID.SwiftnessPotion,
            new RecipeVariant(ItemID.Blinkroot, ItemID.Cactus) { InputInfusion = ChemicalQuality.Inert, Potency = 2 });
    }

    /// <summary>Calamity Mod recipes.</summary>
    private static void RegisterCalamity()
    {
        ModContent.TryFind("CalamityMod", "BloodOrb", out ModItem bloodOrb);
        ModContent.TryFind("CalamityMod", "EssenceofSunlight", out ModItem EssenceofSunlight);
        ModContent.TryFind("CalamityMod", "PhotosynthesisPotion", out ModItem PhotosynthesisPotion);
        ModContent.TryFind("CalamityMod", "AnechoicCoating", out ModItem AnechoicCoating);
        ModContent.TryFind("CalamityMod", "AureusCell", out ModItem AureusCell);
        ModContent.TryFind("CalamityMod", "StarblightSoot", out ModItem StarblightSoot);
        ModContent.TryFind("CalamityMod", "AstralInjection", out ModItem AstralInjection);
        ModContent.TryFind("CalamityMod", "BoundingPotion", out ModItem BoundingPotion);
        ModContent.TryFind("CalamityMod", "AncientBoneDust", out ModItem AncientBoneDust);
        ModContent.TryFind("CalamityMod", "CalciumPotion", out ModItem CalciumPotion);
        ModContent.TryFind("CalamityMod", "DarkPlasma", out ModItem DarkPlasma);
        ModContent.TryFind("CalamityMod", "CeaselessHungerPotion", out ModItem CeaselessHungerPotion);
        ModContent.TryFind("CalamityMod", "GravityNormalizerPotion", out ModItem GravityNormalizerPotion);
        ModContent.TryFind("CalamityMod", "AldebaranAlewife", out ModItem AldebaranAlewife);
        ModContent.TryFind("CalamityMod", "ShadowPotion", out ModItem ShadowPotion);
        ModContent.TryFind("CalamityMod", "Shadowfish", out ModItem Shadowfish);
        ModContent.TryFind("CalamityMod", "SunkenSailfish", out ModItem SunkenSailfish);
        ModContent.TryFind("CalamityMod", "SoaringPotion", out ModItem SoaringPotion);
        ModContent.TryFind("CalamityMod", "SulphurousSand", out ModItem SulphurousSand);
        ModContent.TryFind("CalamityMod", "SulphurskinPotion", out ModItem SulphurskinPotion);
        ModContent.TryFind("CalamityMod", "PurifiedGel", out ModItem PurifiedGel);
        ModContent.TryFind("CalamityMod", "PotionofOmniscience", out ModItem PotionofOmniscience);
        ModContent.TryFind("CalamityMod", "ZergPotion", out ModItem ZergPotion);
        ModContent.TryFind("CalamityMod", "ZenPotion", out ModItem ZenPotion);

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        PhotosynthesisPotion.Type,
        new RecipeVariant(EssenceofSunlight.Type, bloodOrb.Type)
        {
            OutputStack = 1,
            Potency = 3,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        },
        new RecipeVariant(EssenceofSunlight.Type, ItemID.Daybloom) { OutputStack = 1 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        AnechoicCoating.Type,
        new RecipeVariant(bloodOrb.Type) { OutputStack = 1 });

        AlchemyRecipes.CreateRecipe(
        ItemID.SuperManaPotion,
        AstralInjection.Type,
        new RecipeVariant(StarblightSoot.Type, AureusCell.Type) { OutputStack = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        BoundingPotion.Type,
        new RecipeVariant(ItemID.PinkGel, ItemID.Moonglow) { OutputStack = 1 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        CalciumPotion.Type,
        new RecipeVariant(AncientBoneDust.Type) { OutputStack = 1 },
        new RecipeVariant(AncientBoneDust.Type, bloodOrb.Type) { OutputStack = 1, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        CeaselessHungerPotion.Type,
        new RecipeVariant(DarkPlasma.Type) { OutputStack = 1 },
        new RecipeVariant(DarkPlasma.Type, bloodOrb.Type) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        GravityNormalizerPotion.Type,
        new RecipeVariant(AureusCell.Type, bloodOrb.Type) { OutputStack = 1 },
        new RecipeVariant(AldebaranAlewife.Type, AureusCell.Type) { OutputStack = 1, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        ShadowPotion.Type,
        new RecipeVariant(Shadowfish.Type, ItemID.Blinkroot) { OutputStack = 1 },
        new RecipeVariant(Shadowfish.Type, bloodOrb.Type) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        SoaringPotion.Type,
        new RecipeVariant(SunkenSailfish.Type, ItemID.SoulofFlight) { OutputStack = 1 },
        new RecipeVariant(bloodOrb.Type, ItemID.SoulofFlight) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        SulphurskinPotion.Type,
        new RecipeVariant(SulphurousSand.Type, ItemID.Waterleaf) { OutputStack = 1 },
        new RecipeVariant(bloodOrb.Type, ItemID.Waterleaf) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        ZenPotion.Type,
        new RecipeVariant(PurifiedGel.Type, ItemID.Daybloom) { OutputStack = 1 },
        new RecipeVariant(ComponentMatcher.Of(bloodOrb.Type, 5), ComponentMatcher.Of(ItemID.Fireblossom, 1)) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        ZergPotion.Type,
        new RecipeVariant(ComponentMatcher.Of(PurifiedGel.Type, 2), ComponentMatcher.Of(ItemID.Fireblossom, 2)) { OutputStack = 1 },
        new RecipeVariant(ComponentMatcher.Of(bloodOrb.Type, 5), ComponentMatcher.Of(PurifiedGel.Type, 2)) { OutputStack = 2, Potency = 3 });

        AlchemyRecipes.CreateRecipe(
        ItemID.BottledWater,
        PotionofOmniscience.Type,
        new RecipeVariant(ItemID.HunterPotion, ItemID.SpelunkerPotion, ItemID.TrapsightPotion) { OutputStack = 1 });
    }
}

/// <summary>A named set of item types that one <see cref="AlchemyRecipe"/> component slot accepts.</summary>
public sealed class AlchemyComponentGroup
{
    public string Name { get; }

    private readonly HashSet<int> _itemTypes;

    public IReadOnlyCollection<int> ItemTypes => _itemTypes;

    /// <summary>Number of item types in the group. Used by the matcher to prefer specifics over groups.</summary>
    public int Count => _itemTypes.Count;

    public AlchemyComponentGroup(string name, params int[] itemTypes)
    {
        Name = name;
        _itemTypes = new HashSet<int>(itemTypes);
    }

    public AlchemyComponentGroup Add(params int[] itemTypes)
    {
        foreach (var t in itemTypes) _itemTypes.Add(t);
        return this;
    }

    public bool Contains(int itemType) => _itemTypes.Contains(itemType);

    public void Clear() => _itemTypes.Clear();
}

public static class AlchemyRecipeGroups
{
    public static readonly AlchemyComponentGroup AnyOre = new("AnyOre");

    public static readonly AlchemyComponentGroup Critters = new("Critters");

    public static readonly AlchemyComponentGroup Iron = new("Iron");
    public static readonly AlchemyComponentGroup Silver = new("Silver");
    public static readonly AlchemyComponentGroup Gold = new("Gold");
    public static readonly AlchemyComponentGroup SpecularFishOrMoonglow = new("SpecularFishOrMoonglow");

    public static readonly AlchemyComponentGroup AnyEvilOre = new("AnyEvilOre");
    public static readonly AlchemyComponentGroup Mushroom = new("AnyStandardMushroom");
    public static readonly AlchemyComponentGroup AnyEvilMushroom = new("AnyEvilMushroom");
    public static readonly AlchemyComponentGroup CorruptTiles = new("CorruptTiles");
    public static readonly AlchemyComponentGroup BloodTiles = new("BloodTiles");
    public static readonly AlchemyComponentGroup AnyHerb = new("AnyHerb");
    public static readonly AlchemyComponentGroup RottenChunkVertabrae = new("AnyEvilDrop");
    public static readonly AlchemyComponentGroup AnyStandardFish = new("AnyStandardFish");
    public static readonly AlchemyComponentGroup AnySoil = new("AnySoil");
    public static readonly AlchemyComponentGroup AnyDirt = new("AnyDirt");
    public static readonly AlchemyComponentGroup AnyButterfly = new("AnyButterfly");
    public static readonly AlchemyComponentGroup AnyBaseBottle = new("AnyBaseBottle");

    public static readonly AlchemyComponentGroup ShiverthornOrWaterleaf = new("ShiverthornOrWaterleaf");

    // Conventional chemistry. Populated in PopulateRecipeGroups so later recipes can ask for
    // "any acid" rather than naming each one.
    public static readonly AlchemyComponentGroup AnyAcid = new("AnyAcid");
    public static readonly AlchemyComponentGroup AnyAlkali = new("AnyAlkali");
    public static readonly AlchemyComponentGroup AnyMineralSalt = new("AnyMineralSalt");

    public static void Clear()
    {
        AnyAcid.Clear();
        AnyAlkali.Clear();
        AnyMineralSalt.Clear();
        AnyOre.Clear();
        AnyEvilOre.Clear();
        Mushroom.Clear();
        AnyEvilMushroom.Clear();
        AnyHerb.Clear();
        RottenChunkVertabrae.Clear();
        AnySoil.Clear();
        AnyButterfly.Clear();
        SpecularFishOrMoonglow.Clear();
        ShiverthornOrWaterleaf.Clear();
    }
}