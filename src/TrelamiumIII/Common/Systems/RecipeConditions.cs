using Terraria.Localization;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Systems;

public sealed class RecipeConditions : ModSystem
{
    public static Condition RainingForBottles { get; private set; } = null!;

    public override void Load()
    {
        base.Load();
        RainingForBottles = new Condition(Mod.GetLocalization("Conditions.RainingForBottles", () => "In the Rain"), () => Main.LocalPlayer.ZoneRain && Main.raining);
    }

    public static bool IsNear(Player player, InternalLiquid condition)
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
                if (tile.LiquidAmount > 200 && tile.LiquidType == OilLiquids.Instance.Type && tile.InternalLiquid() == condition.Index)
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
            .AddRecipeGroup(CustomRecipeGroups.Fish)
            .AddRecipeGroup(CustomRecipeGroups.Dirts, 3)
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
