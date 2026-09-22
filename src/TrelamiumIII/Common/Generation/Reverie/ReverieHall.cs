using Terraria.DataStructures;
using Terraria.Utilities;
using TrelamiumIII.Content.Reverie;

namespace TrelamiumIII.Common.Generation.Reverie;

/// <summary>
/// Reverie's library hall: a row of pointed stone arches on pillars over a marble floor. Bays are backed by bookshelf
/// walls with stained glass in the arch heads, shelved blocks fill the spandrels, and walkways of books cross the bays
/// with ropes up to them. The first bay holds the front desk. Everything outside the hall is solid stone.
/// </summary>
public sealed class ReverieHall
{
    public const int PillarWidth = 8;
    public const int BayWidth = 48;
    public const int BayCount = 13;
    public const int Pitch = BayWidth + PillarWidth;
    public const int Width = BayCount * Pitch + PillarWidth;

    /// <summary> Floor to the row where the arches spring. </summary>
    public const int SpringHeight = 84;

    /// <summary> Arc radius as a fraction of the bay width; above half, the arch is pointed. </summary>
    private const float ArchRadiusRatio = 0.8f;

    private const int RingThickness = 3;
    private const int VaultThickness = 12;
    private const int CapitalHeight = 3;
    private const int MarbleDepth = 2;

    private const int MullionSpacing = 6;
    private const int PaneBandHeight = 8;

    private const int TierSpacing = 18;
    private const int TierCount = 4;
    private const float TierChance = 0.55f;

    /// <summary> Chance a walkway is a rack: shelves stacked above it, books on every level. </summary>
    private const float RackChance = 0.45f;

    private const int RackLevelsMin = 3;
    private const int RackLevelsMax = 4;
    private const int RackSpacingMin = 3;
    private const int RackSpacingMax = 4;

    /// <summary> Walkways reach this far past the bay into the pillar zone, so neighbouring ones join. </summary>
    private const int TierOverhang = PillarWidth / 2;

    private const int RopeInset = 3;
    private const int BookRunMin = 3;
    private const int BookRunMax = 7;
    private const int BookGapMin = 2;
    private const int BookGapMax = 6;
    private const float CandleChance = 0.3f;

    /// <summary> <see cref="TileID.Books"/> styles 0-4 are plain books; 5 is the Water Bolt. </summary>
    private const int BookStyles = 5;

    /// <summary> <see cref="TileID.Platforms"/> style of the rich mahogany platform. </summary>
    private const int PlatformStyle = 2;

    /// <summary> <see cref="TileID.Chandeliers"/> style of the gold chandelier. </summary>
    private const int ChandelierStyle = 2;

    /// <summary> Rows below the arch's apex where the chandelier hangs, so its three columns fit inside the arch. </summary>
    private const int ChandelierDrop = 3;

    private const int DeskInset = 8;
    private const int DeskWidth = 14;
    private const int DeskHeight = 4;
    private const int SpawnPastDesk = 10;

    /// <summary> Columns into the first bay, and world pixels above the floor, of the scar's centre. </summary>
    private const int ScarBayX = 40;

    private const float ScarLift = 118f;

    private static readonly ushort[] PaneWalls = [WallID.BlueStainedGlass, WallID.PurpleStainedGlass, WallID.YellowStainedGlass];

    private ReverieHall(int left, int floorY)
    {
        Left = left;
        FloorY = floorY;
    }

    /// <summary> Left column of the hall, its first pillar included. </summary>
    public int Left { get; }

    /// <summary> Top row of the floor. </summary>
    public int FloorY { get; }

    public int Right => Left + Width;

    public int SpringY => FloorY - SpringHeight;

    private static float ArchRadius => BayWidth * ArchRadiusRatio;

    private static int ArchRise => (int)MathF.Ceiling(MathF.Sqrt(ArchRadius * ArchRadius - MathF.Pow(ArchRadius - BayWidth * 0.5f, 2f)));

    /// <summary> Top row of the hall's stone; everything from here to the floor is the hall's height. </summary>
    public int VaultY => SpringY - ArchRise - RingThickness - VaultThickness;

    public Rectangle Bounds => new(Left, VaultY, Width, FloorY - VaultY);

    /// <summary> Glass walls, which the realm lights as windows. </summary>
    public static bool IsPane(ushort wall) => Array.IndexOf(PaneWalls, wall) >= 0;

    public int BayLeft(int bay) => Left + PillarWidth + bay * Pitch;

