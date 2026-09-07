namespace TrelamiumIII.Core.Gas;

/// <summary>An inclusive numeric range, rolled or interpolated at the point of use.</summary>
/// <remarks>
///     Exists so a profile can say "eighteen to twenty-four arms at four and a half to six speed"
///     in four numbers rather than eight loose fields, and so the rolling convention is written down
///     once instead of at every call site.
/// </remarks>
public readonly record struct FloatRange(float Min, float Max)
{
    public readonly float Roll() => Main.rand.NextFloat(Min, Max);

    /// <summary>
    ///     A count rolled from the range, with <paramref name="Max" /> exclusive.
    /// </summary>
    /// <remarks>
    ///     Exclusive because <see cref="Terraria.Utilities.UnifiedRandom.Next(int, int)" /> is, and
    ///     every count in this system was written against that convention before it was a range.
    /// </remarks>
    public readonly int RollCount() => Main.rand.Next((int)Min, Math.Max((int)Max, (int)Min + 1));

    public readonly float At(float t) => MathHelper.Lerp(Min, Max, t);
}

/// <summary>How the arms of a burst are laid out.</summary>
public enum BurstShape : byte
{
    /// <summary>
    ///     Arms thrown at random inside a cone about one direction, at random speeds.
    /// </summary>
    /// <remarks>
    ///     The shape of something coming apart. Cheap, chaotic, and right for a payload that is
    ///     escaping under its own pressure rather than being splashed off a surface.
    /// </remarks>
    Scatter,

    /// <summary>
    ///     Arms distributed evenly across an arc, with speed and identity varying from centre to
    ///     edge; a full circle when the burst happens in open air.
    /// </summary>
    /// <remarks>
    ///     The shape of something hitting a wall. Even spacing is what makes it read as a splash
    ///     rather than as a random spray that happens to be pointed away from the surface, and the
    ///     centre-to-edge gradient is what gives it a body and a rim instead of a uniform fan.
    /// </remarks>
    Fan,
}

/// <summary>
///     One arm of a burst, as numbers: how it flies and what it leaves behind it.
/// </summary>
/// <remarks>
///     A record struct so presets compose -- start from <see cref="Default" /> and adjust with a
///     <see langword="with" /> expression, the same way <see cref="Projectiles.GunShotProfile" />
///     does. Composing matters more here than usual, because the fields that are wrong when left at
///     <see langword="default" /> are the two that would silently do nothing: a zero
///     <see cref="Drag" /> stops the arm on its first tick, and a zero <see cref="Interval" /> makes
///     it emit nothing at all.
/// </remarks>
public readonly record struct EmitterProfile
{
    /// <summary>Ticks the arm travels for.</summary>
    public int Lifetime { get; init; }

    /// <inheritdoc cref="GasEmitter.NodeRadius" />
    public float NodeRadius { get; init; }

    /// <inheritdoc cref="GasEmitter.NodeLife" />
    public float NodeLife { get; init; }

    /// <inheritdoc cref="GasEmitter.NodeScatter" />
    public Vector2 NodeScatter { get; init; }

    /// <inheritdoc cref="GasEmitter.Inheritance" />
    public float Inheritance { get; init; }

    /// <inheritdoc cref="GasEmitter.Growth" />
    public float Growth { get; init; }

    /// <inheritdoc cref="GasEmitter.Interval" />
    public byte Interval { get; init; }

    /// <inheritdoc cref="GasEmitter.Gravity" />
    public float Gravity { get; init; }

    /// <inheritdoc cref="GasEmitter.Drag" />
    public float Drag { get; init; }

    /// <inheritdoc cref="GasEmitter.BouncesLeft" />
    public byte Bounces { get; init; }

    /// <inheritdoc cref="GasEmitter.Restitution" />
    public float Restitution { get; init; }

    /// <inheritdoc cref="GasEmitter.TileCollide" />
    public bool TileCollide { get; init; }

    /// <summary>
    ///     A weightless arm that coasts through terrain for fifteen ticks laying a node a tick.
    /// </summary>
    /// <remarks>
    ///     Matches what every splash arm in the mod was before it was a profile, so a flask that
    ///     starts here and overrides only its radii is getting exactly its old behaviour.
    /// </remarks>
    public static EmitterProfile Default => new()
    {
        Lifetime = 15,
        NodeRadius = 9f,
        NodeLife = 80f,
        NodeScatter = new Vector2(1.5f, 1.5f),
        Inheritance = 0.5f,
        Growth = 0f,
        Interval = 1,
        Gravity = 0f,
        Drag = 1f,
        Bounces = 0,
        Restitution = 0f,
        TileCollide = false,
    };

    /// <summary>A heavy arm that arcs, bounces off terrain and comes to rest.</summary>
    public static EmitterProfile Ballistic => Default with
    {
        Lifetime = 100,
        NodeLife = 30f,
        NodeScatter = new Vector2(7.5f, 7.5f),
        Inheritance = 0.2f,
        Gravity = 0.32f,
        Drag = 0.992f,
        Bounces = 2,
        Restitution = 0.42f,
        TileCollide = true,
    };
}

