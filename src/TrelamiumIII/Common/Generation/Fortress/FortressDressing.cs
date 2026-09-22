using System.Collections.Generic;
using Terraria.Utilities;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation.Fortress;

/// <summary> Areas the dressing leaves alone: openings, ladder columns, chest spots and the altar. </summary>
public sealed class FortressKeepOut
{
    private readonly List<Rectangle> areas = [];

    public void Add(Rectangle area) => areas.Add(area);

    public bool Contains(int x, int y)
    {
        foreach (Rectangle area in areas)
        {
            if (area.Contains(x, y))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Weathers a built fortress: holes and peat patches in the back wall, peat and slate patches pushing through the
/// brickwork, and living wood roots across the rooms. Exposed peat is left for the Ingress flora pass, which turns it
/// into grass with vines and foliage. Only brick and air inside the fortress polygon are changed.
/// </summary>
public static class FortressDressing
{
    private const float WallHoleThreshold = 0.3f;
    private const float WallHoleEdge = 0.06f;
    private const float WallHoleFrequency = 0.03f;
    private const float PeatWallThreshold = 0.35f;
    private const float PeatWallFrequency = 0.05f;

    private const int RoomPeatPatchesMin = 3;
    private const int RoomPeatPatchesMax = 5;
    private const int RoomSlatePatchesMin = 1;
    private const int RoomSlatePatchesMax = 3;
    private const int ArenaPeatPatchesMin = 8;
    private const int ArenaPeatPatchesMax = 12;
    private const int ArenaSlatePatchesMin = 3;
    private const int ArenaSlatePatchesMax = 5;
    private const int PeatRadiusMin = 3;
    private const int PeatRadiusMax = 7;
    private const int SlateRadiusMin = 2;
    private const int SlateRadiusMax = 5;

    /// <summary> Fraction of a peat patch's radius that grows out of the brick into the room. </summary>
    private const float PeatBulge = 0.6f;

    private const float PatchWarp = 2f;
    private const float PatchWarpFrequency = 0.2f;

    private const int RoomRootsMin = 1;
    private const int RoomRootsMax = 3;
    private const int ArenaRootsMin = 5;
    private const int ArenaRootsMax = 7;
    private const int RootLengthMin = 18;
    private const int RootLengthMax = 48;

    /// <summary> Rows above a floor that roots stay out of, so the floor stays walkable. </summary>
    private const int RoomRootFloorClearance = 5;

    private const int ArenaRootFloorClearance = 12;

    private const int RootThickSteps = 4;
    private const float RootTurnJitter = 0.3f;
    private const float RootTurnMomentum = 0.7f;
    private const float RootDroop = 0.04f;
    private const float RootBranchChance = 0.4f;
    private const float RootBranchTurn = 0.8f;
    private const int RootEmbedDepth = 2;

    public static void Apply(FortressLayout layout, FortressKeepOut keep, UnifiedRandom random)
    {
        DamageWalls(layout, random);

        ushort brick = (ushort)ModContent.TileType<BramblerootTile>();
        var patches = new PatchStamper(layout, keep, brick, random);
        var roots = new RootGrower(layout, keep, brick, random);

        foreach (FortressRoom room in layout.Rooms)
        {
            patches.Scatter(room.Interior, random.Next(RoomPeatPatchesMin, RoomPeatPatchesMax + 1), random.Next(RoomSlatePatchesMin, RoomSlatePatchesMax + 1));
            roots.Grow(room.Interior, random.Next(RoomRootsMin, RoomRootsMax + 1), RoomRootFloorClearance);
        }

        patches.Scatter(layout.Arena, random.Next(ArenaPeatPatchesMin, ArenaPeatPatchesMax + 1), random.Next(ArenaSlatePatchesMin, ArenaSlatePatchesMax + 1));
        roots.Grow(layout.Arena, random.Next(ArenaRootsMin, ArenaRootsMax + 1), ArenaRootFloorClearance);
        roots.Smooth();
    }

    /// <summary> Behind open tiles only: noise blobs with ragged edges lose their wall, and a second noise swaps some to peat wall. </summary>
    private static void DamageWalls(FortressLayout layout, UnifiedRandom random)
    {
        FastNoiseLite holes = CreateNoise(random.Next(), WallHoleFrequency);
        holes.SetFractalType(FastNoiseLite.FractalType.FBm);
        holes.SetFractalOctaves(3);

        FastNoiseLite peat = CreateNoise(random.Next(), PeatWallFrequency);
        ushort peatWall = (ushort)ModContent.WallType<PeatWall>();
        Rectangle bounds = layout.Bounds;

        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile || tile.WallType == WallID.None || !layout.Contains(x, y))
                    continue;

                float hole = holes.GetNoise(x, y);

                if (hole > WallHoleThreshold || (hole > WallHoleThreshold - WallHoleEdge && random.NextBool(2)))
                    tile.WallType = WallID.None;
                else if (peat.GetNoise(x, y) > PeatWallThreshold)
                    tile.WallType = peatWall;
            }
        }
    }

