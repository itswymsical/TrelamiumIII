namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Every art-direction knob for the Illuminant Ink menu, gathered in one place.
/// </summary>
/// <remarks>
///     The old renderer scattered its magic numbers across the draw calls that used them, which
///     meant retuning the look was a hunt. Nothing below is referenced more than a handful of
///     times; the point is that the whole palette can be read top to bottom without opening a
///     layer.
///     <para />
///     Distances that describe the scene are in <em>half-height units</em>: the same
///     aspect-corrected space <c>InkNebula.fx</c> works in, where <c>0.5</c> is half the screen
///     height. That keeps the singularity the same apparent size on every monitor, and lets the
///     shader and the C# layers agree on where the horizon is without converting between them.
/// </remarks>
internal static class InkStyle
{
    // ---- composition ----------------------------------------------------------------

    /// <summary>Where the singularity sits, in 0..1 screen space.</summary>
    /// <remarks>
    ///     Off-centre and below the logo: dead centre fights the logo for attention, and the
    ///     lensing reads better when the frame isn't symmetric about it.
    /// </remarks>
    public static readonly Vector2 Singularity = new(0.565f, 0.545f);

    /// <summary>Amplitude of the singularity's idle drift, in 0..1 screen space.</summary>
    public static readonly Vector2 SingularityDrift = new(0.012f, 0.008f);

    /// <summary>Event-horizon radius, half-height units. Must match <c>uHorizon</c>.</summary>
    public const float Horizon = 0.055f;

    // ---- nebula (InkNebula.fx) ------------------------------------------------------
    //
    // Tuned against an offline CPU replica of the shader rather than by eye in-game, so
    // these are measured rather than guessed. The two that are not free to move are
    // NebulaPivot and NebulaReliefEps -- see their notes.

    public const float NebulaScale = 2.6f;

    /// <summary>
    ///     Median of the raw four-octave FBM sum for <c>FbmNoise2.png</c>.
    /// </summary>
    /// <remarks>
    ///     Measured, not chosen: that texture averages far brighter than a symmetric noise
    ///     field, and centring on the 0.5 one would assume pins the field at the top of its
    ///     range and washes the whole nebula into flat grey fog. Re-measure if the bound
    ///     noise texture is ever swapped.
    /// </remarks>
    public const float NebulaPivot = 0.682f;

    /// <summary>Stretch that reopens the collapsed FBM histogram to a usable 0..1.</summary>
    public const float NebulaExpand = 5.0f;

    public const float NebulaWarp = 1.30f;

    /// <summary>
    ///     Winding strength. Enough to read as spiral arms, well short of a drain vortex --
    ///     past roughly 1.2 the shear stretches the ink into hair.
    /// </summary>
    public const float NebulaSwirl = 0.75f;

    public const float NebulaSwirlCore = 0.18f;
    public const float NebulaSpin = 0.020f;
    public const float NebulaDensity = 0.56f;
    public const float NebulaContrast = 0.26f;

    /// <summary>Light the ink mass itself emits. Low: the mass is a silhouette.</summary>
    public const float NebulaBodyLevel = 0.16f;

    public const float NebulaSheen = 0.80f;
    public const float NebulaRelief = 1.7f;

    /// <summary>
    ///     Scale of the field the gloss follows. The material knob: small values chase the
    ///     finest octave and the highlight breaks into wet-granite speckle; this sits high
    ///     enough that it flows over whole billows in sheets.
    /// </summary>
    public const float NebulaReliefEps = 0.16f;

    public const float NebulaGloss = 30f;
    public const float NebulaRim = 0.26f;
    public const float NebulaRimPower = 2.2f;

    /// <summary>Key light. Upper-left, tilted toward the viewer.</summary>
    public static readonly Vector3 NebulaLight = new(-0.45f, -0.65f, 0.62f);

    public const float NebulaWeb = 0.14f;
    public const float NebulaWebPower = 3.0f;
    public const float NebulaReach = 1.7f;
    public const float NebulaHalo = 0.25f;
    public const float NebulaVignette = 0.55f;

    public const float RingBrightness = 0.90f;
    public const float RingWidth = 0.010f;
    public const float DiscBrightness = 0.50f;
    public const float DiscRadius = 0.105f;
    public const float DiscWidth = 0.055f;

    /// <summary>
    ///     Vertical squash on the accretion band. A round annulus reads as a lens flare;
    ///     the foreshortening is what says "flat disc seen near edge-on".
    /// </summary>
    public const float DiscFlatten = 2.8f;

