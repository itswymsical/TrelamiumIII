namespace TrelamiumIII.Common.Generation.Caves;

/// <summary>
/// A room in a <see cref="CaveNetwork"/>. Tunnels arrive at <see cref="Entry"/> and leave from <see cref="Exit"/>.
/// <see cref="Carve"/> runs in the cave pass; <see cref="Decorate"/> runs after smoothing, for structures and
/// anything else that must not be sloped or walled over.
/// </summary>
public abstract class CaveNode
{
    public abstract Vector2 Center { get; }

    public virtual Vector2 Entry => Center;

    public virtual Vector2 Exit => Center;

    /// <summary> Every tile <see cref="Carve"/> can clear. </summary>
    public abstract Rectangle Bounds { get; }

    public abstract void Carve(CaveCarver carver);

    public virtual void Decorate() { }

    protected static Rectangle BoundsAround(Vector2 center, Vector2 extent)
    {
        int left = (int)MathF.Floor(center.X - extent.X);
        int top = (int)MathF.Floor(center.Y - extent.Y);

        return new Rectangle(left, top, (int)MathF.Ceiling(center.X + extent.X) - left + 1, (int)MathF.Ceiling(center.Y + extent.Y) - top + 1);
    }
}

/// <summary> A round cavern with a warped outline. </summary>
public sealed class CavernNode(Vector2 center, float radius) : CaveNode
{
    private const float Warp = 9f;

    public override Vector2 Center => center;

    public override Rectangle Bounds => BoundsAround(center, new Vector2(radius + Warp));

    public override void Carve(CaveCarver carver) => carver.CarveBlob(center, radius, Warp);
}

/// <summary> A wide open cave: a warped ellipse with its floor cut level <paramref name="floorOffset"/> tiles below the centre. </summary>
public sealed class ChamberNode(Vector2 center, Vector2 radii, float floorOffset) : CaveNode
{
    private const float Warp = 12f;

    public override Vector2 Center => center;

    public override Rectangle Bounds => BoundsAround(center, radii + new Vector2(Warp));

    public override void Carve(CaveCarver carver) => carver.CarveEllipse(center, radii, Warp, center.Y + floorOffset);
}