    private static FastNoiseLite CreateNoise(int seed, float frequency)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        return noise;
    }

    /// <summary> A cell just outside <paramref name="interior"/>, weighted toward the ceiling, and the direction into it. </summary>
    private static Point BoundaryCell(Rectangle interior, UnifiedRandom random, out Vector2 inward)
    {
        int side = random.Next(10);

        if (side < 4)
        {
            inward = Vector2.UnitY;
            return new Point(random.Next(interior.Left, interior.Right), interior.Top - 1);
        }

        if (side < 6)
        {
            inward = Vector2.UnitX;
            return new Point(interior.Left - 1, random.Next(interior.Top, interior.Bottom));
        }

        if (side < 8)
        {
            inward = -Vector2.UnitX;
            return new Point(interior.Right, random.Next(interior.Top, interior.Bottom));
        }

        inward = -Vector2.UnitY;
        return new Point(random.Next(interior.Left, interior.Right), interior.Bottom);
    }

    private sealed class PatchStamper(FortressLayout layout, FortressKeepOut keep, ushort brick, UnifiedRandom random)
    {
        private readonly FastNoiseLite warp = CreateNoise(random.Next(), PatchWarpFrequency);
        private readonly ushort peat = (ushort)ModContent.TileType<PeatTile>();
        private readonly ushort slate = (ushort)ModContent.TileType<SlateTile>();

        public void Scatter(Rectangle interior, int peatPatches, int slatePatches)
        {
            for (int i = 0; i < peatPatches; i++)
                Stamp(BoundaryCell(interior, random, out _), random.Next(PeatRadiusMin, PeatRadiusMax + 1), peat, PeatBulge);

            for (int i = 0; i < slatePatches; i++)
                Stamp(BoundaryCell(interior, random, out _), random.Next(SlateRadiusMin, SlateRadiusMax + 1), slate, 0f);
        }

        /// <summary> Brick within the warped radius becomes <paramref name="type"/>; air within <paramref name="bulge"/> of it is filled. </summary>
        private void Stamp(Point center, int radius, ushort type, float bulge)
        {
            int reach = radius + (int)MathF.Ceiling(PatchWarp);

            for (int x = center.X - reach; x <= center.X + reach; x++)
            {
                for (int y = center.Y - reach; y <= center.Y + reach; y++)
                {
                    if (!layout.Contains(x, y))
                        continue;

                    float distance = Vector2.Distance(new Vector2(x, y), center.ToVector2()) + warp.GetNoise(x, y) * PatchWarp;

                    if (distance >= radius)
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile)
                    {
                        if (tile.TileType == brick)
                            tile.TileType = type;
                    }
                    else if (distance < radius * bulge && tile.LiquidAmount == 0 && !keep.Contains(x, y))
                    {
                        tile.HasTile = true;
                        tile.TileType = type;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Living wood strands that leave the ceiling or a side wall, wander with momentum and a slight droop, and either
    /// root into the wall they reach or hang loose. Keep-out cells leave gaps rather than stopping the strand.
    /// </summary>
    private sealed class RootGrower(FortressLayout layout, FortressKeepOut keep, ushort brick, UnifiedRandom random)
    {
        private readonly List<Point> placed = [];

        public void Grow(Rectangle interior, int count, int floorClearance)
        {
            for (int i = 0; i < count; i++)
            {
                Point start;
                Vector2 inward;

                do
                    start = BoundaryCell(interior, random, out inward);
                while (inward.Y < 0f);

                Embed(start, -inward);
                Strand(interior, start.ToVector2() + inward, inward.ToRotation() + random.NextFloat(-0.6f, 0.6f), random.Next(RootLengthMin, RootLengthMax + 1), floorClearance, branch: true);
            }
        }

        /// <summary> Slopes root corners so strands read as rounded; brick is left square. </summary>
        public void Smooth()
        {
            foreach (Point cell in placed)
                Tile.SmoothSlope(cell.X, cell.Y, applyToNeighbors: false);
        }

        private void Strand(Rectangle interior, Vector2 position, float heading, int length, int floorClearance, bool branch)
        {
            int branchAt = branch && random.NextFloat() < RootBranchChance ? random.Next(length / 3, length * 2 / 3) : -1;
            Point previous = position.ToPoint();
            float turn = 0f;

            for (int step = 0; step < length; step++)
            {
                Point cell = position.ToPoint();

                if (!interior.Contains(cell))
                {
                    if (step > RootThickSteps)
                        Embed(cell, heading.ToRotationVector2());

                    return;
                }

                if (cell.Y >= interior.Bottom - floorClearance)
                    return;

                // Diagonal moves get an elbow so the strand stays edge-connected.
                if (cell.X != previous.X && cell.Y != previous.Y)
                    Place(new Point(cell.X, previous.Y));

                Place(cell);

                if (step < RootThickSteps)
                    Place(new Point(cell.X + 1, cell.Y));

                if (step == branchAt)
                    Strand(interior, position, heading + RootBranchTurn * (random.NextBool(2) ? 1f : -1f), length / 2, floorClearance, branch: false);

                previous = cell;
                turn = turn * RootTurnMomentum + random.NextFloat(-RootTurnJitter, RootTurnJitter) * (1f - RootTurnMomentum);
                heading += turn + MathHelper.WrapAngle(MathHelper.PiOver2 - heading) * RootDroop;
                position += heading.ToRotationVector2();
            }
        }

        /// <summary> Replaces brick for a couple of cells from <paramref name="cell"/> along <paramref name="direction"/>. </summary>
        private void Embed(Point cell, Vector2 direction)
        {
            Vector2 position = cell.ToVector2();

            for (int k = 0; k < RootEmbedDepth; k++, position += direction)
            {
                Point at = position.ToPoint();
                Tile tile = Main.tile[at.X, at.Y];

                if (tile.HasTile && tile.TileType == brick && layout.Contains(at.X, at.Y))
                {
                    tile.TileType = TileID.LivingWood;
                    placed.Add(at);
                }
            }
        }

        private void Place(Point cell)
        {
            Tile tile = Main.tile[cell.X, cell.Y];

            if (tile.HasTile || tile.LiquidAmount > 0 || keep.Contains(cell.X, cell.Y) || !layout.Contains(cell.X, cell.Y))
                return;

            tile.HasTile = true;
            tile.TileType = TileID.LivingWood;
            placed.Add(cell);
        }
    }
}
