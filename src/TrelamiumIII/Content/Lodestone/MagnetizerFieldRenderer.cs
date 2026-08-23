using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Content.Lodestone;

internal sealed class MagnetizerFieldRenderer : ModSystem
{
    public const string FilterKey = $"{nameof(TrelamiumIII)}:MagnetizerField";


    /// <summary>
    ///     Resolution divisor for the field. Free at half size: the surface is a soft mask deciding
    ///     where the frame shimmers, not an image anybody looks at, so there is no detail in it to
    ///     lose.
    /// </summary>
    private const int FIELD_SCALE = 2;

    // Lightsource.png is a sector with its apex at (10, 48) and a cap bulging out to x = 64.
    private static readonly Rectangle ConeSource = new(10, 9, 55, 80);

    private static readonly Vector2 ConeApex = new(0f, 39f);

    /// <summary>
    ///     How wide the drawn cone is relative to the arc it actually mines.
    /// </summary>
    private const float CONE_FLARE = 0.9f;

    // See MagnetizerBeam.fx for what each of these does. Note that the transverse values are in
    // cone-local t units now, where 1.0 is the cone's lip at any length, rather than in quad v.

    /// <summary>
    ///     Waves along the beam, as <c>DENSITY * length^EXPONENT</c>.
    /// </summary>
    private const float BEAM_WAVE_DENSITY = 0.2f;
    private const float BEAM_WAVE_EXPONENT = 0.62f;

    /// <summary>
    ///     Six reads as a bundle; three read as a corkscrew, because at that count the eye can still
    ///     follow each strand individually and reassemble them into one solid object.
    /// </summary>
    private const float BEAM_STRANDS = 6f;

    private const float BEAM_AMPLITUDE = 0.62f;
    private const float BEAM_THICKNESS = 0.14f;

    /// <summary>
    ///     Spread of the strands' frequencies. Around 0.35 they drift apart fast enough to never repeat but stay close
    ///     enough to still read as one flow.
    /// </summary>
    private const float BEAM_DETUNE = 0.35f;

    private const float BEAM_FLOW = 4.4f;

    private const float BEAM_SWELL_FREQ = 1.35f;
    private const float BEAM_SWELL_AMOUNT = 0.42f;

    private const float BEAM_BEAD_DENSITY = 26f;
    private const float BEAM_BEAD_FLOW = 1.9f;
    private const float BEAM_BEAD_SHARP = 2.6f;
    private const float BEAM_BEAD_AMOUNT = 0.6f;

    /// <summary>EnergyTrail sampled twice at different rates; where the wispy air quality comes from.</summary>
    private const float BEAM_WISP_SCALE = 1.6f;
    private const float BEAM_WISP_AMOUNT = 0.7f;

    private const float BEAM_CORE_WIDTH = 0.22f;
    private const float BEAM_CORE_STRENGTH = 0.35f;

    private const float BEAM_NECK = 0.16f;
    private const float BEAM_FADE = 0.1f;

    /// <summary>Normaliser for the cone art, whose peak alpha sits at about 133 of 255.</summary>
    private const float BEAM_SHAPE_GAIN = 1.95f;

    /// <summary>
    ///     How far the silhouette lookup is displaced by noise, as a fraction of the cone.
    /// </summary>
    /// <remarks>
    ///     This is what makes the mask warp itself rather than only warping the world behind it. A
    ///     smooth, hard-edged cone reads as one big soft decal however much is going on inside it,
    ///     because the outline is the first thing the eye resolves and a clean stretched sprite is
    ///     immediately legible as a stretched sprite.
    ///     <para>
    ///         Kept as a fraction rather than a pixel figure so the boil scales with the beam: a
    ///         fixed-pixel wobble that reads well on a two-block intake disappears on a fifty-block
    ///         one.
    ///     </para>
    /// </remarks>
    private const float BEAM_SHAPE_WARP = 0.075f;

    private const float BEAM_SHAPE_WARP_SCALE = 1.1f;

