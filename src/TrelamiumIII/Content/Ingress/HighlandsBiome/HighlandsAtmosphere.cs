using ReLogic.Content;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Core.Graphics.ScreenEffects;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary>
/// The highlands' light and air at one moment: the sky's gradient and horizon haze, the haze over the frame, the mist rising
/// off the ground, and the tint on tiles. Keyed to the hour, from a cool blue day through an orange evening haze to an azure
/// night, and greyed and thickened by rain.
/// </summary>
internal readonly record struct HighlandsPalette(Color SkyTop, Color SkyHorizon, Color Haze, Color Fog, Color Mist, Vector3 Light, float FogDensity, float MistDensity)
{
    private const float OvercastFog = 0.08f;
    private const float OvercastMist = 0.2f;
    private const float OvercastGrey = 0.55f;

    /// <summary> How far full cloud dims the sky and haze colours, to sit with the dimmer ground under them; mist is lit by the world, so it dims by itself. </summary>
    private const float OvercastDim = 0.3f;

    private static readonly Vector3 OvercastLight = new(0.86f, 0.89f, 0.93f);

    /// <summary> Palettes at hours of the clock, in order and wrapping past midnight. </summary>
    private static readonly (float Hour, HighlandsPalette Palette)[] Keys =
    [
        (3.0f, new(new(12, 24, 62), new(40, 74, 132), new(58, 96, 160), new(48, 76, 126), new(70, 104, 160), new(0.78f, 0.9f, 1.18f), 0.065f, 0.48f)),
        (4.5f, new(new(58, 70, 128), new(236, 150, 112), new(250, 176, 128), new(214, 168, 150), new(230, 196, 182), new(1.04f, 0.9f, 0.84f), 0.07f, 0.55f)),
        (7.0f, new(new(92, 146, 210), new(196, 220, 236), new(214, 228, 240), new(196, 214, 228), new(226, 236, 244), new(0.97f, 1f, 1.05f), 0.06f, 0.45f)),
        (12.0f, new(new(78, 136, 206), new(170, 206, 232), new(200, 222, 240), new(186, 206, 224), new(220, 232, 242), new(0.95f, 0.99f, 1.06f), 0.045f, 0.16f)),
        (16.5f, new(new(84, 134, 200), new(196, 206, 214), new(226, 214, 196), new(200, 204, 210), new(226, 226, 226), new(1f, 0.98f, 0.98f), 0.05f, 0.2f)),
        (18.6f, new(new(86, 92, 150), new(246, 152, 82), new(255, 168, 96), new(236, 164, 116), new(244, 196, 160), new(1.12f, 0.9f, 0.72f), 0.085f, 0.35f)),
        (20.0f, new(new(34, 44, 98), new(120, 92, 140), new(150, 110, 150), new(96, 92, 140), new(120, 120, 164), new(0.86f, 0.86f, 1.08f), 0.07f, 0.38f)),
        (23.0f, new(new(10, 22, 58), new(30, 70, 128), new(44, 92, 160), new(40, 70, 120), new(64, 100, 156), new(0.76f, 0.9f, 1.2f), 0.065f, 0.42f))
    ];

    /// <summary> The palette for the current hour and weather. </summary>
    public static HighlandsPalette Now => Overcast(AtHour(Hour), MathF.Max(Main.cloudAlpha, Main.raining ? Main.maxRaining : 0f));

    /// <summary> Hour of the clock, 0 to 24: the day runs 4:30 to 19:30. </summary>
    private static float Hour => ((Main.dayTime ? 4.5f : 19.5f) + (float)Main.time / 3600f) % 24f;

    private static HighlandsPalette AtHour(float hour)
    {
        for (int i = 0; i < Keys.Length; i++)
        {
            (float fromHour, HighlandsPalette from) = Keys[i];
            (float toHour, HighlandsPalette to) = Keys[(i + 1) % Keys.Length];

            if (toHour <= fromHour)
                toHour += 24f;

            float h = hour < fromHour ? hour + 24f : hour;

            if (h >= fromHour && h < toHour)
                return Lerp(from, to, (h - fromHour) / (toHour - fromHour));
        }

        return Keys[0].Palette;
    }

    private static HighlandsPalette Overcast(HighlandsPalette clear, float overcast)
    {
        if (overcast <= 0f)
            return clear;

        float grey = overcast * OvercastGrey;
        float dim = 1f - overcast * OvercastDim;

        return clear with
        {
            SkyTop = Dim(Desaturate(clear.SkyTop, grey), dim),
            SkyHorizon = Dim(Desaturate(clear.SkyHorizon, grey), dim),
            Haze = Dim(Desaturate(clear.Haze, grey), dim),
            Fog = Dim(Desaturate(clear.Fog, grey), dim),
            Mist = Desaturate(clear.Mist, grey),
            Light = Vector3.Lerp(clear.Light, OvercastLight, overcast),
            FogDensity = clear.FogDensity + OvercastFog * overcast,
            MistDensity = clear.MistDensity + OvercastMist * overcast
        };
    }

    private static Color Desaturate(Color color, float amount)
    {
        byte luma = (byte)(color.R * 0.299f + color.G * 0.587f + color.B * 0.114f);
        return Color.Lerp(color, new Color(luma, luma, luma), amount);
    }

    private static Color Dim(Color color, float amount) => new(color.ToVector3() * amount);

    private static HighlandsPalette Lerp(HighlandsPalette a, HighlandsPalette b, float t)
    {
        // Eased, so each look holds around its hour and the change gathers between them.
        t = t * t * (3f - 2f * t);

        return new HighlandsPalette(
            Color.Lerp(a.SkyTop, b.SkyTop, t),
            Color.Lerp(a.SkyHorizon, b.SkyHorizon, t),
            Color.Lerp(a.Haze, b.Haze, t),
            Color.Lerp(a.Fog, b.Fog, t),
            Color.Lerp(a.Mist, b.Mist, t),
            Vector3.Lerp(a.Light, b.Light, t),
            MathHelper.Lerp(a.FogDensity, b.FogDensity, t),
            MathHelper.Lerp(a.MistDensity, b.MistDensity, t));
    }
}

