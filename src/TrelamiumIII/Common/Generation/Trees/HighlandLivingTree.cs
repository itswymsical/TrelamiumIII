using System.Collections.Generic;
using Terraria.Utilities;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Ingress.HighlandsBiome;
using TrelamiumIII.Content.Ingress.HighlandsBiome.BirchFurniture;
using TrelamiumIII.Content.Ingress.HighlandsBiome.SpruceFurniture;

namespace TrelamiumIII.Common.Generation.Trees;

/// <summary>
/// Giant spruce and birch built from wood and leaf blocks, the highlands' take on vanilla's living trees. A buttressed
/// trunk stands on spreading roots and is hollow up its lower half, with platforms to climb, over a root room holding a
/// chest. Spruce stacks drooping tiers of needles up to a spire; birch runs a slender, leaning trunk up to a tall crown of
/// leaf clusters on short branches.
/// </summary>
public sealed class HighlandLivingTree
{
    public enum Species
    {
        Spruce,
        Birch
    }

    private const int WorldMargin = 20;

    /// <summary> Rows the ground under the trunk's flare may stray from the root column's ground. </summary>
    private const int GroundTolerance = 5;

    /// <summary> Fraction of the canopy's area that may already hold solid tiles, such as a hillside's edge. Foliage only fills open air, so a little is harmless. </summary>
    private const float CanopyCrowding = 0.03f;

    private const int FlareRows = 11;

    /// <summary> Columns the buttress spreads over: a range on the far side from the door, and little on the door's side, where it has to be cut through. </summary>
    private const float FlareColumnsMin = 3.5f;

    private const float FlareColumnsMax = 6f;
    private const float DoorFlareColumns = 2f;

    /// <summary> Rows the trunk's foot runs on into the ground, deeper than the ground can dip under it, drawing in to the shaft as it goes. </summary>
    private const int FootRows = 12;

    /// <summary> Rows a trunk edge holds each one-column step for above the buttress; within it the edge may step every row, to keep its curve. </summary>
    private const int MinStepRows = 2;

    /// <summary> Below one the trunk slims quickly from its base, leaving the height of it thin enough for the foliage to read. </summary>
    private const float TaperPower = 0.55f;

    /// <summary> Fraction of the height the trunk stops at; the foliage alone forms the tip above it. </summary>
    private const float TrunkTopFraction = 0.93f;

    /// <summary> Fraction of the height where the trunk starts drawing in to <see cref="TipHalfWidth"/>. </summary>
    private const float TipTaperFrom = 0.8f;

    private const float TipHalfWidth = 0.4f;

    private const int RootsPerSideMin = 2;
    private const int RootsPerSideMax = 3;
    private const int RootLengthMin = 18;
    private const int RootLengthMax = 36;
    private const float RootAngleShallow = 0.12f;
    private const float RootAngleDeep = 1f;
    private const float RootRadius = 2.4f;
    private const float RootTipRadius = 0.6f;
    private const float RootDroop = 0.012f;

    /// <summary> How hard a shallow root is steered back toward the row it's following just under the surface. </summary>
    private const float RootSurfacePull = 0.12f;

    /// <summary> Steps of a surface root per row it sinks below the ground, so it dives away from the trunk. </summary>
    private const int RootSinkSteps = 5;

    /// <summary> Steps a root may show at the surface; past that it keeps <see cref="RootCoverRows"/> of soil over it, so the grass runs on over it rather than a wooden ramp running down the hillside. </summary>
    private const int RootExposedSteps = 9;

    private const int RootCoverRows = 2;

    /// <summary> Rows of error the surface steering responds to at most, so a neighbouring tree's canopy can't yank a root upward. </summary>
    private const float RootSurfaceMaxRows = 4f;

    /// <summary> Rows above the hollow's top before foliage may lie across the trunk, so its ceiling stays wood. </summary>
    private const int FoliageClearance = 3;

    /// <summary> Fraction of the height the hollow climbs to, if the trunk stays wide enough for walls that far. </summary>
    private const float HollowFraction = 0.3f;

    private const int WallThickness = 2;
    private const int PlatformSpacing = 5;
    private const int DoorRows = 3;

    /// <summary> Columns of soil banked against the trunk that the doorway will dig on through. </summary>
    private const int DoorClearance = 6;

    private const int RoomDepth = 30;
    private const int RoomHeight = 6;
    private const int RoomRadiusMin = 8;
    private const int RoomRadiusMax = 11;
    private const int RoomShell = 2;

    private const int SpruceHeightMin = 72;
    private const int SpruceHeightMax = 96;
    private const int SpruceBaseMin = 5;
    private const int SpruceBaseMax = 6;
    private const float SpruceTopHalfWidth = 1f;
    private const float SpruceCanopyStart = 0.22f;
    private const int SpruceReachMin = 19;
    private const int SpruceReachMax = 25;
    private const int SpruceReachTop = 3;

