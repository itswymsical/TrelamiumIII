using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace TrelamiumIII.Core.NPCs.Combat;

/// <summary>
/// Visualises what a puppet's attacks actually cover.
///
/// <para>
/// Melee hitboxes are built by vanilla from <see cref="Player.itemLocation"/>,
/// facing, and the swing's progress, so when a swing misses it is rarely obvious
/// whether the box was in the wrong place, the wrong size, or never built at all.
/// Toggle this on with <c>/wnpc hitbox</c> and the answer is on screen.
/// </para>
///
/// <para>Development only - nothing enables it in normal play.</para>
/// </summary>
public static class PuppetDebug
{
    /// <summary>Draw melee swing and projectile hitboxes for puppet-owned attacks.</summary>
    public static bool ShowHitboxes { get; set; }

    private const int BorderThickness = 2;

    /// <summary>Outlines a world-space rectangle.</summary>
    public static void DrawHitbox(SpriteBatch spriteBatch, Rectangle worldRect, Color color)
    {
        if (Main.dedServ)
            return;

        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Rectangle screenRect = worldRect;
        screenRect.X -= (int)Main.screenPosition.X;
        screenRect.Y -= (int)Main.screenPosition.Y;

        Color fill = color * 0.18f;
        Color edge = color * 0.85f;

        spriteBatch.Draw(pixel, screenRect, fill);

        // Top, bottom, left, right.
        spriteBatch.Draw(pixel, new Rectangle(screenRect.X, screenRect.Y, screenRect.Width, BorderThickness), edge);
        spriteBatch.Draw(pixel, new Rectangle(screenRect.X, screenRect.Bottom - BorderThickness, screenRect.Width, BorderThickness), edge);
        spriteBatch.Draw(pixel, new Rectangle(screenRect.X, screenRect.Y, BorderThickness, screenRect.Height), edge);
        spriteBatch.Draw(pixel, new Rectangle(screenRect.Right - BorderThickness, screenRect.Y, BorderThickness, screenRect.Height), edge);
    }
}