/// <summary>
/// Eases the highlands' look in and out as the local player enters and leaves <see cref="HighlandBiome"/>, and tints the
/// world's light with <see cref="HighlandsPalette.Light"/> and its sky toward <see cref="HighlandsPalette.SkyHorizon"/>.
/// </summary>
public sealed class HighlandsAtmosphere : ModSystem
{
    private const float FadeRate = 1f / 90f;

    /// <summary> How far the sky's colour leans toward the palette's horizon; the rest keeps vanilla's, which carries its day and night brightness. </summary>
    private const float BackgroundBlend = 0.5f;

    /// <summary> What vanilla takes off the sun per unit of cloud, and what the highlands' overcast takes instead: a storm here is grey and hazy, not a black silhouette against the haze. </summary>
    private const float VanillaCloudDim = 0.9f;

    private const float CloudDim = 0.55f;

    /// <summary> 0 to 1, how far the highlands' look is in. </summary>
    public static float Presence { get; private set; }

    private static HighlandsBackdrop? backdrop;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        SkyManager.Instance[HighlandsSky.Key] = new HighlandsSky();
        Overlays.Scene[HighlandsBackdrop.Key] = backdrop = new HighlandsBackdrop();
    }

    public override void Unload()
    {
        backdrop?.Free();
        backdrop = null;
    }

    public override void OnWorldUnload() => Presence = 0f;

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || backdrop is null)
            return;

        float target = Main.LocalPlayer.InModBiome<HighlandBiome>() ? 1f : 0f;
        Presence = Presence < target ? MathF.Min(Presence + FadeRate, target) : MathF.Max(Presence - FadeRate, target);

        bool showing = backdrop.Mode is OverlayMode.FadeIn or OverlayMode.Active;

        if (Presence > 0f && !showing)
            Overlays.Scene.Activate(HighlandsBackdrop.Key);
        else if (Presence <= 0f && showing)
            Overlays.Scene.Deactivate(HighlandsBackdrop.Key);
    }

    public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
    {
        if (Presence <= 0f)
            return;

        HighlandsPalette palette = HighlandsPalette.Now;

        float cloud = Main.cloudAlpha * Main.atmo;
        float lift = MathHelper.Lerp(1f, (1f - cloud * CloudDim) / (1f - cloud * VanillaCloudDim), Presence);

        tileColor = new Color(tileColor.ToVector3() * lift);
        backgroundColor = new Color(backgroundColor.ToVector3() * lift);

        tileColor = Color.Lerp(tileColor, new Color(tileColor.ToVector3() * palette.Light), Presence);

        Vector3 horizon = palette.SkyHorizon.ToVector3();
        Vector3 tint = horizon / MathF.Max(MathF.Max(horizon.X, horizon.Y), MathF.Max(horizon.Z, 0.01f));
        backgroundColor = Color.Lerp(backgroundColor, new Color(backgroundColor.ToVector3() * tint), Presence * BackgroundBlend);
    }
}

