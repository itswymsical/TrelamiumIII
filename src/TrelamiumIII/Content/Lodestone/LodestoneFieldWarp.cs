using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Content.Lodestone;

/// <summary>
/// The Lodestone set bonus field, rendered as glass rather than as light.
///
/// An additive overlay can only ever add glow on top of the frame, which is why the old blue
/// circle read as a decal. A screen filter gets the frame as a texture, so the sphere can
/// refract it, disperse it, and frost it -- and a thing that visibly bends the world behind it
/// reads as a solid object even at almost zero opacity. That is the whole reason the sphere
/// moved out of <see cref="LodestoneHelmet"/>'s draw hook and in here.
///
/// Two passes, both centred on the local player:
///   Pinch - a wide disc reaching past the grab range, sampling from further out so the
///           surround creeps inward. The magnetic pull, outside the glass.
///   Glass - the sphere itself: refracting core, compressing bevel, chromatic fringe, frost,
///           specular hotspot and a bright silhouette ring.
///
/// The surround squeezing inward while the sphere holds its shape against it is what sells the
/// field as something being contained rather than something being drawn.
/// </summary>
public sealed class LodestoneFieldWarp : ModSystem
{
    public const string PinchKey = "TrelamiumIII:LodestoneFieldPinch";
    public const string GlassKey = "TrelamiumIII:LodestoneFieldGlass";

    /// <summary>
    /// Radius of the field, in unzoomed screen pixels. This is one number on purpose: the
    /// sphere IS the readout for what the set bonus grabs, so drawing it at any other size
    /// would make it lie. <see cref="LodestoneHelmet.UpdateArmorSet"/> pulls from the same
    /// constant.
    /// </summary>
    public const float FieldRadius = 100f;

    // The pull reaches past the sphere so the field has a gradient of influence rather than a
    // hard shell, and so the glass has something visibly moving toward it.
    private const float PullReach = 1.6f;

    private const float PinchSoftness = 0.45f;
    private const float PinchPowerMin = 0.80f; // radial exponent at full pull; < 1 = inward
    private const float PinchSwirl = 0.12f;    // radians of twist at the focus, at full pull

    // See LiquidGlassSphere.fx for what each lobe does.

    private const float GlassBulge = 0.33f;      // magnifying core lobe
    private const float GlassBevel = 2.0f;       // compressing rim lobe
    private const float GlassBevelStart = 0.5f; // normalised radius the bevel begins at
    private const float GlassDispersion = 0.18f; // chromatic split, relative to the bending
    private const float GlassFrost = 0.001f;     // rim blur radius, in units of FieldRadius
    private const float GlassFieldLines = 5f;    // frost bands sweeping the polar axis
    private const float GlassFieldFrost = 0.35f;
    private const float GlassRingInner = 1f;
    private const float GlassRingPower = 1.6f;
    private const float GlassRingBrightness = 0.55f;
    private const float GlassSpecular = 0.3f;
    private const float GlassIridescence = 0.1f;
    private const float GlassTintStrength = 0.1f;
    private static readonly Vector3 GlassTint = new(0.25f, 0.62f, 1f);

    // Carried over from the additive sphere this replaced, so the breathing rate is unchanged
    // even though nothing of the original draw survives.
    private const float PulsePhase = 0.015f * 0.75f;

    // Deliberately not a multiple of PulsePhase, so the twist and the breath drift in and out
    // of step and the field never settles into an obvious loop.
    private const float SwirlPhase = 0.0071f;

    private const float FadeInPerTick = 1f / 24f;
    private const float FadeOutPerTick = 1f / 16f;

    /// <summary>
    /// Items the local player's field currently has a hold of. Written by
    /// <see cref="LodestoneHelmet.UpdateArmorSet"/>, consumed and cleared once per tick here.
    /// </summary>
    internal static int GrabbedItemCount;

    private static float _fade;   // 0..1 equip envelope, drives uMix on both passes
    private static float _strain; // 0..1 smoothed load, from how many items are in the grip
    private static float _pull;   // 0..1 combined drive: breath + strain
    private static float _swirl;  // -1..1 twist oscillator

    private static Vector2 _center;    // 0..1 UV
    private static float _pixelRadius; // sphere radius in real (post-zoom) screen pixels

