namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Every art-direction knob for the Illuminant Ink menu, gathered in one place.
/// </summary>
/// <remarks>
///     Distances that describe the scene are in <em>half-height units</em>: the same
///     aspect-corrected space <c>InkNebula.fx</c> works in, where <c>0.5</c> is half the screen
///     height. That keeps the singularity the same apparent size on every monitor, and lets the
///     shader and the C# layers agree on where the horizon is without converting.
/// </remarks>
internal static class InkStyle
{
    // ---- composition ----------------------------------------------------------------

    /// <summary>Where the singularity sits, in 0..1 screen space.</summary>
    /// <remarks>
    ///     Off-centre and below the logo: dead centre fights the logo for attention, and the
    ///     lensing reads better when the frame isn't symmetric about it.
    /// </remarks>
    public static readonly Vector2 Singularity = new(0.675f, 0.445f);

    /// <summary>Amplitude of the singularity's idle drift, in 0..1 screen space.</summary>
    public static readonly Vector2 SingularityDrift = new(0.012f, 0.008f);

    /// <summary>Event-horizon radius, half-height units. Must match <c>uHorizon</c>.</summary>
    public const float Horizon = 0.055f;

    // ---- nebula (InkNebula.fx) ------------------------------------------------------
    //
    // Tuned against an offline CPU replica of the shader rather than by eye in-game, so
    // these are measured rather than guessed. The two that are not free to move are
    // NebulaPivot and NebulaReliefEps -- see their notes.

    public const float NebulaScale = 2.22f;

    /// <summary>
    ///     Where the raw four-octave FBM sum is recentred before being stretched. Doubles as
    ///     the exposure control for the whole nebula.
    /// </summary>
    /// <remarks>
    ///     Measured medians: <c>FbmNoise2</c> 0.682, <c>FbmNoiseBright</c> 0.571. Sitting well
    ///     above the bound texture's median pushes the field down its range and holds the ink
    ///     dark, so this is an art choice rather than a normalisation, and the first dial for
    ///     overall brightness. If a different noise texture is bound, re-tune by eye.
    /// </remarks>
    public const float NebulaPivot = 0.782f;

    /// <summary>
    ///     Edge of the nebula's pixel grid, in pixels at 1080p. 1 disables pixelation.
    /// </summary>
    /// <remarks>
    ///     Scaled by resolution where it is used (see <see cref="InkFrame.PixelSize" />) so
    ///     the ink reads equally chunky at 4K and 1080p.
    ///     <para />
    ///     Nebula only. The starfield and motes stay crisp on purpose -- quantising point-like
    ///     sprites makes them flicker between cells as they drift.
    /// </remarks>
    public const float NebulaPixelSize = 3.5f;

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
    //
    // A mote is not a sprite. Each is stamped as a soft blob into a coverage field and the
    // whole field is cut at a threshold, so two motes that drift near each other cross that
    // threshold in the gap and fuse into one silhouette -- a sprite per mote could only overlap.

    /// <summary>How many motes a settled field holds, before any are burst or merged.</summary>
    /// <remarks>
    ///     A target rather than a census. Bursting one raises the count and merging two lowers
    ///     it, so what is actually held steady is mass -- see <c>InkMoteLayer</c>'s refill.
    /// </remarks>
    public const int MoteCount = 26;

    /// <summary>Cursor attraction radius in pixels -- the reach of the "magnet".</summary>
    public const float MagnetRadius = 260f;

    public const float MagnetForce = 0.24f;

    /// <summary>
    ///     Radius of the magnet's core, in pixels: inside it the pull fades back out to nothing.
    /// </summary>
    /// <remarks>
    ///     The rim falloff only governs distant motes; the pull is full strength at the pointer, so
    ///     without a core a mote is driven straight through the cursor and buzzes across it forever.
    ///     <para />
    ///     Small, because it sets how tightly a dragged cluster packs -- past roughly 40 the pool
    ///     spreads far enough that <see cref="MoteMergeOverlap" /> stops being reached.
    /// </remarks>
    public const float MagnetCore = 26f;

    /// <summary>Per-tick velocity multiplier applied at the very centre of the core.</summary>
    /// <remarks>
    ///     Fading the force stops a mote being driven through the cursor, but it would still coast
    ///     through on its arrival momentum; this removes it. Together they over-damp the inner half
    ///     of the core so a mote slides to a stop instead of ringing.
    /// </remarks>
    public const float MagnetSettle = 0.78f;

