using StructureHelper;
using Generator = StructureHelper.API.Generator;
using Terraria.DataStructures;
using TrelamiumIII.Core.NPCs.Housing;

namespace TrelamiumIII.Common.Generation.Utils;

/// <summary>
/// Where on a structure a placement point refers to. StructureHelper always places
/// from the top-left; this lets a caller say "put the bottom-centre here" or "put
/// this doorway tile here" instead of doing the subtraction by hand.
/// </summary>
public readonly struct StructureAnchor
{
    private readonly float fractionX, fractionY;
    private readonly int tileX, tileY;
    private readonly bool isTile;

    private StructureAnchor(float fx, float fy, int tx, int ty, bool tile)
    {
        fractionX = fx;
        fractionY = fy;
        tileX = tx;
        tileY = ty;
        isTile = tile;
    }

    /// <summary>A point given as a fraction of the structure's size: 0 is left/top, 1 is right/bottom.</summary>
    public static StructureAnchor Fraction(float x, float y) => new(x, y, 0, 0, false);

    /// <summary>A specific tile inside the structure, counted from its top-left.</summary>
    public static StructureAnchor Tile(int x, int y) => new(0, 0, x, y, true);

    public static readonly StructureAnchor TopLeft = Fraction(0f, 0f);
    public static readonly StructureAnchor TopCenter = Fraction(0.5f, 0f);
    public static readonly StructureAnchor TopRight = Fraction(1f, 0f);
    public static readonly StructureAnchor CenterLeft = Fraction(0f, 0.5f);
    public static readonly StructureAnchor Center = Fraction(0.5f, 0.5f);
    public static readonly StructureAnchor CenterRight = Fraction(1f, 0.5f);
    public static readonly StructureAnchor BottomLeft = Fraction(0f, 1f);
    public static readonly StructureAnchor BottomCenter = Fraction(0.5f, 1f);
    public static readonly StructureAnchor BottomRight = Fraction(1f, 1f);

    /// <summary>The anchor's offset from the structure's top-left, for a structure of <paramref name="size"/>.</summary>
    public Point Offset(Point16 size)
    {
        if (isTile)
            return new Point(tileX, tileY);

        return new Point(
            (int)MathF.Round(fractionX * (size.X - 1)),
            (int)MathF.Round(fractionY * (size.Y - 1)));
    }
}

/// <summary>A structure that has been generated, with its footprint in tile coordinates.</summary>
public readonly record struct PlacedStructure(string Path, Point Origin, Point16 Size)
{
    public Rectangle Area => new(Origin.X, Origin.Y, Size.X, Size.Y);

    /// <summary>World tile of a tile inside the structure, counted from its top-left.</summary>
    public Point Tile(int localX, int localY) => new(Origin.X + localX, Origin.Y + localY);

    public Point Tile(StructureAnchor anchor)
    {
        Point offset = anchor.Offset(Size);
        return Tile(offset.X, offset.Y);
    }

    /// <summary>
    /// Reserves this structure as the home of <typeparamref name="T"/>. The resident
    /// stands at <paramref name="standAt"/>, or on the floor at the bottom-centre.
    /// </summary>
    public NPCHome ReserveFor<T>(StructureAnchor? standAt = null, bool spawnsResident = true) where T : ModNPC
        => WorldNPCHousing.Reserve<T>(Area, standAt is { } at ? Tile(at) : null, spawnsResident);
}

/// <summary>
/// StructureHelper, placed by anchor. Paths are relative to the mod root without the
/// extension, e.g. <c>"Assets/Structures/ArgiesLab"</c>.
/// </summary>
public static class Structures
{
    private static Mod Mod => TrelamiumIII.Instance;

    public static Point16 Dimensions(string path) => Generator.GetStructureDimensions(path, Mod);

    /// <summary>Top-left tile the structure needs so that <paramref name="anchor"/> lands on <paramref name="target"/>.</summary>
    public static Point OriginFor(string path, Point target, StructureAnchor anchor)
    {
        Point offset = anchor.Offset(Dimensions(path));
        return new Point(target.X - offset.X, target.Y - offset.Y);
    }

    /// <summary>True if the whole structure would fit in the world at that placement.</summary>
    public static bool Fits(string path, Point target, StructureAnchor anchor)
    {
        Point origin = OriginFor(path, target, anchor);
        return Generator.IsInBounds(path, Mod, new Point16(origin.X, origin.Y));
    }

    /// <summary>Generates the structure so that <paramref name="anchor"/> lands on <paramref name="target"/>.</summary>
    public static PlacedStructure Place(string path, Point target, StructureAnchor anchor, GenFlags flags = GenFlags.None, bool ignoreNull = false)
    {
        Point16 size = Dimensions(path);
        Point offset = anchor.Offset(size);
        var origin = new Point(target.X - offset.X, target.Y - offset.Y);

        Generator.GenerateStructure(path, new Point16(origin.X, origin.Y), Mod, fullPath: false, ignoreNull, flags);

        return new PlacedStructure(path, origin, size);
    }

    /// <summary>Top-left placement, for callers that already have the origin.</summary>
    public static PlacedStructure Place(string path, Point origin, GenFlags flags = GenFlags.None, bool ignoreNull = false)
        => Place(path, origin, StructureAnchor.TopLeft, flags, ignoreNull);

    /// <summary>Like <see cref="Place(string, Point, StructureAnchor, GenFlags, bool)"/>, but refuses rather than clipping at the world edge.</summary>
    public static bool TryPlace(string path, Point target, StructureAnchor anchor, out PlacedStructure placed, GenFlags flags = GenFlags.None, bool ignoreNull = false)
    {
        placed = default;

        if (!Fits(path, target, anchor))
            return false;

        placed = Place(path, target, anchor, flags, ignoreNull);
        return true;
    }
}