    // Tiers far enough apart that a gap of sky shows between the tips of each bough and the one above.
    private const int TierSpacingMin = 7;
    private const int TierSpacingMax = 9;
    private const float BoughThicknessLow = 6f;
    private const float BoughThicknessHigh = 3f;
    private const float BoughDroopLow = 7f;
    private const float BoughDroopHigh = 2f;

    /// <summary> Above one a bough holds its thickness well along its length and draws in toward the tip, rather than thinning to a long string; below one, a short bough tapers from the start, rather than coming out a square block. </summary>
    private const float BoughTaperPower = 1.5f;

    private const float ShortBoughTaperPower = 0.7f;

    /// <summary> Columns of reach by which a bough tapers at the full <see cref="BoughTaperPower"/>. </summary>
    private const float FullTaperReach = 14f;

    /// <summary> Fraction of a main bough's reach, thickness and droop that the smaller one between it and the next takes. </summary>
    private const float SubBoughScale = 0.55f;

    /// <summary> Columns each side of a tier may stop short of its reach. </summary>
    private const int TipRaggedness = 2;

    /// <summary> Past this fraction of its reach a bough's underside starts its fringe: every <see cref="FringePeriod"/> columns, a hanging row of needles. </summary>
    private const float FringeFrom = 0.2f;

    private const int FringePeriod = 3;
    private const int SpireRows = 4;

    /// <summary> Rows down over the trunk's top that the core of leaves reaches, and how wide it is by then. </summary>
    private const int CoreOverlapRows = 6;

    private const float CoreHalfWidth = 2f;

    /// <summary> Largest pocket of air closed in by foliage that gets filled. </summary>
    private const int PocketMaxTiles = 12;

    private const int BirchHeightMin = 60;
    private const int BirchHeightMax = 82;
    private const int BirchBaseMin = 4;
    private const int BirchBaseMax = 5;
    private const float BirchTopHalfWidth = 1f;
    private const float BirchLeanMax = 3f;
    private const float BirchCrownStart = 0.62f;
    private const int CrownHalfWidthMin = 11;
    private const int CrownHalfWidthMax = 15;
    private const int CrownClustersMin = 20;
    private const int CrownClustersMax = 28;
    private const float CrownClusterRadiusMin = 3f;
    private const float CrownClusterRadiusMax = 6f;

    /// <summary> Fraction of the crown's radii its solid core fills, and the range of them that the lobes round its edge sit at. </summary>
    private const float CrownCoreFraction = 0.6f;

    private const float CrownLobeFractionMin = 0.6f;
    private const float CrownLobeFractionMax = 1f;
    private const float ClusterWobble = 0.2f;
    private const float ClusterLobes = 3f;
    private const int BranchesMin = 4;
    private const int BranchesMax = 6;
    private const int BranchLengthMin = 6;
    private const int BranchLengthMax = 11;

    private readonly Point ground;
    private readonly Species species;
    private readonly UnifiedRandom random;
    private readonly ushort wood;
    private readonly ushort leaves;
    private readonly int height;
    private readonly int baseHalfWidth;
    private readonly float topHalfWidth;
    private readonly float lean;
    private readonly int crownReach;

    /// <summary> The trunk's columns at each row, from the bottom of its foot up, as <see cref="BuildTrunk"/> laid them. </summary>
    private readonly (int Left, int Right)[] edges;

    /// <summary> Rows above the ground the hollow reaches; foliage above it can lie across the trunk. </summary>
    private int hollowRows;

    /// <summary> The side the doorway opens on, -1 or 1. </summary>
    private int door;

    private float flareLeft;
    private float flareRight;

    private int left = int.MaxValue;
    private int top = int.MaxValue;
    private int right = int.MinValue;
    private int bottom = int.MinValue;

    private HighlandLivingTree(Point ground, Species species, UnifiedRandom random)
    {
        this.ground = ground;
        this.species = species;
        this.random = random;

        if (species == Species.Spruce)
        {
            wood = (ushort)ModContent.TileType<SpruceWoodTile>();
            leaves = (ushort)ModContent.TileType<SpruceLeafTile>();
            height = random.Next(SpruceHeightMin, SpruceHeightMax + 1);
            baseHalfWidth = random.Next(SpruceBaseMin, SpruceBaseMax + 1);
            topHalfWidth = SpruceTopHalfWidth;
        }
        else
        {
            wood = (ushort)ModContent.TileType<BirchWoodTile>();
            leaves = (ushort)ModContent.TileType<BirchLeafTile>();
            height = random.Next(BirchHeightMin, BirchHeightMax + 1);
            baseHalfWidth = random.Next(BirchBaseMin, BirchBaseMax + 1);
            topHalfWidth = BirchTopHalfWidth;
            lean = random.NextFloat(-BirchLeanMax, BirchLeanMax);
        }

        crownReach = Reach(species);
        edges = new (int, int)[TrunkRows + FootRows + 1];
    }