    public const float DiscBeaming = 0.45f;

    /// <summary>Colour of empty void. Not pure black -- pure black kills the bloom.</summary>
    public static readonly Vector3 InkLow = new(0.014f, 0.015f, 0.019f);

    /// <summary>Colour of a full specular highlight. Neutral with a hair of cool.</summary>
    public static readonly Vector3 InkHigh = new(0.87f, 0.895f, 0.95f);

    // ---- starfield ------------------------------------------------------------------

    public const int StarCount = 260;

    /// <summary>Outer spawn radius, half-height units. Past the corners of a 21:9 frame.</summary>
    public const float StarFieldRadius = 1.35f;

    /// <summary>Base angular rate at unit radius, radians per tick.</summary>
    public const float StarOrbitRate = 0.00055f;

    /// <summary>Radial infall per tick, half-height units.</summary>
    public const float StarInfall = 0.000075f;

    /// <summary>Tangential smear multiplier as a star nears the horizon.</summary>
    public const float StarStretch = 0.055f;

    public const float StarScaleMin = 0.10f;
    public const float StarScaleMax = 0.30f;

    // ---- singularity dressing -------------------------------------------------------

    /// <summary>Soft halo scale, as a multiple of the horizon radius.</summary>
    public const float HaloScale = 7.5f;

    public const float HaloBrightness = 0.16f;
    public const float FlareBrightness = 0.30f;

    /// <summary>One-in-N chance per tick of spawning an infalling streak.</summary>
    public const int StreakChance = 22;

    public const int StreakLifetime = 190;

    // ---- ferrofluid motes -----------------------------------------------------------

    public const int MoteCount = 26;

    /// <summary>Cursor attraction radius in pixels -- the reach of the "magnet".</summary>
    public const float MagnetRadius = 260f;

    public const float MagnetForce = 0.34f;

    /// <summary>How far a fully magnetised mote stretches toward the cursor.</summary>
    public const float MoteSpikeStretch = 1.9f;

    public const float MoteRadiusMin = 9f;
    public const float MoteRadiusMax = 30f;

    /// <summary>Outward impulse a click applies, and the radius it reaches.</summary>
    public const float PulseForce = 7.5f;

    public const float PulseRadius = 340f;

    // ---- post chain -----------------------------------------------------------------

    /// <summary>Lens disc radius, half-height units. Well outside the accretion band.</summary>
    public const float LensRadius = 0.42f;

    /// <summary>Pinch exponent. 1 is identity; below 1 bends light inward.</summary>
    public const float LensPower = 0.88f;

    public const float LensSoftness = 0.85f;
    public const float LensSwirl = 0.16f;
    public const float LensMix = 0.60f;

    public const float BlurStrength = 0.055f;
    public const float BlurSpin = 0.020f;
    public const float BlurFalloff = 1.7f;
    public const float BlurMix = 0.55f;

    public const float BloomIntensity = 0.55f;
    public static readonly Vector3 BloomTint = new(0.80f, 0.84f, 0.92f);

    /// <summary>Alpha of the vignette overlay laid over the finished frame.</summary>
    public const float VignetteStrength = 0.55f;

    // ---- logo -----------------------------------------------------------------------

    /// <summary>
    ///     Logo scale. ModLogo is 960x376, and the scene draws it centred on
    ///     <c>logoDrawCenter</c> the way vanilla does, so this is sized to occupy roughly the
    ///     footprint the vanilla logo does and leave the menu buttons clear.
    /// </summary>
    public const float LogoScale = 0.62f;

    /// <summary>Nudge below vanilla's logo anchor, in UI-space pixels.</summary>
    public const float LogoOffsetY = 15f;

    /// <summary>
    ///     Alpha of the soft dark scrim laid behind the logo.
    /// </summary>
    /// <remarks>
    ///     A grey logo over a grey nebula is the one contrast failure this palette invites --
    ///     a bright sheen filament passing behind the wordmark would swallow it. The scrim
    ///     keeps a pool of dark under the logo wherever the ink happens to be bright.
    /// </remarks>
    public const float LogoScrim = 0.60f;

    /// <summary>Scrim size relative to the drawn logo's width.</summary>
    public const float LogoScrimSpread = 1.45f;

    // ---- timing ---------------------------------------------------------------------

    /// <summary>Ticks the scene takes to fade up after the menu is entered.</summary>
    public const float FadeInTicks = 90f;
}
