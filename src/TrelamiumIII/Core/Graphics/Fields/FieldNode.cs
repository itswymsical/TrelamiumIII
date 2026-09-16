namespace TrelamiumIII.Core.Graphics.Fields;

/// <summary>
///     One blob in a <see cref="FieldLayer" />'s density field.
/// </summary>
/// <remarks>
///     <para>
///         The union of what the metaball and gas systems each kept in their own node struct. The
///         simulation and the renderer were the same code in both, so the two structs were the same
///         struct with different extras bolted on; carrying all of them costs a visual layer around
///         sixteen unused bytes per node, which at a four-hundred node budget is not worth a second
///         type to avoid.
///     </para>
///     <para>
///         A node is not an entity. It carries no damage, no immunity frames and no netcode of its
///         own -- <see cref="Owner" /> exists to answer what the gas is allowed to do to the person
///         who fired it, not to credit a hit. What acts on the world is the layer, from the hooks
///         the simulation calls as nodes touch things.
///     </para>
/// </remarks>
public struct FieldNode
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Life;
    public float MaxLife;

    /// <summary>
    ///     0.0 = buoyant, 1.0 = dense. <see cref="FieldLayer" /> interpolates between its buoyancy
    ///     and gravity constants across this range.
    /// </summary>
    public float Density;

    /// <summary>
    ///     0.0 = flows freely, 1.0 = sluggish. Scales drag, and damps the spread a node gets when it
    ///     lands on something.
    /// </summary>
    public float Viscosity;

    /// <summary>Temperature in Celsius. Read by reaction hooks; base physics does not use it.</summary>
    public float Temperature;

    /// <summary>Which family of matter this node is, for layers that care. See <c>GasField.GasTypeID</c>.</summary>
    public int TypeId;

    public bool OnCeiling;
    public bool OnFloor;
    public bool OnWall;

    /// <summary>Ticks this node has spent inside solid tiles.</summary>
    public int EnclosedTicks;

    /// <summary>Ticks remaining before the next entity contact sweep.</summary>
    public int ContactCooldown;

    /// <summary>
    ///     The player whose weapon produced this node, or <see cref="NoOwner" />.
    /// </summary>
    /// <remarks>
    ///     Read by the hooks that decide what a layer may do to a player, and carried onward into
    ///     any residue the node pools into, so a flask's leftovers stay harmless to the person who
    ///     threw them for as long as they sit there.
    /// </remarks>
    public int Owner;

    /// <summary>
    ///     The thing this node rides, or <see langword="null" /> when it drifts under its own physics.
    /// </summary>
    /// <remarks>
    ///     Anchored nodes are pinned to <see cref="AnchorOffset" /> from the host's centre and skip
    ///     collision entirely. The anchor releases on <see cref="AnchorTicks" /> expiry or host
    ///     death, and the node finishes its life drifting normally, so a cloud clings then falls off.
    ///     <para>
    ///         Held as the entity rather than as an index into one of the arrays, because a node can
    ///         ride an NPC or a player and there is no index that means the same thing in both. Both
    ///         arrays are pooled for the life of the game, so the reference costs nothing to keep,
    ///         and a host whose slot gets reused reads as dead rather than as somebody else.
    ///     </para>
    /// </remarks>
    public Entity? AnchorHost;

    public Vector2 AnchorOffset;

    public int AnchorTicks;

    public readonly bool IsAnchored => AnchorHost is not null && AnchorTicks > 0;

    public readonly float Alpha => MaxLife <= 0f ? 0f : Life / MaxLife;

    public readonly bool Active => Life > 0f;

    public readonly bool IsOwnedBy(Player player) => Owner == player.whoAmI;

    /// <summary>A node nobody fired: ambient emitters, world hazards, anything spawned by terrain.</summary>
    public const int NoOwner = -1;
}