/// <summary>
/// A gradient from <see cref="HighlandsPalette.SkyTop"/> down to <see cref="HighlandsPalette.SkyHorizon"/> laid thinly over
/// the vanilla sky, with a band of <see cref="HighlandsPalette.Haze"/> along the horizon. The horizon sits on the realm's
/// base ground line, so it drops away below the screen as the player climbs a mountain.
/// </summary>
public sealed class HighlandsSky : CustomSky
{
    public const string Key = "TrelamiumIII:HighlandsSky";

    private const int Bands = 48;
    private const float FadeRate = 0.02f;

    /// <summary> Kept low so the sun, moon and stars still show through. </summary>
    private const float GradientOpacity = 0.35f;

    private const float HazeOpacity = 0.45f;

    /// <summary> Screen heights the gradient climbs above the horizon, and the haze above and below it. </summary>
    private const float GradientHeight = 1.1f;

    private const float HazeHeight = 0.35f;

    private bool active;
    private float intensity;

    /// <summary> Whether the sky has been activated and not deactivated since; it keeps drawing while it fades out after. </summary>
    public bool Wanted => active;

    public override void Update(GameTime gameTime)
        => intensity = active ? MathF.Min(intensity + FadeRate, 1f) : MathF.Max(intensity - FadeRate, 0f);

    public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
    {
        if (maxDepth < float.MaxValue || minDepth >= float.MaxValue || intensity <= 0f)
            return;

        HighlandsPalette palette = HighlandsPalette.Now;
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        float horizon = IngressRealm.BaseSurfaceY * 16f - Main.screenPosition.Y;
        float gradientTop = horizon - Main.screenHeight * GradientHeight;
        float hazeReach = Main.screenHeight * HazeHeight;

        for (int i = 0; i < Bands; i++)
        {
            int top = i * Main.screenHeight / Bands;
            int bottom = (i + 1) * Main.screenHeight / Bands;
            float middle = (top + bottom) * 0.5f;
            var strip = new Rectangle(0, top, Main.screenWidth, bottom - top);

            float t = Math.Clamp((middle - gradientTop) / (horizon - gradientTop), 0f, 1f);
            spriteBatch.Draw(pixel, strip, Color.Lerp(palette.SkyTop, palette.SkyHorizon, t * t) * (GradientOpacity * intensity));

            float haze = 1f - MathF.Min(MathF.Abs(middle - horizon) / hazeReach, 1f);
            spriteBatch.Draw(pixel, strip, palette.Haze * (haze * haze * HazeOpacity * intensity));
        }
    }

    public override bool IsActive() => active || intensity > 0f;

    public override void Activate(Vector2 position, params object[] args) => active = true;

    public override void Deactivate(params object[] args) => active = false;

    public override void Reset()
    {
        active = false;
        intensity = 0f;
    }
}

/// <summary>
/// The highlands' distance: a gradient of haze and blur over the sky and surface background layers, thickest at the horizon
/// and thinning up the sky, building with the hour's mist and the rain. Drawn between the landscape and the world, so walls,
/// tiles and everything standing on them are drawn crisp over it and the haze never goes under a tile.
/// </summary>
internal sealed class HighlandsBackdrop() : Overlay(EffectPriority.VeryLow, RenderLayers.Landscape)
{
    public const string Key = "TrelamiumIII:HighlandsBackdrop";

    private const float HazePerFog = 2f;
    private const float HazePerMist = 0.85f;
    private const float HazeMax = 0.62f;
    private const float BlurPerMist = 14f;
    private const float BlurMax = 10f;

    /// <summary> How much thicker full rain makes the mist's share of the haze and blur. </summary>
    private const float RainBoost = 0.5f;

    /// <summary> Screen heights above the horizon over which the haze and blur thin to nothing. </summary>
    private const float HorizonFalloff = 0.9f;

    /// <summary> Rows over which they fade out approaching the bottom of the surface layer. </summary>
    private const float SurfaceFadeRows = 20f;

