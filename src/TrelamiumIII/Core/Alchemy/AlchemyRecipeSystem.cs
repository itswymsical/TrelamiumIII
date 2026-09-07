using TrelamiumIII.Common.Globals.Items;
using TrelamiumIII.Content.Lodestone;
using TrelamiumIII.Utilities;
using Terraria.Localization;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;
using TrelamiumIII.Content.Alchemy.AlchemyStations;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Common.Globals;
using TrelamiumIII.Content.FarmingOverhaul;
using TrelamiumIII.Content.Rusted;
using TrelamiumIII.Core.Alchemy.Brewing;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// The central class for registering recipes and recipe groups for Substrate. 
/// This class is responsible for adding custom recipes, recipe groups, and managing the overall recipe system within the mod and 
/// Base game potion recipes.
/// </summary>
public class AlchemyRecipeSystem : ModSystem
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
        // Any Copper Bar
        RecipeGroup CopperBarRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.CopperBar)}",
                    ItemID.CopperBar, ItemID.TinBar);
        RecipeGroup.RegisterGroup(nameof(ItemID.CopperBar), CopperBarRecipeGroup);

        // Any Silver Bar
        RecipeGroup SilverBarRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.SilverBar)}",
            ItemID.SilverBar, ItemID.TungstenBar);
        RecipeGroup.RegisterGroup(nameof(ItemID.SilverBar), SilverBarRecipeGroup);

        // Any Gold Bar
        RecipeGroup GoldBarRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.GoldBar)}",
            ItemID.GoldBar, ItemID.PlatinumBar);
        RecipeGroup.RegisterGroup(nameof(ItemID.GoldBar), GoldBarRecipeGroup);

        // Any Fish
        RecipeGroup FishRecipeGroup = new(() => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(ItemID.Bass)}", 2290, 2297, 2298, 2299, 2300, 2301, 2302, 2303, 2304, 2305, 2306, 2307,
            2308, 2309, 2310, 2311, 2312, 2313, 2314, 2315, 2316, 2317, 2318, 2319, 2321, 4401, 4402);
        RecipeGroup.RegisterGroup(nameof(ItemID.Bass), FishRecipeGroup);

        // Any Soil Block (Dirt, Mud, Ash)
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

        // Metadata for almanac entry items (chemical type, potency, rarity, etc.)
        AlmanacEntryDefinitions.Register();

        // Solvents/Solutions that are used to craft potions: think like "Awkward Potion" and "Mundane Potion" from Minecraft.
        RegisterSolutions();

        // Random items that make sense to be craftable in the alchemy system (unholy water, holy water).
        RegisterMisc();

        // Vanilla potions and their variants, as well as the recipes to make them.
        RegisterPotions();

        // Chemical weapon class content.
        RegisterWeapons();

        // Conventional chemistry: acids, alkalis and distillates refined from raw minerals.
        RegisterMinerals();

        if (ModLoader.TryGetMod("SpiritReforged", out Mod _ ))
        {
            RegisterSpiritReforged();
        }

        //if (ModLoader.TryGetMod("CalamityMod", out Mod calamityMod))
        //{
        //    RegisterCalamity();
        //}
        PopulateRecipeGroups();
    }

    private static void PopulateRecipeGroups()
    {
        AnyAcid.Add(
            ModContent.ItemType<OilOfVitriolItem>(), ModContent.ItemType<AquaFortisItem>(),
            ModContent.ItemType<SpiritOfSaltItem>(), ModContent.ItemType<AquaRegiaItem>(),
            ModContent.ItemType<BlueVitriolItem>());

        AnyAlkali.Add(
            ModContent.ItemType<SlakedLimeItem>(), ModContent.ItemType<CausticLyeItem>(),
            ModContent.ItemType<QuicklimeItem>(), ModContent.ItemType<PotashItem>());

        AnyMineralSalt.Add(
            ModContent.ItemType<RockSaltItem>(), ModContent.ItemType<NiterItem>(),
            ModContent.ItemType<FrigorificSaltItem>());

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

        // Cross-mod herbs, resolved through the flower registry so a bad internal name gets reported
        // once rather than silently vanishing. Nothing is added on a vanilla-only load, leaving the
        // group as just the seven vanilla herbs.
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

        AnyBaseBottle.Add(ItemID.Bottle, ItemID.BottledWater, ModContent.ItemType<InertSolutionItem>());

        SpecularFishOrMoonglow.Add(ItemID.SpecularFish, ItemID.Moonglow);
    }

    private static void RegisterWeapons()
    {
        //AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
        //    ModContent.ItemType<BloodtoothExtractItem>(), 
        //    ModContent.ItemType<CrimsonEffusionFlaskItem>(),
        //    new AlchemyVariant(ItemID.ViciousPowder, ItemID.Deathweed) { OutputStack = 8 },
        //    new AlchemyVariant(ItemID.Deathweed) { OutputStack = 4 });

        //AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
        //    ModContent.ItemType<DeadSolutionItem>(),
        //    ModContent.ItemType<CrimsonEffusionFlaskItem>(),
        //    new AlchemyVariant(ItemID.ViciousPowder, ItemID.Deathweed, ModContent.ItemType<BloodtoothFungusItem>()) { OutputStack = 8 },
        //    new AlchemyVariant(ItemID.Deathweed, ModContent.ItemType<BloodtoothFungusItem>()) { OutputStack = 4 },
        //    new AlchemyVariant(ItemID.ViciousPowder, ModContent.ItemType<BloodtoothFungusItem>()) { OutputStack = 4 });

        //AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
        //    ModContent.ItemType<DeadSolutionItem>(),
        //    ModContent.ItemType<CorruptedSporeFlaskItem>(),
        //    new AlchemyVariant(ItemID.VilePowder, ItemID.Deathweed) { OutputStack = 4, Potency = 1 },
        //    new AlchemyVariant(ItemID.Deathweed, ItemID.RottenChunk) { OutputStack = 4, Potency = 1 },
        //    new AlchemyVariant(ItemID.VilePowder, ItemID.RottenChunk, ItemID.Deathweed) { OutputStack = 8, Potency = 1 });

        //AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
        //    ModContent.ItemType<BaneTonicItem>(),
        //    ModContent.ItemType<CorruptedSporeFlaskItem>(),
        //    new AlchemyVariant(ItemID.VilePowder, ItemID.Deathweed) { OutputStack = 8, Potency = 3 },
        //    new AlchemyVariant(ItemID.Deathweed) { OutputStack = 4, Potency = 3});

        //AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
        //    ItemID.BottledWater,
        //    ModContent.ItemType<HyperboreanFlaskItem>(),
        //    new AlchemyVariant(ItemID.Shiverthorn, ItemID.CopperOre, ModContent.ItemType<MagnoliaItem>()) { OutputStack = 8 },
        //    new AlchemyVariant(ItemID.Shiverthorn, ModContent.ItemType<MagnoliaItem>()) { OutputStack = 4 },
        //    new AlchemyVariant(ItemID.Shiverthorn, ItemID.CopperOre) { OutputStack = 3 });

        //AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
        //    ModContent.ItemType<BottledOilItem>(),
        //    ModContent.ItemType<ThermiteFlaskItem>(),
        //    new AlchemyVariant(ItemID.Fireblossom, ModContent.ItemType<OxidizedIronItem>(), ModContent.ItemType<CharcoalItem>()) { OutputStack = 8 },
        //    new AlchemyVariant(ItemID.Fireblossom, ModContent.ItemType<OxidizedIronItem>()) { OutputStack = 4 },
        //    new AlchemyVariant(ItemID.Fireblossom, ModContent.ItemType<CharcoalItem>()) { OutputStack = 4 });
    }

    /// <summary>
    /// The foundational recipes that make most of the other recipes possible.
    /// </summary>
    /// <summary>
    /// The conventional chemistry line: acids, alkalis and distillates refined at the station
    /// from the raw minerals in <c>Content/Alchemy/Reagents/MineralReagents.cs</c>, which are
    /// themselves roasted out of vanilla materials at a furnace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Oil of Vitriol is the hub -- four of these recipes draw on it, which is what makes the
    /// trip to the Underworld for sulfur worth taking.
    /// </para>
    /// <para>
    /// Star ratings for these live on <see cref="AlmanacEntry.Potency"/>, not on
    /// <see cref="AlchemyVariant.Potency"/>: the latter stamps brew strength onto buff-potion
    /// outputs and must stay 0 for plain reagents. Design record:
    /// <c>docs/Alchemy/ConventionalReagents.md</c>.
    /// </para>
    /// </remarks>
    private static void RegisterMinerals()
    {
        #region Acids -- the Sulfur line

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<OilOfVitriolItem>(),
            new AlchemyVariant(
                ComponentMatcher.Of(ModContent.ItemType<SulfurItem>(), 2),
                ModContent.ItemType<NiterItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<OilOfVitriolItem>(),
            outputType: ModContent.ItemType<AquaFortisItem>(),
            new AlchemyVariant(ComponentMatcher.Of(ModContent.ItemType<NiterItem>(), 2)));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<OilOfVitriolItem>(),
            outputType: ModContent.ItemType<SpiritOfSaltItem>(),
            new AlchemyVariant(ComponentMatcher.Of(ModContent.ItemType<RockSaltItem>(), 2)));

        // The one that eats gold. Both of the strong acids, and nothing else.
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<AquaFortisItem>(),
            outputType: ModContent.ItemType<AquaRegiaItem>(),
            new AlchemyVariant(ModContent.ItemType<SpiritOfSaltItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<OilOfVitriolItem>(),
            outputType: ModContent.ItemType<BlueVitriolItem>(),
            new AlchemyVariant(ComponentMatcher.Of(ModContent.ItemType<OxidizedCopperItem>(), 2)));

        #endregion

        #region Alkalis -- the Salt line

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<SlakedLimeItem>(),
            new AlchemyVariant(ComponentMatcher.Of(ModContent.ItemType<QuicklimeItem>(), 2))
            { OutputStack = 2 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<CausticLyeItem>(),
            new AlchemyVariant(
                ComponentMatcher.Of(ModContent.ItemType<PotashItem>(), 2),
                ModContent.ItemType<QuicklimeItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ModContent.ItemType<FrigorificSaltItem>(),
            new AlchemyVariant(
                ModContent.ItemType<NiterItem>(),
                ModContent.ItemType<RockSaltItem>(),
                ComponentMatcher.Of(ItemID.IceBlock, 5)));

        #endregion

        #region Distillates and powders

        // One barrel of oil splits into the light fraction and the residue. Registered as two
        // recipes over the same solvent rather than one recipe with two outputs.
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<BottledOilItem>(),
            outputType: ModContent.ItemType<SpiritOfOilItem>(),
            new AlchemyVariant(ComponentMatcher.Of(ModContent.ItemType<CharcoalItem>(), 2)));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<BottledOilItem>(),
            outputType: ModContent.ItemType<PitchItem>(),
            new AlchemyVariant(ComponentMatcher.Of(ModContent.ItemType<FibrousSlurryItem>(), 2))
            { OutputStack = 2 });

        // Six parts niter to one of fuel, as it should be.
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ModContent.ItemType<BlackPowderItem>(),
            new AlchemyVariant(
                ComponentMatcher.Of(ModContent.ItemType<NiterItem>(), 6),
                ModContent.ItemType<SulfurItem>(),
                ModContent.ItemType<CharcoalItem>())
            { OutputStack = 4 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Alchemy,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ModContent.ItemType<ColdFireItem>(),
            new AlchemyVariant(
                ComponentMatcher.Of(ModContent.ItemType<BoneAshItem>(), 2),
                ModContent.ItemType<CharcoalItem>(),
                ComponentMatcher.Of(ItemID.SandBlock, 5)));

        #endregion
    }

    private static void RegisterSolutions()
    {
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<InertSolutionItem>(),
            new AlchemyVariant(Mushroom));

        #region Inert-bases
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ModContent.ItemType<HarmoniousTonicItem>(),
            new AlchemyVariant(ItemID.CopperOre),
            new AlchemyVariant(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<BriskTonicItem>(),
            new AlchemyVariant(ModContent.ItemType<LodestoneItem>()) { OutputStack = 2 },
            new AlchemyVariant(ItemID.TinOre) { OutputStack = 2 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<VitalTonicItem>(),
            new AlchemyVariant(ItemID.GoldOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ModContent.ItemType<SpecularTonicItem>(),
            new AlchemyVariant(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<SpecularTonicItem>(),
            new AlchemyVariant(ItemID.SilverOre),
            new AlchemyVariant(ItemID.SpecularFish) { OutputStack = 4});

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<MartialTonicItem>(),
            new AlchemyVariant(ItemID.IronOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<FortunateTonicItem>(),
            new AlchemyVariant(ItemID.PlatinumOre) { OutputStack = 3 },
            new AlchemyVariant(ItemID.TinOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ModContent.ItemType<BaneTonicItem>(),
            new AlchemyVariant(AnyEvilOre),
            new AlchemyVariant(ItemID.LeadOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<ArcaneSolutionItem>(),
            new AlchemyVariant(ItemID.FallenStar));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<DeadSolutionItem>(),
            new AlchemyVariant(RottenChunkVertabrae));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ModContent.ItemType<InertSolutionItem>(), 
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new AlchemyVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new AlchemyVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new AlchemyVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new AlchemyVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new AlchemyVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new AlchemyVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });
        #endregion

        #region Water-bases
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<HarmoniousTonicItem>(),
            new AlchemyVariant(ItemID.CopperOre),
            new AlchemyVariant(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<BriskTonicItem>(),
            new AlchemyVariant(ModContent.ItemType<LodestoneItem>()) { OutputStack = 2 },
            new AlchemyVariant(ItemID.TinOre) { OutputStack = 2 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<VitalTonicItem>(),
            new AlchemyVariant(ItemID.GoldOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<SpecularTonicItem>(),
            new AlchemyVariant(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<SpecularTonicItem>(),
            new AlchemyVariant(ItemID.SilverOre),
            new AlchemyVariant(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<MartialTonicItem>(),
            new AlchemyVariant(ItemID.IronOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<FortunateTonicItem>(),
            new AlchemyVariant(ItemID.PlatinumOre) { OutputStack = 3 },
            new AlchemyVariant(ItemID.TinOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<BaneTonicItem>(),
            new AlchemyVariant(AnyEvilOre),
            new AlchemyVariant(ItemID.LeadOre));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<ArcaneSolutionItem>(),
            new AlchemyVariant(ItemID.FallenStar));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<DeadSolutionItem>(),
            new AlchemyVariant(RottenChunkVertabrae));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new AlchemyVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new AlchemyVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new AlchemyVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            new AlchemyVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new AlchemyVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new AlchemyVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });
        #endregion
    }

    /// <summary>
    /// Registers recipes for the vanilla potions, including their inert variants.
    /// </summary>
    private static void RegisterPotions()
    {
        // Actual Buff Potions

        #region Ironskin
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, 
            inputType: ItemID.BottledWater, 
            outputType: ItemID.IronskinPotion,
            new AlchemyVariant(Iron, ItemID.Daybloom) { Potency = 1 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ItemID.IronskinPotion,
            new AlchemyVariant(Iron, ItemID.Daybloom) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MartialTonicItem>(),
            outputType: ItemID.IronskinPotion,
            new AlchemyVariant(ItemID.Daybloom) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new AlchemyVariant(Iron, ItemID.Daybloom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 6) });

        // -- Magnolia Variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.IronskinPotion,
            new AlchemyVariant(ItemID.DaybloomSeeds, Iron)
            {
                OutputStack = 4,
                Potency = 2,
                StrengthPercent = 95,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });
        #endregion  

        #region Swiftness
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.SwiftnessPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 1 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ItemID.SwiftnessPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2, StrengthPercent = 115 },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Feather) { Potency = 2},
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Cactus, ItemID.Feather) { Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<BriskTonicItem>(),
            outputType: ItemID.SwiftnessPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Cactus) { Potency = 2 },
            new AlchemyVariant(ItemID.Feather) { Potency = 1 },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Feather) { Potency = 3 },
            new AlchemyVariant(ItemID.Cactus, ItemID.Blinkroot, ItemID.Feather) { OutputStack = 3, Potency = 5 },
            new AlchemyVariant(ItemID.Sunflower) { OutputStack = 2, Potency = 1, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 7) },
            new AlchemyVariant(ItemID.Sunflower, ItemID.Feather) { OutputStack = 2, Potency = 3, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new AlchemyVariant(ItemID.Sunflower, ItemID.Blinkroot, ItemID.Feather) { OutputStack = 5, Potency = 5, StrengthPercent = 115, DurationTicks = TimeConversion.SetTime(mins: 4) });
        
        // -- Magnolia Variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.SwiftnessPotion,
            new AlchemyVariant(ItemID.BlinkrootSeeds, ItemID.Cactus) 
            { 
                OutputStack = 4, 
                Potency = 2, 
                StrengthPercent = 95, 
                DurationTicks = TimeConversion.SetTime(mins: 10) 
            });
        #endregion

        #region Regeneration
        // Regeneration Variants
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.RegenerationPotion,
            new AlchemyVariant(ItemID.Daybloom, Mushroom) { Potency = 1 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<InertSolutionItem>(),
            outputType: ItemID.RegenerationPotion,
            new AlchemyVariant(ItemID.Daybloom, Mushroom) { Potency = 2 },
            new AlchemyVariant(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { Potency = 3 },
            new AlchemyVariant(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 3, StrengthPercent = 130 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<VitalTonicItem>(),
            outputType: ItemID.RegenerationPotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.Mushroom) { Potency = 3 },
            new AlchemyVariant(ItemID.Daybloom, Mushroom, ItemID.Blinkroot) { OutputStack = 2, Potency = 3 },
            new AlchemyVariant(ItemID.Daybloom, Mushroom, ItemID.GoldOre) { OutputStack = 2, Potency = 4, StrengthPercent = 125, DurationTicks = TimeConversion.SetTime(mins: 7) });

        // -- Magnolia Variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.RegenerationPotion,
            new AlchemyVariant(ItemID.DaybloomSeeds, ItemID.Mushroom)
            {
                OutputStack = 4,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        #endregion

        #region Endurance
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.EndurancePotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MartialTonicItem>(),
            outputType: ItemID.EndurancePotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish) { Potency = 2, StrengthPercent = 110, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.ArmoredCavefish, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Daybloom, Iron) { Potency = 3, StrengthPercent = 120, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) });
        #endregion

        #region Wrath
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.WrathPotion,
            new AlchemyVariant(ItemID.Ebonkoi, ItemID.Deathweed) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new AlchemyVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VilePowder) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new AlchemyVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VileMushroom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },

            new AlchemyVariant(ItemID.EbonstoneBlock, ItemID.VileMushroom, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 50, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            },
            new AlchemyVariant(ItemID.EbonstoneBlock, ItemID.VilePowder, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 60, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
           inputType: ModContent.ItemType<MartialTonicItem>(),
           outputType: ItemID.WrathPotion,
           new AlchemyVariant(ItemID.Ebonkoi, ItemID.Deathweed) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 4) },
           new AlchemyVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VilePowder) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) },
           new AlchemyVariant(ItemID.Ebonkoi, ItemID.Deathweed, ItemID.VileMushroom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45) });

        #endregion

        #region Rage
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.RagePotion,
            new AlchemyVariant(ItemID.Hemopiranha, ItemID.Deathweed) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) },
            new AlchemyVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousPowder) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new AlchemyVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousMushroom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },

            new AlchemyVariant(ItemID.CrimstoneBlock, ItemID.ViciousMushroom, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 50, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            },
            new AlchemyVariant(ItemID.CrimstoneBlock, ItemID.ViciousPowder, ItemID.Deathweed)
            {
                Potency = 1,
                StrengthPercent = 60, // pre-hardmode version
                DurationTicks = TimeConversion.SetTime(mins: 4)
            });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
           inputType: ModContent.ItemType<MartialTonicItem>(),
           outputType: ItemID.RagePotion,
           new AlchemyVariant(ItemID.Hemopiranha, ItemID.Deathweed) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 4) },
           new AlchemyVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousPowder) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) },
           new AlchemyVariant(ItemID.Hemopiranha, ItemID.Deathweed, ItemID.ViciousMushroom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45) });

        #endregion

        #region Archery
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.ArcheryPotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.Lens) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });
        #endregion

        #region Battle
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.BattlePotion,
            new AlchemyVariant(ItemID.Deathweed, RottenChunkVertabrae) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 7) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.BattlePotion,
            new AlchemyVariant(RottenChunkVertabrae, ItemID.DeathweedSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 9, secs: 30)
            });
        #endregion

        #region Ammo Reservation
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.AmmoReservationPotion,
            new AlchemyVariant(ItemID.DoubleCod, ItemID.Moonglow) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.AmmoReservationPotion,
            new AlchemyVariant(ItemID.DoubleCod, ItemID.MoonglowSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });
        #endregion

        #region Heartreach
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.HeartreachPotion,
            new AlchemyVariant(ItemID.CrimsonTigerfish, ItemID.Daybloom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.HeartreachPotion,
            new AlchemyVariant(ItemID.CrimsonTigerfish, ItemID.DaybloomSeeds) { 
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10) 
            });
        #endregion

        #region Inferno
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.InfernoPotion,
            new AlchemyVariant(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.Fireblossom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 4) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.InfernoPotion,
            new AlchemyVariant(ItemID.FlarefinKoi, ItemID.Obsidifish, ItemID.FireblossomSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });
        #endregion

        #region Lifeforce
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.LifeforcePotion,
            new AlchemyVariant(ItemID.Prismite, ItemID.Moonglow, ItemID.Shiverthorn) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new AlchemyVariant(ItemID.Prismite, ItemID.Waterleaf, ItemID.Shiverthorn) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new AlchemyVariant(ItemID.Prismite, ItemID.Moonglow, ItemID.Waterleaf) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.LifeforcePotion,
            new AlchemyVariant(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new AlchemyVariant(ItemID.Prismite, ItemID.MoonglowSeeds, ItemID.WaterleafSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new AlchemyVariant(ItemID.Prismite, ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });
        #endregion

        #region Thorns
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.ThornsPotion,
            new AlchemyVariant(ItemID.Deathweed, ItemID.Cactus) { Potency = 1, StrengthPercent = 100, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MartialTonicItem>(),
            outputType: ItemID.ThornsPotion,
            new AlchemyVariant(ItemID.Deathweed, ItemID.Cactus) 
            { 
                Potency = 1, 
                StrengthPercent = 110, 
                DurationTicks = TimeConversion.SetTime(mins: 8) 
            });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.ThornsPotion,
            new AlchemyVariant(ItemID.DeathweedSeeds, ItemID.Cactus)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            });
        #endregion

        #region Titan
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.TitanPotion,
            new AlchemyVariant(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn) 
            { 
                Potency = 1, 
                StrengthPercent = 100, 
                DurationTicks = TimeConversion.SetTime(mins: 8) 
            },
            new AlchemyVariant(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn)
            {
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MartialTonicItem>(),
            outputType: ItemID.TitanPotion,
            new AlchemyVariant(ItemID.Bone, ItemID.Deathweed, ItemID.Shiverthorn)
            {
                Potency = 2,
                StrengthPercent = 110,
                DurationTicks = TimeConversion.SetTime(mins: 8)
            },
            new AlchemyVariant(ItemID.IronOre, ItemID.Deathweed, ItemID.Shiverthorn)
            {
                Potency = 2,
                StrengthPercent = 110,
                DurationTicks = TimeConversion.SetTime(mins: 6, secs: 30)
            });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.TitanPotion,
            new AlchemyVariant(ItemID.Bone, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 85,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new AlchemyVariant(ItemID.IronOre, ItemID.DeathweedSeeds, ItemID.ShiverthornSeeds)
            {
                Potency = 1,
                StrengthPercent = 75,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });
        #endregion

        // Utility Potions

        #region Restoration & Healing
        // Restoration Variants
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<VitalTonicItem>(),
            outputType: ItemID.RestorationPotion,
            new AlchemyVariant(ItemID.Daybloom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Augmentation,
            inputType: ItemID.RestorationPotion,
            outputType: ItemID.RestorationPotion,
            new AlchemyVariant(Mushroom, ItemID.Daybloom) 
            { 
                OutputStack = 1, 
                Potency = 3, 
                StrengthPercent = 105,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20)) 
            });

        // Healing Potions
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.Bottle,
            outputType: ItemID.LesserHealingPotion,
            new AlchemyVariant(Mushroom, ItemID.Gel) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<VitalTonicItem>(),
            outputType: ItemID.LesserHealingPotion,
            new AlchemyVariant(Mushroom) { OutputStack = 4, Potency = 1, StrengthPercent = 100 },
            new AlchemyVariant(Mushroom, ItemID.Gel) { OutputStack = 4, Potency = 2, StrengthPercent = 107 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Augmentation,
            inputType: ItemID.LesserHealingPotion,
            outputType: ItemID.LesserHealingPotion,
            new AlchemyVariant(ItemID.Daybloom) 
            { 
                OutputStack = 2, 
                Potency = 1, 
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20))
            },
            new AlchemyVariant(ItemID.Blinkroot)
            {
                OutputStack = 2,
                Potency = 2,
                StrengthPercent = 120
            });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Augmentation,
            inputType: ItemID.LesserHealingPotion,
            outputType: ItemID.HealingPotion,
            new AlchemyVariant(ItemID.GlowingMushroom) { OutputStack = 2, Potency = 1, StrengthPercent = 100 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Augmentation,
            inputType: ItemID.HealingPotion,
            outputType: ItemID.HealingPotion,
            new AlchemyVariant(ItemID.Daybloom) 
            { 
                OutputStack = 2, 
                Potency = 1, 
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20))
            });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Augmentation,
            inputType: ItemID.HealingPotion,
            outputType: ItemID.HealingPotion,
            new AlchemyVariant(ItemID.Daybloom)
            {
                OutputStack = 2,
                Potency = 1,
                StrengthPercent = 100,
                Augment = b => b.Add(BuffID.Regeneration, potency: 2, durationTicks: TimeConversion.SetTime(secs: 20))
            });
        #endregion

        #region Mining
        // Mining Potion
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, ItemID.BottledWater, ItemID.MiningPotion,
            new AlchemyVariant(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new AlchemyVariant(Silver, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105 },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 12) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, ModContent.ItemType<BriskTonicItem>(), ItemID.MiningPotion,
            new AlchemyVariant(Silver, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 111, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new AlchemyVariant(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 100, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 4, StrengthPercent = 124, DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30) },
            new AlchemyVariant(Silver, ItemID.Blinkroot, ItemID.Worm) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = TimeConversion.SetTime(mins: 4) });

        // Magnolia Variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, ModContent.ItemType<MagnoliaSolutionItem>(), ItemID.MiningPotion,
            new AlchemyVariant(ItemID.BlinkrootSeeds, Silver) { OutputStack = 4, Potency = 2, StrengthPercent = 95, DurationTicks = TimeConversion.SetTime(mins: 10) });     
        #endregion

        #region Builder

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.BuilderPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow) { 
                OutputStack = 1, 
                Potency = 1, 
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 45)
            },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 125,
                DurationTicks = TimeConversion.SetTime(mins: 45)
            },
            new AlchemyVariant(ItemID.Blinkroot, ModContent.ItemType<LodestoneItem>(), ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 135,
                DurationTicks = TimeConversion.SetTime(mins: 40)
            });

        // brisk tonic

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<BriskTonicItem>(),
            outputType: ItemID.BuilderPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Shiverthorn, ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 150,
                DurationTicks = TimeConversion.SetTime(mins: 45)
            },
            new AlchemyVariant(ItemID.Blinkroot, ModContent.ItemType<LodestoneItem>(), ItemID.Moonglow)
            {
                OutputStack = 1,
                Potency = 5,
                StrengthPercent = 225,
                DurationTicks = TimeConversion.SetTime(mins: 8)
            },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.TinOre, ItemID.Moonglow)
            {
            OutputStack = 1,
            Potency = 4,
            StrengthPercent = 150,
            DurationTicks = TimeConversion.SetTime(mins: 30)
            });

        // Magnolia variant

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.BuilderPotion,
            new AlchemyVariant(ItemID.BlinkrootSeeds, ItemID.TinOre, ItemID.MoonglowSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 60)
            },
            new AlchemyVariant(ItemID.BlinkrootSeeds, ModContent.ItemType<LodestoneItem>(), ItemID.MoonglowSeeds)
            {
                OutputStack = 1,
                Potency = 3,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 60)
            });
        #endregion

        #region Gills
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.GillsPotion,
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 4)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 105,
                DurationTicks = TimeConversion.SetTime(mins: 4, secs: 30)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Coral, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });


        // Magnolia variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.GillsPotion,
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });
        #endregion

        #region Flipper
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.FlipperPotion,
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Shiverthorn)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 8)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Shiverthorn, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        // Magnolia variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.FlipperPotion,
            new AlchemyVariant(ItemID.ShiverthornSeeds, ItemID.WaterleafSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.ShiverthornSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });

        // Brisk variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<BriskTonicItem>(),
            outputType: ItemID.FlipperPotion,
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Waterleaf)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            },
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            });
        #endregion

        #region Water Walking
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.WaterWalkingPotion,
            new AlchemyVariant(ItemID.Waterleaf, ItemID.SharkFin)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            });

        // Magnolia variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.WaterWalkingPotion,
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.SharkFin)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });

        // Brisk variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<BriskTonicItem>(),
            outputType: ItemID.WaterWalkingPotion,
            new AlchemyVariant(ItemID.Waterleaf, ItemID.SharkFin)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 14)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            });
        #endregion

        #region Calming
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.CalmingPotion,
            new AlchemyVariant(ItemID.Damselfish, ItemID.Daybloom)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new AlchemyVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            },
            new AlchemyVariant(ItemID.Waterleaf, ItemID.Daybloom, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 115,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        // Magnolia variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.CalmingPotion,
            new AlchemyVariant(ItemID.Damselfish, ItemID.DaybloomSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            },
            new AlchemyVariant(ItemID.Damselfish, ItemID.DaybloomSeeds, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new AlchemyVariant(ItemID.WaterleafSeeds, ItemID.DaybloomSeeds, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            });

        // Harmony variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<HarmoniousTonicItem>(),
            outputType: ItemID.CalmingPotion,
            new AlchemyVariant(ItemID.Damselfish, ItemID.Daybloom)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            },
            new AlchemyVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Seashell)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            },
            new AlchemyVariant(ItemID.Damselfish, ItemID.Daybloom, ItemID.Coral)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            });
        #endregion

        #region Featherfall
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.FeatherfallPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Feather, ItemID.Daybloom)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        // Magnolia variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.FeatherfallPotion,
            new AlchemyVariant(ItemID.Feather, ItemID.DaybloomSeeds, ItemID.BlinkrootSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 7)
            });

        // Brisk variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<BriskTonicItem>(),
            outputType: ItemID.FeatherfallPotion,
            new AlchemyVariant(ItemID.Feather, ItemID.Daybloom, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 12)
            });
        #endregion

        #region Warmth
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.WarmthPotion,
            new AlchemyVariant(ItemID.FrostMinnow, ItemID.Shiverthorn)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 15)
            },
            new AlchemyVariant(ItemID.FrostMinnow, ItemID.Fireblossom)
            {
                OutputStack = 1,
                Potency = 3,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 20)
            });

        // Magnolia variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.WarmthPotion,
            new AlchemyVariant(ItemID.FrostMinnow, ItemID.ShiverthornSeeds)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 10)
            });

        // Vitality variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<VitalTonicItem>(),
            outputType: ItemID.FeatherfallPotion,
            new AlchemyVariant(ItemID.FrostMinnow, ItemID.Shiverthorn)
            {
                OutputStack = 1,
                Potency = 3,
                DurationTicks = TimeConversion.SetTime(mins: 18)
            },
            new AlchemyVariant(ItemID.FrostMinnow, ItemID.Fireblossom)
            {
                OutputStack = 1,
                Potency = 3,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 22)
            });
        #endregion

        #region Obsidian Skin
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ItemID.BottledWater,
        outputType: ItemID.ObsidianSkinPotion,
        new AlchemyVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        },
        new AlchemyVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 4, secs: 45)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<HarmoniousTonicItem>(),
        outputType: ItemID.ObsidianSkinPotion,
        new AlchemyVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        },
        new AlchemyVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 6, secs: 30)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
        outputType: ItemID.ObsidianSkinPotion,
        new AlchemyVariant(ItemID.Obsidian, ItemID.WaterleafSeeds, ItemID.FireblossomSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new AlchemyVariant(ItemID.Obsidian, ItemID.ShiverthornSeeds, ItemID.FireblossomSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 4, secs: 30)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<BottledOilItem>(),
        outputType: ItemID.ObsidianSkinPotion,
        new AlchemyVariant(ItemID.Fireblossom, ItemID.Waterleaf, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 12)
        },
        new AlchemyVariant(ItemID.Fireblossom, ItemID.Shiverthorn, ItemID.Obsidian)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30)
        });
        #endregion

        #region Gravitation
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.GravitationPotion,
            new AlchemyVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            },
            new AlchemyVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new AlchemyVariant(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 4)
            });

        // Magnolia variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.GravitationPotion,
            new AlchemyVariant(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.BlinkrootSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            },
            new AlchemyVariant(ItemID.Feather, ItemID.FireblossomSeeds, ItemID.BlinkrootSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            },
            new AlchemyVariant(ItemID.Feather, ItemID.DeathweedSeeds, ItemID.FireblossomSeeds)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 3)
            });

        // Brisk variant
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<BriskTonicItem>(),
            outputType: ItemID.GravitationPotion,
            new AlchemyVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Deathweed)
            {
                OutputStack = 1,
                Potency = 1,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            },
            new AlchemyVariant(ItemID.Fireblossom, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 6)
            },
            new AlchemyVariant(ItemID.Deathweed, ItemID.Feather, ItemID.Blinkroot)
            {
                OutputStack = 1,
                Potency = 2,
                StrengthPercent = 100,
                DurationTicks = TimeConversion.SetTime(mins: 5)
            });
        #endregion

        // Visual Potions

        #region Dangersense
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.TrapsightPotion,
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30) },
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<SpecularTonicItem>(),
            outputType: ItemID.TrapsightPotion,
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Cobweb) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.DartTrap) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30) },
            new AlchemyVariant(ItemID.Shiverthorn, ItemID.Cobweb, ItemID.Lens) { Potency = 5 });
        #endregion

        #region Hunter
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.HunterPotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 8) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 8) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<SpecularTonicItem>(),
            outputType: ItemID.HunterPotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.SharkFin) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.DartTrap) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 9, secs: 30) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot, ItemID.Lens) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 10) });
        #endregion

        #region Shine

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.ShinePotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.GlowingMushroom) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new AlchemyVariant(ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<SpecularTonicItem>(),
            outputType: ItemID.ShinePotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.GlowingMushroom) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 12) },
            new AlchemyVariant(ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15, secs: 30) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.GlowingMushroom) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 20) });

        #endregion

        #region Spelunker

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, ItemID.BottledWater, ItemID.SpelunkerPotion,
            new AlchemyVariant(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 1, StrengthPercent = 100 },
            new AlchemyVariant(Gold, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 4, StrengthPercent = 115 },
            new AlchemyVariant(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 12) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, ModContent.ItemType<SpecularTonicItem>(), ItemID.SpelunkerPotion,
            new AlchemyVariant(Gold, ItemID.Blinkroot) { OutputStack = 2, Potency = 3, StrengthPercent = 110 },
            new AlchemyVariant(ItemID.Blinkroot) { OutputStack = 2, Potency = 2, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new AlchemyVariant(ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 3, StrengthPercent = 105, DurationTicks = TimeConversion.SetTime(mins: 9) },
            new AlchemyVariant(Gold, ItemID.Blinkroot, SpecularFishOrMoonglow) { OutputStack = 2, Potency = 5, StrengthPercent = 150, DurationTicks = TimeConversion.SetTime(mins: 4) });

        #endregion

        #region Night Owl

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.NightOwlPotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 10) },
            new AlchemyVariant(ItemID.Sunflower, ItemID.Blinkroot) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.Blinkroot) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<SpecularTonicItem>(),
            outputType: ItemID.NightOwlPotion,
            new AlchemyVariant(ItemID.Daybloom, ItemID.Blinkroot) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 12) },
            new AlchemyVariant(ItemID.Sunflower, ItemID.Blinkroot) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 15, secs: 30) },
            new AlchemyVariant(ItemID.Daybloom, ItemID.Sunflower, ItemID.Blinkroot) { Potency = 4, DurationTicks = TimeConversion.SetTime(mins: 20) });

        #endregion

        #region Invisibilty
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.InvisibilityPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 3) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 5) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<SpecularTonicItem>(),
            outputType: ItemID.InvisibilityPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Moonglow) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 6) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.Worm) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30) });
        #endregion

        #region Biome Sight
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.BiomeSightPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.GrassSeeds) { Potency = 1, DurationTicks = TimeConversion.SetTime(mins: 5) },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Fireblossom, ItemID.GrassSeeds) { Potency = 2, DurationTicks = TimeConversion.SetTime(mins: 5, secs: 30) });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<SpecularTonicItem>(),
            outputType: ItemID.BiomeSightPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Moonglow, ItemID.GrassSeeds) 
            { 
                Potency = 2, 
                DurationTicks = TimeConversion.SetTime(mins: 8) 
            },
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Fireblossom, ItemID.GrassSeeds) { Potency = 3, DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30) });
        #endregion

        // Fishing Potions

        #region Sonar
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ItemID.BottledWater,
        outputType: ItemID.SonarPotion,
        new AlchemyVariant(ItemID.SpecularFish, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 8, secs: 30)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<SpecularTonicItem>(),
        outputType: ItemID.SonarPotion,
        new AlchemyVariant(ItemID.Coral, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        },
        new AlchemyVariant(ItemID.SpecularFish, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
        outputType: ItemID.SonarPotion,
        new AlchemyVariant(ItemID.Coral, ItemID.WaterleafSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 11)
        },
        new AlchemyVariant(ItemID.SpecularFish, ItemID.WaterleafSeeds)
        {
            Potency = 1,
            StrengthPercent = 90,
            DurationTicks = TimeConversion.SetTime(mins: 11, secs: 30)
        });
        #endregion

        #region Fishing
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ItemID.BottledWater,
        outputType: ItemID.FishingPotion,
        new AlchemyVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<FortunateTonicItem>(),
        outputType: ItemID.FishingPotion,
        new AlchemyVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        },
        new AlchemyVariant(ItemID.Worm, ItemID.Waterleaf)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 7, secs: 30)
        },
        new AlchemyVariant(ItemID.EnchantedNightcrawler, ItemID.Waterleaf)
        {
            Potency = 4,
            StrengthPercent = 130,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new AlchemyVariant(ItemID.CrispyHoneyBlock, ItemID.Waterleaf, ItemID.Worm)
        {
            Potency = 2,
            StrengthPercent = 105,
            DurationTicks = TimeConversion.SetTime(mins: 10, secs: 30)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ModContent.ItemType<MagnoliaSolutionItem>(),
            outputType: ItemID.FishingPotion,
            new AlchemyVariant(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds)
            {
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 11)
            },
            new AlchemyVariant(ItemID.Worm, ItemID.WaterleafSeeds)
            {
                Potency = 1,
                StrengthPercent = 90,
                DurationTicks = TimeConversion.SetTime(mins: 11, secs: 30)
            },
            new AlchemyVariant(ItemID.CrispyHoneyBlock, ItemID.WaterleafSeeds, ItemID.Worm)
            {
                Potency = 1,
                StrengthPercent = 80,
                DurationTicks = TimeConversion.SetTime(mins: 14)
            });
        #endregion

        #region Crate Potion
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ItemID.BottledWater,
        outputType: ItemID.CratePotion,
        new AlchemyVariant(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<FortunateTonicItem>(),
        outputType: ItemID.CratePotion,
        new AlchemyVariant(ItemID.Amber, ItemID.Moonglow, ShiverthornOrWaterleaf)
        {
            Potency = 1,
            StrengthPercent = 105,
            DurationTicks = TimeConversion.SetTime(mins: 6, secs: 45)
        },
        new AlchemyVariant(ItemID.Amber, ItemID.Moonglow, Gold)
        {
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        },
        new AlchemyVariant(ItemID.Amber, ShiverthornOrWaterleaf, ItemID.Bass)
        {
            Potency = 4,
            StrengthPercent = 150,
            DurationTicks = TimeConversion.SetTime(mins: 2, secs: 30)
        });
        #endregion

        // Luck Potions

        #region Lesser Luck
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ItemID.BottledWater,
        outputType: ItemID.LuckPotionLesser,
        new AlchemyVariant(ItemID.LadyBug, ItemID.Waterleaf, ItemID.WhitePearl)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new AlchemyVariant(ItemID.LadyBug, ItemID.Daybloom, ItemID.WhitePearl)
        {
            Potency = 1,
            StrengthPercent = 95,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<FortunateTonicItem>(),
        outputType: ItemID.LuckPotionLesser,
        new AlchemyVariant(ItemID.Waterleaf, ItemID.WhitePearl)
        {
            Potency = 2,
            StrengthPercent = 108,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new AlchemyVariant(ItemID.Daybloom, ItemID.WhitePearl)
        {
            Potency = 2,
            StrengthPercent = 102,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });
        #endregion

        #region Luck
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ItemID.BottledWater,
        outputType: ItemID.LuckPotion,
        new AlchemyVariant(ItemID.LadyBug, ItemID.Waterleaf, ItemID.BlackPearl)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        },
        new AlchemyVariant(ItemID.LadyBug, ItemID.Daybloom, ItemID.BlackPearl)
        {
            Potency = 1,
            StrengthPercent = 95,
            DurationTicks = TimeConversion.SetTime(mins: 11)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<FortunateTonicItem>(),
        outputType: ItemID.LuckPotion,
        new AlchemyVariant(ItemID.Waterleaf, ItemID.BlackPearl)
        {
            Potency = 2,
            StrengthPercent = 108,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        },
        new AlchemyVariant(ItemID.Daybloom, ItemID.BlackPearl)
        {
            Potency = 2,
            StrengthPercent = 102,
            DurationTicks = TimeConversion.SetTime(mins: 11)
        });
        #endregion

        #region Greater Luck
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ItemID.BottledWater,
        outputType: ItemID.LuckPotionGreater,
        new AlchemyVariant(ItemID.LadyBug, ItemID.Waterleaf, ItemID.PinkPearl)
        {
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 15)
        },
        new AlchemyVariant(ItemID.LadyBug, ItemID.Daybloom, ItemID.PinkPearl)
        {
            Potency = 1,
            StrengthPercent = 95,
            DurationTicks = TimeConversion.SetTime(mins: 16)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        inputType: ModContent.ItemType<FortunateTonicItem>(),
        outputType: ItemID.LuckPotionGreater,
        new AlchemyVariant(ItemID.Waterleaf, ItemID.PinkPearl)
        {
            Potency = 2,
            StrengthPercent = 108,
            DurationTicks = TimeConversion.SetTime(mins: 15)
        },
        new AlchemyVariant(ItemID.Daybloom, ItemID.PinkPearl)
        {
            Potency = 2,
            StrengthPercent = 102,
            DurationTicks = TimeConversion.SetTime(mins: 16)
        });
        #endregion
    }

    /// <summary>
    /// Spirit Reforged recipes, buffs, and effect behaviors.
    /// </summary>
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
        // Soaring rides SpiritReforged's own FlightPotionBuff, whose id doesn't exist until that mod
        // is loaded, hence a runtime registration rather than a line in the EffectBehaviors table.
        // Their buff adds .25f to FlightPlayer.wingTimeMult; we add the brew's delta on top of it.
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
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        ZephyrPotion.Type,
        new AlchemyVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.Cloud, 5), ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 1,
            DurationTicks = TimeConversion.SetTime(mins: 3)
        },
        new AlchemyVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.Cloud, 10), ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 2,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ModContent.ItemType<BriskTonicItem>(),
        ZephyrPotion.Type,
        new AlchemyVariant(Cloudstalk.Type, ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 2,
            DurationTicks = TimeConversion.SetTime(mins: 4, secs: 30)
        },
        new AlchemyVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.Cloud, 5), ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 3,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });
        #endregion

        #region Soaring Potion
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        SoaringPotion.Type,
        new AlchemyVariant(Cloudstalk.Type, ItemID.SoulofFlight, ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        },
        new AlchemyVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 110,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new AlchemyVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 118,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
       ModContent.ItemType<BriskTonicItem>(),
        SoaringPotion.Type,
        new AlchemyVariant(Cloudstalk.Type, ItemID.SoulofFlight, ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 112,
            DurationTicks = TimeConversion.SetTime(mins: 4)
        },
        new AlchemyVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 2), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 115,
            DurationTicks = TimeConversion.SetTime(mins: 5)
        },
        new AlchemyVariant(Cloudstalk.Type, ComponentMatcher.Of(ItemID.SoulofFlight, 5), ItemID.Damselfish)
        {
            OutputStack = 1,
            Potency = 5,
            StrengthPercent = 120,
            DurationTicks = TimeConversion.SetTime(mins: 6)
        });
        #endregion

        #region Remedy Potion
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        RemedyPotion.Type,
        new AlchemyVariant(ItemID.Stinger, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ModContent.ItemType<VitalTonicItem>(),
        RemedyPotion.Type,
        new AlchemyVariant(ItemID.Stinger, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 115,
            DurationTicks = TimeConversion.SetTime(mins: 12)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ModContent.ItemType<MagnoliaSolutionItem>(),
        RemedyPotion.Type,
        new AlchemyVariant(ItemID.Stinger, ItemID.WaterleafSeeds, ItemID.BlinkrootSeeds)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 75,
            DurationTicks = TimeConversion.SetTime(mins: 8)
        });
        #endregion

        #region Quench Potion
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        QuenchPotion.Type,
        new AlchemyVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(secs: 45)
        },
        new AlchemyVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 1,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(secs: 45)
        });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ModContent.ItemType<VitalTonicItem>(),
        QuenchPotion.Type,
        new AlchemyVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Blinkroot)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 1)
        },
        new AlchemyVariant(GarFish.Type, ItemID.Waterleaf, ItemID.Moonglow)
        {
            OutputStack = 1,
            Potency = 2,
            StrengthPercent = 100,
            DurationTicks = TimeConversion.SetTime(mins: 1)
        },
        new AlchemyVariant(GarFish.Type, ItemID.Waterleaf, Gold)
        {
            OutputStack = 1,
            Potency = 3,
            StrengthPercent = 125,
            DurationTicks = TimeConversion.SetTime(secs: 20)
        },
        new AlchemyVariant(GarFish.Type, ItemID.Waterleaf, ItemID.SoulofLight)
        {
            OutputStack = 1,
            Potency = 4,
            StrengthPercent = 125,
            DurationTicks = TimeConversion.SetTime(secs: 30)
        });
        #endregion
    }

    /// <summary>
    /// Random items that should be craftable (e.g. unholy water, love potion, etc.)
    /// </summary>
    private static void RegisterMisc()
    {
        #region Synthesis: Misc
        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.BottledWater,
            ItemID.UnholyWater,
            new AlchemyVariant(CorruptTiles, ItemID.CorruptSeeds) { OutputStack = 4 },
            new AlchemyVariant(ItemID.VileMushroom, AnyDirt, ItemID.Worm) { OutputStack = 4 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.BottledWater,
            ItemID.BloodWater,
            new AlchemyVariant(BloodTiles, ItemID.CrimsonSeeds) { OutputStack = 4 },
            new AlchemyVariant(ItemID.ViciousMushroom, AnyDirt, ItemID.Worm) { OutputStack = 4 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.BottledWater,
            ItemID.HolyWater,
            new AlchemyVariant(ItemID.PixieDust, ItemID.HallowedSeeds) { OutputStack = 4 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.BottledWater,
            ItemID.LovePotion,
            new AlchemyVariant(ItemID.LifeCrystal, ItemID.FallenStar) { OutputStack = 4 },
            new AlchemyVariant(ItemID.PrincessFish, ItemID.Shiverthorn) { OutputStack = 8 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.BottledWater,
            ItemID.StinkPotion,
            new AlchemyVariant(ItemID.Stinkfish, ItemID.Deathweed) { OutputStack = 2 },
            new AlchemyVariant(ItemID.Stinkfish, ItemID.DeathweedSeeds) { OutputStack = 1 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.Bottle,
            ModContent.ItemType<BloodtoothExtractItem>(),
            new AlchemyVariant(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.BottledWater,
            ModContent.ItemType<BloodtoothExtractItem>(),
            new AlchemyVariant(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ModContent.ItemType<InertSolutionItem>(),
            ModContent.ItemType<BloodtoothExtractItem>(),
            new AlchemyVariant(ModContent.ItemType<BloodtoothFungusItem>()));

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.Bottle,
            ItemID.ViciousPowder,
            new AlchemyVariant(ItemID.ViciousMushroom)
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

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            ItemID.Bottle,
            ItemID.VilePowder,
            new AlchemyVariant(ItemID.VileMushroom)
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

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis, ItemID.Bottle, ModContent.ItemType<RustedNugget>(),
            new AlchemyVariant(ItemID.IronOre)
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

        AlchemyRecipeRegistry.Clear();
        AlchemyIngredients.Clear();
        AlmanacEntryRegistry.Clear();
        AlchemyRecipeGroups.Clear();
        EffectBehaviors.ClearRuntime();   // cross-mod entries hold another mod's types
        QuenchHook.Unhook();              // ...and this one lives on another mod's static event

    }

    private static void InfusionRecipes()
    {
        ReadOnlySpan<ChemicalType> flags =
        [
            ChemicalType.Inert, ChemicalType.Catalytic,
            ChemicalType.Alkaline, ChemicalType.Acidic,
            ChemicalType.Volatile, ChemicalType.Kinetic,
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

            AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
                inputType: ItemID.BottledWater,
                outputType: ItemID.BottledWater,
                new AlchemyVariant(group) { OutputInfusion = flag });
        }

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
            inputType: ItemID.BottledWater,
            outputType: ItemID.SwiftnessPotion,
            new AlchemyVariant(ItemID.Blinkroot, ItemID.Cactus) { SolventInfusion = ChemicalType.Inert, Potency = 2 });
    }

    /// <summary>
    /// Calamity Mod recipes.
    /// </summary>
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

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        PhotosynthesisPotion.Type,
        new AlchemyVariant(EssenceofSunlight.Type, bloodOrb.Type)
        {
            OutputStack = 1,
            Potency = 3,
            DurationTicks = TimeConversion.SetTime(mins: 10)
        },
        new AlchemyVariant(EssenceofSunlight.Type, ItemID.Daybloom) { OutputStack = 1 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        AnechoicCoating.Type,
        new AlchemyVariant(bloodOrb.Type) { OutputStack = 1 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.SuperManaPotion,
        AstralInjection.Type,
        new AlchemyVariant(StarblightSoot.Type, AureusCell.Type) { OutputStack = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        BoundingPotion.Type,
        new AlchemyVariant(ItemID.PinkGel, ItemID.Moonglow) { OutputStack = 1 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        CalciumPotion.Type,
        new AlchemyVariant(AncientBoneDust.Type) { OutputStack = 1 },
        new AlchemyVariant(AncientBoneDust.Type, bloodOrb.Type) { OutputStack = 1, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        CeaselessHungerPotion.Type,
        new AlchemyVariant(DarkPlasma.Type) { OutputStack = 1 },
        new AlchemyVariant(DarkPlasma.Type, bloodOrb.Type) { OutputStack = 2, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        GravityNormalizerPotion.Type,
        new AlchemyVariant(AureusCell.Type, bloodOrb.Type) { OutputStack = 1 },
        new AlchemyVariant(AldebaranAlewife.Type, AureusCell.Type) { OutputStack = 1, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        ShadowPotion.Type,
        new AlchemyVariant(Shadowfish.Type, ItemID.Blinkroot) { OutputStack = 1 },
        new AlchemyVariant(Shadowfish.Type, bloodOrb.Type) { OutputStack = 2, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        SoaringPotion.Type,
        new AlchemyVariant(SunkenSailfish.Type, ItemID.SoulofFlight) { OutputStack = 1 },
        new AlchemyVariant(bloodOrb.Type, ItemID.SoulofFlight) { OutputStack = 2, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        SulphurskinPotion.Type,
        new AlchemyVariant(SulphurousSand.Type, ItemID.Waterleaf) { OutputStack = 1 },
        new AlchemyVariant(bloodOrb.Type, ItemID.Waterleaf) { OutputStack = 2, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        ZenPotion.Type,
        new AlchemyVariant(PurifiedGel.Type, ItemID.Daybloom) { OutputStack = 1 },
        new AlchemyVariant(ComponentMatcher.Of(bloodOrb.Type, 5), ComponentMatcher.Of(ItemID.Fireblossom, 1)) { OutputStack = 2, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        ZergPotion.Type,
        new AlchemyVariant(ComponentMatcher.Of(PurifiedGel.Type, 2), ComponentMatcher.Of(ItemID.Fireblossom, 2)) { OutputStack = 1 },
        new AlchemyVariant(ComponentMatcher.Of(bloodOrb.Type, 5), ComponentMatcher.Of(PurifiedGel.Type, 2)) { OutputStack = 2, Potency = 3 });

        AlchemyRecipeRegistry.RegisterVariants(AlchemyMode.Synthesis,
        ItemID.BottledWater,
        PotionofOmniscience.Type,
        new AlchemyVariant(ItemID.HunterPotion, ItemID.SpelunkerPotion, ItemID.TrapsightPotion) { OutputStack = 1 });
    }
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