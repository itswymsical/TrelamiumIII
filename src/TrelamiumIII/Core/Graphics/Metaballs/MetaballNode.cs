namespace TrelamiumIII.Core.Graphics.Metaballs;

/// <summary>
///     One blob in a <see cref="MetaballLayer" />'s density field.
/// </summary>
/// <remarks>
///     Nodes are simulated, but they are not entities -- they carry no damage, no owner and no
///     immunity frames, and nothing outside the layer can be hit by one. A node is a place where
///     the field is dense, and the layer's shader decides what that looks like.
/// </remarks>
public struct MetaballNode
{
    /// <summary>Sentinel for <see cref="AnchorNPC" />: this node drifts under its own physics.</summary>
    public const int NO_ANCHOR = -1;

    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Life;
    public float MaxLife;

    /// <summary>
    ///     0.0 = very buoyant, 1.0 = very dense. <see cref="MetaballLayer" /> interpolates between
    ///     its buoyancy and gravity constants across this range.
    /// </summary>
    public float Density;

    /// <summary>
    ///     0.0 = flows freely, 1.0 = sluggish. Scales drag, and damps the lateral spread a node
    ///     gets when it lands on something.
    /// </summary>
    public float Viscosity;

    public bool OnCeiling;
    public bool OnFloor;
    public bool OnWall;

    /// <summary>Ticks this node has spent buried inside solid geometry.</summary>
    public int EnclosedTicks;

    /// <summary>
    ///     Index of the NPC this node rides, or <see cref="NO_ANCHOR" />.
    /// </summary>
    /// <remarks>
    ///     Anchored nodes are pinned to <see cref="AnchorOffset" /> from the host's centre and skip
    ///     collision entirely: they are cloth on a moving body, not gas in a room. Reseeding alone
    ///     smears into a trail behind anything quick, which reads as exhaust rather than infection.
    ///     The anchor releases on <see cref="AnchorTicks" /> expiry or host death, and the node
    ///     finishes its life drifting normally -- so a cloud clings, then sloughs off.
    /// </remarks>
    public int AnchorNPC;

    public Vector2 AnchorOffset;

    public int AnchorTicks;

    public readonly bool IsAnchored => AnchorNPC != NO_ANCHOR && AnchorTicks > 0;

    /// <summary>Remaining fraction of life, which the layer draws the node's opacity from.</summary>
    public readonly float Alpha => MaxLife <= 0f ? 0f : Life / MaxLife;

    public readonly bool Active => Life > 0f;
}
