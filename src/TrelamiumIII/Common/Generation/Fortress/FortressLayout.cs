using System.Collections.Generic;
using Terraria.Utilities;

namespace TrelamiumIII.Common.Generation.Fortress;

public enum RoomSide
{
    Left,
    Right,
    Top,
    Bottom
}

/// <summary> A challenge room on the route. <see cref="Interior"/> is the air a template fills, in world tiles. </summary>
public sealed record FortressRoom(int Index, Point Cell, Rectangle Interior, RoomSide Entry, RoomSide Exit)
{
    /// <summary> Entry and exit side letters, e.g. <c>"LR"</c> or <c>"TB"</c>; the kind a room template is authored for. </summary>
    public string Kind => $"{Letter(Entry)}{Letter(Exit)}";

    public static char Letter(RoomSide side) => side switch
    {
        RoomSide.Left => 'L',
        RoomSide.Right => 'R',
        RoomSide.Top => 'T',
        _ => 'B'
    };
}

/// <summary>
/// The Ligneous Fortress plan: a <see cref="Columns"/> x <see cref="Rows"/> grid of room cells, a route through
/// <see cref="RoomCount"/> of them that never climbs, and an arena under the middle columns where the route ends.
/// Rooms are joined through the <see cref="Wall"/> between cells: side doors on the floor, hatches at the centre.
/// </summary>
public sealed class FortressLayout
{
    public const int RoomWidth = 64;
    public const int RoomHeight = 34;
    public const int Columns = 5;
    public const int Rows = 3;
    public const int RoomCount = 10;

    /// <summary> Solid thickness between neighbouring room interiors and around the grid. </summary>
    public const int Wall = 8;

    /// <summary> Side doors cover this many rows up from an interior's bottom row. </summary>
    public const int DoorHeight = 6;

    /// <summary> Hatches are this many columns wide, centred on the interior. </summary>
    public const int HatchWidth = 6;

    public const int ArenaFirstColumn = 1;
    public const int ArenaColumns = 3;
    public const int ArenaHeight = 56;

    /// <summary> Mass outside the grid and the arena. </summary>
    public const int Shell = 16;

    private const int Chamfer = 24;

    public static readonly int GridWidth = Columns * RoomWidth + (Columns + 1) * Wall;
    public static readonly int GridHeight = Rows * RoomHeight + (Rows + 1) * Wall;
    public static readonly int TotalWidth = GridWidth + Shell * 2;
    public static readonly int TotalHeight = GridHeight + ArenaHeight + Shell * 2;

    private readonly List<Vector2> outline;

    public Rectangle Bounds { get; }

    public Rectangle Grid { get; }

    public Rectangle Arena { get; }

    public IReadOnlyList<FortressRoom> Rooms { get; }

    /// <summary> Grid cells off the route, left solid. </summary>
    public IReadOnlyList<Point> SpareCells { get; }

    /// <summary> Shaft from the roof down to the first room's ceiling. </summary>
    public Rectangle Gate { get; }

    /// <summary> Shaft from the last room's floor down into the arena. </summary>
    public Rectangle ArenaHatch { get; }

    /// <summary> Just above the roof over <see cref="Gate"/>, where the caves connect. </summary>
    public Point GateApproach => new(Gate.Center.X, Bounds.Top - 2);

