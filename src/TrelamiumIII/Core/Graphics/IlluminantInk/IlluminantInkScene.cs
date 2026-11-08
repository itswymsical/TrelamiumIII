using Terraria.GameContent;
using TrelamiumIII.Content.Menus;
using static Terraria.GameContent.Animations.IL_Actions.Sprites;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Hosts <see cref="InkScene" /> behind the title screen: builds the menu's frame, composites the
///     result over the backbuffer, and puts the vignette and the logo on top.
/// </summary>
/// <remarks>
///     <para>
///         The renderer itself moved to <see cref="InkScene" />, which knows nothing about menus. What
///         is left here is everything that is <em>only</em> true of the title screen: the drifting
///         singularity, the cursor conversion out of UI space, the fade on entry, the vignette, and
///         the logo. A second host doing the same job for a world lives alongside this one and shares
///         none of it, which is the point of the split.
///     </para>
///     <para>
///         <see cref="Update" /> runs on the game tick and is the only place simulation advances;
///         <see cref="Draw" /> runs on the frame and only ever reads. The renderer this replaced
///         advanced its particles from inside its draw call, which tied the speed of the whole scene
///         to frame rate -- a 144 Hz monitor ran it at more than twice the speed a 60 Hz one did.
///     </para>
///     <para>
///         All state is static because a <see cref="ModMenu" /> is a singleton and the scene has to
///         survive being deselected and reselected, but it is confined to this one type.
///     </para>
/// </remarks>
internal static class IlluminantInkScene
{
    /// <summary>
    ///     The menu stack: everything, because the title screen is the frame.
    /// </summary>
    /// <remarks>
    ///     The mote layer chases the cursor and the starfield fills the frame corner to corner. Both
    ///     belong to a screen the player is looking at directly, and both are why the stack is a
    ///     constructor argument rather than something <see cref="InkScene" /> decides for itself.
    /// </remarks>
    private static readonly InkScene Scene = new(
        new InkMoteLayer(),
        new StarfieldLayer(),
        new SingularityLayer()
    );

    public static float time;
    public static float fade;

    public static void Load()
    {
        time = 0f;
        fade = 0f;
    }

    public static void Unload()
    {
        // Graphics resources must not outlive the mod. Mods unload on a worker thread, and FNA3D only
        // frees them on the main one, which is live behind the menu when mods are reloaded.
        Main.QueueMainThreadAction(Release);
    }

    /// <summary>
    ///     Advances the scene by one tick, or tears it down if the menu is no longer showing.
    /// </summary>
    public static void Update(bool active)
    {
        if (!active)
        {
            if (Scene.Ready)
                Release();

            return;
        }

        if (!Scene.Ready)
        {
            // Nothing to simulate against until Draw has resolved a target size; the frame geometry
            // every layer depends on isn't known yet.
            return;
        }

        time += 1f / 60f;
        fade = MathF.Min(fade + 1f / InkStyle.FadeInTicks, 1f);

        var frame = BuildFrame();

        Scene.Update(in frame);
    }

    /// <summary>
    ///     Composes, post-processes and blits the scene, then draws the logo over it.
    /// </summary>
    /// <remarks>
    ///     Called from <see cref="ModMenu.PreDrawLogo" />, which means a batch is already open in the
    ///     state <c>Main.DrawMenu</c> established. This closes it, does its work, and reopens it in
    ///     exactly that state before returning.
    /// </remarks>
    public static void Draw(SpriteBatch spriteBatch, Vector2 logoCenter, float logoScale, float logoRotation, Texture2D? logo)
    {
        var device = Main.instance.GraphicsDevice;
        var presentation = device.PresentationParameters;

        var width = presentation.BackBufferWidth;
        var height = presentation.BackBufferHeight;

        if (!Scene.Ensure(device, width, height))
            return;

        var frame = BuildFrame();

        spriteBatch.End();

        // MagicPixel as the coverage mask: white and opaque, so the nebula's mask read is a no-op and
        // the ink fills the frame. This is the half of the renderer a world host changes.
        var result = Scene.Render(spriteBatch, device, in frame, TextureAssets.MagicPixel.Value, new Rectangle(0, 0, width, height));
        var bloom = Scene.PrepareBloom(in frame);

        // No transform matrix on the composite: the target is in real backbuffer pixels, so drawing it
        // 1:1 lands it exactly over the frame whatever the menu's UI scale is. Applying UIScaleMatrix
        // here would blow it up by that factor instead.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        bloom.Apply();

        spriteBatch.Draw(result, new Rectangle(0, 0, width, height), Color.White);

        spriteBatch.End();

        // Vignette.png is transparent in the middle and opaque black at the edges, so it is an alpha
        // overlay rather than a mask. Applied after the composite so it darkens the stars too -- the
        // shader's own vignette only reaches the ink.
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        /// <summary>
        /// the logo is rendered in <see cref="SingularityLayer"/> 
        /// </summary>

        FloatingLogo.SetLayout(logoCenter, logoScale); 

        spriteBatch.Draw(
            Assets.Textures.VFX.Vignette.Asset.Value,
            new Rectangle(0, 0, width, height),
            Color.White * (InkStyle.VignetteStrength * fade)
        );

        //DrawLogo(spriteBatch, logoCenter, logoScale);

        spriteBatch.End();

        // After the vignette so the credits aren't darkened at the edges.
        MenuCreditsRoll.Redraw(spriteBatch);

        // Back into the menu's own space, exactly as Main.DrawMenu had it, and left open for the rest
        // of the menu to keep drawing into.
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.UIScaleMatrix
        );

    }

    private static InkFrame BuildFrame()
    {
        var width = Scene.Width;
        var height = Scene.Height;

        // A slow drift on two incommensurate periods, so the hole never quite retraces its path and
        // the frame stays alive under a static logo.
        var drift = new Vector2(
            MathF.Sin(time * 0.11f) * InkStyle.SingularityDrift.X,
            MathF.Cos(time * 0.077f) * InkStyle.SingularityDrift.Y
        );

        var singularity = (InkStyle.Singularity + drift) * new Vector2(width, height);

        // Terraria divides Main.screenWidth by the UI scale for the duration of the menu, so
        // Main.mouse* is in UI space while the targets are in backbuffer pixels. Recovering the factor
        // from the two widths avoids depending on how Main.UIScale is set.
        var uiScale = width / (float)Math.Max(Main.screenWidth, 1);
        var cursor = new Vector2(Main.mouseX, Main.mouseY) * uiScale;

        return new InkFrame(width, height, time, singularity, cursor, fade);
    }

    private static void Release()
    {
        Scene.Release();

        time = 0f;
        fade = 0f;
        FloatingLogo.Reset();

    }

    /// <summary>
    ///     Drops the scene's render targets when the player leaves the menu for a world.
    /// </summary>
    /// <remarks>
    ///     <see cref="ModMenu.OnDeselected" /> covers switching between menus, but entering a world
    ///     leaves this menu selected and simply stops updating it -- without this, two
    ///     backbuffer-sized surfaces stay resident for the rest of the session for a background nobody
    ///     is looking at.
    /// </remarks>
    internal sealed class WorldEntryReleaser : ModSystem
    {
        public override void OnWorldLoad()
        {
            // OnWorldLoad runs on whichever thread loaded the world -- a pool worker, not the main
            // thread -- and FNA3D refuses to touch a graphics resource from anywhere else, so
            // disposing the targets inline here throws and tModLoader reports it as a world data
            // problem. Deferring the teardown puts it on the next frame's main thread.
            Main.QueueMainThreadAction(static () => Update(active: false));
        }
    }
}
