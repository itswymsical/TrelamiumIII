using System.Linq;
using Microsoft.Xna.Framework.Input;
using TrelamiumIII.Core.Residues;

namespace TrelamiumIII.Content.DevTools;

/// <summary>
///     Paints residues at the cursor. Right-click cycles through every registered residue;
///     hold Shift while painting to give the marks a random tint.
/// </summary>
[LegacyName("TileConditionWand")]
public class ResidueWand : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.LeafWand;

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

        IResidue[] residues = ResidueSystem.All.ToArray();

        if (residues.Length == 0)
            return null;

        if (player.altFunctionUse == 2)
        {
            selected = (selected + 1) % residues.Length;
            Main.NewText($"Residue: {residues[selected].Name}", Color.LightSkyBlue);

            return true;
        }

        IResidue residue = residues[selected % residues.Length];
        Color? tint = Main.keyState.IsKeyDown(Keys.LeftShift) ? Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.55f) : null;

        ResidueSystem.ApplyCircle(residue, Main.MouseWorld, BRUSH_RADIUS, origin: ResidueOrigin.Owned, tint: tint);

        return true;
    }
}