    /// <summary>
    ///     Split of the mask channel between the cone's own coverage and its strands. Coverage
    ///     dominates: the mask decides where the frame shimmers, and that should be the whole cone,
    ///     with the strands only modulating how hard.
    /// </summary>
    /// <remarks>
    ///     These used to have to sum under 1, back when the filter differentiated this channel to
    ///     find a refraction normal and a clipped mask meant a flat one. The lens is gone, so the mask
    ///     is read directly and saturating it is now harmless -- it just means a solidly shimmering
    ///     interior, which is what is wanted.
    /// </remarks>
    private const float BEAM_FIELD_BODY = 0.9f;
    private const float BEAM_FIELD_STRAND = 0.25f;

    private const float BEAM_GLOW = 2.5f;

    private static readonly Vector3 BeamCoreColor = new(0.9f, 0.96f, 1f);


    /// <summary>
    ///     Peak wobble, in UV. Roughly 15px on a 1080-tall frame.
    /// </summary>
    /// <remarks>
    ///     The only displacement left, and the only screen effect here that earns its cost. It covers
    ///     the whole cone rather than concentrating at the lip, and it is fine-grained enough that the
    ///     ore veins stay countable through it -- which is exactly what the refraction lens this
    ///     replaced could not manage on a tool used at arm's length from a wall of tiles. See
    ///     MagnetizerField.fx for the full account of what came out and why.
    /// </remarks>
    private const float FIELD_WOBBLE = 0.02f;
    private const float FIELD_WOBBLE_SCALE = 1.75f;
    private static readonly Vector2 FieldWobbleDrift = new(0.06f, -0.1f);

    private const float FIELD_GLOW = 1.35f;

    /// <summary>
    ///     How far the added light tracks the brightness behind it, 0..1. High, so the beam holds one
    ///     material across a frame that runs from open sky to unlit stone. See MagnetizerField.fx.
    /// </summary>
    private const float FIELD_LIGHT_RESPONSE = 0.8f;


    private const float FADE_IN_PER_TICK = 1f / 10f;
    private const float FADE_OUT_PER_TICK = 1f / 12f;

    private static WorldFieldTarget? target;
    private static Asset<Effect>? beamShader;
    private static Asset<Texture2D>? coneTexture;
    private static Asset<Texture2D>? wispTexture;
    private static Asset<Texture2D>? noiseTexture;

    private static float fade;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        beamShader = Assets.Shaders.MagnetizerBeam.Asset;
        coneTexture = Assets.Textures.VFX.Lightsource.Asset;
        wispTexture = Assets.Textures.VFX.EnergyTrail.Asset;
        noiseTexture = Assets.Textures.Noise.Perlin.Asset;

        target = new WorldFieldTarget(StampBeams)
        {
            PixelScale = FIELD_SCALE,

            // Premultiplied, because the beam pass emits premultiplied colour, and additive so that
            // two magnetizers pointed at the same rock sum into one denser field.
            Blend = Blends.PremultipliedAdditive,

            // Linear, unlike the metaball layers. Their shaders snap decisions to the field grid and
            // want the chunky edge; a shimmer mask wants the opposite, because a point-sampled mask
            // edge puts a visible staircase along the boundary of where the frame moves.
            Sampler = SamplerState.LinearClamp,
        };

        // High rather than VeryHigh: this is a thing in the world, so it belongs above an affliction
        // the player is under (ScreenWobble, at Medium) and below a whiteout the game is doing to
        // them (ScreenFlash, at VeryHigh).
        Filters.Scene[FilterKey] = new Filter(new FieldData(Assets.Shaders.MagnetizerField.Asset, "P0"), EffectPriority.High);
        Filters.Scene[FilterKey].Load();

