using ReLogic.Graphics;
using SubworldLibrary;
using Terraria.GameContent;
using Terraria.GameInput;
using TrelamiumIII.Core.Graphics.FbmScreen;

namespace TrelamiumIII.Common.Subworlds;

/// <summary>
///     A subworld with its own loading screen. The title is <see cref="Subworld.DisplayName" />; tips are every key
///     under <c>Mods.TrelamiumIII.Subworlds.{Name}.Tips</c>.
/// </summary>
public abstract class SubworldRealm : Subworld
{
    private RealmLoadingScreen? loadingScreen;

    /// <summary>Cover-fitted behind the loading screen. Null leaves it black.</summary>
    public virtual Texture2D? LoadingBackground => VanillaBackground(SurfaceBackgroundID.Forest1);

    /// <summary>Stretched over the whole screen above <see cref="LoadingBackground" />; transparent where it should show through.</summary>
    public virtual Texture2D? LoadingSplash => null;

    /// <summary>Replaces the text title when set. Fitted to the title box and projected like the text.</summary>
    public virtual Texture2D? LoadingTitleImage => null;

    /// <summary>Music slot played while this realm loads, or -1 to keep vanilla's menu track.</summary>
    public virtual int LoadingMusic => -1;

    public virtual DynamicSpriteFont TitleFont => FontAssets.DeathText.Value;

    public virtual DynamicSpriteFont TipFont => FontAssets.MouseText.Value;

    /// <summary>Colour of the title underline, tip label and debug progress bar.</summary>
    public virtual Color LoadingAccent => new(255, 190, 60);

    /// <summary>
    /// Mist drawn by <see cref="RealmTransition"/> and behind the loading screen in place of <see cref="LoadingBackground"/>.
    /// Null disables both.
    /// </summary>
    internal virtual FbmScreenStyle? ScreenStyle => null;

    /// <summary>Adds this realm's <see cref="ScreenObject"/>s to a freshly reset renderer.</summary>
    internal virtual void PopulateScreen(FbmScreenRenderer screen)
    {
    }

    /// <summary>Starts a <see cref="RealmEntryCutscene"/> for this realm; false enters without one.</summary>
    internal virtual bool StartEntryCutscene() => false;

    public override void SetStaticDefaults()
    {
        // Registers the key so tML adds it to the localization file.
        _ = DisplayName;
    }

    /// <summary>Only while entering this realm; leaving it keeps vanilla's menu music.</summary>
    public override bool ChangeAudio()
    {
        if (!Main.gameMenu || LoadingMusic < 0 || SubworldSystem.Current != this)
            return false;

        Main.newMusic = LoadingMusic;
        return true;
    }

    public override void DrawSetup(GameTime gameTime)
    {
        PlayerInput.SetZoom_UI();
        Main.instance.GraphicsDevice.Clear(Color.Black);

        DrawMenu(gameTime);

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        Main.DrawCursor(Main.DrawThickCursor());
        Main.spriteBatch.End();
    }

    /// <summary>Begins and ends its own sprite batches; <see cref="DrawSetup" /> calls it with none open.</summary>
    public override void DrawMenu(GameTime gameTime)
    {
        loadingScreen ??= new RealmLoadingScreen(this);
        loadingScreen.Draw(Main.spriteBatch, gameTime);
    }

    /// <summary>Loads the vanilla surface background on first use.</summary>
    protected static Texture2D VanillaBackground(int id)
    {
        Main.instance.LoadBackground(id);
        return TextureAssets.Background[id].Value;
    }
}
