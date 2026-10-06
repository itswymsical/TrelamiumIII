using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics.Shockwaves;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     A render-only copy of a patch of terrain: block types on a small grid, framed as if the patch stood on its own.
///     Sampling never modifies the world.
/// </summary>
internal sealed class TileCluster
{
    public const int TILE = 16;

    /// <summary>Marks an empty cell. Zero is dirt, so it cannot be the sentinel.</summary>
    public const ushort EMPTY = ushort.MaxValue;

    /// <summary>Fraction of the disc that must hold real blocks for the sample to be kept as it is.</summary>
    private const float MIN_FILL = 0.4f;

    /// <summary>How far in from the radius a rim cell may be cut, in tiles.</summary>
    private const float RIM_JAG = 0.9f;

    /// <summary>Lighting floor, so a cluster conjured in the dark is still readable.</summary>
    private const float MIN_LIGHT = 0.35f;

    /// <summary>
    ///     Local-space origin for <see cref="BlockWave.LiftAt" />. A tile centre, so a wave evaluated around it quantises
    ///     to this cluster's own cells rather than the world grid.
    /// </summary>
    public static readonly Vector2 WaveOrigin = new(2056f);

    /// <summary>Vanilla's <c>Framing.selfFrame8WayLookup</c> (neighbour mask → three frame variants), read on first use.</summary>
    private static Point16[][]? frame8WayLookup;

    private readonly ushort[] types;
    private readonly Point[] frames;

    public int Width { get; }

    public int Height { get; }

    public int Seed { get; }

    /// <summary>World tile the sample's cell (0, 0) was taken from.</summary>
    public Point Corner { get; }

    /// <summary>Filled cells.</summary>
    public int Count { get; }