/// <summary>
///     What a payload does when its container stops existing: how many arms, thrown where, carrying
///     what.
/// </summary>
/// <remarks>
///     <para>
///         This is the half of a chemical weapon that has nothing to do with which chemical it is.
///         Six flasks in the mod shattered through six copies of the same forty lines -- roll a
///         count, walk an arc, pick a variant off the edge fraction, spawn a carrier -- differing
///         only in the constants. Pulling the constants out into a value means a new flask is a
///         preset, and a flask that wants a genuinely different shatter overrides
///         <see cref="Projectiles.ChemicalProjectile.Shatter" /> instead of restating the arithmetic
///         with two numbers changed.
///     </para>
///     <para>
///         Ground and air are separate parameter sets rather than one set with a multiplier, because
///         they are not the same event. A flask that hits a floor throws its contents away from the
///         floor in a fan; one that bursts in mid-air has nothing to push off and opens in every
///         direction. Sharing counts and speeds between the two would make one of them wrong.
///     </para>
/// </remarks>
public readonly record struct BurstProfile
{
    public BurstShape Shape { get; init; }

    /// <summary>Arms thrown by a burst in open air.</summary>
    public FloatRange AirCount { get; init; }

    /// <summary>Arms thrown by a burst against terrain.</summary>
    public FloatRange GroundCount { get; init; }

    /// <summary>Speed of an arm thrown in open air.</summary>
    public FloatRange AirSpeed { get; init; }

    /// <summary>
    ///     Speed of an arm thrown against terrain: <see cref="FloatRange.Min" /> at the centre of the
    ///     fan, <see cref="FloatRange.Max" /> at its edges.
    /// </summary>
    /// <remarks>
    ///     Ordered rather than random, and allowed to run either way. Faster at the edges throws a
    ///     splash that spreads out along the surface; faster at the centre throws one that stands up
    ///     off it. Both are real splashes and which one a payload wants depends on how heavy it is.
    /// </remarks>
    public FloatRange GroundSpeed { get; init; }

    /// <summary>
    ///     Radians the arms are spread over: the full width of a <see cref="BurstShape.Fan" />, or
    ///     the half-angle of a <see cref="BurstShape.Scatter" /> cone.
    /// </summary>
    public float Arc { get; init; }

    /// <summary>
    ///     How far a surface splash is tilted toward vertical, 0..1.
    /// </summary>
    /// <remarks>
    ///     Applied only when the surface already faces upward at all. A splash off a floor that
    ///     travels perfectly horizontally hugs the ground and disappears behind it; tilting it up
    ///     gets the payload into the air where it can be seen and walked into. A ceiling gets none
    ///     of this, because a splash that runs back up into the rock it just hit is nonsense.
    /// </remarks>
    public float UpwardBias { get; init; }

    /// <summary>Edge fraction below which an arm is a <see cref="Core" />.</summary>
    /// <remarks>
    ///     Edge is 0 at the middle of the fan and 1 at its rim, so the cuts read outward: everything
    ///     under <see cref="CoreCut" /> is core, everything under <see cref="BodyCut" /> is body, and
    ///     the rest is droplets. Set <see cref="CoreCut" /> to 1 for a burst that is all one thing.
    /// </remarks>
    public float CoreCut { get; init; }

    /// <inheritdoc cref="CoreCut" />
    public float BodyCut { get; init; }

    /// <summary>The heavy arms at the middle of the burst.</summary>
    public EmitterProfile Core { get; init; }

    /// <summary>The arms between the core and the rim.</summary>
    public EmitterProfile Body { get; init; }

    /// <summary>The thin fast arms at the rim.</summary>
    public EmitterProfile Droplet { get; init; }

    /// <summary>Pixels the burst origin is pushed out along the impact normal.</summary>
    /// <remarks>
    ///     Small but load-bearing: the flask dies flush against the surface, and arms born exactly
    ///     on it spend their first tick inside solid rock.
    /// </remarks>
    public float OriginOffset { get; init; }

    /// <summary>Damage each arm carries, as a fraction of the parent's own.</summary>
    /// <remarks>
    ///     Zero for a payload whose whole effect is the cloud it leaves, which is where a
    ///     controller-class weapon would normally sit. Every flask that exists today shares in full,
    ///     because the carriers this replaces were friendly projectiles holding the flask's damage
    ///     and quietly dropping that would be a balance change wearing a refactor's clothes.
    /// </remarks>
    public float DamageShare { get; init; }

    /// <summary>The arm that a given edge fraction belongs to.</summary>
    public readonly EmitterProfile Select(float edge)
        => edge < CoreCut ? Core : edge < BodyCut ? Body : Droplet;

    /// <summary>
    ///     A shattered flask: an even fan off whatever it hit, opening to a full circle in mid-air.
    /// </summary>
    public static BurstProfile FlaskShatter => new()
    {
        Shape = BurstShape.Fan,
        AirCount = new FloatRange(16f, 24f),
        GroundCount = new FloatRange(11f, 19f),
        AirSpeed = new FloatRange(3.5f, 5.5f),
        GroundSpeed = new FloatRange(7.5f, 6f),
        Arc = MathHelper.Pi * 0.9f,
        UpwardBias = 0.35f,
        CoreCut = 0.3f,
        BodyCut = 0.7f,
        Core = EmitterProfile.Default,
        Body = EmitterProfile.Default,
        Droplet = EmitterProfile.Default,
        OriginOffset = 2f,
        DamageShare = 0f,
    };

    /// <summary>
    ///     A payload escaping under pressure: one cone, random within it, all of a piece.
    /// </summary>
    public static BurstProfile Vent => new()
    {
        Shape = BurstShape.Scatter,
        AirCount = new FloatRange(16f, 22f),
        GroundCount = new FloatRange(16f, 22f),
        AirSpeed = new FloatRange(2f, 9f),
        GroundSpeed = new FloatRange(2f, 9f),
        Arc = MathHelper.PiOver2,
        UpwardBias = 0.5f,
        CoreCut = 1f,
        BodyCut = 1f,
        Core = EmitterProfile.Default,
        Body = EmitterProfile.Default,
        Droplet = EmitterProfile.Default,
        OriginOffset = 0f,
        DamageShare = 0f,
    };
}
