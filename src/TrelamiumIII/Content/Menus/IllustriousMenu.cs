using ReLogic.Content;
using System.Collections.Generic;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Content.Menus;

public partial class IllustriousMenu : ModMenu
{
    public override void Load()
    {
        IllustriousRenderer.Load();
    }

    public override void Unload()
    {
        IllustriousRenderer.Unload();
    }

    public override int Music => Assets.Music.IlluminantInkiness.Slot;
    public override string DisplayName => "Illustrious Space";
    public override Asset<Texture2D> Logo => Assets.Textures.ModLogo.Asset;
    public override Asset<Texture2D> SunTexture => null;
    public override Asset<Texture2D> MoonTexture => null;

    public static bool InMenu => MenuLoader.CurrentMenu == ModContent.GetInstance<IllustriousMenu>() && Main.gameMenu;

    public override void OnSelected()
    {
        //SoundEngine.PlaySound(new SoundStyle($"{SFX_DIRECTORY}ReverieMenu") { Volume = 0.65f });
    }

    public override void Update(bool isOnTitleScreen)
    {
        base.Update(isOnTitleScreen);

        IllustriousRenderer.Update(InMenu);
    }

    public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor)
    {
        drawColor = Color.White;
        logoScale = 1.4f;
        logoRotation = 0f;
        logoDrawCenter = logoDrawCenter + new Vector2(0, 20);

        if (!InMenu || !Main.gameMenu)
        {
            // The renderer handles its own cleanup now if it's inactive, 
            // so we just return true to let the base game draw the logo normally if needed.
            return true;
        }

        IllustriousRenderer.DrawForMenu(spriteBatch, logoDrawCenter, logoScale, logoRotation, Logo.Value);

        return false;
    }
}