    /// <summary> Rows of trunk above the ground. </summary>
    private int TrunkRows => (int)(height * TrunkTopFraction);

    /// <summary> Grows a tree whose trunk stands on the ground tile <paramref name="ground"/>; empty if the spot is too uneven or the canopy has no room. </summary>
    public static Rectangle Grow(Point ground, Species species, UnifiedRandom random)
    {
        var tree = new HighlandLivingTree(ground, species, random);
        return tree.Fits() ? tree.Build() : Rectangle.Empty;
    }

    /// <summary> Columns either side of the root column the tree's canopy can reach, for spacing trees apart. </summary>
    public static int Reach(Species species) => species == Species.Spruce ? SpruceReachMax + SpruceBaseMax + 2 : CrownHalfWidthMax + BranchLengthMax + (int)BirchLeanMax;

    private bool Fits()
    {
        int flare = baseHalfWidth + (int)FlareColumnsMax + 1;

        if (!WorldGen.InWorld(ground.X - crownReach, ground.Y - height - SpireRows, WorldMargin) || !WorldGen.InWorld(ground.X + crownReach, ground.Y + RoomDepth + RoomShell, WorldMargin))
            return false;

        for (int x = ground.X - flare; x <= ground.X + flare; x++)
        {
            int row = GroundRow(x);

            if (Math.Abs(row - ground.Y) > GroundTolerance || Main.tile[x, row - 1].LiquidAmount > 0)
                return false;
        }

        int canopyTop = ground.Y - height - SpireRows;
        int canopyBottom = ground.Y - FlareRows - 2;
        int solid = 0;
        int area = (crownReach * 2 + 1) * (canopyBottom - canopyTop);

        for (int x = ground.X - crownReach; x <= ground.X + crownReach; x++)
        {
            for (int y = canopyTop; y < canopyBottom; y++)
            {
                if (SurfaceSites.IsGround(Main.tile[x, y]))
                    solid++;
            }
        }

        return solid <= area * CanopyCrowding;
    }

