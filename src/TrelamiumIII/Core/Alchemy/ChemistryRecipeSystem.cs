using Terraria.Localization;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Reagents.Misc;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.FarmingOverhaul.SatchelContent;
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
    //            && AlchemyRecipes.Produces(recipe.createItem.type))
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

        RegisterSolvents();
        RegisterMisc();

        RegisterCombatPotions();
        RegisterUtilityPotions();
        RegisterVisionPotions();
        RegisterFishingPotions();
        RegisterLuckPotions();
        RegisterAugmentations();
        RegisterTransmutations();

        RegisterWeapons();
        RegisterMinerals();

        if (ModLoader.HasMod("SpiritReforged"))
            RegisterSpiritReforged();

        if (ModLoader.HasMod("CalamityMod"))
            RegisterCalamity();

        PopulateRecipeGroups();
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

    /// <summary>A brew duration in ticks.</summary>
    private static int Time(int mins = 0, int secs = 0) => TimeConversion.SetTime(mins: mins, secs: secs);

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

        ShiverthornOrSeeds.Add(ItemID.Shiverthorn, ItemID.ShiverthornSeeds);

        AnySoil.Add(
            ItemID.DirtBlock, ItemID.MudBlock,
            ItemID.EbonsandBlock, ItemID.CrimsandBlock, ItemID.PearlsandBlock,
            ItemID.EbonstoneBlock, ItemID.CrimstoneBlock, ItemID.PearlstoneBlock);

        DirtOrMud.Add(ItemID.DirtBlock, ItemID.MudBlock);

        AnyButterfly.Add(
            ItemID.GoldButterfly, ItemID.HellButterfly, ItemID.JuliaButterfly,
            ItemID.MonarchButterfly, ItemID.PurpleEmperorButterfly, ItemID.RedAdmiralButterfly,
            ItemID.SulphurButterfly, ItemID.TreeNymphButterfly, ItemID.UlyssesButterfly,
            ItemID.ZebraSwallowtailButterfly);

        AnyBaseBottle.Add(ItemID.Bottle, ItemID.BottledWater, SolventID.Inert);

        SpecularFishOrMoonglow.Add(ItemID.SpecularFish, ItemID.Moonglow);
    }
}
