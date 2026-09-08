namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>
///     Every part of a bubble worked out from its content size, in texels.
/// </summary>
/// <remarks>
///     Measuring is split from drawing because the box needs the finished dimensions a frame before
///     anything is rasterised: where the panel sits on screen decides which way the tail leans, and
///     the tail is part of what it is measuring. Sharing one geometry between the two is what keeps
///     the answer to that from being computed twice and drifting.
/// </remarks>
internal readonly struct BubbleGeometry
{
    /// <summary>The panel's face, inside its border.</summary>
    public required Rectangle Body { get; init; }

    /// <summary>Top left of the text area.</summary>
    public required Point ContentOrigin { get; init; }

    /// <summary>The speaker plate, or empty when the line carries no name.</summary>
    public required Rectangle NameTab { get; init; }

    /// <summary>The region of the surface this bubble occupies.</summary>
    public required Point Used { get; init; }

    /// <summary>Centre of the tail's join with the body.</summary>
    public required Vector2 TailBase { get; init; }

    public required Vector2 TailApex { get; init; }
}

/// <summary>
///     The look of a projected bubble: one place for every metric and colour it is made of.
/// </summary>
/// <remarks>
///     <para>
///         Everything here is in texels, and the numbers are small on purpose. The panel is authored
///         at the resolution it will be seen at as chunky pixels and magnified afterwards, so a border
///         is two pixels because two pixels is what it should look like -- not because two divided by
///         some scale factor happens to land near the right thickness.
///     </para>
///     <para>
///         White on black, with the speaker's colour spent only on the offset ghost behind the shape.
///         A bubble that took the speaker's colour over its whole face would compete with the world
///         behind it and stop reading as a projection; a bubble with no trace of them at all makes
///         every character sound the same. The ghost is the cheapest place to put the difference.
///     </para>
/// </remarks>
internal static class PanelStyle
{
    /// <summary>
    ///     Screen pixels per texel, before UI scale. Both the pixelation dial and the size dial --
    ///     the panel is authored once in texels and this is the only thing between that and the
    ///     screen, so raising it makes the box bigger and chunkier together.
    /// </summary>
    /// <remarks>
    ///     Deliberately a float. There is no requirement that a texel land on a whole screen pixel:
    ///     the quad is under a perspective divide by the time it is sampled, so its texels are already
    ///     landing on fractional boundaries and varying in size across the panel. That is what makes
    ///     it read as a pixelated object in the scene rather than a pixelated overlay on the screen.
    /// </remarks>
    public const float PIXEL = 1.75f;

    /// <summary>
    ///     The surface a bubble is drawn into, in texels. Sized once for the largest box this style
    ///     allows; anything smaller is sampled out of the corner of it.
    /// </summary>
    public const int SURFACE_WIDTH = 400;

    public const int SURFACE_HEIGHT = 384;

    /// <summary>One row of the response atlas, in texels. A slot has to fit inside this with its
    /// ghost and border, or the row below would be sampled into the bottom of it.</summary>
    public const int CHOICE_PITCH = 64;

    public const int CHOICE_SURFACE_WIDTH = 288;

    public const int BORDER = 3;

    /// <summary>How far behind, and up-left, the second outline sits.</summary>
    public const int GHOST = 4;

    public const int CHAMFER = 4;

    public const int PAD_X = 10;
    public const int PAD_Y = 7;

    /// <summary>Room reserved at the top and left of the surface for the ghost to hang into.</summary>
    public const int MARGIN = BORDER + GHOST;

    // Stubby rather than long: a narrow tail tapering over a long run reads as a spike stuck to the
    // panel, where a wide one over a short run reads as part of the same shape.
    public const int TAIL_WIDTH = 22;

    public const int TAIL_LENGTH = 19;

    public const int NAME_PAD_X = 7;
    public const int NAME_GAP = 3;

    public static readonly Color Fill = new(10, 10, 12);
    public static readonly Color Border = new(244, 244, 240);
    public static readonly Color Text = new(244, 244, 240);

    /// <summary>Text on a surface that is drawn inverted: the speaker plate, a selected response.</summary>
    public static readonly Color Inverted = new(10, 10, 12);

    /// <summary>
    ///     The tallest text area that still leaves the plate, the tail and the border room on the
    ///     surface. A line long enough to want more than this is clipped rather than allowed to run
    ///     off the target, which would take the tail with it and leave the bubble anchored to nothing.
    /// </summary>
    public static int MaxContentHeight(int nameHeight) =>
        SURFACE_HEIGHT - MARGIN - (nameHeight > 0 ? nameHeight + NAME_GAP : 0)
        - PAD_Y * 2 - TAIL_LENGTH - BORDER * 2;

