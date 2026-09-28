using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas;

/// <summary>The surface a crawling emitter follows. Its direction is the sign of its velocity along that surface.</summary>
public enum CrawlMode : byte
{
    /// <summary>Not crawling: ballistic flight.</summary>
    None,

    /// <summary>Along the open row above a floor. Direction is the sign of <see cref="GasEmitter.Velocity" />.X.</summary>
    Floor,

    /// <summary>Along a wall face. Direction is the sign of <see cref="GasEmitter.Velocity" />.Y.</summary>
    Wall,
}

/// <summary>How a crawl moves over uneven terrain.</summary>
/// <param name="StepUp">Tiles a floor run climbs in one step.</param>
/// <param name="StepDown">Tiles a floor run drops before it counts as going over a ledge.</param>
/// <param name="Climbs">Whether a floor run turns up a wall it cannot step over, and over the wall's top.</param>
public readonly record struct CrawlRules(int StepUp, int StepDown, bool Climbs)
{
    /// <summary>Runs down walls and over ledges, never up.</summary>
    public static CrawlRules Liquid => new(1, 2, false);

    /// <summary>As <see cref="Liquid" />, but also climbs walls.</summary>
    public static CrawlRules Climbing => new(1, 2, true);
}

/// <summary>
///     Tile-grid movement for emitters that follow terrain: floor runs, wall runs, steps, ledges and climbs. Uses the same
///     solid-tile test as node collision, so slopes and half bricks count as open.
/// </summary>
public static class SurfaceCrawl
{
    /// <summary>Pixels a crawler keeps between itself and the surface it follows.</summary>
    public const float Gap = 1f;

    /// <summary>Half a tile, so a step never passes over a tile it should have tested.</summary>
    public const float MaxStep = 8f;

    /// <summary>Advances a crawler by <paramref name="speed" /> pixels. Sets <see cref="GasEmitter.Life" /> to 0 where it is blocked.</summary>
    public static void Step(ref GasEmitter emitter, float speed, in CrawlRules rules)
    {
        speed = MathF.Min(speed, MaxStep);

        if (emitter.Crawl == CrawlMode.Wall)
        {
            if (emitter.Velocity.Y < 0f)
                StepWallUp(ref emitter, speed);
            else
                StepWallDown(ref emitter, speed);
        }
        else
            StepFloor(ref emitter, speed, rules);
    }

    private static bool IsSolid(int x, int y) => FieldLayer.IsSolidTile(x, y);

    private static int Tile(float coordinate) => (int)(coordinate / 16f);

    private static void StepFloor(ref GasEmitter emitter, float speed, in CrawlRules rules)
    {
        float direction = emitter.Velocity.X < 0f ? -1f : 1f;
        int row = Tile(emitter.Position.Y);
        int current = Tile(emitter.Position.X);
        float nextX = emitter.Position.X + direction * speed;
        int column = Tile(nextX);

        if (IsSolid(column, row))
        {
            int rise = Rise(column, current, row, rules.StepUp);

            if (rise == 0)
            {
                if (rules.Climbs)
                    TurnUpWall(ref emitter, direction, column);
                else
                    emitter.Life = 0f;

                return;
            }

            row -= rise;
        }
        else
        {
            int landing = row;

            while (!IsSolid(column, landing + 1))
            {
                if (++landing - row > rules.StepDown)
                {
                    TurnOverLedge(ref emitter, direction, row);
                    return;
                }
            }

            row = landing;
        }

        emitter.Position = new Vector2(nextX, (row + 1) * 16f - Gap);
        emitter.Velocity = new Vector2(direction * speed, 0f);
    }

    /// <summary>Height of the step up into <paramref name="column" />, or 0 when it is taller than <paramref name="stepUp" /> or has no headroom.</summary>
    private static int Rise(int column, int current, int row, int stepUp)
    {
        int rise = 0;

        while (IsSolid(column, row - rise))
        {
            if (++rise > stepUp)
                return 0;
        }

        for (int k = 1; k <= rise; k++)
        {
            if (IsSolid(current, row - k))
                return 0;
        }

        return rise;
    }

    private static void TurnUpWall(ref GasEmitter emitter, float direction, int wallColumn)
    {
        float face = direction > 0f ? wallColumn * 16f : (wallColumn + 1) * 16f;

        emitter.Position.X = face - direction * Gap;
        emitter.Velocity = -Vector2.UnitY;
        emitter.Crawl = CrawlMode.Wall;
    }

