using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.DevTools;

/// <summary>
///     Dev tool. Fills the tile under the cursor with a liquid; right click cycles through the vanilla liquids, every
///     liquid condition, and a brew of the first potion in the inventory.
/// </summary>
public class LiquidWand : ModItem
{
    private static int mode;

    private readonly record struct Mode(int Type, string? Condition, string Label);

    public override string Texture => "Terraria/Images/Item_" + ItemID.LeafWand;

    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.useTime = Item.useAnimation = 6;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.useTurn = true;
        Item.rare = ItemRarityID.Quest;
        Item.value = 0;
    }

    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse != 2)
            return true;

        if (player.whoAmI == Main.myPlayer && player.ItemTimeIsZero)
        {
            List<Mode> modes = Modes(player);
            mode = (mode + 1) % modes.Count;
            Main.NewText($"Liquid Wand: {modes[mode].Label}");
            player.ApplyItemTime(Item);
        }

        return false;
    }

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI != Main.myPlayer)
            return null;

        List<Mode> modes = Modes(player);
        Mode selected = modes[mode % modes.Count];
        Point tile = Main.MouseWorld.ToTileCoordinates();

        if (WorldGen.SolidTile(tile.X, tile.Y))
            return false;

        LiquidInternalSystem.Place(tile.X, tile.Y, selected.Type, selected.Condition, byte.MaxValue);
        return true;
    }

    private static List<Mode> Modes(Player player)
    {
        List<Mode> modes = [];

        // Water and lava have condition sets, so their conditions stand in for the plain liquids.
        foreach (InternalLiquidSet liquid in InternalLiquidSet.All)
        {
            foreach (InternalLiquidType condition in liquid.InternalLiquids.Where(condition => condition is not BrewCondition))
                modes.Add(new Mode(liquid.LiquidType, condition.Name, $"{liquid.Name} / {condition.DisplayName}"));
        }

        modes.Add(new Mode(LiquidID.Honey, null, "Honey"));

        if (player.inventory.FirstOrDefault(item => !item.IsAir && BrewLiquid.IsBrew(item.type)) is { } potion)
            modes.Add(new Mode(ModContent.GetInstance<BrewLiquid>().Type, BrewLiquid.ItemKey(potion.type), $"Brew of {potion.Name}"));

        return modes;
    }
}