    private TileCluster(int width, int height, ushort[] types, int seed, Point corner)
    {
        Width = width;
        Height = height;
        Seed = seed;
        Corner = corner;

        this.types = types;
        frames = new Point[types.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!Filled(x, y))
                    continue;

                Count++;

                ushort type = TypeAt(x, y);
                int variant = (int)(Hash01(seed + 7, x, y) * 3f);

                Point frame = TileID.Sets.GemsparkFramingTypes[type] != 0
                    ? Frame8Way(x, y, variant)
                    : Frame(
                        Filled(x, y - 1), Filled(x, y + 1), Filled(x - 1, y), Filled(x + 1, y),
                        Filled(x - 1, y - 1), Filled(x + 1, y - 1), Filled(x - 1, y + 1), Filled(x + 1, y + 1),
                        variant
                    );

                frames[y * width + x] = WithSheetOffset(frame, type, corner.X + x, corner.Y + y);
            }
        }
    }

    public bool Filled(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && types[y * Width + x] != EMPTY;

    public ushort TypeAt(int x, int y) => types[y * Width + x];

    public Point FrameAt(int x, int y) => frames[y * Width + x];

    /// <summary>Unrotated offset of cell (x, y)'s centre from the cluster's centre, in pixels.</summary>
    public Vector2 Offset(int x, int y) => new((x + 0.5f - Width * 0.5f) * TILE, (y + 0.5f - Height * 0.5f) * TILE);

    /// <summary>
    ///     Copies the blocks inside a jagged disc of <paramref name="radius" /> tiles around <paramref name="centre" />.
    ///     Air stays empty, so a disc centred on the surface keeps the surface as its top. A mostly empty disc is filled
    ///     solid with the commonest block it found, or dirt.
    /// </summary>
    public static TileCluster Sample(Point centre, float radius, int seed)
    {
        int half = (int)MathF.Ceiling(radius);
        int size = half * 2 + 1;

        var types = new ushort[size * size];
        var tally = new Dictionary<ushort, int>();

        int inside = 0;
        int filled = 0;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;

                types[index] = EMPTY;

                if (!InDisc(x - half, y - half, radius, seed))
                    continue;

                inside++;

                if (!TryGetBlock(centre.X + x - half, centre.Y + y - half, out ushort type))
                    continue;

                types[index] = type;
                filled++;

                tally[type] = tally.GetValueOrDefault(type) + 1;
            }
        }

        if (filled < inside * MIN_FILL)
            FillDisc(types, size, radius, seed, Commonest(tally));

        return new TileCluster(size, size, types, seed, new Point(centre.X - half, centre.Y - half));
    }

    /// <summary>Frame of an isolated block, for debris that is never part of a cluster.</summary>
    public static Point LooseFrame(int variant) => new(162 + Math.Clamp(variant, 0, 2) * 18, 54);

    /// <summary>
    ///     Draws every cell around <paramref name="centre" />. <paramref name="wobble" /> is evaluated around
    ///     <see cref="WaveOrigin" /> and lifts each cell by its own amount.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Vector2 centre, float rotation, float opacity = 1f, BlockWave? wobble = null)
    {
        (float sin, float cos) = MathF.SinCos(rotation);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!Filled(x, y) || !TryGetTexture(TypeAt(x, y), out Texture2D texture))
                    continue;

                Vector2 local = Offset(x, y);

                if (wobble is not null)
                    local.Y += wobble.LiftAt(WaveOrigin + local);

                Vector2 world = centre + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
                Point frame = FrameAt(x, y);

                spriteBatch.Draw(
                    texture,
                    world - Main.screenPosition,
                    new Rectangle(frame.X, frame.Y, TILE, TILE),
                    LightAt(world) * opacity,
                    rotation,
                    new Vector2(TILE * 0.5f),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    /// <summary>World lighting with a floor, so hostile debris never goes fully dark.</summary>
    public static Color LightAt(Vector2 world)
    {
        Vector3 light = Lighting.GetColor(world.ToTileCoordinates()).ToVector3();

        return new Color(Vector3.Max(light, new Vector3(MIN_LIGHT)));
    }

    /// <summary>The tile's sprite sheet, loaded on demand. False for a type with no sheet.</summary>
    public static bool TryGetTexture(ushort type, out Texture2D texture)
    {
        texture = null!;

        if (type >= TileLoader.TileCount)
            return false;

        Main.instance.LoadTiles(type);

        if (TextureAssets.Tile[type] is not { Value: { } sheet })
            return false;

        texture = sheet;

        return true;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write((byte)Width);
        writer.Write((byte)Height);
        writer.Write(Seed);
        writer.Write(Corner.X);
        writer.Write(Corner.Y);

        foreach (ushort type in types)
            writer.Write(type);
    }

    public static TileCluster Read(BinaryReader reader)
    {
        int width = reader.ReadByte();
        int height = reader.ReadByte();
        int seed = reader.ReadInt32();
        var corner = new Point(reader.ReadInt32(), reader.ReadInt32());

        var types = new ushort[width * height];

        for (int i = 0; i < types.Length; i++)
            types[i] = reader.ReadUInt16();

        return new TileCluster(width, height, types, seed, corner);
    }

    private static bool InDisc(int dx, int dy, float radius, int seed)
    {
        float rim = radius - Hash01(seed, dx, dy) * RIM_JAG;

        return dx * dx + dy * dy <= rim * rim;
    }

    private static void FillDisc(ushort[] types, int size, float radius, int seed, ushort type)
    {
        int half = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (InDisc(x - half, y - half, radius, seed))
                    types[y * size + x] = type;
            }
        }
    }

    private static ushort Commonest(Dictionary<ushort, int> tally)
    {
        ushort best = TileID.Dirt;
        int most = 0;

        foreach ((ushort type, int count) in tally)
        {
            if (count <= most)
                continue;

            best = type;
            most = count;
        }

        return best;
    }

    /// <summary>Solid, full, unframed blocks only: platforms and furniture have no standard sheet layout.</summary>
    private static bool TryGetBlock(int i, int j, out ushort type)
    {
        type = EMPTY;

        if (!WorldGen.InWorld(i, j, 10))
            return false;

        Tile tile = Framing.GetTileSafely(i, j);

        if (!tile.HasUnactuatedTile)
            return false;

        ushort found = tile.TileType;

        if (!Main.tileSolid[found] || Main.tileSolidTop[found] || Main.tileFrameImportant[found])
            return false;

        type = found;

        return true;
    }

    /// <summary>
    ///     Vanilla's plain block framing from <c>WorldGen.TileFrame</c>, with every filled cell counted as a neighbour so
    ///     mixed materials meet without seams. <paramref name="variant" /> is 0..2.
    /// </summary>
    private static Point Frame(bool up, bool down, bool left, bool right, bool upLeft, bool upRight, bool downLeft, bool downRight, int variant)
    {
        int v = Math.Clamp(variant, 0, 2) * 18;

        return (up, down, left, right) switch
        {
            (true, true, true, true) when !upLeft && !upRight => new Point(108 + v, 18),
            (true, true, true, true) when !downLeft && !downRight => new Point(108 + v, 36),
            (true, true, true, true) when !upLeft && !downLeft => new Point(180, v),
            (true, true, true, true) when !upRight && !downRight => new Point(198, v),
            (true, true, true, true) => new Point(18 + v, 18),

            (false, true, true, true) => new Point(18 + v, 0),
            (true, false, true, true) => new Point(18 + v, 36),
            (true, true, false, true) => new Point(0, v),
            (true, true, true, false) => new Point(72, v),

            (false, true, false, true) => new Point(v * 2, 54),
            (false, true, true, false) => new Point(18 + v * 2, 54),
            (true, false, false, true) => new Point(v * 2, 72),
            (true, false, true, false) => new Point(18 + v * 2, 72),

            (true, true, false, false) => new Point(90, v),
            (false, false, true, true) => new Point(108 + v, 72),

            (false, true, false, false) => new Point(108 + v, 0),
            (true, false, false, false) => new Point(108 + v, 54),
            (false, false, false, true) => new Point(162, v),
            (false, false, true, false) => new Point(216, v),

            _ => LooseFrame(variant),
        };
    }

    /// <summary>
    ///     <c>Framing.SelfFrame8Way</c> for gemspark-style sheets. Only cells that <see cref="Blends" /> count as
    ///     neighbours, and a corner only when both of its adjacent sides do.
    /// </summary>
    private Point Frame8Way(int x, int y, int variant)
    {
        ushort type = TypeAt(x, y);

        bool up = Blends(type, x, y - 1);
        bool down = Blends(type, x, y + 1);
        bool left = Blends(type, x - 1, y);
        bool right = Blends(type, x + 1, y);

        int mask = (up ? 1 : 0) | (left ? 2 : 0) | (right ? 4 : 0) | (down ? 8 : 0);

        if (up && left && Blends(type, x - 1, y - 1))
            mask |= 0x10;

        if (up && right && Blends(type, x + 1, y - 1))
            mask |= 0x20;

        if (down && left && Blends(type, x - 1, y + 1))
            mask |= 0x40;

        if (down && right && Blends(type, x + 1, y + 1))
            mask |= 0x80;

        frame8WayLookup ??= (Point16[][])typeof(Framing).GetField("selfFrame8WayLookup", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Point16 frame = frame8WayLookup[mask][Math.Clamp(variant, 0, 2)];

        return new Point(frame.X, frame.Y);
    }

    /// <summary><c>Framing.WillItBlend</c> against cell (x, y).</summary>
    private bool Blends(ushort type, int x, int y)
    {
        if (!Filled(x, y))
            return false;

        ushort other = TypeAt(x, y);

        return TileID.Sets.ForcedDirtMerging[type] && other == TileID.Dirt
               || Main.tileBrick[type] && Main.tileBrick[other]
               || TileID.Sets.GemsparkFramingTypes[other] == TileID.Sets.GemsparkFramingTypes[type];
    }

    /// <summary>Adds the sheet offset a modded tile draws with at world tile (i, j), e.g. from <c>AnimateIndividualTile</c>.</summary>
    private static Point WithSheetOffset(Point frame, ushort type, int i, int j)
    {
        int offsetX = 0;
        int offsetY = 0;

        TileLoader.SetAnimationFrame(type, i, j, ref offsetX, ref offsetY);

        return new Point(frame.X + offsetX, frame.Y + offsetY);
    }

    /// <summary>Deterministic 0..1 hash of a cell, so every client cuts and frames the same cluster.</summary>
    public static float Hash01(int seed, int x, int y)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;

            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;

            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