    /// <summary> The desk's top row; Edith stands on it at <see cref="DeskCenterX"/>. </summary>
    public int DeskTop => FloorY - DeskHeight;

    public int DeskCenterX => BayLeft(0) + DeskInset + DeskWidth / 2;

    /// <summary> Ground tile for the player's spawn, a little right of the desk. </summary>
    public Point Spawn => new(BayLeft(0) + DeskInset + DeskWidth + SpawnPastDesk, FloorY);

    /// <summary> World position of the scar back to Ingress, past the spawn, its lower tip a couple of tiles off the floor. </summary>
    public Vector2 ScarCenter => new((BayLeft(0) + ScarBayX) * 16f, FloorY * 16f - ScarLift);

    public static ReverieHall Create(int left, int floorY) => new(left, floorY);

    public void Build(UnifiedRandom random)
    {
        ushort stone = TileID.SandstoneBrick;
        ushort shelves = (ushort)ModContent.TileType<LibrarianBlockTile>();
        ushort shelfWall = (ushort)ModContent.WallType<LibrarianWall>();

        for (int x = 0; x < Main.maxTilesX; x++)
        {
            for (int y = 0; y < Main.maxTilesY; y++)
                Carve(x, y, stone, shelves, shelfWall);
        }

        for (int bay = 1; bay < BayCount; bay++)
            BuildWalkways(bay, random);

        for (int bay = 0; bay < BayCount; bay++)
            HangChandelier(bay, stone);

        BuildDesk();
    }

    private void Carve(int x, int y, ushort stone, ushort shelves, ushort shelfWall)
    {
        Tile tile = Main.tile[x, y];
        tile.ClearEverything();

        bool inHall = x >= Left + PillarWidth && x < Right - PillarWidth && y >= VaultY && y < FloorY;

        if (!inHall)
        {
            tile.HasTile = true;
            tile.TileType = y >= FloorY && y < FloorY + MarbleDepth && x >= Left && x < Right ? TileID.MarbleBlock : stone;
            tile.WallType = WallID.SandstoneBrick;
            return;
        }

        int bay = (x - Left - PillarWidth) / Pitch;
        int bayX = x - BayLeft(bay);
        bool pillar = bayX >= BayWidth;

        if (y >= SpringY)
        {
            if (pillar && y < SpringY + CapitalHeight)
            {
                tile.HasTile = true;
                tile.TileType = stone;
            }

            tile.WallType = pillar ? WallID.SandstoneBrick : shelfWall;
            return;
        }

        tile.WallType = WallID.SandstoneBrick;

        if (pillar)
        {
            tile.HasTile = true;
            tile.TileType = stone;
            return;
        }

        switch (ArchCell(bayX, y))
        {
            case ArchPart.Opening:
                tile.WallType = PaneWall(bayX, y);
                break;

            case ArchPart.Ring:
                tile.HasTile = true;
                tile.TileType = stone;
                break;

            default:
                tile.HasTile = true;
                tile.TileType = shelves;
                break;
        }
    }

    private enum ArchPart
    {
        Opening,
        Ring,
        Spandrel
    }

    /// <summary> A pointed arch: two arcs through the springing points, each centred <see cref="ArchRadius"/> in from its own side. </summary>
    private ArchPart ArchCell(int bayX, int y)
    {
        float radius = ArchRadius;
        float dy = y + 0.5f - SpringY;
        float fromLeft = bayX + 0.5f - radius;
        float fromRight = bayX + 0.5f - (BayWidth - radius);
        float left = fromLeft * fromLeft + dy * dy;
        float right = fromRight * fromRight + dy * dy;

        if (left < radius * radius && right < radius * radius)
            return ArchPart.Opening;

        float ring = (radius + RingThickness) * (radius + RingThickness);

        return left < ring && right < ring ? ArchPart.Ring : ArchPart.Spandrel;
    }

    /// <summary> Glass panes in bands of colour between stone mullions. </summary>
    private ushort PaneWall(int bayX, int y)
    {
        if (bayX % MullionSpacing == 0 || y == SpringY - 1)
            return WallID.SandstoneBrick;

        int band = (SpringY - y) / PaneBandHeight + bayX / MullionSpacing;

        return PaneWalls[band % PaneWalls.Length];
    }

