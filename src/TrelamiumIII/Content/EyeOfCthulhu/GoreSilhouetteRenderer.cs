using ReLogic.Content;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.IlluminantInk;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>
///     Renders the drifting remains a second time, as one wobbling ink silhouette being dragged
///     toward the vortex.
/// </summary>
/// <remarks>
///     <para>
///         This is not the pieces and it is not the gas. Each piece already draws its own sprite and
///         its own bloom, and <see cref="Core.Graphics.Metaballs.Layers.InkLayer" /> already sheds gas
///         around them. What neither can do is see more than one piece at a time: a sprite draw only
///         knows the fragment it is on, so nothing in either path can fuse two pieces into a single
///         shape, or smear that shape along a direction. Both need a surface that holds the whole
///         accumulation, which is what <see cref="WorldFieldTarget" /> is for.
///     </para>
///     <para>
///         The pieces stamp into that surface as flat coverage, and <c>InkSilhouette.fx</c> reads it
///         back once: wobble the lookup, smear it toward the drain, cut it hard, rim the cut. The cut
///         happening <em>after</em> the smear is what makes separate pieces fuse -- two whose own
///         coverage never touches will still both trail into the gap between them and cross the
///         threshold there.
///     </para>
///     <para>
///         <b>The one hard rule about where this runs.</b> The field is built in
///         <c>CheckMonoliths</c>, the single point in the frame where Terraria expects a mod to be
///         binding a render target, and only read back in <c>DrawDust</c>. Building it any later means
///         claiming a target mid-scene and having to restore whatever was there.
///     </para>
/// </remarks>
internal sealed class GoreSilhouetteRenderer : ModSystem
{
    /// <summary>
    ///     Resolution divisor for the field.
    /// </summary>
    /// <remarks>
    ///     Free at half size, and better than free: the shader quantises every decision to this grid,
    ///     so one field texel becomes one chunky output pixel, which is the same look the metaball
    ///     layers get out of the same trick.
    /// </remarks>
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

    /// <summary>
    ///     Radial reach of the smear at full wind-in, as a fraction of the focus-to-pixel vector.
    /// </summary>
    private const float SMEAR = 0.09f;

    private const float SPIN = 0.05f;

    /// <summary>
    ///     How fast the smear ramps up away from the focus.
    /// </summary>
    /// <remarks>
    ///     Above 1 so the throat itself stays crisp while pieces further out tear badly. A uniform
    ///     smear reads as the whole image being out of focus rather than as matter being pulled.
    /// </remarks>
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

            // Additive so two overlapping pieces sum into a denser field rather than the second
            // simply replacing the first -- summing is the whole basis of them fusing.
            Blend = BlendState.Additive,

            // Point, like the metaball layers and unlike the shimmer masks: the shader snaps its
            // decisions to this grid and wants the chunky edge that comes with it.
            Sampler = SamplerState.PointClamp,
        };

        On_Main.CheckMonoliths += static orig =>
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

    /// <summary>
    ///     How strongly the silhouette shows, taken from the pieces rather than set by the cutscene.
    /// </summary>
    /// <remarks>
    ///     The brightest piece wins. Every piece's own envelope is already driven by the cutscene, so
    ///     reading it back here means there is one schedule for the whole effect instead of two that
    ///     can disagree -- and a player who has seen the wake already, and so gets no cutscene, gets
    ///     ordinary debris with no silhouette for free.
    /// </remarks>
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

    /// <summary>Stamps every live piece into the field.</summary>
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

        // Set by name against the raw effect rather than through a generated parameter struct, which
        // is what the metaball layers and MagnetizerFieldRenderer do. The null-conditionals matter:
        // fxc strips any uniform a pass does not actually reference, so a parameter can legitimately
        // be absent from a shader that still compiled fine.
        effect.Parameters["uNoiseTex"]?.SetValue(noise);
        effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        effect.Parameters["uFieldTexel"]?.SetValue(new Vector2(
            FIELD_SCALE / (float)Main.screenWidth,
            FIELD_SCALE / (float)Main.screenHeight));

        effect.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // Locks the wobble to the world, so the outline boils in place while the camera pans instead
        // of swimming along with it. Same convention as GasFbm.fx and MagnetizerField.fx.
        effect.Parameters["uWorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);

        effect.Parameters["uWobble"]?.SetValue(WOBBLE);
        effect.Parameters["uWobbleScale"]?.SetValue(WOBBLE_SCALE);
        effect.Parameters["uWobbleDrift"]?.SetValue(WobbleDrift);

        effect.Parameters["uCenter"]?.SetValue(FocusUv());

        // Smear ramps with the vortex rather than with the pieces: while they are only drifting there
        // is nothing pulling them, and a silhouette already tearing toward a hole that has not opened
        // yet gives the ending away.
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
    /// <remarks>
    ///     Through the game view matrix rather than a bare subtraction, so the focus tracks the world
    ///     under zoom -- the same conversion <see cref="InkVortex" /> uses to place its disc, and for
    ///     the same reason.
    /// </remarks>
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
