using Terraria.GameContent;
using Terraria.ObjectData;

namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     A placed tile object (i.e., a workbench, a furnace, anvil, et.c) as a free-standing sprite that
///     can be drawn anywhere, rotated and squashed as one piece.
/// </summary>
/// <remarks>
///     base-game only draws these snapped to the tile grid, one 16x16 cell at a time, with the
///     framing baked into the tile's own <c>frameX</c>/<c>frameY</c>. Nothing exists off that grid,
///     so the layout has to be rebuilt from <see cref="TileObjectData" />: where this placement style
///     starts in the sheet, and which cell sits where within it.
///     <para>
///         Worked out once per station and cached, since none of it changes while the world is
///         loaded.
///     </para>
/// </remarks>
public sealed class TileObjectSprite
{
    private const int DEFAULT_CELL = 16;

    /// <summary>Where each cell is in the sheet, before animation is added.</summary>
    private readonly Rectangle[] cells;

    /// <summary>Where each cell sits within the sprite, from its top-left.</summary>
    private readonly Vector2[] offsets;

    /// <summary>Sheet rows one animation frame advances by, or 0 for a tile that does not animate.</summary>
    private readonly int animationFrameHeight;

    public int TileType { get; }

    public int Width { get; }

    public int Height { get; }

    public Vector2 Size => new(Width, Height);

    private TileObjectSprite(int tileType, Rectangle[] cells, Vector2[] offsets, int width, int height, int animationFrameHeight)
    {
        TileType = tileType;
        Width = width;
        Height = height;

        this.cells = cells;
        this.offsets = offsets;
        this.animationFrameHeight = animationFrameHeight;
    }

    /// <summary>Builds the sprite an item would place, or null if it places nothing.</summary>
    public static TileObjectSprite? For(Item item)
        => item.IsAir || item.createTile < TileID.Dirt ? null : For(item.createTile, item.placeStyle);

    public static TileObjectSprite? For(int tileType, int placeStyle)
    {
        if (tileType < TileID.Dirt || tileType >= TileLoader.TileCount)
            return null;

        TileObjectData? data = TileObjectData.GetTileData(tileType, placeStyle);

        // A tile with no object data is a plain 1x1, which is most of them.
        int columns = data?.Width ?? 1;
        int rows = data?.Height ?? 1;
        int cellWidth = data?.CoordinateWidth ?? DEFAULT_CELL;
        int padding = data?.CoordinatePadding ?? 2;
        int[] rowHeights = data?.CoordinateHeights ?? [DEFAULT_CELL];

        Point origin = SheetOrigin(data, placeStyle);

        var cells = new Rectangle[columns * rows];
        var offsets = new Vector2[columns * rows];

        int sheetY = origin.Y;
        int drawY = 0;

        for (int row = 0; row < rows; row++)
        {
            int rowHeight = rowHeights[Math.Min(row, rowHeights.Length - 1)];

            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;

                cells[index] = new Rectangle(origin.X + column * (cellWidth + padding), sheetY, cellWidth, rowHeight);
                offsets[index] = new Vector2(column * cellWidth, drawY);
            }

            // The sheet steps by the padded height; the sprite steps by the bare one, so the object
            // comes back together without the gutters the sheet keeps between objects.
            sheetY += rowHeight + padding;
            drawY += rowHeight;
        }

        int animation = TileLoader.GetTile(tileType)?.AnimationFrameHeight ?? 0;

        return new TileObjectSprite(tileType, cells, offsets, columns * cellWidth, drawY, animation);
    }

    /// <summary>Top-left of this placement style within the tile's sheet.</summary>
    private static Point SheetOrigin(TileObjectData? data, int placeStyle)
    {
        if (data is null)
            return Point.Zero;

        // Styles are not simply indexed: a tile can burn several sheet slots per style, and wraps
        // onto a second row or column once it runs out. This is the same arithmetic placement uses.
        int style = data.CalculatePlacementStyle(placeStyle, 0, 0);
        int wrapped = 0;

        if (data.StyleWrapLimit > 0)
        {
            wrapped = style / data.StyleWrapLimit;
            style %= data.StyleWrapLimit;
        }

        return data.StyleHorizontal
            ? new Point(style * data.CoordinateFullWidth, wrapped * data.CoordinateFullHeight)
            : new Point(wrapped * data.CoordinateFullWidth, style * data.CoordinateFullHeight);
    }

    /// <summary>
    ///     Draws the whole object at <paramref name="worldPosition" />, about a shared
    ///     <paramref name="origin" /> measured from the sprite's top-left.
    /// </summary>
    /// <remarks>
    ///     Every cell is drawn about that one origin, so rotation and a non-uniform scale carry the
    ///     object as a single piece instead of breaking it into loose tiles.
    /// </remarks>
    public void Draw(Vector2 worldPosition, Color color, float rotation, Vector2 origin, Vector2 scale)
    {
        // Tile sheets load lazily, and a station nobody has placed this session has not been asked
        // for yet.
        Main.instance.LoadTiles(TileType);

        if (TextureAssets.Tile[TileType] is not { Value: { } texture })
            return;

        int animation = animationFrameHeight * Main.tileFrame[TileType];

        Vector2 screen = worldPosition - Main.screenPosition;

        // When we render, we'll append a quad to the open batch, so every cell comes from the same texture.
        // A 3x2 tile is six quads in one batch, but this isn't much too worry about as most tile stations stay below 9x9. 
        for (int i = 0; i < cells.Length; i++)
        {
            Rectangle source = cells[i];
            source.Y += animation;

            // The obvious approach would be to draw each cell at position + offset with a zero origin, but this falls apart the moment
            // you rotate or squash, because every cell pivots about its own corner and the object shears into loose tiles.
            var cellOrigin = new Vector2(origin.X - offsets[i].X, origin.Y - offsets[i].Y);


            Main.EntitySpriteDraw(texture, screen, source, color, rotation, cellOrigin, scale, SpriteEffects.None, 0);
        }
    }
}