    public override void Load()
    {
        if (Main.dedServ) return;

        Filters.Scene[PinchKey] = new Filter(new PinchData(Assets.Shaders.PinchShader.Asset, "P0"), EffectPriority.VeryHigh);
        Filters.Scene[PinchKey].Load();

        // Activated second, which puts it last in the chain and so composites over the pull
        // rather than being dragged by it: FilterManager.OnActivate inserts after the first
        // node of equal-or-higher priority, and EndCapture walks the list front to back.
        Filters.Scene[GlassKey] = new Filter(new GlassData(Assets.Shaders.LiquidGlassSphere.Asset, "P0"), EffectPriority.VeryHigh);
        Filters.Scene[GlassKey].Load();
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

        SetFilterActive(PinchKey, _fade > 0f);
        SetFilterActive(GlassKey, _fade > 0f);

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
    /// opacity down at 1/sec and only unlinks it at zero, so both shaders keep running for
    /// about a second afterwards. They must therefore resolve to an exact passthrough at
    /// uMix 0, which is also why neither may tint by COLOR0 -- see the notes in the .fx files.
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
    /// Both shaders declare their own uniforms rather than the vanilla screen-shader block, so
    /// Apply() must NOT chain to <see cref="ScreenShaderData.Apply"/>. That writes uColor,
    /// uOpacity, uZoom and friends, which the HLSL compiler stripped out of these effects for
    /// being unused. Holding our own asset reference also keeps this off every protected member
    /// of the base class.
    /// </summary>
    private abstract class FieldWarpData : ScreenShaderData
    {
        private readonly Asset<Effect> _warpAsset;
        private readonly string _pass;

        protected FieldWarpData(Asset<Effect> shader, string passName) : base(shader, passName)
        {
            _warpAsset = shader;
            _pass = passName;
        }

        public override void Apply()
        {
            var effect = _warpAsset.Value;
            if (effect == null) return;

            effect.Parameters["uCenter"]?.SetValue(_center);
            effect.Parameters["uAspect"]?.SetValue(new Vector2((float)Main.screenWidth / Main.screenHeight, 1f));
            effect.Parameters["uMix"]?.SetValue(_fade);

            SetWarp(effect);

            // Belt and braces: every Begin in FilterManager.EndCapture already resolves to
            // LinearClamp, but a sub-pixel warp sampled point-clamped stair-steps badly and
            // this costs nothing. Passthrough is unaffected -- outside the disc both shaders
            // resolve to exactly uv, so bilinear still lands on texel centres.
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            effect.CurrentTechnique.Passes[_pass].Apply();
        }

        protected abstract void SetWarp(Effect effect);
    }

    private sealed class PinchData : FieldWarpData
    {
        public PinchData(Asset<Effect> shader, string passName) : base(shader, passName) { }

        protected override void SetWarp(Effect effect)
        {
            effect.Parameters["uRadius"]?.SetValue(_pixelRadius * PullReach / Main.screenHeight);
            effect.Parameters["uPower"]?.SetValue(MathHelper.Lerp(1f, PinchPowerMin, _pull));
            effect.Parameters["uSoftness"]?.SetValue(PinchSoftness);

            effect.Parameters["uSwirl"]?.SetValue(0.025f * _swirl * _pull);
        }
    }

    private sealed class GlassData : FieldWarpData
    {
        public GlassData(Asset<Effect> shader, string passName) : base(shader, passName) { }

        protected override void SetWarp(Effect effect)
        {
            effect.Parameters["uRadius"]?.SetValue(_pixelRadius / Main.screenHeight);

            // Only the bulge and the ring breathe. Frost and bevel are properties of the
            // material, and a material that pulses thickness reads as jelly, not glass.
            effect.Parameters["uBulge"]?.SetValue(GlassBulge * MathHelper.Lerp(0.75f, 1.05f, _pull));
            effect.Parameters["uBevel"]?.SetValue(GlassBevel);
            effect.Parameters["uBevelStart"]?.SetValue(GlassBevelStart);
            effect.Parameters["uDispersion"]?.SetValue(GlassDispersion);

            effect.Parameters["uFrost"]?.SetValue(GlassFrost);
            effect.Parameters["uFieldLines"]?.SetValue(GlassFieldLines);
            effect.Parameters["uFieldFrost"]?.SetValue(GlassFieldFrost);
            effect.Parameters["uTime"]?.SetValue(Main.GameUpdateCount * 0.02f);

            effect.Parameters["uRingInner"]?.SetValue(GlassRingInner);
            effect.Parameters["uRingPower"]?.SetValue(GlassRingPower);
            effect.Parameters["uRingBrightness"]?.SetValue(GlassRingBrightness * (0.1f + 0.35f * _pull));

            effect.Parameters["uSpecular"]?.SetValue(GlassSpecular);
            effect.Parameters["uIridescence"]?.SetValue(GlassIridescence);
            effect.Parameters["uTint"]?.SetValue(GlassTint * 0.6f);
            effect.Parameters["uTintStrength"]?.SetValue(GlassTintStrength);
        }
    }
}
