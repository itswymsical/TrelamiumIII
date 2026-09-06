namespace TrelamiumIII.Core.Graphics.Metaballs;

/// <summary>
///     One blob in a <see cref="MetaballField" />'s density field.
/// </summary>
/// <remarks>
///     Nodes are simulated, but they are not entities like the gas system so they don't carry damage, or have an owner,
///     immunity frames, etc., etc.
/// </remarks>
public struct MetaballNode
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Life;
    public float MaxLife;

    /// <summary>
    ///     0.0 = buoyoyoyant, 1.0 = dense. <see cref="MetaballField" /> interpolates between
    ///     its buoyancy and gravity constants across this range.
    /// </summary>
    public float Density;

    /// <summary>
    ///     0.0 = flows freely, 1.0 = sluggish. Scales drag, and damps the spread a node
    ///     gets when it lands on something.
    /// </summary>
    public float Viscosity;

    public bool OnCeiling;
    public bool OnFloor;
    public bool OnWall;

    /// <summary>Ticks this node has spent inside solid tiles.</summary>
    public int EnclosedTicks;

    /// <summary>
    ///     The thing this node rides, or <see langword="null" /> when it drifts under its own physics.
    /// </summary>
    /// <remarks>
    ///     Anchored nodes are pinned to <see cref="AnchorOffset" /> from the host's centre and skip
    ///     collision entirely.
    ///     The anchor releases on <see cref="AnchorTicks" /> expiry or host death, and the node
    ///     finishes its life drifting normally, so a cloud clings then falls off.
    ///     <para>
    ///         Held as the entity rather than as an index into one of the arrays, because a node can
    ///         ride an NPC or a player and there is no index that means the same thing in both. Both
    ///         arrays are pooled for the life of the game, so the reference costs nothing to keep, and
    ///         a host whose slot gets reused reads as dead rather than as somebody else.
    ///     </para>
    /// </remarks>
    public Entity? AnchorHost;

    public Vector2 AnchorOffset;

    public int AnchorTicks;

    public readonly bool IsAnchored => AnchorHost is not null && AnchorTicks > 0;

    public readonly float Alpha => MaxLife <= 0f ? 0f : Life / MaxLife;

    public readonly bool Active => Life > 0f;
}
