using Terraria.Audio;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     How a melee weapon swings: the shape of one strike, and the pattern the strikes fall into.
/// </summary>
/// <remarks>
///     <para>
///         The counterpart to <see cref="GunShotProfile" />. Swords that deal the same damage can feel
///         nothing alike, and the difference lives in numbers like arc travel, windup hang,
///         follow-through, edge start and arm tracking. Pulling those into a value means a new weapon
///         is a preset and a hit effect rather than another copy of the swing arithmetic.
///     </para>
///     <para>
///         A strike is a windup then a sweep; only the sweep deals damage, and the aim is locked through
///         it. A queued strike follows through a recover; otherwise the blade holds for
///         <see cref="ComboWindow" /> ticks.
///     </para>
///     <para>
///         Everything angular is stated in <i>facing space</i>: positive is toward the ground, and
///         forward is wherever the weapon is pointed, on both sides of the screen, so use sites never
///         have to mirror.
///     </para>
///     <para>
///         The combo lives here too, as <see cref="Steps" />. Unset, it defaults to
///         <see cref="SwingStep.Alternating" /> (down, up, repeat). See <see cref="SwingStep" />.
///     </para>
///     <para>
///         Start from a preset such as <see cref="Broadsword" /> and adjust with a
///         <see langword="with" /> expression. A profile built from <see langword="default" /> has a
///         zero-length blade and no timing, and only fields whose zero would crash are defended
///         against.
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
    ///     The rotation that maps the sprite's blade onto +X: up-right art is <c>PiOver4</c>, straight-up art is
    ///     <c>PiOver2</c>, art along +X is zero. State the unflipped angle; mirroring is applied when drawn.
    /// </remarks>
    public float SpriteAngle { get; init; }

    #endregion

    #region pose

    /// <summary>How closely the front arm tracks the blade, 0 to 1. Zero means unset.</summary>
    /// <remarks>
    ///     A wide arc taken literally swings the arm through the player's torso. Lowering this keeps
    ///     the shoulder in range while the blade still covers the whole sweep, since the blade pivots
    ///     at the hand; past about two radians of arc the two have to disagree.
    /// </remarks>
    public float ArmFollow { get; init; }

    /// <summary>Radians off the aim where the blade starts before the first strike. Negative is raised.</summary>
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

    /// <summary>Ticks from the end of a sweep to the start of the next, when a strike is queued. Aimable.</summary>
    public int RecoverTicks { get; init; }

    /// <summary>Ticks the blade holds after a sweep with nothing queued, waiting for a press before it is put away.</summary>
    /// <remarks>Holding the button, or pressing during a strike, continues the combo without this wait.</remarks>
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
    ///     The whole of a weapon's weight, as one number. It costs no time, but the swing has further
    ///     to travel in the same frames, so it moves faster and arrives harder. The extra ground is
    ///     part of the damaging sweep too.
    /// </remarks>
    public float Anticipation { get; init; }

    /// <summary>
    ///     Pixels the grip is driven forward along the blade at the far end of a sweep.
    /// </summary>
    /// <remarks>
    ///     Moves the grip rather than lengthening the blade, so the sprite, hitbox and arc all extend
    ///     together; stretching <see cref="BladeLength" /> would put the extra reach on a hitbox
    ///     hanging off the end of the drawn weapon. Keep it small: past about a fifth of the blade the
    ///     grip visibly detaches from the hand. <see cref="SwingStep.Extend" /> scales it per step.
    /// </remarks>
    public float Extend { get; init; }

    /// <summary>Curve of the windup. Wants to decelerate into the top of the arc.</summary>
    public EaseFunction WindupEase { get; init; }

    /// <summary>Curve of the sweep. The single field that most decides how a weapon feels.</summary>
    public EaseFunction SwingEase { get; init; }

    /// <summary>Curve of the recover into a queued strike.</summary>
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

    #region trail

    /// <summary>Master opacity of the swing trail. Zero draws no trail.</summary>
    public float TrailOpacity { get; init; }

    /// <summary>Colour of the trail's body. Premultiplied: alpha below 255 blends toward additive.</summary>
    public Color TrailColor { get; init; }

    /// <summary>Colour of the rim just inside the trail's outer edge. Premultiplied, like <see cref="TrailColor" />.</summary>
    public Color TrailEdgeColor { get; init; }

    /// <summary>Inner edge of the trail, as a fraction of reach.</summary>
    public float TrailInner { get; init; }

    /// <summary>Outer edge of the trail, as a fraction of reach.</summary>
    public float TrailOuter { get; init; }

    /// <summary>How much of the sweep's duration the trail covers behind the blade.</summary>
    /// <remarks>Also how long, as a fraction of the sweep, the trail takes to clear after the sweep ends.</remarks>
    public float TrailLength { get; init; }

    /// <summary>Points along the trail mesh. Under three means unset.</summary>
    public int TrailPoints { get; init; }

    /// <summary>Texture multiplied into the trail, wrapped along the arc. Null draws the trail untextured.</summary>
    public string? TrailTexture { get; init; }

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

        TrailOpacity = 1f,
        TrailColor = new Color(255, 214, 158, 90),
        TrailEdgeColor = new Color(255, 248, 232, 0),
        TrailInner = 0.3f,
        TrailOuter = 1.02f,
        TrailLength = 0.5f,
        TrailPoints = 32,
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

        TrailOpacity = 0.75f,
        TrailInner = 0.35f,
        TrailLength = 0.45f,
    };

    public static SwordProfile Shortsword => Broadsword with
    {
        BladeLength = 28f,
        HiltFraction = 0.22f,
        Thickness = 12f,

        WindupTicks = 3,
        SwingTicks = 8,
        RecoverTicks = 4,
        ComboWindow = 10,

        Arc = 1.3f,
        Anticipation = 0.3f,
        Extend = 6f,
        SwingEase = EaseCircularOut,

        SwingSound = SoundID.Item1,
        Steps = [SwingStep.Thrust, SwingStep.Up, SwingStep.Down],
        TrailOpacity = 0.75f,
        TrailInner = 0.35f,
        TrailLength = 0.45f,
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

        Steps = [SwingStep.Down, SwingStep.Up, SwingStep.SlashDown],

        TrailColor = new Color(198, 232, 255, 90),
        TrailEdgeColor = new Color(240, 250, 255, 0),
        TrailLength = 0.35f,
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

        TrailColor = new Color(255, 186, 150, 90),
        TrailInner = 0.25f,
        TrailLength = 0.55f,
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

        TrailInner = 0.5f,
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
    public int TrailPointCount => TrailPoints > 2 ? TrailPoints : 32;

    #endregion
}
