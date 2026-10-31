using System.Linq;
using Microsoft.Xna.Framework.Input;
using TrelamiumIII.Core.Tiles.Conditions;

namespace TrelamiumIII.Content.DevTools;

/// <summary>
///     Paints tile conditions at the cursor. Right-click cycles through every registered condition;
///     hold Shift while painting to give the marks a random tint.
/// </summary>
public class TileConditionWand : ModItem
{
    private const float BRUSH_RADIUS = 24f;

    private static int selected;

    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.useTime = Item.useAnimation = 8;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.useTurn = true;
        Item.rare = ItemRarityID.Quest;
        Item.value = 0;
    }

    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player)
    {
        // Cycling on a held right-click would spin through the whole list.
        Item.autoReuse = player.altFunctionUse != 2;
        Item.useTime = Item.useAnimation = player.altFunctionUse == 2 ? 20 : 8;

        return true;
    }

    public override bool? UseItem(Player player)
    {
        if (Main.dedServ || player.whoAmI != Main.myPlayer)
            return null;

        ITileCondition[] conditions = TileConditionSystem.All.ToArray();

        if (conditions.Length == 0)
            return null;

        if (player.altFunctionUse == 2)
        {
            selected = (selected + 1) % conditions.Length;
            Main.NewText($"Tile condition: {conditions[selected].Name}", Color.LightSkyBlue);

            return true;
        }

        ITileCondition condition = conditions[selected % conditions.Length];
        Color? tint = Main.keyState.IsKeyDown(Keys.LeftShift) ? Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.55f) : null;

        TileConditionSystem.ApplyCircle(condition, Main.MouseWorld, BRUSH_RADIUS, sync: true, tint: tint);

        return true;
    }
}
