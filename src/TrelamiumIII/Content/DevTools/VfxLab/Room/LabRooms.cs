using SubworldLibrary;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.VfxLab.Room;

/// <summary> The lab's wings, one for each tab of the panels. </summary>
internal enum LabRoom
{
    Fields,
    Tiles,
    Shaders,
}

/// <summary>
///     The rooms of the <see cref="VfxLabRealm" />: where they are, what's built in them, how bright they are, and moving between them.
/// </summary>
internal static class LabRooms
{
    public const int WorldWidth = 620;
    public const int WorldHeight = 230;

    private const int RoomWidth = 118;
    private const int RoomHeight = 46;
    private const int RoomGap = 44;
    private const int MarginX = 60;
    private const int MarginY = 90;

    /// <summary> Ticks to fade out, and again to fade back in. Slow enough to read as moving, with no flash. </summary>
    private const int FadeTicks = 14;

    /// <summary> How much light the walls around a room get, so the shell doesn't compete with what's in the room. </summary>
    private const float ShellLight = 0.08f;

    private static LabRoom? destination;
    private static int fadeTick;
    private static bool arriving;

    public static bool InLab => SubworldSystem.IsActive<VfxLabRealm>();

    /// <summary> 0 is dark and 1 is full light. Applies from the next lighting pass. </summary>
    public static float LightLevel { get; set; } = 1f;

    /// <summary> How far the screen has gone to black on the way between rooms. </summary>
    public static float Fade => destination is null && !arriving ? 0f : MathHelper.Clamp(fadeTick / (float)FadeTicks, 0f, 1f);

    /// <summary> On the way out of one room, before arriving in the next. </summary>
    public static bool Leaving => destination is not null;

    /// <summary> The room the player is in, by which wing they're nearest. </summary>
    public static LabRoom Current
    {
        get
        {
            int x = (int)(Main.LocalPlayer.Center.X / 16f);
            int index = (x - MarginX + RoomGap / 2) / (RoomWidth + RoomGap);

            return (LabRoom)Math.Clamp(index, 0, 2);
        }
    }

    /// <summary> The open space of a room, in tiles. </summary>
    public static Rectangle AreaOf(LabRoom room)
        => new(MarginX + (int)room * (RoomWidth + RoomGap), MarginY, RoomWidth, RoomHeight);

    /// <summary> Where the player stands on arriving, in tiles: on the floor, out in the open. </summary>
    public static Point ArrivalOf(LabRoom room)
    {
        Rectangle area = AreaOf(room);

        return new Point(area.X + 34, area.Bottom);
    }

    public static Vector3 LightAt(int x, int y)
    {
        foreach (LabRoom room in Enum.GetValues<LabRoom>())
        {
            Rectangle area = AreaOf(room);
            area.Inflate(2, 2);

            if (area.Contains(x, y))
                return new Vector3(LightLevel);
        }

        return new Vector3(ShellLight);
    }

    /// <summary> Starts the walk to another room: out to black, across, and back in. Does nothing mid-move or from outside the lab. </summary>
    public static void Go(LabRoom room)
    {
        if (!InLab || destination is not null || arriving)
            return;

        destination = room;
        fadeTick = 0;
    }

    public static void Update()
    {
        if (arriving)
        {
            if (--fadeTick <= 0)
                arriving = false;

            return;
        }

        if (destination is not { } room)
            return;

        if (++fadeTick < FadeTicks)
            return;

        MoveTo(room);
        destination = null;
        arriving = true;
    }

    public static void Reset()
    {
        destination = null;
        arriving = false;
        fadeTick = 0;
    }

    public static void MoveTo(LabRoom room)
    {
        Point arrival = ArrivalOf(room);
        Player player = Main.LocalPlayer;

        player.Bottom = arrival.ToWorldCoordinates(8f, 0f);
        player.velocity = Vector2.Zero;
        player.fallStart = (int)(player.position.Y / 16f);
    }

    /// <summary> Run by the realm's generation pass. </summary>
    public static void Build()
    {
        Main.worldSurface = 20;
        Main.rockLayer = 40;

        Point spawn = ArrivalOf(LabRoom.Fields);
        Main.spawnTileX = spawn.X;
        Main.spawnTileY = spawn.Y;

        for (int x = 0; x < WorldWidth; x++)
        {
            for (int y = 0; y < WorldHeight; y++)
                Solid(x, y, TileID.GrayBrick);
        }

        BuildFieldsRoom(AreaOf(LabRoom.Fields));
        BuildTilesRoom(AreaOf(LabRoom.Tiles));
        BuildShadersRoom(AreaOf(LabRoom.Shaders));

        StructureWorld.Frame(new Rectangle(1, 1, WorldWidth - 2, WorldHeight - 2));
    }

