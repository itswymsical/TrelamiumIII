using ReLogic.Content;
using TrelamiumIII.Core.Graphics.IlluminantInk;

namespace TrelamiumIII.Content.Menus;

/// <summary>
///     Illuminant Ink: a ferrofluid nebula wound around a black hole, in black and grey.
/// </summary>
/// <remarks>
///     Deliberately thin. Everything visual lives in <see cref="IlluminantInkScene" />; this type
///     exists to answer tModLoader's questions about the menu and to forward the two calls that
///     matter, so the scene can be driven from anywhere without a menu being selected.
/// </remarks>
public sealed class IlluminantInkMenu : ModMenu
{
    /// <summary>True while this menu is the one actually on screen.</summary>
    public static bool InMenu => Main.gameMenu && MenuLoader.CurrentMenu == ModContent.GetInstance<IlluminantInkMenu>();

    public override int Music => Assets.Music.IlluminantInkiness.Slot;

    public override string DisplayName => "Illuminant Ink";

    public override Asset<Texture2D> Logo => Assets.Textures.ModLogo.Asset;

    // The scene owns the whole sky. A sun or moon crossing it would sit outside the render
    // target and miss every post pass, so both are suppressed.
    public override Asset<Texture2D>? SunTexture => null;

    public override Asset<Texture2D>? MoonTexture => null;

    public override void Load()
    {
        IlluminantInkScene.Load();
    }

    public override void Unload()
    {
        IlluminantInkScene.Unload();
    }

    public override void OnDeselected()
    {
        // Frees the render targets as soon as the player moves on, rather than holding two
        // backbuffer-sized surfaces for the rest of the session.
        IlluminantInkScene.Update(active: false);
    }

    public override void Update(bool isOnTitleScreen)
    {
        base.Update(isOnTitleScreen);

        IlluminantInkScene.Update(InMenu);
    }

    public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor)
    {
        drawColor = Color.White;
        logoRotation = 0f;
        logoScale = InkStyle.LogoScale;
        logoDrawCenter.Y += InkStyle.LogoOffsetY;

        if (!InMenu)
        {
            return true;
        }

        // The scene draws the logo itself, at the end of its own pass, so it lands over the
        // composite instead of under it.
        IlluminantInkScene.Draw(spriteBatch, logoDrawCenter, logoScale, logoRotation, Logo.Value);

        return false;
    }
}
