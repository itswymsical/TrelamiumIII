using Terraria.Localization;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Common.Systems;

public sealed class RecipeConditions : ModSystem
{
    public static Condition NearSunflowerOil { get; private set; } = null!;
    public static Condition RainingForBottles { get; private set; } = null!;

    public override void Load()
    {
        base.Load();
        NearSunflowerOil = new Condition(Mod.GetLocalization("Conditions.NearSunflowerOil", () => "Near Sunflower Oil"), () => IsNear(Main.LocalPlayer, OilLiquids.Instance.Sunflower));
        RainingForBottles = new Condition(Mod.GetLocalization("Conditions.RainingForBottles", () => "In the Rain"), () => Main.LocalPlayer.ZoneRain && Main.raining);
    }

    public static bool IsNear(Player player, LiquidCondition condition)
    {
        int centerX = (int)((player.position.X + player.width * 0.5f) / 16f);
        int feetY = (int)((player.position.Y + player.height) / 16f);

        for (int x = centerX - 4; x <= centerX + 4; x++)
        {
            for (int y = feetY - 3; y < feetY + 3; y++)
            {
                if (!WorldGen.InWorld(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.LiquidAmount > 200 && tile.LiquidType == OilLiquids.Instance.Type && tile.LiquidCondition() == condition.Index)
                    return true;
            }
        }

        return false;
    }

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

        Recipe bottledWater = Recipe.Create(ItemID.BottledWater);
        bottledWater.AddIngredient(ItemID.Bottle)
            .AddCondition(RainingForBottles)
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
}
