using System.Globalization;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.Text.Tooltips;

/// <summary>
///     Blank width in a tooltip line, so a floating <see cref="TooltipBlock" /> has margin to sit
///     in. Terraria sizes the box to its widest line, summing snippet widths and taking the
///     maximum of their heights, so a zero-height spacer widens the box without adding to it.
/// </summary>
public sealed class TooltipReserveSnippet : TextSnippet
{
    private readonly float width;

    public TooltipReserveSnippet(float width, string rawTag) : base(rawTag, Color.Transparent)
    {
        this.width = width;

        DeleteWhole = true;
    }

    public override void OnHover() { }

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        size = new Vector2(width * scale, 0f);

        return true;
    }
}

/// <summary>Parses <c>[ttspace:24]</c> into a <see cref="TooltipReserveSnippet" /> of that many pixels.</summary>
public sealed class TooltipReserveTagHandler : ITagHandler
{
    private const string PREFIX = $"[{InlineTagSystem.SPACE_TAG}:";

    /// <summary>
    ///     The tag for <paramref name="width" />, rounded up: the caller sizes the box by adding this
    ///     to a line it has measured, and a rounded-down tag would leave the box a fraction short.
    /// </summary>
    internal static string Compose(float width)
        => PREFIX + ((int)MathF.Ceiling(width)).ToString(CultureInfo.InvariantCulture) + "]";

    /// <summary>Whether <paramref name="text" /> carries a reservation, to skip parsing lines that do not.</summary>
    internal static bool IsPresent(string text) => text.Contains(PREFIX, StringComparison.Ordinal);

    /// <summary>
    ///     <paramref name="text" /> without its reservation, for anything that measures or draws a
    ///     tooltip line without the snippet parser. A line carries at most one.
    /// </summary>
    internal static string Strip(string text)
    {
        int start = text.IndexOf(PREFIX, StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf(']', start);

        return end < 0 ? text : text.Remove(start, end - start + 1);
    }

    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) || width <= 0)
            return new TextSnippet(string.Empty, baseColor);

        return new TooltipReserveSnippet(width, Compose(width));
    }
}
