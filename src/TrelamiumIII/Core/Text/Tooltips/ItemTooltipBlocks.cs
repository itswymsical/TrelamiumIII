using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.Text.Tooltips;

/// <summary>
///     Contributes <see cref="TooltipBlock" />s to an item's tooltip. Implement on a
///     <see cref="ModItem" />, or on any <see cref="GlobalItem" /> that applies to the item -- an
///     <c>ItemComponent</c> included.
/// </summary>
public interface ITooltipBlockProvider
{
    void AddTooltipBlocks(Item item, List<TooltipBlock> blocks);
}

/// <summary>
///     Gathers every provider's blocks each time a tooltip is built and places each one as a
///     tagged line at its anchor.
/// </summary>
/// <remarks>
///     Runs on every item because which items carry blocks is only known at draw time: an
///     <see cref="GlobalItem.AppliesToEntity" /> gate would be asked before the things that decide
///     (recipe registries, per-instance components) exist. The cost on an item with no providers
///     is one walk of its globals.
/// </remarks>
public sealed class ItemTooltipBlocks : GlobalItem
{
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        List<TooltipBlock>? blocks = null;

        if (item.ModItem is ITooltipBlockProvider own)
            own.AddTooltipBlocks(item, blocks ??= []);

        foreach (GlobalItem globalItem in item.Globals)
        {
            if (globalItem is ITooltipBlockProvider provider)
                provider.AddTooltipBlocks(item, blocks ??= []);
        }

        if (blocks is not { Count: > 0 })
            return;

        TooltipBlockRegistry.Publish(item.type, blocks);

        for (int i = 0; i < blocks.Count; i++)
        {
            // A floating block is drawn over the finished tooltip instead of taking a line in it.
            if (blocks[i].Placement != TooltipPlacement.Inline)
                continue;

            var line = new TooltipLine(Mod, $"Block{i}", TooltipBlockTagHandler.Compose(item.type, i, blocks[i]));

            tooltips.Insert(blocks[i].Anchor.Resolve(tooltips), line);
        }
    }

    /// <summary>
    ///     Hands the tooltip's finished width to the blocks about to be drawn in it.
    /// </summary>
    /// <remarks>
    ///     A right-aligned block needs the width of the whole tooltip, not of its own line, and this
    ///     is the first hook that runs once every line has been measured. It is the same measurement
    ///     vanilla just made to size the box, repeated because that figure is a local it does not
    ///     hand out. Doing it here rather than per line keeps it to once a frame.
    /// </remarks>
    public override bool PreDrawTooltip(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)
    {
        var font = FontAssets.MouseText.Value;
        float widest = 0f;

        foreach (TooltipLine line in lines)
            widest = Math.Max(widest, ChatManager.GetStringSize(font, line.Text, Vector2.One).X);

        TooltipBlockRegistry.SetTooltipWidth(widest);

        return true;
    }

    /// <summary>Draws the floating blocks over the finished tooltip.</summary>
    /// <remarks>
    ///     Here rather than through a snippet because a floating block is not a line: it borrows the
    ///     margin the longest line already paid for and adds no height. The lines arrive with their
    ///     final positions, which is what makes the corner findable, and they have all been drawn,
    ///     so the block lands on top of anything it does overlap rather than under it.
    /// </remarks>
    public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines)
    {
        if (lines.Count == 0)
            return;

        var font = FontAssets.MouseText.Value;
        float width = TooltipBlockRegistry.TooltipWidth;

        foreach (TooltipBlock block in TooltipBlockRegistry.Floating(item.type))
        {
            float blockWidth = block.MeasureWidth(font, 1f);
            int row = FirstRowWithRoom(lines, font, width, blockWidth, block.LineCount);

            var position = new Vector2(lines[0].X + width - blockWidth, lines[row].Y);

            DrawWithShadow(block, font, position);
        }
    }

    /// <summary>Gap kept between a tooltip line's text and a floating block beside it.</summary>
    private const float FLOAT_CLEARANCE = 12f;

    /// <summary>
    ///     The topmost line where the block's rows all clear the text beside them, or the first line
    ///     when the tooltip is too narrow for any of them to.
    /// </summary>
    private static int FirstRowWithRoom(
        ReadOnlyCollection<DrawableTooltipLine> lines,
        DynamicSpriteFont font,
        float width,
        float blockWidth,
        int rowCount)
    {
        for (int start = 0; start + rowCount <= lines.Count; start++)
        {
            bool fits = true;

            for (int row = 0; row < rowCount && fits; row++)
            {
                float text = ChatManager.GetStringSize(font, lines[start + row].Text, Vector2.One).X;

                fits = text + FLOAT_CLEARANCE + blockWidth <= width;
            }

            if (fits)
                return start;
        }

        return 0;
    }

    /// <summary>
    ///     Draws the block the way vanilla draws a line: four black offsets, then the block itself.
    /// </summary>
    /// <remarks>
    ///     A line drawn through the snippet parser gets this from <c>ChatManager</c>. A floating
    ///     block never goes through it, so without this it would be the one thing on the tooltip
    ///     with no drop shadow, which shows the moment it lies over a bright background.
    /// </remarks>
    private static void DrawWithShadow(TooltipBlock block, DynamicSpriteFont font, Vector2 position)
    {
        int pulse = Main.mouseTextColor;

        var color = new Color(pulse, pulse, pulse, pulse);
        var shadow = new Color(0, 0, 0, pulse);

        foreach (Vector2 direction in ChatManager.ShadowDirections)
            block.Draw(Main.spriteBatch, font, position + direction * 2f, shadow, 1f);

        block.Draw(Main.spriteBatch, font, position, color, 1f);
    }
}
