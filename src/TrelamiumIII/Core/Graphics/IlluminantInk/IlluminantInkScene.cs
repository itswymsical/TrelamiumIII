namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Owns the Illuminant Ink menu scene: its render targets, its layer stack, and the frame
///     it hands them.
/// </summary>
/// <remarks>
///     The division of labour here is the point of the rewrite. <see cref="Update" /> runs on
///     the game tick and is the only place simulation advances; <see cref="Draw" /> runs on the
///     frame and only ever reads. The renderer this replaced advanced its particles from inside
///     its draw call, which tied the speed of the whole scene to frame rate -- a 144 Hz monitor
///     ran it at more than twice the speed a 60 Hz one did.
///     <para />
///     All state is static because a <see cref="ModMenu" /> is a singleton and the scene has to
///     survive being deselected and reselected, but it is confined to this one type instead of
///     being spread over the renderer's file as loose fields.
/// </remarks>
internal static class IlluminantInkScene
{
    private static readonly InkTargets Targets = new();
    private static readonly NebulaLayer Nebula = new();
    private static readonly InkPostChain Post = new();

    private static readonly IInkLayer[] Layers =
    [
        new InkMoteLayer(),
        new StarfieldLayer(),
        new SingularityLayer(),
    ];

    private static float time;
    private static float fade;
    private static bool seeded;
    private static int seededWidth;
    private static int seededHeight;

    public static void Load()
    {
        time = 0f;
        fade = 0f;
        seeded = false;
    }

    public static void Unload()
    {
        // Graphics resources must not outlive the mod, and Main.QueueMainThreadAction is
        // unnecessary here: Unload for client content already runs on the main thread.
        Release();
    }

    /// <summary>
    ///     Advances the scene by one tick, or tears it down if the menu is no longer showing.
    /// </summary>
    public static void Update(bool active)
    {
        if (!active)
        {
            if (seeded)
            {
                Release();
            }

            return;
        }

        if (!Targets.Ready)
        {
            // Nothing to simulate against until Draw has resolved a target size; the frame
            // geometry every layer depends on isn't known yet.
            return;
        }

        time += 1f / 60f;
        fade = MathF.Min(fade + 1f / InkStyle.FadeInTicks, 1f);

        var frame = BuildFrame();

        foreach (var layer in Layers)
        {
            layer.Update(in frame);
        }
    }

    /// <summary>
    ///     Composes, post-processes and blits the scene, then draws the logo over it.
    /// </summary>
    /// <remarks>
    ///     Called from <see cref="ModMenu.PreDrawLogo" />, which means a batch is already open
    ///     in the state <c>Main.DrawMenu</c> established. This closes it, does its work, and
    ///     reopens it in exactly that state before returning.
    /// </remarks>
    public static void Draw(SpriteBatch spriteBatch, Vector2 logoCenter, float logoScale, float logoRotation, Texture2D? logo)
    {
        var device = Main.instance.GraphicsDevice;
        var presentation = device.PresentationParameters;

        var width = presentation.BackBufferWidth;
        var height = presentation.BackBufferHeight;

        if (!Targets.Ensure(device, width, height))
        {
            return;
        }

        if (!seeded || seededWidth != width || seededHeight != height)
        {
            Seed(width, height);
        }

        var frame = BuildFrame();

        spriteBatch.End();

        // Whatever Terraria had bound -- usually the backbuffer, but a scene filter may have
        // it capturing into screenTarget, and clobbering that would drop the capture.
        var restore = device.GetRenderTargets();

        device.SetRenderTarget(Targets.Scene);
        device.Clear(Color.Black);

        Compose(spriteBatch, in frame);

        var result = Post.Run(spriteBatch, device, Targets, in frame);
        var bloom = Post.PrepareBloom(in frame);

        if (restore.Length > 0)
        {
            device.SetRenderTargets(restore);
        }
        else
        {
            device.SetRenderTarget(null);
        }

        // No transform matrix on the composite: the target is in real backbuffer pixels, so
        // drawing it 1:1 lands it exactly over the frame whatever the menu's UI scale is.
        // Applying UIScaleMatrix here would blow it up by that factor instead.
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

        // Vignette.png is transparent in the middle and opaque black at the edges, so it is
        // an alpha overlay rather than a mask. Applied after the composite so it darkens the
        // stars too -- the shader's own vignette only reaches the ink.
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        spriteBatch.Draw(
            Assets.Textures.VFX.Vignette.Asset.Value,
            new Rectangle(0, 0, width, height),
            Color.White * (InkStyle.VignetteStrength * fade)
        );

        spriteBatch.End();

        // Back into the menu's own space, exactly as Main.DrawMenu had it, and left open for
        // the rest of the menu to keep drawing into.
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.UIScaleMatrix
        );