    /// <summary>Starts a wall run down the face below the lip of the floor tile the run is leaving.</summary>
    private static void TurnOverLedge(ref GasEmitter emitter, float direction, int row)
    {
        int column = Tile(emitter.Position.X);
        float lip = direction > 0f ? (column + 1) * 16f : column * 16f;

        emitter.Position = new Vector2(lip + direction * Gap, (row + 1) * 16f + 1f);
        emitter.Velocity = Vector2.UnitY;
        emitter.Crawl = CrawlMode.Wall;
    }

    /// <summary>On reaching a floor, turns away from the wall. Where the wall ends, falls with <see cref="CrawlMode.None" />.</summary>
    private static void StepWallDown(ref GasEmitter emitter, float speed)
    {
        int column = Tile(emitter.Position.X);
        float nextY = emitter.Position.Y + speed;
        int row = Tile(nextY);

        if (IsSolid(column, row))
        {
            bool left = IsSolid(column - 1, row - 1);
            bool right = IsSolid(column + 1, row - 1);

            emitter.Position.Y = row * 16f - Gap;

            // Walled on both sides (a shaft) or neither: nowhere to run.
            if (left == right)
            {
                emitter.Life = 0f;
                return;
            }

            emitter.Velocity = new Vector2(left ? speed : -speed, 0f);
            emitter.Crawl = CrawlMode.Floor;
            return;
        }

        emitter.Position.Y = nextY;
        emitter.Velocity = new Vector2(0f, speed);

        if (!IsSolid(column - 1, row) && !IsSolid(column + 1, row))
            emitter.Crawl = CrawlMode.None;
    }

    /// <summary>A ceiling ends the climb; the top of the wall turns it into a floor run over that top.</summary>
    private static void StepWallUp(ref GasEmitter emitter, float speed)
    {
        int column = Tile(emitter.Position.X);
        int side = WallSide(column, Tile(emitter.Position.Y));
        float nextY = emitter.Position.Y - speed;
        int row = Tile(nextY);

        if (side == 0 || IsSolid(column, row))
        {
            emitter.Life = 0f;
            return;
        }

        if (!IsSolid(column + side, row))
        {
            float face = side > 0 ? (column + 1) * 16f : column * 16f;

            emitter.Position = new Vector2(face + side * Gap, (row + 1) * 16f - Gap);
            emitter.Velocity = new Vector2(side * speed, 0f);
            emitter.Crawl = CrawlMode.Floor;
            return;
        }

        emitter.Position.Y = nextY;
        emitter.Velocity = new Vector2(0f, -speed);
    }

    /// <summary>-1 or 1 for the side of <paramref name="column" /> with a solid tile in <paramref name="row" />, preferring left; 0 for neither.</summary>
    private static int WallSide(int column, int row)
        => IsSolid(column - 1, row) ? -1 : IsSolid(column + 1, row) ? 1 : 0;

    /// <summary>Away from the surface a crawler follows: up off a floor, out from a wall.</summary>
    public static Vector2 Outward(in GasEmitter emitter)
    {
        if (emitter.Crawl != CrawlMode.Wall)
            return -Vector2.UnitY;

        return WallSide(Tile(emitter.Position.X), Tile(emitter.Position.Y)) < 0 ? Vector2.UnitX : -Vector2.UnitX;
    }

    /// <summary>The solid tile a crawler is on or against.</summary>
    public static Point SurfaceTile(in GasEmitter emitter)
    {
        Vector2 inside = emitter.Position - Outward(in emitter) * 8f;

        return new Point(Tile(inside.X), Tile(inside.Y));
    }

    /// <summary>Where a floor run starts after landing on the floor tile in row <paramref name="tileY" />.</summary>
    public static Vector2 FloorStart(Vector2 landing, int tileY) => new(landing.X, tileY * 16f - Gap);

    /// <summary>Where a wall run starts after landing against the wall tile in column <paramref name="tileX" />.</summary>
    public static Vector2 WallStart(Vector2 landing, Vector2 normal, int tileX)
    {
        float face = normal.X > 0f ? (tileX + 1) * 16f : tileX * 16f;

        return new Vector2(face + MathF.Sign(normal.X) * Gap, landing.Y);
    }

    /// <summary>Whether a floor run starting at <paramref name="start" /> could take its first step in <paramref name="direction" />.</summary>
    public static bool CanStart(Vector2 start, int direction, in CrawlRules rules)
    {
        int column = Tile(start.X);
        int row = Tile(start.Y);

        return rules.Climbs || !IsSolid(column + direction, row) || Rise(column + direction, column, row, rules.StepUp) > 0;
    }

    /// <summary>Whether a node at <paramref name="position" /> has a solid tile beside or below it to hold onto.</summary>
    public static bool IsSupported(Vector2 position)
    {
        int x = Tile(position.X);
        int y = Tile(position.Y);

        return IsSolid(x, y + 1) || IsSolid(x - 1, y) || IsSolid(x + 1, y);
    }
}