    private RenderTarget2D? copy;

    public override bool IsVisible() => HighlandsAtmosphere.Presence > 0f;

    public override void Activate(Vector2 position, params object[] args) => Mode = OverlayMode.FadeIn;

    public override void Deactivate(params object[] args) => Mode = OverlayMode.FadeOut;

    public override void Update(GameTime gameTime) { }

    public override void Draw(SpriteBatch spriteBatch)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;
        RenderTargetBinding[] bound = device.GetRenderTargets();

        // Straight to the backbuffer there is nothing to read back; HighlandsCaptureFilter is what keeps that from happening.
        if (bound.Length == 0 || bound[0].RenderTarget is not RenderTarget2D frame || Assets.Shaders.HighlandsBackdrop.Asset is not { IsLoaded: true, Value: { } effect })
            return;

        if (copy is null || copy.IsDisposed || copy.Width != frame.Width || copy.Height != frame.Height)
        {
            copy?.Dispose();
            copy = new RenderTarget2D(device, frame.Width, frame.Height, false, frame.Format, DepthFormat.None);
        }

        Apply(effect, frame);
        spriteBatch.End();

        device.SetRenderTarget(copy);
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        effect.CurrentTechnique.Passes[0].Apply();
        spriteBatch.Draw(frame, Vector2.Zero, Color.White);
        spriteBatch.End();

        // The frame's target discards what it held on rebinding, so all of it is written back.
        device.SetRenderTarget(frame);
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        spriteBatch.Draw(copy, Vector2.Zero, Color.White);
        spriteBatch.End();

        // Back as the overlay manager began it, since it ends the batch after the last overlay.
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Main.Transform);
    }

    public void Free()
    {
        RenderTarget2D? target = copy;
        copy = null;

        if (target is not null)
            Main.QueueMainThreadAction(target.Dispose);
    }

    private static void Apply(Effect effect, RenderTarget2D frame)
    {
        HighlandsPalette palette = HighlandsPalette.Now;
        float presence = HighlandsAtmosphere.Presence;
        float rain = Main.raining ? Main.maxRaining : 0f;
        float mist = palette.MistDensity * (1f + rain * RainBoost);
        float zoom = Main.GameViewMatrix.Zoom.Y;

        effect.Parameters["uTexel"]?.SetValue(new Vector2(1f / frame.Width, 1f / frame.Height));
        effect.Parameters["uHorizon"]?.SetValue(ScreenRow(IngressRealm.BaseSurfaceY, zoom));
        effect.Parameters["uFalloff"]?.SetValue(HorizonFalloff);
        effect.Parameters["uSurface"]?.SetValue(ScreenRow((float)Main.worldSurface, zoom));
        effect.Parameters["uSurfaceFade"]?.SetValue(SurfaceFadeRows * 16f * zoom / Main.screenHeight);
        effect.Parameters["uHazeColor"]?.SetValue(palette.Fog.ToVector3());
        effect.Parameters["uHaze"]?.SetValue(MathF.Min(palette.FogDensity * HazePerFog + mist * HazePerMist, HazeMax) * presence);
        effect.Parameters["uBlur"]?.SetValue(MathF.Min(mist * BlurPerMist, BlurMax) * presence);
    }

    /// <summary> Where world row <paramref name="tileY"/> falls on screen, in uv; the view zooms about the middle of the screen. </summary>
    private static float ScreenRow(float tileY, float zoom)
        => ((tileY * 16f - Main.screenPosition.Y - Main.screenHeight * 0.5f) * zoom + Main.screenHeight * 0.5f) / Main.screenHeight;
}

/// <summary>
/// Leaves the frame as it is. An active scene filter is what makes vanilla render the frame into a target rather than straight
/// to the backbuffer, and <see cref="HighlandsBackdrop"/> needs a target it can read back.
/// </summary>
internal sealed class HighlandsCaptureFilter : ScreenFilterSystem<HighlandsCaptureFilter, Assets.Shaders.HighlandsCapture.Parameters>
{
    protected override Asset<Effect> Shader => Assets.Shaders.HighlandsCapture.Asset;

    protected override EffectPriority Priority => EffectPriority.VeryLow;

    protected override void Tick() => RampTo(HighlandsAtmosphere.Presence, 1);

    protected override bool Push(Assets.Shaders.HighlandsCapture.Parameters parameters) => true;
}
