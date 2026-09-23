using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Content.Items.Lodestone;

internal sealed class MagnetizerFieldRenderer : ModSystem
{
    public const string FilterKey = $"{nameof(TrelamiumIII)}:MagnetizerField";


    /// <summary>Resolution divisor for the field; free at half size since it's a soft mask, not an image.</summary>
    private const int FIELD_SCALE = 2;

    // Lightsource.png is a sector with its apex at (10, 48) and a cap bulging out to x = 64.
    private static readonly Rectangle ConeSource = new(10, 9, 55, 80);

    private static readonly Vector2 ConeApex = new(0f, 39f);

    /// <summary>How wide the drawn cone is relative to the arc it actually mines.</summary>
    private const float CONE_FLARE = 0.9f;

    // See MagnetizerBeam.fx for what each of these does. Note that the transverse values are in
    // cone-local t units now, where 1.0 is the cone's lip at any length, rather than in quad v.

    /// <summary>Waves along the beam, as <c>DENSITY * length^EXPONENT</c>.</summary>
    private const float BEAM_WAVE_DENSITY = 0.2f;
    private const float BEAM_WAVE_EXPONENT = 0.62f;

    /// <summary>Six reads as one bundle; at three the eye follows each strand and it reads as a corkscrew.</summary>
    private const float BEAM_STRANDS = 6f;

    private const float BEAM_AMPLITUDE = 0.62f;
    private const float BEAM_THICKNESS = 0.14f;

    /// <summary>Strand frequency spread; around 0.35 they never repeat but still read as one flow.</summary>
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
    ///     Noise displacement of the silhouette lookup, as a fraction of the cone so it scales with beam length. A clean edge reads as a stretched sprite.
    /// </summary>
    private const float BEAM_SHAPE_WARP = 0.075f;

    private const float BEAM_SHAPE_WARP_SCALE = 1.1f;

    /// <summary>Mask split between cone coverage and strands; coverage dominates. Saturating the mask is harmless.</summary>
    private const float BEAM_FIELD_BODY = 0.9f;
    private const float BEAM_FIELD_STRAND = 0.25f;

    private const float BEAM_GLOW = 2.5f;

    private static readonly Vector3 BeamCoreColor = new(0.9f, 0.96f, 1f);


    /// <summary>Peak wobble in UV (~15px at 1080p), fine enough that ore veins stay countable. See MagnetizerField.fx.</summary>
    private const float FIELD_WOBBLE = 0.02f;
    private const float FIELD_WOBBLE_SCALE = 1.75f;
    private static readonly Vector2 FieldWobbleDrift = new(0.06f, -0.1f);

    private const float FIELD_GLOW = 1.35f;

    /// <summary>How much added light tracks the brightness behind it, 0..1; high so the beam reads the same from sky to stone.</summary>
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

            // Linear: a point-sampled shimmer mask would staircase along the boundary.
            Sampler = SamplerState.LinearClamp,
        };

        // High priority: above ScreenWobble (Medium), below ScreenFlash (VeryHigh).
        Filters.Scene[FilterKey] = new Filter(new FieldData(Assets.Shaders.MagnetizerField.Asset, "P0"), EffectPriority.High);
        Filters.Scene[FilterKey].Load();

        On_Main.CheckMonoliths += static orig =>
        {
            orig();

            // Kept building through the fade: FilterManager runs a deactivated filter for ~1 s, which would freeze the last frame.
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
    ///     Deactivation isn't immediate: FilterManager fades a filter out over ~1 s, so the shader must be an exact passthrough at
    ///     uMix 0 and can't tint by COLOR0 (see the .fx notes).
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

    /// <summary>Stamps one cone per live magnetizer into the field.</summary>
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

        // A source rect makes TEXCOORD0 span the crop, not 0..1; without this offset the length-wise feathering never applies.
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

    /// <summary>Carries the field pass's uniforms on the base type <see cref="Filters.Scene" /> accepts.</summary>
    private sealed class FieldData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
    {
        private readonly Asset<Effect> fieldAsset = shader;
        private readonly string pass = passName;

        public override void Apply()
        {
            if (fieldAsset.Value is not { } effect)
                return;

            RenderTarget2D? field = target?.Field;

            // uMix 0 is an exact passthrough, so a missing surface degrades to no effect. The pass is always applied, or FilterManager
            // draws through whatever effect is current.
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

                // The world offset locks the wobble to the world, as in GasFbm.fx; the shader also undoes the zoom the field bakes in.
                effect.Parameters["uWorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
                effect.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
                effect.Parameters["uZoom"]?.SetValue(Main.GameViewMatrix.Zoom);

                effect.Parameters["uGlow"]?.SetValue(0.7f);
                effect.Parameters["uLightResponse"]?.SetValue(0);
            }

            // LinearClamp explicitly: a sub-pixel warp sampled point-clamped stair-steps.
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            effect.CurrentTechnique.Passes[pass].Apply();
        }
    }
}