    /// <summary>
    ///     Everything a field can do something to: an open floor for it to pool on, water and lava to touch, a ledge to collect under, a
    ///     thin wall that shouldn't leak, and a pillar to flow around.
    /// </summary>
    private static void BuildFieldsRoom(Rectangle area)
    {
        Hollow(area);

        int floor = area.Bottom;

        Pool(area.X + 8, floor, 14, 5, LiquidID.Water);
        Pool(area.Right - 22, floor, 14, 5, LiquidID.Lava);

        // A ledge to pool under and spill off.
        Block(area.X + 48, floor - 13, 22, 2, TileID.GrayBrick);

        // A wall thin enough that a careless field would leak through it.
        Block(area.X + 80, floor - 16, 1, 16, TileID.GrayBrick);

        // A pillar down from the ceiling.
        Block(area.X + 88, area.Y, 3, area.Height - 20, TileID.GrayBrick);
    }

    /// <summary> A strip of every common surface, with ledges and steps, so a condition can be judged on what it'll really land on and from every side. </summary>
    private static void BuildTilesRoom(Rectangle area)
    {
        Hollow(area);

        int floor = area.Bottom;

        (ushort Type, int Width)[] strips =
        [
            (TileID.Grass, 18),
            (TileID.Stone, 16),
            (TileID.Mud, 14),
            (TileID.Sand, 14),
            (TileID.SnowBlock, 14),
            (TileID.IceBlock, 14),
            (TileID.WoodBlock, 14),
            (TileID.GrayBrick, 14),
        ];

        int x = area.X;

        foreach ((ushort type, int width) in strips)
        {
            for (int column = x; column < Math.Min(x + width, area.Right); column++)
            {
                for (int depth = 0; depth < 4; depth++)
                    Solid(column, floor + depth, type == TileID.Grass && depth > 0 ? TileID.Dirt : type);
            }

            x += width;
        }

        // Undersides and side faces, for how a coat drips and clings.
        Block(area.X + 8, floor - 12, 26, 3, TileID.Stone);
        Block(area.X + 52, floor - 12, 26, 3, TileID.Mud);
        Block(area.X + 92, floor - 12, 18, 3, TileID.SnowBlock);

        // Steps up to a wall.
        for (int step = 0; step < 4; step++)
            Block(area.Right - 4 - step * 2, floor - step - 1, 4 + step * 2, 1, TileID.GrayBrick);

        // A shallow pool for what a condition does at the waterline.
        Pool(area.X + 40, floor, 8, 3, LiquidID.Water);
    }

    /// <summary> A plain dark gallery with nothing to get in the way of the preview plate. </summary>
    private static void BuildShadersRoom(Rectangle area) => Hollow(area);

    /// <summary> Dark, smooth walls in every room: a backdrop that's quiet enough to judge a colour against, and gets out of the way. </summary>
    private static void Hollow(Rectangle area)
    {
        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                tile.HasTile = false;
                tile.WallType = WallID.StoneSlab;
                tile.WallColor = PaintID.BlackPaint;
            }
        }
    }

    private static void Pool(int x, int floor, int width, int depth, int liquid)
    {
        for (int column = x; column < x + width; column++)
        {
            for (int y = floor; y < floor + depth; y++)
            {
                Tile tile = Main.tile[column, y];

                tile.HasTile = false;
                tile.LiquidType = liquid;
                tile.LiquidAmount = 255;
                tile.WallType = WallID.StoneSlab;
                tile.WallColor = PaintID.BlackPaint;
            }
        }
    }

    private static void Block(int x, int y, int width, int height, ushort type)
    {
        for (int column = x; column < x + width; column++)
        {
            for (int row = y; row < y + height; row++)
                Solid(column, row, type);
        }
    }

    private static void Solid(int x, int y, ushort type)
    {
        Tile tile = Main.tile[x, y];

        tile.HasTile = true;
        tile.TileType = type;
    }
}

/// <summary> Nothing in the lab should stop a test: no damage, air or footing to worry about. Debuffs still land, as those may be the test. </summary>
internal sealed class VfxLabPlayer : ModPlayer
{
    public override void PostUpdateEquips()
    {
        if (!LabRooms.InLab)
            return;

        Player.gills = true;
        Player.lavaImmune = true;
        Player.noFallDmg = true;
        Player.statLife = Player.statLifeMax2;
    }

    public override bool FreeDodge(Player.HurtInfo info) => LabRooms.InLab;
}