    /// <summary>How far a fully magnetised mote stretches toward the cursor.</summary>
    public const float MoteSpikeStretch = 1.2f;

    public const float MoteRadiusMin = 9f;
    public const float MoteRadiusMax = 30f;

    /// <summary>Outward impulse a click applies, and the radius it reaches.</summary>
    public const float PulseForce = 7.5f;

    public const float PulseRadius = 340f;

    /// <summary>Hard ceiling on live motes, however hard the player clicks.</summary>
    public const int MoteBudget = 170;

    /// <summary>
    ///     One-in-N chance per tick of drifting a fresh mote in while the field is under mass.
    /// </summary>
    /// <remarks>
    ///     A chance rather than a top-up, so a field emptied by a run of clicks refills over a
    ///     few seconds instead of snapping back the tick the mass check goes under.
    /// </remarks>
    public const int MoteRefillChance = 14;

    // ---- clicking motes apart -------------------------------------------------------

    /// <summary>How far past its own radius a mote still counts as clicked.</summary>
    public const float MoteHitScale = 1.3f;

    /// <summary>Droplets a clicked mote comes apart into, at its largest.</summary>
    /// <remarks>
    ///     A ceiling, not a count: a small mote splits into fewer, because dividing its area any
    ///     further would put every piece under <see cref="MoteRadiusMin" /> and the burst would
    ///     read as the mote having simply vanished.
    /// </remarks>
    public const int MoteBurstPieces = 6;

    /// <summary>Speed the droplets leave at, before the parent's own momentum.</summary>
    /// <remarks>
    ///     Sized against <see cref="MagnetRadius" /> rather than picked for its own sake: under the
    ///     field's drag this throws a droplet a little over half the magnet's reach, so a burst
    ///     scatters clear of the pointer and is then drawn back in. Past the reach the droplets
    ///     escape and the click stops being a loop.
    /// </remarks>
    public const float MoteBurstSpeed = 6.5f;

    /// <summary>Ticks a fresh droplet refuses to merge for.</summary>
    /// <remarks>
    ///     Without it a burst is invisible: the pieces are born inside each other's merge reach
    ///     and the cursor that just split them is still pulling, so they would recombine on the
    ///     next tick or two and the click would read as nothing having happened.
    /// </remarks>
    public const int MoteBurstCohesion = 50;

    // ---- gathering motes back together ----------------------------------------------

    /// <summary>
    ///     How near the pointer a mote has to be, in pixels, before it will merge with a neighbour.
    /// </summary>
    /// <remarks>
    ///     Ungated, the whole field would slowly collapse into a few large beads on its own; gated on
    ///     the pointer, motes only pool where the cursor has dragged them. Uses distance rather than
    ///     <c>Spike</c>, which fades out inside <see cref="MagnetCore" /> and would refuse the motes
    ///     that have arrived.
    /// </remarks>
    public const float MoteMergeReach = 200f;

    /// <summary>Centre distance two motes merge at, as a fraction of their summed radii.</summary>
    public const float MoteMergeOverlap = 0.62f;

    /// <summary>Ticks a freshly merged mote settles for before it will merge again.</summary>
    public const int MoteMergeSettle = 12;

    /// <summary>Ceiling on a merged mote's radius, as a multiple of <see cref="MoteRadiusMax" />.</summary>
    public const float MoteMergeCap = 2.4f;

    // ---- silhouette pass (InkSilhouette.fx) -----------------------------------------

    /// <summary>Screen pixels per field texel.</summary>
    /// <remarks>
    ///     The field is blurry data rather than an image, so it loses nothing to the downscale,
    ///     and the shader snaps its decisions to this grid -- one field texel becomes one chunky
    ///     output pixel. Deliberately not <see cref="NebulaPixelSize" />: the two grids are
    ///     different sizes on purpose, so the ink reads as sitting on the nebula rather than as
    ///     being painted into it.
    /// </remarks>
    public const int MoteFieldScale = 2;

