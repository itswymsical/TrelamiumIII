using Terraria.Audio;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>How a swing paints its arc.</summary>
public enum SwingArcStyle
{
    /// <summary>Nothing. For a weapon that draws its own.</summary>
    None,

    /// <summary>
    ///     A pre-drawn arc texture rotated to follow the blade. Cheap, warm, and fixed.
    /// </summary>
    /// <remarks>
    ///     The texture's arc spans a fixed angle, so it reads best on a weapon whose sweep is near
    ///     it. On a dagger flicking through a fifth of that, the streak is visibly describing a
    ///     journey longer than the one the blade made.
    /// </remarks>
    Twirl,

    /// <summary>
    ///     A vertex ribbon generated to the swept angle. Matches any arc, at the cost of a shader.
    /// </summary>
    /// <remarks>
    ///     The spine is built from the angles the blade has actually passed through this swing
    ///     rather than from a history of tip positions, which is what makes it exact at any speed: a
    ///     six-tick dagger swing has six positions to remember and the same smooth arc to draw.
    /// </remarks>
    Ribbon,
}

/// <summary>
///     How a melee weapon swings: the shape of one strike, and the pattern the strikes fall into.
/// </summary>
/// <remarks>
///     <para>
///         The counterpart to <see cref="GunShotProfile" />, and it exists for the same reason. Two
///         swords that deal the same damage feel nothing alike, and all of the difference is in
///         numbers that have no business being restated in every weapon: how far the arc travels,
///         how long the blade hangs before it commits, how much of it is follow-through, where the
///         edge starts, how closely the arm tracks. Pulling those into a value means a new weapon is
///         a preset and a hit effect rather than another copy of the same swing arithmetic.
///     </para>
///     <para>
///         One strike runs windup, swing, recover. The windup carries the blade from its guard to
///         the far side of the arc and is the only part that can still be aimed; the swing is the
///         only part that hurts anything; the recover brings it back to guard and is the gap the
///         next strike opens from. <see cref="Anticipation" /> is a number rather than a fourth
///         phase -- it widens the arc backwards past its nominal start, so the windup overshoots and
///         the swing has that overshoot to spend. A heavy weapon reads as heavy without being given
///         any extra frames to do it in.
///     </para>
///     <para>
///         Everything angular is stated in <i>facing space</i>: positive is toward the ground, and
///         forward is wherever the weapon is pointed, on both sides of the screen. The alternative
///         is stating world angles and mirroring at every use site, which buys a weapon exactly one
///         swing before somebody turns around and the down-slash comes up.
///     </para>
///     <para>
///         The combo lives here too, as <see cref="Steps" />. A weapon that says nothing gets
///         <see cref="SwingStep.Alternating" /> -- down, up, repeat -- which is what most swords want
///         and none of them should have to ask for. See <see cref="SwingStep" />.
///     </para>
///     <para>
///         A record struct so presets compose: start from <see cref="Broadsword" /> and adjust with
///         a <see langword="with" /> expression rather than restating thirty fields. Starting from a
///         preset is the supported path -- a profile built field by field from
///         <see langword="default" /> has a zero-length blade and no timing, and only the handful of
///         fields whose zero would crash rather than merely look wrong are defended against.
///     </para>
/// </remarks>
public readonly record struct SwordProfile
{
    #region geometry

    /// <summary>Pixels from the grip to the tip. The reach of the weapon.</summary>
    public float BladeLength { get; init; }

    /// <summary>
    ///     How far along the blade the edge begins, 0 to 1. Everything before it is grip.
    /// </summary>
    /// <remarks>
    ///     A hammer is mostly handle and wants this near the middle; a dagger is nearly all edge. It
    ///     is not cosmetic -- it is where the hitbox segment starts, so it decides whether something
    ///     standing on top of the player can be hit at all.
    /// </remarks>
    public float HiltFraction { get; init; }

    /// <summary>Pixels either side of the blade line that still count as a hit.</summary>
    public float Thickness { get; init; }

    /// <summary>Sprite origin as a fraction of the texture. Wants to sit on the grip corner.</summary>
    /// <remarks>
    ///     The pivot, so it is what makes the weapon rotate about the hand rather than about its own
    ///     middle. Standard Terraria weapon art puts the grip at the bottom-left.
    /// </remarks>
    public Vector2 Origin { get; init; }

    /// <summary>Radians the blade sits off the texture's own axes at rest.</summary>
    /// <remarks>
    ///     Terraria weapon sprites point up and to the right, which is a quarter turn off the
    ///     positive X axis that rotation is measured from -- hence the default. A sprite drawn
    ///     straight up wants zero, and one drawn along the X axis wants a negative quarter turn.
    ///     Mirroring is handled for you; state the unflipped angle.
    /// </remarks>
    public float SpriteAngle { get; init; }

    #endregion

    #region pose

    /// <summary>How closely the front arm tracks the blade, 0 to 1. Zero means unset.</summary>
    /// <remarks>
    ///     A wide arc taken literally swings the arm through the player's own torso and out the far
    ///     side. Dropping the follow keeps the shoulder inside its range while the blade still
    ///     covers the whole sweep, because the blade pivots at the hand rather than being welded to
    ///     the arm's angle -- the two are allowed to disagree, and past about two radians they have
    ///     to.
    /// </remarks>
    public float ArmFollow { get; init; }

    /// <summary>Radians off the aim where the blade rests between strikes. Negative is raised.</summary>
    public float Guard { get; init; }

    /// <summary>Horizontal velocity multiplier applied each tick of a strike. Zero means unset.</summary>
    /// <remarks>
    ///     Applied to velocity rather than to <see cref="Player.moveSpeed" /> because projectiles
    ///     update after players, so the stat has already been spent by the time this runs.
    /// </remarks>
    public float MoveSlow { get; init; }

    #endregion

    #region timing

    /// <summary>Ticks spent carrying the blade back to the start of the arc. Aimable.</summary>
    public int WindupTicks { get; init; }

    /// <summary>Ticks the damaging sweep takes. The aim is locked for all of them.</summary>
    public int SwingTicks { get; init; }

    /// <summary>Ticks spent returning to guard afterwards.</summary>
    public int RecoverTicks { get; init; }

    /// <summary>
    ///     Ticks after a strike in which pressing again continues the combo rather than restarting it.
    /// </summary>
    /// <remarks>
    ///     What makes a combo work for a player who clicks rather than holds. Holding runs the
    ///     pattern straight through; releasing parks the weapon at guard for this long, and a press
    ///     inside the window picks up at the next step. Let it lapse and the weapon is put away, so
    ///     the next press opens on the first step again.
    /// </remarks>
    public int ComboWindow { get; init; }

    /// <summary>How much melee attack speed shortens a strike, 0 to 1. Zero means unset.</summary>
    /// <remarks>
    ///     Full is right for nearly everything -- the stat exists to make melee weapons swing
    ///     faster. Damping it is for a weapon whose timing is the point, where a player in full
    ///     speed gear would otherwise skip the wind-up the whole design is built around.
    /// </remarks>
    public float AttackSpeedInfluence { get; init; }

    #endregion

    #region arc

    /// <summary>Radians one strike sweeps through, before the step scales it.</summary>
    public float Arc { get; init; }

    /// <summary>Radians the arc's centre sits off the aim. Positive is toward the ground.</summary>
    public float ArcOffset { get; init; }

    /// <summary>
    ///     Radians the windup carries the blade past the start of the arc, against the sweep.
    /// </summary>
    /// <remarks>
    ///     The whole of a weapon's weight, as one number. It costs no time -- the windup is however
    ///     many ticks it is either way -- but the swing then has further to travel in the same
    ///     frames, so it moves faster and arrives harder. It widens the damaging sweep too, since
    ///     the blade genuinely passes through that extra ground.
    /// </remarks>
    public float Anticipation { get; init; }

    /// <summary>
    ///     Pixels the grip is driven forward along the blade at the far end of a sweep.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The lean into a strike. It moves the grip rather than lengthening the blade, so the
    ///         sprite, the hitbox and the arc all extend together -- reach bought this way is reach
    ///         the player can see. Stretching <see cref="BladeLength" /> instead would buy the same
    ///         pixels and hand them to a hitbox hanging off the end of the drawn weapon.
    ///     </para>
    ///     <para>
    ///         Small on purpose. This is a body committing to a swing, not a lunge; past about a
    ///         fifth of the blade the grip visibly detaches from the hand it is supposed to be in.
    ///         <see cref="SwingStep.Extend" /> scales it per step, which is what lets a down-slash
    ///         lean in and its return stroke not.
    ///     </para>
    /// </remarks>
    public float Extend { get; init; }

    /// <summary>Curve of the windup. Wants to decelerate into the top of the arc.</summary>
    public EaseFunction WindupEase { get; init; }

    /// <summary>Curve of the sweep. The single field that most decides how a weapon feels.</summary>
    public EaseFunction SwingEase { get; init; }

    /// <summary>Curve of the return to guard. Wants to be unhurried and unremarkable.</summary>
    public EaseFunction RecoverEase { get; init; }

    /// <summary>Played as the sweep opens. Null is silent, which is what a subclass wants.</summary>
    public SoundStyle? SwingSound { get; init; }

    #endregion

    #region combo

    /// <summary>
    ///     The pattern of strikes, looped. Null or empty takes <see cref="SwingStep.Alternating" />.
    /// </summary>
    /// <remarks>
    ///     A single-element array is a weapon with no combo at all: every strike is that one, which
    ///     is what an overhead-only weapon like <see cref="Hammer" /> actually is.
    /// </remarks>
    public SwingStep[] Steps { get; init; }

    /// <inheritdoc cref="Steps" />
    public SwingStep[] Combo => Steps is { Length: > 0 } ? Steps : SwingStep.Alternating;

    /// <summary>Strikes in the loop.</summary>
    public int ComboLength => Combo.Length;

    /// <summary>The step at an index, wrapped. Negative indices wrap too.</summary>
    public SwingStep StepAt(int index)
    {
        SwingStep[] combo = Combo;

        return combo[((index % combo.Length) + combo.Length) % combo.Length];
    }

    /// <summary>Ticks one pass of the whole combo takes, windups and recoveries included.</summary>
    /// <remarks>
    ///     What an item's <c>useAnimation</c> wants to be set from, so the item's own clock keeps
    ///     step with the weapon's rather than drifting against it. Attack speed is not in it -- this
    ///     is the profile's own number, and vanilla scales the item's clock by the stat itself.
    /// </remarks>
    public int ComboPeriod
    {
        get
        {
            float ticks = 0f;

            foreach (SwingStep step in Combo)
                ticks += (WindupTicks + SwingTicks + RecoverTicks) / step.SpeedScale;

            return Math.Max(1, (int)ticks);
        }
    }

    #endregion

    #region arc vfx

    /// <summary>How the sweep is painted. See <see cref="SwingArcStyle" />.</summary>
    public SwingArcStyle ArcStyle { get; init; }

    /// <summary>Colour of the arc.</summary>
    public Color ArcColor { get; init; }

    /// <summary>Colour of the bright inner edge. <see cref="SwingArcStyle.Twirl" /> only.</summary>
    public Color ArcCoreColor { get; init; }

    /// <summary>Master opacity of the arc.</summary>
    public float ArcOpacity { get; init; }

    /// <summary>Where along the blade the arc is drawn, as a fraction of reach.</summary>
    /// <remarks>
    ///     Slightly under one puts the streak on the edge rather than beyond the tip, which is where
    ///     a swing actually leaves its mark.
    /// </remarks>
    public float ArcReach { get; init; }

    /// <summary>Fraction of the sweep the arc takes to reach full strength.</summary>
    /// <remarks>
    ///     A streak at full brightness on the frame the blade starts moving is a streak describing a
    ///     journey it has not made. This matters most with a back-loaded
    ///     <see cref="SwingEase" />, where the first third of the clock covers almost no ground.
    /// </remarks>
    public float ArcFadeIn { get; init; }

    /// <summary>Texture-space angle of the twirl's leading tip. <see cref="SwingArcStyle.Twirl" /> only.</summary>
    /// <remarks>
    ///     The arc texture is anchored by its tip rather than pinned at a fixed rotation, so the
    ///     streak trails the blade instead of hanging in the world waiting to be caught up with.
    ///     This is where in the texture that tip is.
    /// </remarks>
    public float TwirlTip { get; init; }

    /// <summary>Spine points in the ribbon. Under three means unset.</summary>
    /// <remarks>
    ///     Fixed for the life of the weapon -- <see cref="Core.Graphics.Primitives.VertexTrail" />
    ///     sizes its buffers from it once -- so it has to cover the widest arc the weapon can throw,
    ///     not the one it happens to be throwing. Cheap enough that overshooting costs nothing.
    /// </remarks>
    public int RibbonPoints { get; init; }

    /// <summary>Half-width of the ribbon at its thickest, in pixels.</summary>
    public float RibbonWidth { get; init; }

    /// <summary>Exponent shaping the ribbon's taper from tail to head.</summary>
    /// <remarks>
    ///     Below one keeps the streak broad for most of its length and narrows sharply at the very
    ///     tail; above one tapers the whole way, which reads thinner and faster.
    /// </remarks>
    public float RibbonTaper { get; init; }

    /// <summary>Radians of already-swept arc the ribbon trails behind the blade.</summary>
    /// <remarks>
    ///     Clamped to the sweep actually performed, so the streak grows out of the start of the
    ///     swing rather than being born at full length, and never trails back across ground the
    ///     blade has not covered.
    /// </remarks>
    public float RibbonSweep { get; init; }

    /// <summary>Texture sampled along the ribbon. Null takes a soft default.</summary>
    public string RibbonTexture { get; init; }

    /// <summary>Noise texture the arc shader distorts with, if it wants one.</summary>
    public string RibbonNoise { get; init; }

    /// <summary>Shader the ribbon is drawn with. Null takes the stock primitive shader.</summary>
    /// <remarks>
    ///     Anything set here must transform by <c>uWorldViewProjection</c>, which
    ///     <see cref="Core.Graphics.Primitives.VertexTrail.Render" /> fills in on its own, or take
    ///     the <c>transformMatrix</c> and pixelation parameters that <c>pixelTrail</c> wants -- both
    ///     sets are supplied, and a shader ignores whichever it does not declare.
    /// </remarks>
    public string RibbonShader { get; init; }

    #endregion

    #region presets

    /// <summary>
    ///     The baseline: a wide two-handed arc, alternating down and up, at a workable tempo.
    /// </summary>
    /// <remarks>
    ///     Every other preset here is this one with things moved, which is the intended way to write
    ///     a new weapon as well.
    /// </remarks>
    public static SwordProfile Broadsword => new()
    {
        BladeLength = 52f,
        HiltFraction = 0.26f,
        Thickness = 18f,
        Origin = new Vector2(0.1f, 0.9f),
        SpriteAngle = MathHelper.PiOver4,

        ArmFollow = 0.8f,
        Guard = -0.75f,
        MoveSlow = 1f,

        WindupTicks = 6,
        SwingTicks = 13,
        RecoverTicks = 7,
        ComboWindow = 14,
        AttackSpeedInfluence = 1f,

        Arc = 2.5f,
        ArcOffset = 0f,
        Anticipation = 0.45f,
        Extend = 9f,
        WindupEase = EaseCircularOut,
        SwingEase = Snap,
        RecoverEase = EaseQuadOut,

        ArcStyle = SwingArcStyle.Twirl,
        ArcColor = new Color(255, 214, 158),
        ArcCoreColor = new Color(255, 248, 232),
        ArcOpacity = 1f,
        ArcReach = 0.95f,
        ArcFadeIn = 0.33f,
        TwirlTip = 0.73f,
        RibbonPoints = 32,
        RibbonWidth = 15f,
        RibbonTaper = 0.85f,
        RibbonSweep = 1.5f,
    };

    /// <summary>
    ///     Short, quick and three-beat: two flicks and a lunge that ends pointing at the target.
    /// </summary>
    /// <remarks>
    ///     The reach is the balance. A weapon that strikes this fast is only fair because landing it
    ///     means standing inside everything the target is doing.
    /// </remarks>
    public static SwordProfile Dagger => Broadsword with
    {
        BladeLength = 30f,
        HiltFraction = 0.22f,
        Thickness = 12f,

        ArmFollow = 0.95f,
        Guard = -0.5f,

        WindupTicks = 3,
        SwingTicks = 6,
        RecoverTicks = 4,
        ComboWindow = 10,

        Arc = 1.7f,
        Anticipation = 0.3f,
        Extend = 6f,
        SwingEase = EaseCircularOut,

        Steps = [SwingStep.Down, SwingStep.Up, SwingStep.Thrust],

        ArcOpacity = 0.75f,
        RibbonWidth = 9f,
        RibbonSweep = 1.1f,
    };

    /// <summary>
    ///     Two long draws and a turn. Hangs, then crosses the whole arc in a handful of frames.
    /// </summary>
    /// <remarks>
    ///     <see cref="Draw" /> is the entire character of the weapon: nearly half the swing is spent
    ///     not moving, so the strike lands as one event rather than as a sweep the eye can follow.
    ///     The wide arc and the long anticipation are what give those few frames enough ground to
    ///     cover for the snap to register.
    /// </remarks>
    public static SwordProfile Katana => Broadsword with
    {
        BladeLength = 62f,
        HiltFraction = 0.2f,
        Thickness = 15f,

        ArmFollow = 0.85f,
        Guard = -0.95f,

        WindupTicks = 7,
        SwingTicks = 8,
        RecoverTicks = 9,
        ComboWindow = 16,

        Arc = 2.9f,
        Anticipation = 0.75f,
        Extend = 12f,
        SwingEase = Draw,

        Steps = [SwingStep.Down, SwingStep.Up, SwingStep.Spin],

        ArcColor = new Color(198, 232, 255),
        ArcCoreColor = new Color(240, 250, 255),
        ArcFadeIn = 0.2f,
        RibbonWidth = 13f,
        RibbonSweep = 2f,
    };

    /// <summary>
    ///     Heavy and broad. Slow enough to be committed to, thick enough not to need precision.
    /// </summary>
    public static SwordProfile Cleaver => Broadsword with
    {
        BladeLength = 48f,
        HiltFraction = 0.3f,
        Thickness = 28f,

        ArmFollow = 0.7f,
        Guard = -0.6f,
        MoveSlow = 0.95f,

        WindupTicks = 9,
        SwingTicks = 15,
        RecoverTicks = 12,
        ComboWindow = 16,

        Arc = 2.3f,
        Anticipation = 0.6f,
        Extend = 8f,
        SwingEase = Heavy,

        ArcColor = new Color(255, 186, 150),
        RibbonWidth = 22f,
        RibbonSweep = 1.4f,
    };

    /// <summary>
    ///     One overhead, every time. No combo, no up-swing, and a windup you can read across a room.
    /// </summary>
    /// <remarks>
    ///     The single-element <see cref="Steps" /> is the whole design. A hammer that alternated
    ///     would spend half its strikes swinging upward, which is neither what a hammer does nor
    ///     what the player is being asked to time.
    /// </remarks>
    public static SwordProfile Hammer => Broadsword with
    {
        BladeLength = 60f,
        HiltFraction = 0.5f,
        Thickness = 26f,

        ArmFollow = 0.85f,
        Guard = -1.1f,
        MoveSlow = 0.88f,

        WindupTicks = 14,
        SwingTicks = 11,
        RecoverTicks = 16,
        ComboWindow = 20,
        AttackSpeedInfluence = 0.6f,

        Arc = 2.4f,
        ArcOffset = -0.15f,
        Anticipation = 0.95f,
        Extend = 10f,
        SwingEase = Heavy,

        Steps = [SwingStep.Down],

        RibbonWidth = 20f,
        RibbonSweep = 1.3f,
    };

    #endregion

    #region eases

    /// <summary>Hangs for a beat, whips through, and settles into the follow-through.</summary>
    public static EaseFunction Snap { get; } = BuildSnap();

    /// <summary>Almost half the swing spent still, then the whole arc at once.</summary>
    public static EaseFunction Draw { get; } = BuildDraw();

    /// <summary>Builds the whole way and arrives at its fastest. Nothing after the landing.</summary>
    public static EaseFunction Heavy { get; } = BuildHeavy();

    private static EaseFunction BuildSnap()
    {
        var ease = new EaseBuilder();

        ease.AddPoint(0.22f, 0.10f, EaseQuadIn);
        ease.AddPoint(0.78f, 0.94f, EaseCircularOut);
        ease.AddPoint(1f, 1f, EaseQuadOut);

        return ease;
    }

    private static EaseFunction BuildDraw()
    {
        var ease = new EaseBuilder();

        ease.AddPoint(0.45f, 0.05f, EaseQuadIn);
        ease.AddPoint(0.72f, 0.97f, EaseCircularOut);
        ease.AddPoint(1f, 1f, EaseQuadOut);

        return ease;
    }

    private static EaseFunction BuildHeavy()
    {
        var ease = new EaseBuilder();

        ease.AddPoint(0.30f, 0.07f, EaseQuadIn);
        ease.AddPoint(1f, 1f, EaseCubicIn);

        return ease;
    }

    #endregion

    #region fallbacks

    /// <summary>
    ///     The handful of fields whose zero would break something rather than merely look wrong.
    /// </summary>
    /// <remarks>
    ///     Deliberately short. Defending every field would mean no profile could ever ask for a
    ///     value of zero, and several of them -- a stationary arc offset, a weightless
    ///     anticipation -- legitimately want one.
    /// </remarks>
    public float ArmTracking => ArmFollow > 0f ? MathHelper.Clamp(ArmFollow, 0f, 1f) : 1f;

    /// <inheritdoc cref="ArmTracking" />
    public float MoveDamping => MoveSlow > 0f ? MoveSlow : 1f;

    /// <inheritdoc cref="ArmTracking" />
    public float SpeedInfluence => AttackSpeedInfluence > 0f ? MathHelper.Clamp(AttackSpeedInfluence, 0f, 1f) : 1f;

    /// <inheritdoc cref="ArmTracking" />
    public int RibbonPointCount => RibbonPoints > 2 ? RibbonPoints : 32;

    /// <inheritdoc cref="ArmTracking" />
    public string RibbonTextureKey => RibbonTexture ?? Assets.Textures.VFX.BeamTrail.KEY;

    /// <inheritdoc cref="ArmTracking" />
    public string RibbonNoiseKey => RibbonNoise ?? Assets.Textures.VFX.StormTrail.KEY;

    /// <inheritdoc cref="ArmTracking" />
    public string RibbonShaderKey => RibbonShader ?? Assets.Shaders.Primitives.KEY;

    #endregion
}
