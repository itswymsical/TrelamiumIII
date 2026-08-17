using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Content.Lodestone;

/// <summary>
/// The Lodestone set bonus field, rendered as a ring of glass rather than as a bubble.
///
/// An additive overlay can only ever add glow on top of the frame, which is why the original blue
/// circle read as a decal. A screen filter gets the frame as a texture, so the field can refract
/// it, disperse it and frost it -- and a thing that visibly bends the world behind it reads as a
/// solid object even at almost zero opacity. That is why the field lives here rather than in
/// <see cref="LodestoneHelmet"/>'s draw hook.
///
/// It used to be two passes -- a wide pinch sucking the surround toward the player, and a glass
/// sphere over the top -- and both of them put their strongest distortion where the player was
/// standing. On a <i>mining</i> set that is backwards: the pinch's radial remap has an unbounded
/// gradient at its focus, so the tiles directly under the wearer were the most smeared thing on
/// screen, and an ore in a one-block dip stopped reading as being in range at all.
///
/// So there is one pass now, and the glass is a torus. Everything inside
/// <see cref="BandInner"/> of the radius is untouched frame; all of the bending lives in a band
/// out at the grab radius, where the curvature is carried by the refraction itself. The field is
/// projected outward rather than worn, which is what the set bonus claims it does anyway.
/// </summary>
public sealed class LodestoneFieldWarp : ModSystem
{
    public const string FieldKey = "TrelamiumIII:LodestoneField";

    /// <summary>
    /// Radius of the field, in unzoomed screen pixels. This is one number on purpose: the ring IS
    /// the readout for what the set bonus grabs, so drawing it at any other size would make it
    /// lie. <see cref="LodestoneHelmet.UpdateArmorSet"/> pulls from the same constant.
    /// </summary>
    public const float FieldRadius = 100f;

    // ---- range -------------------------------------------------------------------------------
    // The two ends of the glass, as fractions of FieldRadius. Everything outside [inner, outer] is
    // returned untouched, so together these are the width of the ring and where it sits.

    /// <summary>
    /// Where the glass starts. This is really the promise the shader makes to the wearer: at 0.58
    /// of a 100px field that is a 58px clear disc, comfortably more than the player sprite and
    /// the ring of tiles they are stood on. Raise it for a tighter band and a bigger clear core;
    /// lower it to spread the bending over more of the field.
    /// </summary>
    private const float BandInner = 0.58f;

    /// <summary>
    /// Where it ends. Kept at 1.0 so the outer lip lands exactly on the grab radius -- the ring is
    /// the readout for what the set bonus picks up, and pulling this in would make it under-report.
    /// </summary>
    private const float BandOuter = 1f;

    /// <summary>
    /// Peak radial displacement, in units of <see cref="FieldRadius" /> -- so ~8px on a 100px
    /// field. Small numbers go a long way here because the band concentrates all of it into the
    /// outer two fifths instead of spreading it over the whole disc.
    /// </summary>
    private const float Refract = 0.085f;

    private const float Dispersion = 0.05f;

    private const float Frost = 0.03f;
    private const float FieldLines = 5f;
    private const float FieldFrost = 0.35f;

    // ---- smoothness --------------------------------------------------------------------------

    private const float Glow = 0.05f;

    /// <summary>
    /// Ring tightness. The glow rides <c>thickness^GlowPower</c>, and thickness is already a
    /// smooth hump across the band, so raising this pulls the bright part into a thinner line
    /// <i>and</i> softens how it approaches both lips -- 1 is a broad even swell, 4 is a filament
    /// with a long fade either side. This is the knob for how hard the ring's edges read.
    /// </summary>
    private const float GlowPower = 1.7f;

    /// <summary>
    /// How far the ring's added light tracks the brightness of whatever is behind it, 0..1.
    /// <para>
    /// This is what lets one ring cross a frame with mixed lighting. At 0 the glow, sheen and
    /// specular all add a fixed amount, so the band is invisible against daylit sky and a bright
    /// hoop against unlit stone -- the same material reading two different ways depending on where
    /// it happens to fall. At 1 it holds a constant ratio to its surroundings instead, so it stays
    /// the same glass over grass, cave and sky.
    /// </para>
    /// Back it off toward 0 only if the ring should stay legible in the dark at the cost of
    /// standing out there.
    /// </summary>
    private const float LightResponse = 0.85f;

    private const float Specular = 0.28f;
    private const float Iridescence = 0.02f;
    private const float TintStrength = 0.14f;
    private static readonly Vector3 Tint = new(0.25f, 0.62f, 1f);

    // Carried over from the additive sphere this replaced, so the breathing rate is unchanged
    // even though nothing of the original draw survives.
    private const float PulsePhase = 0.015f * 0.75f;

    // Deliberately not a multiple of PulsePhase, so the band sweep and the breath drift in and
    // out of step and the field never settles into an obvious loop.
    private const float SwirlPhase = 0.0071f;

    private const float FadeInPerTick = 1f / 24f;
    private const float FadeOutPerTick = 1f / 16f;

    /// <summary>
    /// Items the local player's field currently has a hold of. Written by
    /// <see cref="LodestoneHelmet.UpdateArmorSet"/>, consumed and cleared once per tick here.
    /// </summary>
    internal static int GrabbedItemCount;

    private static float _fade;   // 0..1 equip envelope, drives uMix
    private static float _strain; // 0..1 smoothed load, from how many items are in the grip
    private static float _pull;   // 0..1 combined drive: breath + strain
    private static float _swirl;  // -1..1 sweep oscillator

