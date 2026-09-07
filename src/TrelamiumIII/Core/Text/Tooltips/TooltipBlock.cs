using System.Collections.Generic;
using ReLogic.Graphics;

namespace TrelamiumIII.Core.Text.Tooltips;

/// <summary>
///     A custom-drawn region of an item tooltip, measured like text so the box fits around it.
/// </summary>
/// <remarks>
///     <para>
///         Terraria sizes a tooltip by walking each line's snippets and asking any that draw
///         themselves how large they are, then steps between lines by the font height of each
///         line's raw text. A block rides both: <see cref="ItemTooltipBlocks" /> publishes it as an
///         inline <c>[ttblock]</c> tag whose snippet reports the block's size, and pads the line
///         with newlines so the step matches. The padding can only be whole lines, which is why a
///         block's height is counted in <see cref="LineCount" /> rather than pixels.
///     </para>
///     <para>
///         Blocks reach the tooltip through <see cref="ITooltipBlockProvider" />, which a
///         <see cref="ModItem" /> or any <see cref="GlobalItem" /> on the item can implement. Most
///         blocks are a <see cref="TooltipRowsBlock" />; derive from this directly only for
///         something rows cannot express.
///     </para>
/// </remarks>
public abstract class TooltipBlock
{
    /// <summary>Where in the tooltip the block is inserted.</summary>
    public virtual TooltipAnchor Anchor => TooltipAnchor.End;

    /// <summary>Height in text lines, each one <see cref="DynamicSpriteFont.LineSpacing" /> tall.</summary>
    public abstract int LineCount { get; }

    /// <summary>
    ///     What the block says when it cannot be drawn: search filters, other mods reading tooltip
    ///     text, or the tag parsed somewhere other than the tooltip it was made for.
    /// </summary>
    public abstract string FallbackText { get; }

    public abstract float MeasureWidth(DynamicSpriteFont font, float scale);

    /// <summary>Draws the block with its top-left corner at <paramref name="position" />.</summary>
    /// <param name="color">
    ///     The line colour Terraria handed the snippet, already dimmed by the tooltip pulse. On the
    ///     four shadow passes its colour channels are zero (<see cref="IsShadowPass" />): text drawn
    ///     in it as-is picks up vanilla's drop shadow for free, and icons should be skipped so they
    ///     are not stamped as black silhouettes.
    /// </param>
    public abstract void Draw(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 position, Color color, float scale);

    public Vector2 Measure(DynamicSpriteFont font, float scale)
        => new(MeasureWidth(font, scale), LineCount * font.LineSpacing * scale);

    /// <summary>
    ///     Whether a draw colour belongs to one of the shadow offsets rather than the real pass. Not
    ///     an equality test against black: the shadow passes keep the tooltip's alpha.
    /// </summary>
    public static bool IsShadowPass(Color color) => color is { R: 0, G: 0, B: 0 };
}

/// <summary>A position in the tooltip, resolved against the lines present when the block is inserted.</summary>
public readonly struct TooltipAnchor
{
    private enum Kind
    {
        End,
        AfterName,
        AfterLine,
    }

    /// <summary>Vanilla lines that trail the item name and belong with it.</summary>
    private static readonly string[] NameGroup = ["Favorite", "FavoriteDesc", "NoTransfer", "Social"];

    private readonly Kind kind;
    private readonly string[]? lineNames;

    private TooltipAnchor(Kind kind, string[]? lineNames)
    {
        this.kind = kind;
        this.lineNames = lineNames;
    }

    /// <summary>After every other line.</summary>
    public static TooltipAnchor End => new(Kind.End, null);

    /// <summary>Directly under the item name, below its favourite and social notes.</summary>
    public static TooltipAnchor AfterName => new(Kind.AfterName, null);

    /// <summary>
    ///     After the first line found among <paramref name="lineNames" />, tried in order, so a block
    ///     can prefer <c>Damage</c> and settle for <c>ItemName</c> on an item with no damage line.
    ///     Falls back to the end. Vanilla line names are listed on <see cref="TooltipLine" />.
    /// </summary>
    public static TooltipAnchor After(params string[] lineNames) => new(Kind.AfterLine, lineNames);

    /// <summary>The index to insert at.</summary>
    public int Resolve(List<TooltipLine> tooltips)
    {
        switch (kind)
        {
            case Kind.AfterName:
            {
                int index = IndexOf(tooltips, "ItemName");

                if (index < 0)
                    return 0;

                index++;

                while (index < tooltips.Count
                    && tooltips[index].Mod == "Terraria"
                    && Array.IndexOf(NameGroup, tooltips[index].Name) >= 0)
                {
                    index++;
                }

                return index;
            }

            case Kind.AfterLine when lineNames is not null:
            {
                foreach (string name in lineNames)
                {
                    int index = IndexOf(tooltips, name);

                    if (index >= 0)
                        return index + 1;
                }

                return tooltips.Count;
            }

            default:
                return tooltips.Count;
        }
    }

    private static int IndexOf(List<TooltipLine> tooltips, string name)
    {
        for (int i = 0; i < tooltips.Count; i++)
        {
            if (tooltips[i].Name == name)
                return i;
        }

        return -1;
    }
}