        if (logo is not null)
        {
            DrawLogo(spriteBatch, logo, logoCenter, logoScale, logoRotation);
        }
    }

    /// <summary>Lays a scrim under the logo so it survives a bright filament passing behind it.</summary>
    private static void DrawLogo(SpriteBatch spriteBatch, Texture2D logo, Vector2 center, float scale, float rotation)
    {
        var scrimTexture = Assets.Textures.VFX.Glow.Asset.Value;

        var spread = logo.Width * scale * InkStyle.LogoScrimSpread / scrimTexture.Width;

        spriteBatch.Draw(
            scrimTexture,
            center,
            null,
            Color.Black * (InkStyle.LogoScrim * fade),
            0f,
            scrimTexture.Size() * 0.5f,
            new Vector2(spread, spread * (logo.Height / (float)logo.Width) * 1.6f),
            SpriteEffects.None,
            0f
        );

        spriteBatch.Draw(
            logo,
            center,
            null,
            Color.White * fade,
            rotation,
            logo.Size() * 0.5f,
            scale,
            SpriteEffects.None,
            0f
        );
    }

    /// <summary>Draws the layer stack into the scene target, batched by blend mode.</summary>
    private static void Compose(SpriteBatch spriteBatch, in InkFrame frame)
    {
        Nebula.Draw(spriteBatch, in frame);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        foreach (var layer in Layers)
        {
            layer.DrawInk(spriteBatch, in frame);
        }

        spriteBatch.End();

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        foreach (var layer in Layers)
        {
            layer.DrawLight(spriteBatch, in frame);
        }

        spriteBatch.End();
    }

    private static InkFrame BuildFrame()
    {
        var width = Targets.Width;
        var height = Targets.Height;

        // A slow drift on two incommensurate periods, so the hole never quite retraces its
        // path and the frame stays alive under a static logo.
        var drift = new Vector2(
            MathF.Sin(time * 0.11f) * InkStyle.SingularityDrift.X,
            MathF.Cos(time * 0.077f) * InkStyle.SingularityDrift.Y
        );

        var singularity = (InkStyle.Singularity + drift) * new Vector2(width, height);

        // Terraria divides Main.screenWidth by the UI scale for the duration of the menu, so
        // Main.mouse* is in UI space while the targets are in backbuffer pixels. Recovering
        // the factor from the two widths avoids depending on how Main.UIScale is set.
        var uiScale = width / (float)Math.Max(Main.screenWidth, 1);
        var cursor = new Vector2(Main.mouseX, Main.mouseY) * uiScale;

        return new InkFrame(width, height, time, singularity, cursor, fade);
    }

    private static void Seed(int width, int height)
    {
        var frame = new InkFrame(width, height, time, InkStyle.Singularity * new Vector2(width, height), Vector2.Zero, fade);

        foreach (var layer in Layers)
        {
            layer.Reset(in frame);
        }

        seeded = true;
        seededWidth = width;
        seededHeight = height;
    }

    private static void Release()
    {
        Targets.Dispose();
        Nebula.Reset();
        Post.Reset();

        seeded = false;
        seededWidth = 0;
        seededHeight = 0;
        time = 0f;
        fade = 0f;
    }

    /// <summary>
    ///     Drops the scene's render targets when the player leaves the menu for a world.
    /// </summary>
    /// <remarks>
    ///     <see cref="ModMenu.OnDeselected" /> covers switching between menus, but entering a
    ///     world leaves this menu selected and simply stops updating it -- without this, two
    ///     backbuffer-sized surfaces stay resident for the rest of the session for a background
    ///     nobody is looking at.
    /// </remarks>
    internal sealed class WorldEntryReleaser : ModSystem
    {
        public override void OnWorldLoad()
        {
            IlluminantInkScene.Update(active: false);
        }
    }
}
