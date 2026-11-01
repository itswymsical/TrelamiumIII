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
    private const int GroundTolerance = 4;

    /// <summary> Fraction of the canopy's area that may already hold solid tiles, such as a hillside's edge. </summary>
    private const float CanopyCrowding = 0.01f;

    private const int FlareRows = 7;
    private const float FlareColumns = 3f;
    private const int FootRows = 3;

    private const int RootsMin = 3;
    private const int RootsMax = 5;
    private const int RootLengthMin = 10;
    private const int RootLengthMax = 22;
    private const int RootStartRowsMax = 4;
    private const float RootThickness = 2f;
    private const float RootDroop = 0.04f;

    private const float HollowFraction = 0.5f;
    private const int WallThickness = 2;
    private const int PlatformSpacing = 5;
    private const int DoorRows = 3;

    private const int RoomDepth = 16;
    private const int RoomHeight = 6;
    private const int RoomRadiusMin = 8;
    private const int RoomRadiusMax = 11;
    private const int RoomShell = 2;

    private const int SpruceHeightMin = 72;
    private const int SpruceHeightMax = 96;
    private const int SpruceBaseMin = 3;
    private const int SpruceBaseMax = 4;
    private const float SpruceTopHalfWidth = 0.6f;
    private const float SpruceCanopyStart = 0.26f;
    private const int SpruceReachMin = 16;
    private const int SpruceReachMax = 21;
    private const int SpruceReachTop = 2;

    // Tiers far enough apart, and thin enough, that a gap of sky shows between each skirt and the one above.
    private const int TierSpacingMin = 7;
    private const int TierSpacingMax = 9;
    private const float TierThicknessLow = 3f;
    private const float TierThicknessHigh = 2f;
    private const float TierDroopLow = 6f;
    private const float TierDroopHigh = 2f;

    /// <summary> Columns each side of a tier may stop short of its reach. </summary>
    private const int TipRaggedness = 2;
    private const int SpireRows = 4;

    private const int BirchHeightMin = 60;
    private const int BirchHeightMax = 82;
    private const int BirchBaseMin = 2;
    private const int BirchBaseMax = 3;
    private const float BirchTopHalfWidth = 1f;
    private const float BirchLeanMax = 4f;
    private const float BirchCrownStart = 0.62f;
    private const int CrownHalfWidthMin = 11;
    private const int CrownHalfWidthMax = 15;
    private const int CrownClustersMin = 16;
    private const int CrownClustersMax = 22;
    private const float CrownClusterRadiusMin = 4f;
    private const float CrownClusterRadiusMax = 7f;
    private const float ClusterWobble = 0.14f;
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
    }

    /// <summary> Grows a tree whose trunk stands on the ground tile <paramref name="ground"/>; empty if the spot is too uneven or the canopy has no room. </summary>
    public static Rectangle Grow(Point ground, Species species, UnifiedRandom random)
    {
        var tree = new HighlandLivingTree(ground, species, random);
        return tree.Fits() ? tree.Build() : Rectangle.Empty;
    }

    /// <summary> Columns either side of the root column the tree's canopy can reach, for spacing trees apart. </summary>
    public static int Reach(Species species) => species == Species.Spruce ? SpruceReachMax + 2 : CrownHalfWidthMax + BranchLengthMax + (int)BirchLeanMax;

    private bool Fits()
    {
        int flare = baseHalfWidth + (int)FlareColumns + 1;

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
        BuildTrunk();
        BuildRoots();

        if (species == Species.Spruce)
            BuildSpruceCanopy();
        else
            BuildBirchCrown();

        int door = random.NextBool() ? 1 : -1;
        int roomFloor = ground.Y + RoomDepth;

        BuildRoom(roomFloor, out int roomRadius);
        HollowTrunk(roomFloor - RoomHeight, door);
        PlaceChest(roomFloor, -door * roomRadius / 2);

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary> Column of the trunk's centre <paramref name="rowsUp"/> rows above the ground; birch bows along a sine. </summary>
    private float CenterAt(int rowsUp) => ground.X + lean * MathF.Sin(Math.Max(rowsUp, 0) / (float)height * MathHelper.Pi * 0.75f);

    /// <summary> Trunk half-width <paramref name="rowsUp"/> rows above the ground, tapering up, with the root flare at its foot when <paramref name="flared"/>. </summary>
    private float HalfWidthAt(int rowsUp, bool flared = true)
    {
        float t = Math.Max(rowsUp, 0) / (float)height;
        float half = MathHelper.Lerp(baseHalfWidth, topHalfWidth, MathF.Pow(t, 0.8f));

        if (flared && rowsUp < FlareRows)
        {
            float f = 1f - Math.Max(rowsUp, 0) / (float)FlareRows;
            half += f * f * FlareColumns;
        }

        return half;
    }

    private void BuildTrunk()
    {
        for (int rowsUp = -FootRows; rowsUp <= height; rowsUp++)
        {
            float center = CenterAt(rowsUp);
            float half = HalfWidthAt(rowsUp);

            for (int x = (int)MathF.Round(center - half); x <= (int)MathF.Round(center + half); x++)
                PlaceWood(x, ground.Y - rowsUp);
        }

        // Fill under the flare where the ground dips, so no part of the foot hangs in the air.
        int flare = (int)MathF.Ceiling(HalfWidthAt(0));

        for (int x = ground.X - flare; x <= ground.X + flare; x++)
        {
            for (int y = ground.Y + FootRows; y <= ground.Y + GroundTolerance + FootRows && !SurfaceSites.IsGround(Main.tile[x, y]); y++)
                PlaceWood(x, y);
        }
    }

    /// <summary> Roots alternate sides, start at or a little above the ground so the higher ones arch over it as buttresses, and bend down as they go. </summary>
    private void BuildRoots()
    {
        int count = random.Next(RootsMin, RootsMax + 1);
        int firstSide = random.NextBool() ? 1 : -1;

        for (int i = 0; i < count; i++)
        {
            int side = i % 2 == 0 ? firstSide : -firstSide;
            int rowsUp = random.Next(RootStartRowsMax + 1);
            var position = new Vector2(CenterAt(rowsUp) + side * HalfWidthAt(rowsUp), ground.Y - rowsUp);
            float angle = random.NextFloat(0.2f, 0.9f);
            var direction = new Vector2(side * MathF.Cos(angle), MathF.Sin(angle));
            int length = random.Next(RootLengthMin, RootLengthMax + 1);

            for (int step = 0; step < length; step++)
            {
                float radius = MathHelper.Lerp(RootThickness, 0.5f, step / (float)length);

                PlaceDisc(position, radius, wood, replace: true);

                direction.Y += RootDroop + random.NextFloat(-0.08f, 0.08f);
                direction.Normalize();
                position += direction;
            }
        }
    }

    /// <summary> Tiers of needles from a quarter of the way up to a spire, each a skirt thinning and drooping toward its tips. </summary>
    private void BuildSpruceCanopy()
    {
        int start = ground.Y - (int)(height * SpruceCanopyStart);
        int crown = ground.Y - height;
        int reachMax = random.Next(SpruceReachMin, SpruceReachMax + 1);

        for (int y = start; y > crown + SpireRows;)
        {
            float u = (start - y) / (float)(start - crown);
            int reach = (int)MathHelper.Lerp(reachMax, SpruceReachTop, MathF.Pow(u, 0.85f)) + random.Next(-1, 2);
            float thickness = MathHelper.Lerp(TierThicknessLow, TierThicknessHigh, u);
            float droop = MathHelper.Lerp(TierDroopLow, TierDroopHigh, u);
            int center = (int)MathF.Round(CenterAt(ground.Y - y));

            // Each side ends a little short of the other at random, so the tips are ragged without leaving loose leaves.
            int leftReach = Math.Max(reach - random.Next(TipRaggedness + 1), 1);
            int rightReach = Math.Max(reach - random.Next(TipRaggedness + 1), 1);

            for (int dx = -leftReach; dx <= rightReach; dx++)
            {
                float a = Math.Abs(dx) / (float)(dx < 0 ? leftReach : rightReach);
                int skirtBottom = y + (int)(a * a * droop);
                int skirtTop = skirtBottom - (int)(thickness + (1f - a) * 2f);

                for (int row = skirtTop; row <= skirtBottom; row++)
                    PlaceLeaf(center + dx, row);
            }

            y -= random.Next(TierSpacingMin, TierSpacingMax + 1) - (u > 0.6f ? 1 : 0);
        }

        int tip = (int)MathF.Round(CenterAt(height));

        for (int row = crown - 2; row <= crown + SpireRows; row++)
        {
            int half = row < crown + 1 ? 0 : 1;

            for (int dx = -half; dx <= half; dx++)
                PlaceLeaf(tip + dx, row);
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
            var position = new Vector2(CenterAt(rowsUp) + side * (HalfWidthAt(rowsUp, flared: false) + 0.5f), ground.Y - rowsUp);
            float angle = random.NextFloat(0.35f, 0.8f);
            var direction = new Vector2(side * MathF.Cos(angle), -MathF.Sin(angle));
            int length = random.Next(BranchLengthMin, BranchLengthMax + 1);

            for (int step = 0; step < length; step++)
            {
                PlaceDisc(position, step < 2 ? 1f : 0.5f, wood, replace: false);
                position += direction;
            }

            PlaceCluster(position, random.NextFloat(3f, 5f));
        }

        var center = new Vector2(CenterAt(height), crown + (crownBottom - crown) * 0.45f);
        var radii = new Vector2(crownHalfWidth, (crownBottom - crown) * 0.5f + 2f);
        int clusters = random.Next(CrownClustersMin, CrownClustersMax + 1);

        for (int i = 0; i < clusters; i++)
            PlaceCluster(center + random.NextVector2Circular(radii.X, radii.Y) * 0.8f, random.NextFloat(CrownClusterRadiusMin, CrownClusterRadiusMax));

        PlaceCluster(new Vector2(CenterAt(height), crown + 2), 4f);
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
    /// Clears the trunk's core from <see cref="HollowFraction"/> of the way up down to <paramref name="roomTop"/>, walls it,
    /// lays platforms across it, and opens a doorway at the ground on the <paramref name="door"/> side.
    /// </summary>
    private void HollowTrunk(int roomTop, int door)
    {
        int hollowTop = ground.Y - (int)(height * HollowFraction);

        for (int y = hollowTop; y < roomTop; y++)
        {
            int rowsUp = ground.Y - y;
            float center = CenterAt(rowsUp);
            int inner = Math.Max(1, (int)(HalfWidthAt(rowsUp, flared: false) - WallThickness + 0.5f));
            int from = (int)MathF.Round(center) - inner;
            int to = (int)MathF.Round(center) + inner;

            // Below ground the shaft is cut through soil, so it gets a wooden lining of its own.
            if (y > ground.Y)
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

        int doorFrom = ground.X + door * Math.Max(1, (int)(baseHalfWidth - WallThickness + 0.5f));
        int doorTo = ground.X + door * ((int)MathF.Ceiling(HalfWidthAt(0)) + 1);

        for (int x = Math.Min(doorFrom, doorTo); x <= Math.Max(doorFrom, doorTo); x++)
        {
            for (int y = ground.Y - DoorRows; y < ground.Y; y++)
                Main.tile[x, y].ClearTile();
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
        int center = (int)MathF.Round(CenterAt(ground.Y - y));
        int inner = Math.Max(1, (int)(HalfWidthAt(ground.Y - y, flared: false) - WallThickness + 0.5f));

        for (int x = center - inner; x <= center + inner; x++)
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
                    PlaceLeaf(x, y);
            }
        }
    }

    private void PlaceDisc(Vector2 center, float radius, ushort type, bool replace)
    {
        int reach = (int)MathF.Ceiling(radius);

        for (int x = (int)MathF.Round(center.X) - reach; x <= (int)MathF.Round(center.X) + reach; x++)
        {
            for (int y = (int)MathF.Round(center.Y) - reach; y <= (int)MathF.Round(center.Y) + reach; y++)
            {
                if (Vector2.DistanceSquared(new Vector2(x, y), center) > radius * radius + 0.25f)
                    continue;

                if (replace || !Main.tile[x, y].HasTile)
                    Place(x, y, type);
            }
        }
    }

    private void PlaceWood(int x, int y) => Place(x, y, wood);

    private void PlaceLeaf(int x, int y)
    {
        if (!Main.tile[x, y].HasTile)
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
