using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.Text.Tooltips;

/// <summary>
///     The snippet behind a <c>[ttblock]</c> tag: reports the block's size to the tooltip box and
///     draws the block in the line's place.
/// </summary>
public sealed class TooltipBlockSnippet : TextSnippet
{
    private readonly TooltipBlock block;

    public TooltipBlockSnippet(TooltipBlock block, string rawTag, Color baseColor) : base(rawTag, baseColor)
    {
        this.block = block;

        // Atomic: wrapping or selection should never split a block.
        DeleteWhole = true;
    }

    public override void OnHover() { }

    /// <remarks>
    ///     The line colour arrives already dimmed by the tooltip pulse. The base would dim it again
    ///     -- the same reason Terraria swaps its plain snippets for <c>PlainSnippet</c> before a
    ///     tooltip draw -- and the block would read a shade darker than the lines around it.
    /// </remarks>
    public override Color GetVisibleColor() => Color;

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        var font = FontAssets.MouseText.Value;

        size = block.Measure(font, scale);

        if (justCheckingString)
            return true;

        block.Draw(spriteBatch, font, position, color, scale);

        return true;
    }
}

/// <summary>
///     The blocks of the tooltip currently being built, so a tag can find the object it stands for.
/// </summary>
/// <remarks>
///     A tag is only text, and a block is not something worth encoding as text. Terraria calls
///     <c>ModifyTooltips</c> and then parses the resulting lines within the same draw, so the blocks
///     published by the one are exactly what the tags of the other refer to. The item type travels
///     with the index as a guard against a tag parsed against another item's blocks; such a tag
///     falls back to its plain text.
/// </remarks>
internal static class TooltipBlockRegistry
{
    private static readonly List<TooltipBlock> blocks = [];
    private static int itemType;

    internal static void Publish(int type, List<TooltipBlock> published)
    {
        itemType = type;
        blocks.Clear();
        blocks.AddRange(published);
    }

    internal static bool TryGet(int type, int index, [NotNullWhen(true)] out TooltipBlock? block)
    {
        block = type == itemType && index >= 0 && index < blocks.Count ? blocks[index] : null;
        return block is not null;
    }

    internal static void Clear()
    {
        blocks.Clear();
        itemType = 0;
    }
}

/// <summary>
///     Parses <c>[ttblock/type.index:fallback]</c> into a <see cref="TooltipBlockSnippet" />, or into
///     its fallback text when the block is not the one being drawn.
/// </summary>
public sealed class TooltipBlockTagHandler : ITagHandler
{
    /// <summary>
    ///     The line text that carries a block: the tag, then newlines to pad the line out to the
    ///     block's height.
    /// </summary>
    internal static string Compose(int itemType, int index, TooltipBlock block)
    {
        // Brackets would end the tag early; a newline would be counted as another line.
        string fallback = block.FallbackText
            .Replace('[', '(')
            .Replace(']', ')')
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        // The tag pattern refuses an empty text group.
        if (string.IsNullOrWhiteSpace(fallback))
            fallback = "-";

        return string.Concat(
            "[", InlineTagSystem.BLOCK_TAG, "/",
            itemType.ToString(CultureInfo.InvariantCulture), ".", index.ToString(CultureInfo.InvariantCulture),
            ":", fallback, "]",
            new string('\n', Math.Max(0, block.LineCount - 1))
        );
    }

    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        if (TryParseReference(options, out int type, out int index)
            && TooltipBlockRegistry.TryGet(type, index, out TooltipBlock? block))
        {
            return new TooltipBlockSnippet(block, $"[{InlineTagSystem.BLOCK_TAG}/{options}:{text}]", baseColor);
        }

        return new TextSnippet(text, baseColor);
    }

    private static bool TryParseReference(string options, out int type, out int index)
    {
        type = 0;
        index = 0;

        if (string.IsNullOrEmpty(options))
            return false;

        int dot = options.IndexOf('.');

        if (dot <= 0)
            return false;

        return int.TryParse(options.AsSpan(0, dot), NumberStyles.Integer, CultureInfo.InvariantCulture, out type)
            && int.TryParse(options.AsSpan(dot + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
    }
}