    private FortressLayout(Point origin, List<Point> route)
    {
        Bounds = new Rectangle(origin.X, origin.Y, TotalWidth, TotalHeight);
        Grid = new Rectangle(origin.X + Shell, origin.Y + Shell, GridWidth, GridHeight);

        Rectangle first = Interior(new Point(ArenaFirstColumn, 0));
        int arenaWidth = ArenaColumns * RoomWidth + (ArenaColumns - 1) * Wall;
        Arena = new Rectangle(first.X, Grid.Bottom, arenaWidth, ArenaHeight);

        var rooms = new List<FortressRoom>(route.Count);

        for (int i = 0; i < route.Count; i++)
        {
            RoomSide entry = i == 0 ? RoomSide.Top : SideToward(route[i], route[i - 1]);
            RoomSide exit = i == route.Count - 1 ? RoomSide.Bottom : SideToward(route[i], route[i + 1]);
            rooms.Add(new FortressRoom(i, route[i], Interior(route[i]), entry, exit));
        }

        Rooms = rooms;

        var spare = new List<Point>();

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (!route.Contains(new Point(column, row)))
                    spare.Add(new Point(column, row));
            }
        }

        SpareCells = spare;

        Rectangle start = rooms[0].Interior;
        Rectangle end = rooms[^1].Interior;
        Gate = new Rectangle(HatchLeft(start), Bounds.Top, HatchWidth, start.Top - Bounds.Top);
        ArenaHatch = new Rectangle(HatchLeft(end), end.Bottom, HatchWidth, Arena.Top - end.Bottom);

        outline = Outline();
    }

    /// <summary> <paramref name="origin"/> is the top-left of <see cref="Bounds"/>. </summary>
    public static FortressLayout Create(Point origin, UnifiedRandom random)
    {
        List<List<Point>> routes = [];
        List<Point> cells = [];

        for (int start = 0; start < Columns; start++)
            AddRoutes(0, start, cells, routes);

        return new FortressLayout(origin, routes[random.Next(routes.Count)]);
    }

    public Rectangle Interior(Point cell) => new(Grid.X + Wall + cell.X * (RoomWidth + Wall), Grid.Y + Wall + cell.Y * (RoomHeight + Wall), RoomWidth, RoomHeight);

    /// <summary> Left column of a hatch through the ceiling or floor of <paramref name="interior"/>. </summary>
    public static int HatchLeft(Rectangle interior) => interior.X + (interior.Width - HatchWidth) / 2;

    /// <summary> The opening between two neighbouring route rooms, through the wall that separates them. </summary>
    public static Rectangle Link(FortressRoom from, FortressRoom to)
    {
        Rectangle a = from.Interior;
        Rectangle b = to.Interior;

        if (from.Cell.Y != to.Cell.Y)
            return new Rectangle(HatchLeft(a), a.Bottom, HatchWidth, b.Top - a.Bottom);

        Rectangle left = a.X < b.X ? a : b;
        Rectangle right = a.X < b.X ? b : a;

        return new Rectangle(left.Right, left.Bottom - DoorHeight, right.Left - left.Right, DoorHeight);
    }

    /// <summary> Inside the fortress's outer polygon, tested at the tile centre. </summary>
    public bool Contains(int x, int y)
    {
        var point = new Vector2(x + 0.5f, y + 0.5f);
        bool inside = false;

        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            Vector2 a = outline[i];
            Vector2 b = outline[j];

            if (a.Y > point.Y != b.Y > point.Y && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }

        return inside;
    }

    /// <summary> Chamfered block around the grid, tapering down to a narrower block around the arena. </summary>
    private List<Vector2> Outline()
    {
        float left = Bounds.Left;
        float right = Bounds.Right;
        float top = Bounds.Top;
        float bottom = Bounds.Bottom;
        float arenaLeft = Arena.Left - Shell;
        float arenaRight = Arena.Right + Shell;
        float taperTop = Grid.Bottom + Shell - Wall;
        float taperBottom = bottom - Chamfer;

        return
        [
            new(left + Chamfer, top),
            new(right - Chamfer, top),
            new(right, top + Chamfer),
            new(right, taperTop),
            new(arenaRight, taperBottom),
            new(arenaRight - Chamfer, bottom),
            new(arenaLeft + Chamfer, bottom),
            new(arenaLeft, taperBottom),
            new(left, taperTop),
            new(left, top + Chamfer)
        ];
    }

    /// <summary> The arena's outer columns, so the hatch ladder never comes down on the altar at its centre. </summary>
    private static bool IsArenaEntryColumn(int column) => column == ArenaFirstColumn || column == ArenaFirstColumn + ArenaColumns - 1;

    private static RoomSide SideToward(Point from, Point to)
    {
        if (to.X < from.X)
            return RoomSide.Left;

        if (to.X > from.X)
            return RoomSide.Right;

        return to.Y < from.Y ? RoomSide.Top : RoomSide.Bottom;
    }

    /// <summary>
    /// Every route that enters row <paramref name="row"/> at <paramref name="start"/>: one horizontal run per row, a drop
    /// to the next row below the run's last cell, and a last run ending over one of the arena's outer columns.
    /// </summary>
    private static void AddRoutes(int row, int start, List<Point> cells, List<List<Point>> routes)
    {
        int remaining = RoomCount - cells.Count;
        int rowsAfter = Rows - row - 1;

        for (int direction = -1; direction <= 1; direction += 2)
        {
            // A one-cell run has no direction; count it once.
            for (int length = direction > 0 ? 2 : 1; length <= Math.Min(Columns, remaining); length++)
            {
                int end = start + direction * (length - 1);

                if (end < 0 || end >= Columns)
                    break;

                int after = remaining - length;

                if (rowsAfter == 0 ? after != 0 || !IsArenaEntryColumn(end) : after < rowsAfter || after > rowsAfter * Columns)
                    continue;

                for (int k = 0; k < length; k++)
                    cells.Add(new Point(start + direction * k, row));

                if (rowsAfter == 0)
                    routes.Add([.. cells]);
                else
                    AddRoutes(row + 1, end, cells, routes);

                cells.RemoveRange(cells.Count - length, length);
            }
        }
    }
}