    /// <param name="contentSize">Text area, in texels.</param>
    /// <param name="nameWidth">Width of the speaker plate, or zero for no plate.</param>
    /// <param name="tailAim">
    ///     Where the tail points along the body's width, -1 hard left through 1 hard right. Its base is
    ///     pinned inside the body while only the apex leans, so a speaker off to one side gets a tail
    ///     angled at them rather than one hanging off a corner.
    /// </param>
    public static BubbleGeometry Measure(Point contentSize, int nameWidth, int nameHeight, float tailAim)
    {
        int plateHeight = nameWidth > 0 ? nameHeight + NAME_GAP : 0;

        Rectangle body = new(
            MARGIN,
            MARGIN + plateHeight,
            contentSize.X + PAD_X * 2,
            contentSize.Y + PAD_Y * 2);

        float aim = Math.Clamp(tailAim, -1f, 1f);
        float centre = body.X + body.Width * 0.5f;

        // Guarded rather than clamped outright: a body narrower than the tail's own join has no room
        // to slide it, and would hand the clamp a minimum above its maximum.
        float lowest = body.X + CHAMFER + TAIL_WIDTH * 0.5f;
        float highest = MathF.Max(body.Right - CHAMFER - TAIL_WIDTH * 0.5f, lowest);
        float baseX = Math.Clamp(centre + aim * body.Width * 0.5f, lowest, highest);

        return new BubbleGeometry
        {
            Body = body,
            ContentOrigin = new Point(body.X + PAD_X, body.Y + PAD_Y),
            NameTab = nameWidth > 0 ? new Rectangle(MARGIN, MARGIN, nameWidth, nameHeight) : Rectangle.Empty,
            TailBase = new Vector2(baseX, body.Bottom),
            TailApex = new Vector2(centre + aim * (body.Width * 0.5f + TAIL_WIDTH), body.Bottom + TAIL_LENGTH),
            Used = new Point(
                MARGIN + Math.Max(body.Width, nameWidth) + BORDER,
                body.Bottom + TAIL_LENGTH + BORDER * 2),
        };
    }

    /// <summary>Draws the bubble's frame. Content and the speaker's name go in on top.</summary>
    public static void DrawBubble(PixelCanvas canvas, in BubbleGeometry geometry, Color ghost)
    {
        Rectangle body = geometry.Body;
        Vector2 join = geometry.TailBase;
        Vector2 point = geometry.TailApex;

        Silhouette(-GHOST, -GHOST, BORDER, ghost);
        Silhouette(0, 0, BORDER, Border);
        Silhouette(0, 0, 0, Fill);

        if (geometry.NameTab != Rectangle.Empty)
        {
            Rectangle tab = geometry.NameTab;

            canvas.ChamferedRect(tab.X - GHOST, tab.Y - GHOST, tab.Width, tab.Height, CHAMFER, ghost);
            canvas.ChamferedRect(tab.X, tab.Y, tab.Width, tab.Height, CHAMFER, Border);
        }

        // Body and tail in one pass at each size, so the two grow together. Drawn as solid shapes
        // rather than outlines because a solid shape does not have to know where it overlaps itself:
        // the smaller pass simply covers the middle of the larger one.
        void Silhouette(int offsetX, int offsetY, int grow, Color color)
        {
            canvas.ChamferedRect(
                body.X + offsetX - grow,
                body.Y + offsetY - grow,
                body.Width + grow * 2,
                body.Height + grow * 2,
                CHAMFER + grow,
                color);

            float half = TAIL_WIDTH * 0.5f + grow;

            // The join sits a texel inside the body: flush, the two shapes would leave a seam row
            // wherever the scanline rounding of one disagreed with the rectangle edge of the other.
            canvas.Triangle(
                new Vector2(join.X + offsetX - half, join.Y + offsetY - grow - 1f),
                new Vector2(join.X + offsetX + half, join.Y + offsetY - grow - 1f),
                new Vector2(point.X + offsetX, point.Y + offsetY + grow),
                color);
        }
    }

    /// <summary>
    ///     Draws one response slot's frame and returns where its label goes.
    /// </summary>
    /// <remarks>
    ///     The selected slot inverts rather than brightening. A colour shift has nowhere to go on a
    ///     panel that is already pure white on pure black; swapping which of the two is the ink is
    ///     unmistakable at any size, and survives the pixelation.
    /// </remarks>
    public static Point DrawChoice(PixelCanvas canvas, Point size, bool selected, Color ghost)
    {
        canvas.ChamferedRect(MARGIN - GHOST - BORDER, MARGIN - GHOST - BORDER,
            size.X + BORDER * 2, size.Y + BORDER * 2, CHAMFER + BORDER, ghost);

        canvas.ChamferedRect(MARGIN - BORDER, MARGIN - BORDER,
            size.X + BORDER * 2, size.Y + BORDER * 2, CHAMFER + BORDER, Border);

        if (!selected)
            canvas.ChamferedRect(MARGIN, MARGIN, size.X, size.Y, CHAMFER, Fill);

        return new Point(MARGIN, MARGIN);
    }

    /// <summary>The surface region a choice slot of this size occupies, ghost and border included.</summary>
    public static Point ChoiceFootprint(Point size) => new(MARGIN + size.X + BORDER, MARGIN + size.Y + BORDER);

    /// <summary>
    ///     The largest label area a response slot may claim before its footprint stops fitting one row
    ///     of the atlas.
    /// </summary>
    /// <remarks>
    ///     The footprint is the label plus the ghost margin plus the border, and it is the footprint
    ///     the quad samples. Sizing the label against the row directly is what put the bottom border
    ///     of every slot outside the region it was read back from -- the slots were not clipped by the
    ///     screen, they were clipped by their own atlas row.
    /// </remarks>
    public static Point MaxChoiceContent() =>
        new(CHOICE_SURFACE_WIDTH - MARGIN - BORDER, CHOICE_PITCH - MARGIN - BORDER);
}
