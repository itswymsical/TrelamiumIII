namespace TrelamiumIII.Core.Tiles;

/// <summary> Pixel layout of a <see cref="CustomTree"/> texture: trunk frames, the canopy, and leaf clumps drawn over the canopy. </summary>
public sealed record TreeSheet(TrunkFrames Trunk, TreeCanopy Canopy, TreeLeaf[] Leaves);

/// <summary>
/// <paramref name="Count"/> frames of <paramref name="Size"/> along the texture's top row, <paramref name="Stride"/> pixels
/// apart. <paramref name="Offset"/> places a frame's top-left relative to its tile's top-left.
/// </summary>
public readonly record struct TrunkFrames(Point Size, int Stride, int Count, Point Offset)
{
    public Rectangle Source(int index) => new(Math.Clamp(index, 0, Count - 1) * Stride, 0, Size.X, Size.Y);
}

/// <summary>
/// The canopy sprite at <paramref name="Source"/>. <paramref name="Anchor"/>, in pixels within the source, sits on the top
/// trunk tile's top centre, <paramref name="Overlap"/> pixels down into that tile, and the canopy rotates around it.
/// </summary>
public readonly record struct TreeCanopy(Rectangle Source, Vector2 Anchor, int Overlap);

/// <summary>
/// A leaf clump drawn over the canopy. <paramref name="Pivot"/>, within <paramref name="Source"/>, is placed on
/// <paramref name="Mount"/>, within the canopy source, and the clump rotates around it.
/// </summary>
public readonly record struct TreeLeaf(Rectangle Source, Vector2 Pivot, Vector2 Mount);
