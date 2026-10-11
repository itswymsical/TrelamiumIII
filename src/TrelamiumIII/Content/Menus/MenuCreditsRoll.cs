using Terraria.Graphics.Effects;

namespace TrelamiumIII.Content.Menus;

/// <summary>
///     The credits are the CreditsRoll sky, drawn during the background pass, so a menu scene that covers the screen in
///     PreDrawLogo hides them. <see cref="Redraw" /> draws them again on top.
/// </summary>
internal static class MenuCreditsRoll
{
    /// <summary>Call with no batch open. The sky's draw is stateless (time advances in Update), so a second draw is harmless.</summary>
    public static void Redraw(SpriteBatch spriteBatch)
    {
        if (SkyManager.Instance["CreditsRoll"] is not { } credits || !credits.IsActive())
            return;

        var presentation = Main.instance.GraphicsDevice.PresentationParameters;

        // DrawMenu runs with the screen size in UI space; the roll anchors to the size the background pass had.
        var screenWidth = Main.screenWidth;
        var screenHeight = Main.screenHeight;

        Main.screenWidth = (int)(presentation.BackBufferWidth / Main.BackgroundViewMatrix.Zoom.X);
        Main.screenHeight = (int)(presentation.BackBufferHeight / Main.BackgroundViewMatrix.Zoom.Y);

        try
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.CurrentFrameFlags.Hacks.CurrentBackgroundMatrixForCreditsRoll);
            credits.Draw(spriteBatch, 1f, 1f);
            spriteBatch.End();
        }
        finally
        {
            Main.screenWidth = screenWidth;
            Main.screenHeight = screenHeight;
        }
    }
}
