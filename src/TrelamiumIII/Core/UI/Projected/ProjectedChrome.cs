using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     The widget vocabulary for projected panels: cards, slots and button frames, in the same
///     white-on-black with a coloured ghost that <see cref="PanelStyle" /> draws bubbles in.
/// </summary>
/// <remarks>
///     Everything here is a few chamfered rectangles, and none of it carries text: labels belong to
///     the face's text layer. The style has one trick -- an inverted shape for anything selected,
///     because on pure white over pure black a colour shift has nowhere to go -- and this keeps every
///     widget using it the same way.
/// </remarks>
internal static class ProjectedChrome
{
    /// <summary>Secondary text: labels, provenance, hints.</summary>
    public static readonly Color Dim = new(158, 158, 152);

    /// <summary>The one warm colour the board spends on what matters: selection marks, the reward label.</summary>
    public static readonly Color Accent = new(255, 196, 48);

    /// <summary>The ghost the job UI casts. Amber rather than a speaker's colour: nobody is talking.</summary>
    public static readonly Color JobGhost = new Color(255, 160, 40) * 0.55f;

    private const int SLOT_CHAMFER = 3;

    /// <summary>
    ///     A closed card with no tail: ghost, border and fill. For panels that sit next to the
    ///     cursor rather than hanging off anything.
    /// </summary>
    public static void Card(PixelCanvas canvas, Rectangle body, Color ghost)
    {
        int b = PanelStyle.BORDER;
        int g = PanelStyle.GHOST;
        int chamfer = PanelStyle.CHAMFER;

        canvas.ChamferedRect(body.X - g - b, body.Y - g - b, body.Width + b * 2, body.Height + b * 2, chamfer + b, ghost);
        canvas.ChamferedRect(body.X - b, body.Y - b, body.Width + b * 2, body.Height + b * 2, chamfer + b, PanelStyle.Border);
        canvas.ChamferedRect(body.X, body.Y, body.Width, body.Height, chamfer, PanelStyle.Fill);
    }

    /// <summary>The surface region a card of this body size occupies, ghost and border included.</summary>
    public static Point CardFootprint(Point body) =>
        new(PanelStyle.MARGIN + body.X + PanelStyle.BORDER, PanelStyle.MARGIN + body.Y + PanelStyle.BORDER);

    /// <summary>
    ///     A framed slot. Selected inverts it; hovered outlines it in the accent.
    /// </summary>
    public static void Slot(PixelCanvas canvas, Rectangle rect, bool selected, bool hovered)
    {
        Color edge = hovered ? Accent : PanelStyle.Border;

        canvas.ChamferedRect(rect.X, rect.Y, rect.Width, rect.Height, SLOT_CHAMFER, edge);

        if (!selected)
            canvas.ChamferedRect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, SLOT_CHAMFER - 1, PanelStyle.Fill);
    }

    /// <summary>
    ///     A button's frame, inverted while hovered. Disabled buttons dim rather than vanish, so the
    ///     layout holds still. The label goes on the text layer in <see cref="ButtonText" />.
    /// </summary>
    public static void ButtonFrame(PixelCanvas canvas, Rectangle rect, bool hovered, bool enabled)
    {
        bool inverted = hovered && enabled;

        canvas.ChamferedRect(rect.X, rect.Y, rect.Width, rect.Height, SLOT_CHAMFER, enabled ? PanelStyle.Border : Dim);

        if (!inverted)
            canvas.ChamferedRect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, SLOT_CHAMFER - 1, PanelStyle.Fill);
    }

    /// <summary>The colour a button's label takes for the state its frame was drawn in.</summary>
    public static Color ButtonText(bool hovered, bool enabled) =>
        hovered && enabled ? PanelStyle.Inverted : enabled ? PanelStyle.Text : Dim;

    /// <summary>A one-texel rule.</summary>
    public static void Rule(PixelCanvas canvas, int x, int y, int length, bool vertical, Color color)
    {
        if (vertical)
            canvas.Rect(x, y, 1, length, color);
        else
            canvas.Rect(x, y, length, 1, color);
    }
}