    /// <summary>
    /// A random set of walkway heights across the bay, some stacked into racks, a rope from the floor to the highest
    /// shelf, and runs of books with the odd candle along every shelf.
    /// </summary>
    private void BuildWalkways(int bay, UnifiedRandom random)
    {
        int left = BayLeft(bay) - TierOverhang;
        int right = BayLeft(bay) + BayWidth + TierOverhang;
        int ropeX = bay % 2 == 0 ? BayLeft(bay) + RopeInset : BayLeft(bay) + BayWidth - 1 - RopeInset;
        int highest = -1;

        for (int tier = 1; tier <= TierCount; tier++)
        {
            if (random.NextFloat() >= TierChance)
                continue;

            // Racks stay off the top tier, where they would crowd the capitals.
            int levels = tier < TierCount && random.NextFloat() < RackChance ? random.Next(RackLevelsMin, RackLevelsMax + 1) : 1;
            int spacing = random.Next(RackSpacingMin, RackSpacingMax + 1);

            for (int level = 0; level < levels; level++)
            {
                int y = FloorY - tier * TierSpacing - level * spacing;
                highest = y;

                for (int x = left; x < right; x++)
                {
                    if (x != ropeX)
                        SetPlatform(x, y);
                }

                PlaceBooks(left, right, y - 1, ropeX, random);
            }
        }

        if (highest < 0)
            return;

        for (int y = highest - 2; y < FloorY; y++)
        {
            Tile tile = Main.tile[ropeX, y];

            if (tile.HasTile)
                continue;

            tile.HasTile = true;
            tile.TileType = TileID.Rope;
        }
    }

    private static void SetPlatform(int x, int y)
    {
        Tile tile = Main.tile[x, y];

        if (tile.HasTile)
            return;

        tile.HasTile = true;
        tile.TileType = TileID.Platforms;
        tile.TileFrameX = 0;
        tile.TileFrameY = PlatformStyle * 18;
    }

    private static void PlaceBooks(int left, int right, int y, int ropeX, UnifiedRandom random)
    {
        int x = left + random.Next(BookGapMin, BookGapMax + 1);

        while (x < right)
        {
            int run = random.Next(BookRunMin, BookRunMax + 1);

            for (int k = 0; k < run && x < right; k++, x++)
            {
                if (Math.Abs(x - ropeX) <= 1 || Main.tile[x, y].HasTile)
                    continue;

                Tile book = Main.tile[x, y];
                book.HasTile = true;
                book.TileType = TileID.Books;
                book.TileFrameX = (short)(random.Next(BookStyles) * 18);
                book.TileFrameY = 0;
            }

            if (x < right && Math.Abs(x - ropeX) > 1 && random.NextFloat() < CandleChance)
                WorldGen.PlaceTile(x, y, TileID.Candles, mute: true);

            x += random.Next(BookGapMin, BookGapMax + 1);
        }
    }

    /// <summary> A stone stem down from the arch's apex with a gold chandelier on it. </summary>
    private void HangChandelier(int bay, ushort stone)
    {
        int centerX = BayLeft(bay) + BayWidth / 2;
        int apex = SpringY - 1;

        while (apex > VaultY && !Main.tile[centerX, apex - 1].HasTile)
            apex--;

        int anchorY = apex + ChandelierDrop;

        for (int y = apex; y < anchorY; y++)
        {
            Tile tile = Main.tile[centerX, y];
            tile.HasTile = true;
            tile.TileType = stone;
        }

        WorldGen.PlaceObject(centerX, anchorY, TileID.Chandeliers, mute: true, style: ChandelierStyle);
    }

    /// <summary> A solid mahogany counter with books and a candle along its top. </summary>
    private void BuildDesk()
    {
        int left = BayLeft(0) + DeskInset;

        for (int x = left; x < left + DeskWidth; x++)
        {
            for (int y = DeskTop; y < FloorY; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.HasTile = true;
                tile.TileType = TileID.RichMahogany;
            }
        }

        for (int x = left + 1; x < left + 4; x++)
        {
            Tile book = Main.tile[x, DeskTop - 1];
            book.HasTile = true;
            book.TileType = TileID.Books;
            book.TileFrameX = (short)((x - left) % BookStyles * 18);
            book.TileFrameY = 0;
        }

        WorldGen.PlaceTile(left + DeskWidth - 2, DeskTop - 1, TileID.Candles, mute: true);
    }

    /// <summary> Spawns Edith standing on the desk. Generation only. </summary>
    public void PlaceEdith() => NPC.NewNPC(new EntitySource_WorldGen(), DeskCenterX * 16 + 8, DeskTop * 16, ModContent.NPCType<Edith>());
}