        On_Main.CheckMonoliths += static orig =>
        {
            orig();

            // Kept running through the whole fade rather than only while a beam exists. The surface
            // preserves its contents, and FilterManager goes on running a deactivated filter for
            // about a second, so a beam that stops without this leaves its last frame frozen on
            // screen for that second.
            if (fade > 0f)
                target?.Build();
        };
    }

    public override void Unload()
    {
        target = null;
        beamShader = null;
        coneTexture = null;
        wispTexture = null;
        noiseTexture = null;
        fade = 0f;
    }

    public override void OnWorldUnload() => fade = 0f;

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu)
            return;

        bool live = AnyBeam();

        fade = MathHelper.Clamp(fade + (live ? FADE_IN_PER_TICK : -FADE_OUT_PER_TICK), 0f, 1f);

        SetFilterActive(fade > 0f);
    }

    private static bool AnyBeam()
    {
        int type = ModContent.ProjectileType<LodestoneMagnetizerProjectile>();

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (projectile.active && projectile.type == type)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Deactivating does not stop the pass immediately: FilterManager ramps a filter's own opacity
    ///     down at 1/sec and only unlinks it at zero, so the shader keeps running for about a second
    ///     afterwards. It must therefore resolve to an exact passthrough at uMix 0, which is also why
    ///     it may not tint by COLOR0 -- see the notes in the .fx file.
    /// </summary>
    private static void SetFilterActive(bool active)
    {
        Filter filter = Filters.Scene[FilterKey];

        if (active == filter.IsActive())
            return;

        if (active)
            Filters.Scene.Activate(FilterKey, Main.LocalPlayer.Center);
        else
            filter.Deactivate();
    }

    /// <summary>
    ///     Stamps one cone per live magnetizer into the field surface.
    /// </summary>
    private static void StampBeams(SpriteBatch spriteBatch)
    {
        if (beamShader?.Value is not { } effect || coneTexture is not { Value: { } cone })
            return;

        int type = ModContent.ProjectileType<LodestoneMagnetizerProjectile>();

        // Shared across every beam, so they are set once outside the loop.
        effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 2.75f);
        effect.Parameters["uStrandCount"]?.SetValue(BEAM_STRANDS);
        effect.Parameters["uAmplitude"]?.SetValue(BEAM_AMPLITUDE);
        effect.Parameters["uThickness"]?.SetValue(BEAM_THICKNESS);
        effect.Parameters["uDetune"]?.SetValue(BEAM_DETUNE);
        effect.Parameters["uFlow"]?.SetValue(BEAM_FLOW);
        effect.Parameters["uSwellFreq"]?.SetValue(BEAM_SWELL_FREQ);
        effect.Parameters["uSwellAmount"]?.SetValue(BEAM_SWELL_AMOUNT);
        effect.Parameters["uBeadDensity"]?.SetValue(BEAM_BEAD_DENSITY);
        effect.Parameters["uBeadFlow"]?.SetValue(BEAM_BEAD_FLOW);
        effect.Parameters["uBeadSharp"]?.SetValue(BEAM_BEAD_SHARP);
        effect.Parameters["uBeadAmount"]?.SetValue(BEAM_BEAD_AMOUNT);
        effect.Parameters["uWispScale"]?.SetValue(BEAM_WISP_SCALE);
        effect.Parameters["uWispAmount"]?.SetValue(BEAM_WISP_AMOUNT);
        effect.Parameters["uCoreWidth"]?.SetValue(BEAM_CORE_WIDTH);
        effect.Parameters["uCoreStrength"]?.SetValue(BEAM_CORE_STRENGTH);
        effect.Parameters["uNeck"]?.SetValue(BEAM_NECK);
        effect.Parameters["uFade"]?.SetValue(BEAM_FADE);
        effect.Parameters["uShapeGain"]?.SetValue(BEAM_SHAPE_GAIN);
        effect.Parameters["uShapeWarp"]?.SetValue(BEAM_SHAPE_WARP);
        effect.Parameters["uShapeWarpScale"]?.SetValue(BEAM_SHAPE_WARP_SCALE);

        // The crop's place in the sheet, so the pass can turn TEXCOORD0 into a fraction of the BEAM.
        // A source rectangle makes TEXCOORD0 span that sub-rectangle's range within the texture rather
        // than 0..1, and every length-wise term in the shader is a fraction of the beam -- so without
        // these the end feathers and the muzzle neck silently never apply at all. See the .fx header.
        effect.Parameters["uSourceOffset"]?.SetValue(new Vector2(
            ConeSource.X / (float)cone.Width,
            ConeSource.Y / (float)cone.Height));

        effect.Parameters["uSourceSize"]?.SetValue(new Vector2(
            ConeSource.Width / (float)cone.Width,
            ConeSource.Height / (float)cone.Height));

        // The apex is a pixel off the crop's vertical centre, and the strands are measured from it.
        effect.Parameters["uAxis"]?.SetValue(ConeApex.Y / ConeSource.Height);

        effect.Parameters["uFieldBody"]?.SetValue(BEAM_FIELD_BODY);
        effect.Parameters["uFieldStrand"]?.SetValue(BEAM_FIELD_STRAND);
        effect.Parameters["uGlow"]?.SetValue(BEAM_GLOW);
        effect.Parameters["uColorCore"]?.SetValue(BeamCoreColor);

        if (wispTexture is { Value: { } wisp })
            effect.Parameters["uWispTex"]?.SetValue(wisp);

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (!projectile.active || projectile.type != type)
                continue;

            if (projectile.ModProjectile is not LodestoneMagnetizerProjectile beam)
                continue;

            Vector2 muzzle = beam.Muzzle;
            Vector2 delta = beam.Focus - muzzle;
            float length = delta.Length();

            // Below a couple of pixels there is no direction to speak of and the quad degenerates.
            if (length < 2f || beam.Intensity <= 0f)
                continue;

            effect.Parameters["uWaves"]?.SetValue(BEAM_WAVE_DENSITY * MathF.Pow(length, BEAM_WAVE_EXPONENT));
            effect.Parameters["uColorEdge"]?.SetValue(beam.FieldColor.ToVector3());
            effect.Parameters["uIntensity"]?.SetValue(beam.Intensity);

            effect.CurrentTechnique.Passes[0].Apply();

            float width = 2f * length * MathF.Tan(LodestoneMagnetizerProjectile.ARC_ANGLE * 0.5f) * CONE_FLARE;

            spriteBatch.Draw(
                cone,
                muzzle - Main.screenPosition,
                ConeSource,
                Color.White,
                delta.ToRotation(),
                ConeApex,
                new Vector2(length / ConeSource.Width, width / ConeSource.Height),
                SpriteEffects.None,
                0f
            );
        }
    }

    /// <summary>
    ///     Carries the field pass's uniforms on the base type <see cref="Filters.Scene" /> will accept.
    /// </summary>
    private sealed class FieldData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
    {
        private readonly Asset<Effect> fieldAsset = shader;
        private readonly string pass = passName;

        public override void Apply()
        {
            if (fieldAsset.Value is not { } effect)
                return;

            RenderTarget2D? field = target?.Field;

            // uMix carries the whole envelope, and at 0 the pass is an exact passthrough -- so a
            // missing surface degrades to "no effect" rather than to a sampler reading whatever was
            // last bound. The pass is still applied either way: skipping it would leave FilterManager
            // drawing the captured frame through whichever effect happened to be current.
            bool ready = field is { IsDisposed: false } && noiseTexture is { Value: not null };

            effect.Parameters["uMix"]?.SetValue(ready ? fade : 0f);

            if (ready)
            {
                effect.Parameters["uFieldTex"]?.SetValue(field);
                effect.Parameters["uNoiseTex"]?.SetValue(noiseTexture!.Value);

                effect.Parameters["uWobble"]?.SetValue(FIELD_WOBBLE);
                effect.Parameters["uWobbleScale"]?.SetValue(FIELD_WOBBLE_SCALE);
                effect.Parameters["uWobbleDrift"]?.SetValue(FieldWobbleDrift);
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * FIELD_WOBBLE_SCALE);

                // Locks the wobble to the world rather than the screen, so it boils in place while
                // the camera pans instead of swimming along with it. Same convention as GasFbm.fx.
                effect.Parameters["uWorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
                effect.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

                effect.Parameters["uGlow"]?.SetValue(0.7f);
                effect.Parameters["uLightResponse"]?.SetValue(0);
            }

            // Belt and braces: every Begin in FilterManager.EndCapture already resolves to
            // LinearClamp, but a sub-pixel warp sampled point-clamped stair-steps badly and this
            // costs nothing.
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            effect.CurrentTechnique.Passes[pass].Apply();
        }
    }
}
