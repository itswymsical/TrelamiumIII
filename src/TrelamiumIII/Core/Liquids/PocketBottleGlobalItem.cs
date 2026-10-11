using Terraria.Audio;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     An empty bottle used on a <see cref="SubmergedPocket" /> draws a dose out of it instead of being placed.
/// </summary>
public sealed class PocketBottleGlobalItem : GlobalItem
{
    /// <summary>Ticks between draws while the button is held, since a refused use is retried every tick.</summary>
    private const int DrawCooldown = 20;

    /// <summary>Pixels past a pocket's edge the cursor may be and still reach it.</summary>
    private const float CursorSlack = 10f;

    /// <summary>How far from the player, in pixels, a pocket can be bottled.</summary>
    private const float Reach = 16f * 6f;

    private static ulong lastDraw;

    public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.Bottle;

    public override bool CanUseItem(Item item, Player player)
    {
        if (player.whoAmI != Main.myPlayer)
            return true;

        Vector2 cursor = Main.MouseWorld;

        if (Vector2.Distance(player.Center, cursor) > Reach || SubmergedLiquidSystem.Nearest(cursor, CursorSlack) is not { } pocket)
            return true;

        if (Main.GameUpdateCount - lastDraw >= DrawCooldown && SubmergedLiquidSystem.CanBottle(pocket))
        {
            lastDraw = Main.GameUpdateCount;
            SubmergedLiquidSystem.RequestExtract(pocket, player);

            if (--item.stack <= 0)
                item.TurnToAir();

            SoundEngine.PlaySound(SoundID.SplashWeak, cursor);
        }

        return false;
    }
}