    private static Vector2 _center;    // 0..1 UV
    private static float _pixelRadius; // field radius in real (post-zoom) screen pixels

    public override void Load()
    {
        if (Main.dedServ) return;

        Filters.Scene[FieldKey] = new Filter(new FieldData(Assets.Shaders.LodestoneField.Asset, "P0"), EffectPriority.VeryHigh);
        Filters.Scene[FieldKey].Load();
    }

    public override void Unload()
    {
        _fade = _strain = _pull = _swirl = 0f;
        GrabbedItemCount = 0;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu) return;

        var player = Main.LocalPlayer;
        bool wearing = player.active && !player.dead && LodestoneHelmet.HasLodestoneSet(player);

        _fade = MathHelper.Clamp(_fade + (wearing ? FadeInPerTick : -FadeOutPerTick), 0f, 1f);

        // Six items in range is already a decent haul; past that the field is just "busy".
        float targetStrain = wearing ? MathHelper.Clamp(GrabbedItemCount / 6f, 0f, 1f) : 0f;
        _strain = MathHelper.Lerp(_strain, targetStrain, 0.08f);
        GrabbedItemCount = 0;

        SetFilterActive(FieldKey, _fade > 0f);

        if (_fade <= 0f) return;

        float breathe = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * PulsePhase);
        _pull = MathHelper.Clamp(0.45f + 0.25f * breathe + 0.45f * _strain, 0f, 1f);
        _swirl = MathF.Sin(Main.GameUpdateCount * SwirlPhase);

        // Main.screenPosition is the corner of the *unzoomed* view, but the frame the filter
        // runs on already has the zoom baked in, so the centre has to go through the same
        // matrix every entity does.
        Vector2 screenPos = Vector2.Transform(player.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
        _center = screenPos / new Vector2(Main.screenWidth, Main.screenHeight);
        _pixelRadius = FieldRadius * Main.GameViewMatrix.Zoom.X;
    }

    /// <summary>
    /// Deactivating does not stop the pass immediately: FilterManager ramps a filter's own
    /// opacity down at 1/sec and only unlinks it at zero, so the shader keeps running for about
    /// a second afterwards. It must therefore resolve to an exact passthrough at uMix 0, which is
    /// also why it may not tint by COLOR0 -- see the notes in the .fx file.
    /// </summary>
    private static void SetFilterActive(string key, bool active)
    {
        var filter = Filters.Scene[key];
        if (active == filter.IsActive()) return;

        if (active)
            Filters.Scene.Activate(key, Main.LocalPlayer.Center);
        else
            filter.Deactivate();
    }

    /// <summary>
    /// The shader declares its own uniforms rather than the vanilla screen-shader block, so
    /// Apply() must NOT chain to <see cref="ScreenShaderData.Apply"/>. That writes uColor,
    /// uOpacity, uZoom and friends, which the HLSL compiler stripped out of this effect for being
    /// unused. Holding our own asset reference also keeps this off every protected member of the
    /// base class.
    /// </summary>
    private sealed class FieldData : ScreenShaderData
    {
        private readonly Asset<Effect> _asset;
        private readonly string _pass;

        public FieldData(Asset<Effect> shader, string passName) : base(shader, passName)
        {
            _asset = shader;
            _pass = passName;
        }

        public override void Apply()
        {
            var effect = _asset.Value;
            if (effect == null) return;

            effect.Parameters["uCenter"]?.SetValue(_center);
            effect.Parameters["uAspect"]?.SetValue(new Vector2((float)Main.screenWidth / Main.screenHeight, 1f));
            effect.Parameters["uMix"]?.SetValue(_fade);
            effect.Parameters["uRadius"]?.SetValue(_pixelRadius / Main.screenHeight);

            effect.Parameters["uBandInner"]?.SetValue(BandInner);
            effect.Parameters["uBandOuter"]?.SetValue(BandOuter);

            // Only the bending and the glow breathe. Frost and dispersion are properties of the
            // material, and a material that pulses thickness reads as jelly, not glass.
            effect.Parameters["uRefract"]?.SetValue(Refract * MathHelper.Lerp(0.8f, 1.15f, _pull));
            effect.Parameters["uDispersion"]?.SetValue(Dispersion);

            effect.Parameters["uFrost"]?.SetValue(Frost);
            effect.Parameters["uFieldLines"]?.SetValue(FieldLines);
            effect.Parameters["uFieldFrost"]?.SetValue(FieldFrost);

            // The swirl rides along in the sweep phase, so the bands speed up and ease off
            // instead of turning at a constant, mechanical rate.
            effect.Parameters["uTime"]?.SetValue(Main.GameUpdateCount * 0.02f + 0.6f * _swirl);

            effect.Parameters["uGlow"]?.SetValue(Glow * (0.02f + 0.4f * _pull));
            effect.Parameters["uGlowPower"]?.SetValue(GlowPower);
            effect.Parameters["uLightResponse"]?.SetValue(LightResponse);

            effect.Parameters["uSpecular"]?.SetValue(Specular);
            effect.Parameters["uIridescence"]?.SetValue(Iridescence);
            effect.Parameters["uTint"]?.SetValue(Tint * 0.32f);
            effect.Parameters["uTintStrength"]?.SetValue(TintStrength);

            // Belt and braces: every Begin in FilterManager.EndCapture already resolves to
            // LinearClamp, but a sub-pixel warp sampled point-clamped stair-steps badly and this
            // costs nothing. Passthrough is unaffected -- inside the core and outside the field
            // the shader returns bg directly.
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            effect.CurrentTechnique.Passes[_pass].Apply();
        }
    }
}
