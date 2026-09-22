using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Content.Lodestone;

/// <summary>
/// The Lodestone set bonus field as a screen filter, so it can refract, disperse and frost the frame. The glass is a torus:
/// everything inside <see cref="BandInner"/> is untouched, and all bending sits in a band at the grab radius.
/// </summary>
public sealed class LodestoneFieldWarp : ModSystem
{
    public const string FieldKey = "TrelamiumIII:LodestoneField";

    /// <summary>
    /// Field radius in unzoomed screen pixels. Also the grab radius (<see cref="LodestoneHelmet.UpdateArmorSet"/>), so the ring shows true range.
    /// </summary>
    public const float FieldRadius = 100f;

    // ---- range -------------------------------------------------------------------------------
    // The two ends of the glass, as fractions of FieldRadius; outside [inner, outer] is untouched.

    /// <summary>
    /// Where the glass starts: 0.58 of a 100px field is a 58px clear disc around the wearer. Lower spreads the bending wider.
    /// </summary>
    private const float BandInner = 0.58f;

    /// <summary>Where it ends. Kept at 1 so the outer lip lands exactly on the grab radius.</summary>
    private const float BandOuter = 1f;

    /// <summary>Peak radial displacement in units of <see cref="FieldRadius" /> (~8px at 100px), concentrated in the band.</summary>
    private const float Refract = 0.085f;

    private const float Dispersion = 0.05f;

    private const float Frost = 0.03f;
    private const float FieldLines = 5f;
    private const float FieldFrost = 0.35f;

    // ---- smoothness --------------------------------------------------------------------------

    private const float Glow = 0.05f;

    /// <summary>Ring tightness: glow rides <c>thickness^GlowPower</c>; 1 is a broad swell, 4 a thin filament with long fades.</summary>
    private const float GlowPower = 1.7f;

    /// <summary>
    /// How much the ring's added light tracks the brightness behind it, 0..1. At 1 it reads as the same glass over sky, grass
    /// and cave; toward 0 it stays legible in the dark but glares there.
    /// </summary>
    private const float LightResponse = 0.85f;

    private const float Specular = 0.28f;
    private const float Iridescence = 0.02f;
    private const float TintStrength = 0.14f;
    private static readonly Vector3 Tint = new(0.25f, 0.62f, 1f);

    // Carried over from the old additive sphere so the breathing rate is unchanged.
    private const float PulsePhase = 0.015f * 0.75f;

    // Deliberately not a multiple of PulsePhase, so the band sweep and the breath drift in and
    // out of step and the field never settles into an obvious loop.
    private const float SwirlPhase = 0.0071f;

    private const float FadeInPerTick = 1f / 24f;
    private const float FadeOutPerTick = 1f / 16f;

    /// <summary>Items the local field is holding. Written by <see cref="LodestoneHelmet.UpdateArmorSet"/>, cleared here each tick.</summary>
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

        // screenPosition is the unzoomed view corner, but the filtered frame has zoom baked in, so transform like any entity.
        Vector2 screenPos = Vector2.Transform(player.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
        _center = screenPos / new Vector2(Main.screenWidth, Main.screenHeight);
        _pixelRadius = FieldRadius * Main.GameViewMatrix.Zoom.X;
    }

    /// <summary>
    /// Deactivation isn't immediate: FilterManager fades a filter out over ~1 s, so the shader must be an exact passthrough at
    /// uMix 0 and can't tint by COLOR0 (see the .fx notes).
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
    /// The shader declares its own uniforms, so Apply() must NOT chain to <see cref="ScreenShaderData.Apply"/>, which writes
    /// vanilla uniforms the compiler stripped from this effect.
    /// </summary>
    private sealed class FieldData : ScreenShaderData
    {
        private readonly Asset<Effect> _warpAsset;
        private readonly string _pass;

        public FieldData(Asset<Effect> shader, string passName) : base(shader, passName)
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

            // LinearClamp explicitly: a sub-pixel warp sampled point-clamped stair-steps. Passthrough regions are unaffected.
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            effect.CurrentTechnique.Passes[_pass].Apply();
        }
    }
}