    private Rectangle Build()
    {
        ChooseDoor();
        BuildTrunk();
        hollowRows = FindHollowRows();
        BuildRoots();

        if (species == Species.Spruce)
            BuildSpruceCanopy();
        else
            BuildBirchCrown();

        TidyCanopy();

        int roomFloor = ground.Y + RoomDepth;

        BuildRoom(roomFloor, out int roomRadius);
        HollowTrunk(roomFloor - RoomHeight);
        PlaceChest(roomFloor, -door * roomRadius / 2);

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary> The door faces the lower ground, so it opens onto the slope and not into it. The buttress is uneven, big on the far side and small by the door, so a tall one isn't left overhanging the way in. </summary>
    private void ChooseDoor()
    {
        int reach = baseHalfWidth + (int)DoorFlareColumns + 2;
        int leftRow = GroundRow(ground.X - reach);
        int rightRow = GroundRow(ground.X + reach);

        door = leftRow == rightRow ? (random.NextBool() ? 1 : -1) : leftRow > rightRow ? -1 : 1;

        float far = random.NextFloat(FlareColumnsMin, FlareColumnsMax);
        flareLeft = door < 0 ? DoorFlareColumns : far;
        flareRight = door > 0 ? DoorFlareColumns : far;
    }

    /// <summary> Column of the trunk's centre <paramref name="rowsUp"/> rows above the ground; birch bows along a sine. </summary>
    private float CenterAt(int rowsUp) => ground.X + lean * MathF.Sin(Math.Max(rowsUp, 0) / (float)height * MathHelper.Pi * 0.75f);

    /// <summary> Trunk half-width <paramref name="rowsUp"/> rows above the ground, tapering up. </summary>
    private float HalfWidthAt(int rowsUp)
    {
        float t = Math.Max(rowsUp, 0) / (float)height;
        float half = MathHelper.Lerp(baseHalfWidth, topHalfWidth, MathF.Pow(t, TaperPower));

        // Drawn in to a point near the top, so the crown closes over it instead of a blunt post standing out of the leaves.
        if (t > TipTaperFrom)
            half = MathHelper.Lerp(half, TipHalfWidth, (t - TipTaperFrom) / (1f - TipTaperFrom));

        return half;
    }

    /// <summary> Columns the buttress adds on <paramref name="side"/>: sweeping out to the ground on a concave curve, then drawing back in under it, so the foot runs down to the shaft instead of ending in a flat block. </summary>
    private float FlareAt(int rowsUp, int side)
    {
        float f = rowsUp >= 0 ? 1f - rowsUp / (float)FlareRows : 1f + rowsUp / (float)FootRows;

        return f > 0f ? f * f * (side < 0 ? flareLeft : flareRight) : 0f;
    }

    /// <summary>
    /// Builds the trunk from the foot up. Each edge follows the taper and lean but only ever steps one column at a time and,
    /// above the buttress, holds every step for <see cref="MinStepRows"/> rows, so the bark climbs in clean steps instead of
    /// jagging back and forth as the rounding flips.
    /// </summary>
    private void BuildTrunk()
    {
        int left = 0;
        int right = 0;
        int leftHeld = 0;
        int rightHeld = 0;

        for (int rowsUp = -FootRows; rowsUp <= TrunkRows; rowsUp++)
        {
            float center = CenterAt(rowsUp);
            float half = HalfWidthAt(rowsUp);
            int wantLeft = (int)MathF.Round(center - half - FlareAt(rowsUp, -1));
            int wantRight = (int)MathF.Round(center + half + FlareAt(rowsUp, 1));
            int hold = rowsUp < FlareRows ? 1 : MinStepRows;

            if (rowsUp == -FootRows)
            {
                left = wantLeft;
                right = wantRight;
            }
            else
            {
                if (++leftHeld >= hold && wantLeft != left)
                {
                    left += Math.Sign(wantLeft - left);
                    leftHeld = 0;
                }

                if (++rightHeld >= hold && wantRight != right)
                {
                    right += Math.Sign(wantRight - right);
                    rightHeld = 0;
                }
            }

            edges[rowsUp + FootRows] = (left, Math.Max(right, left));

            for (int x = left; x <= Math.Max(right, left); x++)
                PlaceWood(x, ground.Y - rowsUp);
        }
    }

    /// <summary> How far up the hollow can go: to <see cref="HollowFraction"/> of the height, or to where the trunk gets too thin to leave walls either side of a shaft. </summary>
    private int FindHollowRows()
    {
        int limit = (int)(height * HollowFraction);

        for (int rowsUp = 0; rowsUp < limit; rowsUp++)
        {
            (int shaftLeft, int shaftRight) = ShaftAt(rowsUp);

            if (shaftRight - shaftLeft < 2)
                return rowsUp;
        }

        return limit;
    }

    /// <summary> The columns of the hollow <paramref name="rowsUp"/> rows above the ground, centred in the trunk and always leaving <see cref="WallThickness"/> of it either side. </summary>
    private (int From, int To) ShaftAt(int rowsUp)
    {
        (int trunkLeft, int trunkRight) = edges[Math.Clamp(rowsUp + FootRows, 0, edges.Length - 1)];
        int center = (trunkLeft + trunkRight) / 2;
        int inner = Math.Max(1, (int)(HalfWidthAt(rowsUp) - WallThickness + 0.5f));

        return (Math.Max(center - inner, trunkLeft + WallThickness), Math.Min(center + inner, trunkRight - WallThickness));
    }

    /// <summary>
    /// Fans out from under the buttress on both sides: shallow roots leave along the surface and soon sink under it, deeper
    /// ones dive. Each tapers to a point along a smooth curve and only ever replaces soil, so none hangs in the air.
    /// </summary>
    private void BuildRoots()
    {
        foreach (int side in (ReadOnlySpan<int>)[-1, 1])
        {
            int count = random.Next(RootsPerSideMin, RootsPerSideMax + 1);

            for (int i = 0; i < count; i++)
                GrowRoot(side, (i + random.NextFloat(0.2f, 0.8f)) / count, surfaceHugging: i == 0);
        }
    }

    /// <param name="side"> -1 or 1. </param>
    /// <param name="fan"> 0 for the shallowest, 1 for the deepest; deeper roots start lower, dive steeper and run shorter. </param>
    /// <param name="surfaceHugging"> Whether it follows the ground rather than its own heading. </param>
    private void GrowRoot(int side, float fan, bool surfaceHugging)
    {
        int startRow = (int)MathHelper.Lerp(1f, FootRows, fan);
        (int trunkLeft, int trunkRight) = edges[Math.Clamp(FootRows - startRow, 0, edges.Length - 1)];

        var position = new Vector2(side < 0 ? trunkLeft : trunkRight, ground.Y + startRow);
        float angle = MathHelper.Lerp(RootAngleShallow, RootAngleDeep, fan);
        var direction = new Vector2(side * MathF.Cos(angle), MathF.Sin(angle));
        int length = (int)(MathHelper.Lerp(RootLengthMax, RootLengthMin, fan) * random.NextFloat(0.8f, 1.1f));
        float curve = random.NextFloat(-0.008f, 0.012f);

        for (int step = 0; step < length; step++)
        {
            float radius = MathHelper.Lerp(RootRadius, RootTipRadius, MathF.Pow(step / (float)length, 0.7f));

            PlaceRootDisc(position, radius, step < RootExposedSteps ? 0 : RootCoverRows);

            direction.Y += RootDroop * (0.5f + fan) + curve;

            if (surfaceHugging && WorldGen.InWorld((int)position.X, (int)position.Y, WorldMargin))
                direction.Y += Math.Clamp(GroundRow((int)position.X) + step / RootSinkSteps - position.Y, -RootSurfaceMaxRows, RootSurfaceMaxRows) * RootSurfacePull;

            // Once under the surface it only goes down, or a hill rising over it would curl its tip back up.
            if (step >= RootExposedSteps)
                direction.Y = MathF.Max(direction.Y, 0f);

            direction.Normalize();

            // Never turned back toward the trunk, however the steering pulls it.
            if (MathF.Abs(direction.X) < 0.3f)
                direction = Vector2.Normalize(new Vector2(side * 0.3f, direction.Y));

            position += direction;
        }
    }

    /// <summary> Wood in a disc wherever there is already soil or rock at least <paramref name="cover"/> rows under the surface, so a root follows the ground and never bridges open air. </summary>
    private void PlaceRootDisc(Vector2 center, float radius, int cover)
    {
        int reach = (int)MathF.Ceiling(radius);

        for (int x = (int)MathF.Round(center.X) - reach; x <= (int)MathF.Round(center.X) + reach; x++)
        {
            if (!WorldGen.InWorld(x, WorldMargin, WorldMargin))
                continue;

            int firstRow = cover > 0 ? GroundRow(x) + cover : 0;

            for (int y = Math.Max((int)MathF.Round(center.Y) - reach, firstRow); y <= (int)MathF.Round(center.Y) + reach; y++)
            {
                if (Vector2.DistanceSquared(new Vector2(x, y), center) <= radius * radius + 0.25f && WorldGen.InWorld(x, y, WorldMargin)
                    && SurfaceSites.IsGround(Main.tile[x, y]) && Main.tile[x, y].TileType != leaves)
                    PlaceWood(x, y);
            }
        }
    }

    /// <summary>
    /// Boughs from near the foot up to a spire, widest at the bottom and each drooping to a point, with a smaller bough in the
    /// gap above it so the crown stays full by the trunk while the long tips stand apart.
    /// </summary>
    private void BuildSpruceCanopy()
    {
        int start = ground.Y - (int)(height * SpruceCanopyStart);
        int crown = ground.Y - height;
        int reachMax = random.Next(SpruceReachMin, SpruceReachMax + 1);

        for (int y = start; y > crown + 1;)
        {
            float u = (start - y) / (float)(start - crown);

            // Measured from the bark, so the upper tiers still stand clear of the trunk.
            int reach = (int)(HalfWidthAt(ground.Y - y) + MathHelper.Lerp(reachMax, SpruceReachTop, MathF.Pow(u, 0.85f))) + random.Next(-1, 2);
            float thickness = MathHelper.Lerp(BoughThicknessLow, BoughThicknessHigh, u);
            float droop = MathHelper.Lerp(BoughDroopLow, BoughDroopHigh, u);
            int center = (int)MathF.Round(CenterAt(ground.Y - y));

            // Each side ends a little short of the other at random, so the tiers aren't mirrored.
            int leftReach = Math.Max(reach - random.Next(TipRaggedness + 1), 1);
            int rightReach = Math.Max(reach - random.Next(TipRaggedness + 1), 1);

            PlaceBough(center, y, leftReach, rightReach, thickness, droop);

            int spacing = random.Next(TierSpacingMin, TierSpacingMax + 1) - (u > 0.6f ? 1 : 0);
            int between = y - spacing / 2;

            if (between > crown + 1)
                PlaceBough(center, between, (int)(leftReach * SubBoughScale), (int)(rightReach * SubBoughScale), thickness * SubBoughScale, droop * SubBoughScale);

            y -= spacing;
        }

        // A core of leaves from over the trunk's top up to the spire, widening as it comes down, so the top tiers stay joined to the tree and the trunk's end never shows.
        int spire = crown - SpireRows;
        int coreBottom = ground.Y - TrunkRows + CoreOverlapRows;

        for (int row = spire; row <= coreBottom; row++)
        {
            int half = (int)MathF.Round(CoreHalfWidth * (row - spire) / (coreBottom - spire));
            int column = (int)MathF.Round(CenterAt(ground.Y - row));

            for (int dx = -half; dx <= half; dx++)
                PlaceFoliage(column + dx, row);
        }
    }

    /// <summary> Clears up where the foliage overlaps, above the buttress: pockets of air it has closed in are filled, and scraps of wood left between leaves become leaves. </summary>
    private void TidyCanopy()
    {
        int trunkFree = ground.Y - hollowRows - FoliageClearance;

        for (int y = top; y <= ground.Y - FlareRows; y++)
        {
            for (int x = left; x <= right; x++)
            {
                Tile tile = Main.tile[x, y];

                if (!tile.HasTile)
                {
                    if (IsLeaves(x, y - 1))
                        FillPocket(x, y);
                }
                else if (y < trunkFree && tile.TileType == wood && IsWoodScrap(x, y))
                {
                    Place(x, y, leaves);
                }
            }
        }
    }

    private bool IsLeaves(int x, int y) => Main.tile[x, y].HasTile && Main.tile[x, y].TileType == leaves;

    private bool IsWood(int x, int y) => Main.tile[x, y].HasTile && Main.tile[x, y].TileType == wood;

    /// <summary> A single row of wood between leaves, or a tile of it joined to no other. </summary>
    private bool IsWoodScrap(int x, int y)
        => (IsLeaves(x, y - 1) && IsLeaves(x, y + 1)) || !(IsWood(x, y - 1) || IsWood(x, y + 1) || IsWood(x - 1, y) || IsWood(x + 1, y));

    /// <summary> Leaves through the air around (<paramref name="x"/>, <paramref name="y"/>), if it's closed in and no bigger than <see cref="PocketMaxTiles"/>. The hollow has walls, so it's never taken for one. </summary>
    private void FillPocket(int x, int y)
    {
        var cells = new List<Point> { new(x, y) };

        for (int i = 0; i < cells.Count; i++)
        {
            foreach (Point step in (ReadOnlySpan<Point>)[new(0, -1), new(0, 1), new(-1, 0), new(1, 0)])
            {
                var next = new Point(cells[i].X + step.X, cells[i].Y + step.Y);
                Tile tile = Main.tile[next.X, next.Y];

                if (tile.HasTile || cells.Contains(next))
                    continue;

                if (tile.WallType != WallID.None || cells.Count == PocketMaxTiles)
                    return;

                cells.Add(next);
            }
        }

        foreach (Point cell in cells)
            Place(cell.X, cell.Y, leaves);
    }

    /// <summary> One bough each way from the trunk, thick where it leaves and sweeping down to a point, its underside fringed with hanging needles. </summary>
    private void PlaceBough(int center, int y, int leftReach, int rightReach, float thickness, float droop)
    {
        PlaceBoughSide(center, y, -1, leftReach, thickness, droop);
        PlaceBoughSide(center, y, 1, rightReach, thickness, droop);
    }

    private void PlaceBoughSide(int center, int y, int side, int reach, float thickness, float droop)
    {
        int previous = y;
        int phase = random.Next(FringePeriod);
        float taper = MathHelper.Lerp(ShortBoughTaperPower, BoughTaperPower, Math.Min(reach / FullTaperReach, 1f));

        for (int dx = 0; dx <= reach; dx++)
        {
            float a = dx / (float)Math.Max(reach, 1);
            int underside = y + (int)(a * a * droop);
            int rows = Math.Max((int)MathF.Round(MathHelper.Lerp(thickness, 1f, MathF.Pow(a, taper))), 1);
            int fringe = a > FringeFrom && (dx + phase) % FringePeriod == 0 ? 1 : 0;

            // Up to the last column's underside at the least, so a steep tip steps down still joined to it instead of breaking off.
            for (int row = Math.Min(underside - rows + 1, previous); row <= underside + fringe; row++)
                PlaceFoliage(center + side * dx, row);

            previous = underside;
        }
    }

    /// <summary> Short branches up the upper trunk, each ending in a leaf cluster, under a tall crown of overlapping clusters. </summary>
    private void BuildBirchCrown()
    {
        int crown = ground.Y - height;
        int crownBottom = ground.Y - (int)(height * BirchCrownStart);
        int crownHalfWidth = random.Next(CrownHalfWidthMin, CrownHalfWidthMax + 1);
        int branches = random.Next(BranchesMin, BranchesMax + 1);
        int side = random.NextBool() ? 1 : -1;

        for (int i = 0; i < branches; i++, side = -side)
        {
            int rowsUp = (int)MathHelper.Lerp(height * 0.45f, height * 0.85f, i / (float)Math.Max(branches - 1, 1)) + random.Next(-2, 3);
            var position = new Vector2(CenterAt(rowsUp) + side * (HalfWidthAt(rowsUp) + 0.5f), ground.Y - rowsUp);
            float angle = random.NextFloat(0.35f, 0.8f);
            var direction = new Vector2(side * MathF.Cos(angle), -MathF.Sin(angle));
            int length = random.Next(BranchLengthMin, BranchLengthMax + 1);

            for (int step = 0; step < length; step++)
            {
                PlaceBranch(position);
                position += direction;
            }

            PlaceCluster(position, random.NextFloat(3f, 5f));
        }

        var center = new Vector2(CenterAt(height), crown + (crownBottom - crown) * 0.45f);
        var radii = new Vector2(crownHalfWidth, (crownBottom - crown) * 0.5f + 2f);
        int clusters = random.Next(CrownClustersMin, CrownClustersMax + 1);

        PlaceEllipse(center, radii * CrownCoreFraction);

        // Lobes all round the core's edge, so the outline comes out a run of small bumps and not a few big flat-sided blobs.
        for (int i = 0; i < clusters; i++)
        {
            Vector2 edge = (i / (float)clusters * MathHelper.TwoPi + random.NextFloat(-0.2f, 0.2f)).ToRotationVector2();
            PlaceCluster(center + edge * radii * random.NextFloat(CrownLobeFractionMin, CrownLobeFractionMax), random.NextFloat(CrownClusterRadiusMin, CrownClusterRadiusMax));
        }

        PlaceCluster(new Vector2(CenterAt(height), crown + 2), 4f);
    }

    private void PlaceEllipse(Vector2 center, Vector2 radii)
    {
        for (int x = (int)(center.X - radii.X); x <= (int)(center.X + radii.X); x++)
        {
            for (int y = (int)(center.Y - radii.Y); y <= (int)(center.Y + radii.Y); y++)
            {
                if (((new Vector2(x, y) - center) / radii).LengthSquared() < 1f)
                    PlaceFoliage(x, y);
            }
        }
    }

    /// <summary> A rounded room in the roots, shelled in wood, with a flat wooden floor at <paramref name="floor"/>. </summary>
    private void BuildRoom(int floor, out int radius)
    {
        radius = random.Next(RoomRadiusMin, RoomRadiusMax + 1);
        int roomTop = floor - RoomHeight;

        for (int x = ground.X - radius - RoomShell; x <= ground.X + radius + RoomShell; x++)
        {
            int dx = Math.Abs(x - ground.X);

            // Corners round off over the top two rows.
            int inset = Math.Max(0, dx - (radius - 2));

            for (int y = roomTop - RoomShell; y <= floor + RoomShell; y++)
            {
                bool inside = dx <= radius && y >= roomTop + inset && y < floor;

                if (inside)
                    Hollow(x, y);
                else
                    PlaceWood(x, y);
            }
        }
    }

    /// <summary>
    /// Clears the trunk's core from <see cref="hollowRows"/> up down to <paramref name="roomTop"/>, walls it, lays platforms
    /// across it, and opens a doorway at the ground on the <see cref="door"/> side.
    /// </summary>
    private void HollowTrunk(int roomTop)
    {
        int hollowTop = ground.Y - hollowRows;

        for (int y = hollowTop; y < roomTop; y++)
        {
            (int from, int to) = ShaftAt(ground.Y - y);

            // Under the trunk's foot the shaft is cut through soil, so it gets a wooden lining of its own.
            if (y > ground.Y + FootRows)
            {
                for (int w = 1; w <= WallThickness; w++)
                {
                    PlaceWood(from - w, y);
                    PlaceWood(to + w, y);
                }
            }

            for (int x = from; x <= to; x++)
                Hollow(x, y);
        }

        // A floor across the doorway, then rungs a jump apart from the room up.
        PlacePlatform(ground.Y);

        for (int y = roomTop + RoomHeight - PlatformSpacing; y > hollowTop + 2; y -= PlatformSpacing)
        {
            if (Math.Abs(y - ground.Y) > 2)
                PlacePlatform(y);
        }

        for (int rowsUp = 1; rowsUp <= DoorRows; rowsUp++)
        {
            (int trunkLeft, int trunkRight) = edges[rowsUp + FootRows];
            (int from, int to) = ShaftAt(rowsUp);
            int y = ground.Y - rowsUp;

            // Walled like the shaft, so it reads as a passage into the tree rather than a hole through it.
            for (int x = door > 0 ? to + 1 : trunkLeft; x <= (door > 0 ? trunkRight : from - 1); x++)
                Hollow(x, y);

            int outside = door > 0 ? trunkRight + 1 : trunkLeft - 1;

            for (int n = 0; n < DoorClearance && Main.tile[outside, y].HasTile; n++, outside += door)
                Main.tile[outside, y].ClearTile();
        }
    }

    private void PlaceChest(int floor, int offset)
    {
        int type = species == Species.Spruce ? ModContent.Find<ModTile>(nameof(TrelamiumIII), "SpruceChestTile").Type : ModContent.TileType<BirchChestTile>();
        int chest = WorldGen.PlaceChest(ground.X + offset - 1, floor - 1, (ushort)type);

        if (chest < 0)
            return;

        Item[] items = Main.chest[chest].item;
        int slot = 0;

        void Add(int item, int stack)
        {
            items[slot].SetDefaults(item);
            items[slot].stack = stack;
            slot++;
        }

        // Placeholder loot until the highlands have a loot table.
        Add(species == Species.Spruce ? ModContent.ItemType<SpruceWoodItem>() : ModContent.ItemType<BirchWoodItem>(), random.Next(20, 41));
        Add(species == Species.Spruce ? ItemID.Torch : ModContent.ItemType<BirchTorchItem>(), random.Next(6, 13));
        Add(ItemID.LesserHealingPotion, random.Next(2, 5));
        Add(random.NextFromList(ItemID.SwiftnessPotion, ItemID.ShinePotion, ItemID.NightOwlPotion), random.Next(1, 3));
        Add(ItemID.SilverCoin, random.Next(20, 61));
    }

    /// <summary> Platforms across the hollow's width at row <paramref name="y"/>. </summary>
    private void PlacePlatform(int y)
    {
        int type = species == Species.Spruce ? ModContent.Find<ModTile>(nameof(TrelamiumIII), "SprucePlatformTile").Type : ModContent.TileType<BirchPlatformTile>();
        (int from, int to) = ShaftAt(ground.Y - y);

        for (int x = from; x <= to; x++)
            WorldGen.PlaceTile(x, y, type, mute: true, forced: true);
    }

    /// <summary> A leafy blob whose outline wobbles with the angle rather than per tile, so its edge never sheds loose leaves. </summary>
    private void PlaceCluster(Vector2 center, float radius)
    {
        int reach = (int)MathF.Ceiling(radius + 1f);
        float phase = random.NextFloat(MathHelper.TwoPi);

        for (int x = (int)center.X - reach; x <= (int)center.X + reach; x++)
        {
            for (int y = (int)center.Y - reach; y <= (int)center.Y + reach; y++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float wobble = ClusterWobble * MathF.Sin(MathF.Atan2(offset.Y, offset.X) * ClusterLobes + phase);

                if (offset.Length() / radius < 1f + wobble)
                    PlaceFoliage(x, y);
            }
        }
    }

    private void PlaceWood(int x, int y) => Place(x, y, wood);

    /// <summary> Two tiles tall, so a diagonal branch stays joined corner to corner instead of breaking into single tiles. </summary>
    private void PlaceBranch(Vector2 position)
    {
        int x = (int)MathF.Round(position.X);
        int y = (int)MathF.Round(position.Y);

        foreach (int row in (ReadOnlySpan<int>)[y, y + 1])
        {
            if (!Main.tile[x, row].HasTile)
                Place(x, row, wood);
        }
    }

    /// <summary>
    /// Leaves into open air, and above the hollow across the trunk as well, so the foliage reads over the wood rather than
    /// stopping at its edge.
    /// </summary>
    private void PlaceFoliage(int x, int y)
    {
        Tile tile = Main.tile[x, y];

        if (!tile.HasTile || (tile.TileType == wood && ground.Y - y > hollowRows + FoliageClearance))
            Place(x, y, leaves);
    }

    private void Place(int x, int y, ushort type)
    {
        if (!WorldGen.InWorld(x, y, WorldMargin))
            return;

        Tile tile = Main.tile[x, y];
        tile.HasTile = true;
        tile.TileType = type;
        tile.Slope = SlopeType.Solid;
        tile.IsHalfBlock = false;
        tile.LiquidAmount = 0;

        Include(x, y);
    }

    private void Hollow(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, WorldMargin))
            return;

        Tile tile = Main.tile[x, y];
        tile.ClearTile();
        tile.LiquidAmount = 0;
        tile.WallType = WallID.LivingWoodUnsafe;

        Include(x, y);
    }

    private void Include(int x, int y)
    {
        left = Math.Min(left, x);
        top = Math.Min(top, y);
        right = Math.Max(right, x);
        bottom = Math.Max(bottom, y);
    }

    /// <summary> First row from the top of column <paramref name="x"/> with a solid, non-platform tile. </summary>
    private static int GroundRow(int x)
    {
        for (int y = WorldMargin; y < Main.maxTilesY - WorldMargin; y++)
        {
            if (SurfaceSites.IsGround(Main.tile[x, y]))
                return y;
        }

        return Main.maxTilesY - WorldMargin;
    }
}
