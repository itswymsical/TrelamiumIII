using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas;

/// <summary>
///     A moving source of gas: something ballistic that lays <see cref="FieldNode" />s behind it and
///     then stops existing.
/// </summary>
/// <remarks>
///     <para>
///         This is the thing a burst projectile used to be. Every flask in the mod shattered into a
///         fan of <see cref="ModProjectile" />s whose entire job was to travel for fifteen ticks
///         spawning one node per tick from an invisible sprite -- eighteen to twenty-eight of them
///         per throw, each one occupying a slot in a global array of a thousand that the whole game
///         shares. Two flasks in the air could account for five percent of it, spent on carriers the
///         player never sees.
///     </para>
///     <para>
///         An emitter does the same job for the cost of a struct in a list. What it gives up is
///         everything a <see cref="Projectile" /> provides that a spray of gas was never using: a
///         sprite, a network identity, an AI style, a damage class of its own. What it keeps is the
///         only two things that mattered -- an arc through the world and a hitbox -- and it keeps
///         them cheaply enough that a burst can afford to be as dense as it should look.
///     </para>
///     <para>
///         The split between an emitter and the nodes it lays down is deliberate and is the reason
///         this is not simply "a node with gravity". Nodes are numerous, dumb and drawn; emitters
///         are few, ballistic and invisible. Collapsing them would mean either paying collision and
///         contact sweeps on every one of a thousand blobs, or giving up the trail and having each
///         arm of a burst be a single moving dot.
///     </para>
/// </remarks>
public struct GasEmitter
{
    public Vector2 Position;

    public Vector2 Velocity;

    /// <summary>Ticks left before the emitter stops. Zero or less ends it on the current tick.</summary>
    public float Life;

    /// <summary>The life it started with, so <see cref="Progress" /> has something to divide by.</summary>
    public float MaxLife;

    /// <summary>Downward acceleration per tick. Zero for an emitter that coasts.</summary>
    /// <remarks>
    ///     Unlike a node's fall, this is not derived from density. An emitter is not a parcel of gas
    ///     being buoyed around -- it is a thrown piece of the flask, and it arcs like one.
    /// </remarks>
    public float Gravity;

    /// <summary>Per-tick velocity multiplier. 1 coasts; below 1 slows to a halt.</summary>
    public float Drag;

    /// <summary>Radius of the nodes it lays down, before <see cref="Growth" />.</summary>
    public float NodeRadius;

    /// <summary>Life given to each node it lays down.</summary>
    public float NodeLife;

    /// <summary>Random velocity added to each node, per axis.</summary>
    /// <remarks>
    ///     Per-axis rather than a single radius because the two axes are doing different jobs. A
    ///     spore burst wants to be wide and slow horizontally and to climb vertically; a thermite
    ///     spray wants to be equally chaotic in both. One number cannot say that.
    /// </remarks>
    public Vector2 NodeScatter;

    /// <summary>Fraction of the emitter's own velocity handed to each node it lays down.</summary>
    /// <remarks>
    ///     What makes a trail read as shed rather than as the emitter dragging a rigid tail. At 1
    ///     the nodes travel with it and the arm looks solid; near 0 they are dropped in place and
    ///     the arm looks like a fuse burning along a line.
    /// </remarks>
    public float Inheritance;

    /// <summary>
    ///     How much smaller the first node is than the last, 0..1. Zero emits at a constant size.
    /// </summary>
    /// <remarks>
    ///     A growing arm is the difference between a burst that expands and one that is simply
    ///     already there. The curve is square-rooted in <see cref="GasField.Emit" />, so most of the
    ///     growth happens early and the arm settles rather than accelerating outward.
    /// </remarks>
    public float Growth;

    /// <summary>Ticks between emissions. 1 emits every tick.</summary>
    public byte Interval;

    /// <summary>Ticks remaining before the next emission.</summary>
    public byte Timer;

    /// <summary>Bounces left before a terrain hit ends the emitter instead of deflecting it.</summary>
    public byte BouncesLeft;

    /// <summary>Fraction of speed kept through a bounce.</summary>
    public float Restitution;

    /// <summary>Whether terrain stops it at all. False passes straight through geometry.</summary>
    /// <remarks>
    ///     Off for the spore and mist bursts, and that is not laziness. Those arms are a cloud
    ///     expanding outward from a point, and a cloud does not stop at a wall -- it goes around it,
    ///     which a point-collided emitter cannot do and a field of nodes does for free once the
    ///     nodes are past the corner. On for anything with weight to it, where clipping through a
    ///     floor would read as a bug.
    /// </remarks>
    public bool TileCollide;

    /// <summary>
    ///     The player whose weapon threw this, or <see cref="FieldNode.NoOwner" />.
    /// </summary>
    /// <inheritdoc cref="FieldNode.Owner" path="/remarks" />
    public int Owner;

    /// <summary>The chemical release this emitter belongs to: passed to every node it lays, delivered on a landed hit.</summary>
    public ChemicalPayload? Payload;

    /// <summary>Damage dealt to an NPC the emitter passes through, or 0 for a harmless one.</summary>
    public int Damage;

    public float Knockback;

    /// <summary>Ticks remaining before the emitter may strike again.</summary>
    /// <remarks>
    ///     Stands in for the local immunity frames a real projectile got from the engine. Without
    ///     it an emitter travelling through a large NPC lands a hit on every tick of the crossing,
    ///     which is a different weapon entirely from the one the numbers were tuned for.
    /// </remarks>
    public int HitCooldown;

    /// <summary>
    ///     Content-defined arm identity -- core, body, droplet, or whatever a burst wants to mean.
    /// </summary>
    /// <remarks>
    ///     Carried but never read by <see cref="GasField" />, in the same spirit as
    ///     <c>TileConditionInstance.Stage</c>: the field has no opinion about what an arm is, and a
    ///     layer that does can branch on this inside its own hooks.
    /// </remarks>
    public byte Variant;

    /// <summary>Layer-defined amount of payload the emitter still carries. Never read by <see cref="GasField" />.</summary>
    public float Volume;

    /// <summary>How far through its life the emitter is, 0 at spawn and 1 at the end.</summary>
    public readonly float Progress => MaxLife <= 0f ? 1f : MathHelper.Clamp(1f - Life / MaxLife, 0f, 1f);

    public readonly bool Active => Life > 0f;

    /// <summary>Whether this client is the one that should apply the emitter's damage.</summary>
    /// <remarks>
    ///     A burst is spawned on every client, because it is a visual effect and every client has to
    ///     see it. Damage is not a visual effect, so exactly one of them may deal it.
    /// </remarks>
    public readonly bool IsLocallyOwned
        => Main.netMode == NetmodeID.SinglePlayer || Owner == Main.myPlayer;
}