    /// <summary>Stamp diameter as a multiple of a mote's radius.</summary>
    /// <remarks>
    ///     Past 2 because the threshold cut lands well inside the stamp: at exactly 2 the
    ///     silhouettes come out visibly smaller than the radii say, and neighbours stop reaching
    ///     each other. This is the knob for how eagerly motes fuse, together with
    ///     <see cref="MoteThreshold" />.
    /// </remarks>
    public const float MoteStampSpread = 2.9f;

    /// <summary>Coverage level where ink exists.</summary>
    /// <remarks>
    ///     Well under 1, which is what makes motes fuse rather than merely overlap: two stamps
    ///     whose own falloffs have each dropped to a sixth still sum past this in the gap between
    ///     them, so the cut closes over it. Raise it and the field becomes a heap of discs.
    /// </remarks>
    public const float MoteThreshold = 0.30f;

    public const float MoteBodyAlpha = 0.93f;

    /// <summary>Posterise step count for the body shade. Void, mass, rim.</summary>
    public const float MoteBands = 3f;

    public const float MoteRimAlpha = 0.88f;

    /// <summary>Peak displacement of the silhouette lookup, in UV, and how fine it is.</summary>
    /// <remarks>
    ///     Warps the outline rather than the fill. A clean edge reads as a sprite however much is
    ///     churning inside it, and the edge is the first thing the eye resolves.
    /// </remarks>
    public const float MoteWobble = 0.009f;

    public const float MoteWobbleScale = 2.6f;

    public static readonly Vector2 MoteWobbleDrift = new(0.021f, -0.034f);

    /// <summary>Radial reach of the smear toward the singularity, and its tangential slice.</summary>
    /// <remarks>
    ///     Small. The motes are adrift, not being drained; this is the hint that the hole has a
    ///     hold on them, and past roughly 0.1 it stops being a hint and the field visibly streams.
    /// </remarks>
    public const float MoteSmear = 0.045f;

    public const float MoteSpin = 0.030f;

    /// <summary>How fast the smear ramps up away from the singularity.</summary>
    public const float MoteFalloff = 1.5f;

    /// <summary>Sparse ink, dense ink, deepest ink.</summary>
    /// <remarks>
    ///     Greyer than <c>InkLayer</c>'s violet palette, because this menu is black and grey and
    ///     the motes have to sit on the nebula rather than beside it -- the ends are
    ///     <see cref="InkLow" /> and a step short of <see cref="InkHigh" />.
    /// </remarks>
    public static readonly Vector3 MoteShadowColor = new(0.014f, 0.015f, 0.019f);

    public static readonly Vector3 MoteBodyColor = new(0.044f, 0.046f, 0.056f);

    public static readonly Vector3 MoteCoreColor = new(0.13f, 0.135f, 0.155f);
    public static readonly Vector3 TextHighlightColor = new(0.953f, 0.910f, 1.000f);


    /// <summary>The meniscus: the rim the shader paints along its own cut.</summary>
    /// <remarks>
    ///     This replaces the ring sprite each mote used to carry. A ring per mote outlines every
    ///     mote, including the ones that have fused, so it drew the seams the whole field exists
    ///     to hide; a rim found from the cut follows the merged outline instead.
    /// </remarks>
    public static readonly Vector3 MoteRimColor = new(0.82f, 0.84f, 0.90f);

    /// <summary>Brightness of the specular glint travelling over a mote's surface.</summary>
    public const float MoteGlint = 0.34f;

    // ---- post chain -----------------------------------------------------------------

    /// <summary>Lens disc radius, half-height units. Well outside the accretion band.</summary>
    public const float LensRadius = 0.42f;

    /// <summary>Pinch exponent. 1 is identity; below 1 bends light inward.</summary>
    public const float LensPower = 0.88f;

    public const float LensSoftness = 0.85f;
    public const float LensSwirl = 0.16f;
    public const float LensMix = 0.60f;

    public const float BlurStrength = 0.8f;
    public const float BlurSpin = 0.020f;
    public const float BlurFalloff = 1.7f;
    public const float BlurMix = 0.55f;

    public const float BloomIntensity = 0.55f;
    public static readonly Vector3 BloomTint = new(0.80f, 0.84f, 0.92f);

    /// <summary>Alpha of the vignette overlay laid over the finished frame.</summary>
    public const float VignetteStrength = 0.55f;

    // ---- logo -----------------------------------------------------------------------

    public const float LogoScale = 0.54f;

    public const float LogoOffsetY = 24f;

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
