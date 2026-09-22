using ReLogic.Content;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.IlluminantInk;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>Renders the drifting remains again as one wobbling ink silhouette dragged toward the vortex.</summary>
internal sealed class GoreSilhouetteRenderer : ModSystem
{
    /// <summary>Resolution divisor for the field.</summary>
    private const int FIELD_SCALE = 2;

    /// <summary>Coverage level where ink exists. Low, so pieces fuse early.</summary>
    private const float THRESHOLD = 0.30f;

    private const float BODY_ALPHA = 0.9f;

    private const float BANDS = 3f;

    private const float RIM_ALPHA = 0.85f;

    /// <summary>Peak displacement of the silhouette lookup, in UV.</summary>
    private const float WOBBLE = 0.014f;

    private const float WOBBLE_SCALE = 1.9f;

    private static readonly Vector2 WobbleDrift = new(0.03f, -0.05f);

    /// <summary>Radial reach of the smear at full wind-in, as a fraction of the focus-to-pixel vector.</summary>
    private const float SMEAR = 0.09f;

    private const float SPIN = 0.05f;

    /// <summary>How fast the smear ramps up away from the focus.</summary>
    private const float FALLOFF = 1.4f;

    private static WorldFieldTarget? field;
    private static Asset<Effect>? shader;
    private static Asset<Texture2D>? noiseTexture;

    /// <summary>0..1, driven off the pieces themselves. See <see cref="Envelope" />.</summary>
    private static float envelope;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;

        field = new WorldFieldTarget(StampPieces)
        {
            PixelScale = FIELD_SCALE,

            // Additive: overlapping pieces must sum to fuse.
            Blend = BlendState.Additive,

            // Point sampling: the shader snaps to this grid and wants the chunky edge.
            Sampler = SamplerState.PointClamp,
        };

        On_Main.DoDraw_UpdateCameraPosition += static orig =>
        {
            orig();

            if (envelope > 0f)
                field?.Build();
        };

        On_Main.DrawDust += static (orig, self) =>
        {
            orig(self);

            Composite();
        };
    }

    public override void Unload()
    {
        field = null;
        shader = null;
        noiseTexture = null;
        envelope = 0f;
    }

    public override void OnWorldUnload() => envelope = 0f;

    public override void PostUpdateEverything()
    {
        if (!Main.dedServ && !Main.gameMenu)
            envelope = Envelope();
    }

    /// <summary>How strongly the silhouette shows, taken from the pieces rather than set by the cutscene.</summary>
    private static float Envelope()
    {
        float peak = 0f;

        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            if (piece.GlowEnvelope > peak)
                peak = piece.GlowEnvelope;
        }

        return MathHelper.Clamp(peak, 0f, 1f);
    }

    private static void StampPieces(SpriteBatch spriteBatch)
    {
        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
            piece.StampCoverage(spriteBatch);
    }

    /// <summary>Reads the field back through the silhouette pass.</summary>
    private static void Composite()
    {
        if (envelope <= 0f || Main.gameMenu)
            return;

        if (field?.Field is not { IsDisposed: false } surface)
            return;

        if (noiseTexture is not { Value: { } noise })
            return;

        shader ??= Assets.Shaders.InkSilhouette.Asset;

        if (shader.Value is not { } effect)
            return;

        // Null-conditionals matter: fxc strips uniforms a pass doesn't reference.
        effect.Parameters["uNoiseTex"]?.SetValue(noise);
        effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        effect.Parameters["uFieldTexel"]?.SetValue(new Vector2(
            FIELD_SCALE / (float)Main.screenWidth,
            FIELD_SCALE / (float)Main.screenHeight));

        effect.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // The world offset locks the wobble to the world so it boils in place while the camera pans. The zoom completes the
        // lock: the field bakes zoom into its transform, so the shader must undo it first.
        effect.Parameters["uWorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["uZoom"]?.SetValue(Main.GameViewMatrix.Zoom);

        effect.Parameters["uWobble"]?.SetValue(WOBBLE);
        effect.Parameters["uWobbleScale"]?.SetValue(WOBBLE_SCALE);
        effect.Parameters["uWobbleDrift"]?.SetValue(WobbleDrift);

        effect.Parameters["uCenter"]?.SetValue(FocusUv());

        // Smear follows the vortex, not the pieces, so the silhouette doesn't tear toward a hole that hasn't opened.
        float pull = MathHelper.Clamp(InkVortex.Fade, 0f, 1f);

        effect.Parameters["uSmear"]?.SetValue(SMEAR * pull);
        effect.Parameters["uSpin"]?.SetValue(SPIN * pull);
        effect.Parameters["uFalloff"]?.SetValue(FALLOFF);

        effect.Parameters["uThreshold"]?.SetValue(THRESHOLD);
        effect.Parameters["uBodyAlpha"]?.SetValue(BODY_ALPHA);
        effect.Parameters["uBands"]?.SetValue(BANDS);

        // A shade under InkLayer's palette, so the pieces read as denser than the gas around them.
        effect.Parameters["uShadowColor"]?.SetValue(new Vector3(0.010f, 0.011f, 0.017f));
        effect.Parameters["uBodyColor"]?.SetValue(new Vector3(0.038f, 0.035f, 0.070f));
        effect.Parameters["uCoreColor"]?.SetValue(new Vector3(0.14f, 0.13f, 0.24f));
        effect.Parameters["uRimColor"]?.SetValue(new Vector3(0.70f, 0.71f, 0.85f));
        effect.Parameters["uRimAlpha"]?.SetValue(RIM_ALPHA);
        effect.Parameters["uOpacity"]?.SetValue(envelope);

        var spriteBatch = Main.spriteBatch;

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        effect.CurrentTechnique.Passes[0].Apply();

        spriteBatch.Draw(surface, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, FIELD_SCALE, SpriteEffects.None, 0f);

        spriteBatch.End();
    }

    /// <summary>The point the coverage is smeared toward, in 0..1 screen space.</summary>
    private static Vector2 FocusUv()
    {
        Vector2 screen = Vector2.Transform(
            InkVortex.Center - Main.screenPosition,
            Main.GameViewMatrix.TransformationMatrix
        );

        return new Vector2(
            screen.X / Math.Max(Main.screenWidth, 1),
            screen.Y / Math.Max(Main.screenHeight, 1)
        );
    }
}
