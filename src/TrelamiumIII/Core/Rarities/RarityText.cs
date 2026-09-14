using ReLogic.Graphics;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Rarities.Text;

namespace TrelamiumIII.Core.Rarities;

/// <summary>Draws text with a <see cref="RarityStyle" />'s flairs into any active batch: tooltips, labels, panels.</summary>
public static class RarityText
{
    /// <summary>Draws <paramref name="text" /> with its line box's top-left at <paramref name="position" />, in the active batch's space.</summary>
    /// <param name="key">Identifies this text to fields that keep per-text content. Defaults to the text itself.</param>
    /// <param name="item">The item the text belongs to, for flairs that read it.</param>
    public static void Draw(SpriteBatch spriteBatch, RarityStyle style, string text, Vector2 position, Color color, float scale = 1f,
        DynamicSpriteFont? font = null, object? key = null, Item? item = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Draw(new RarityNameContext(spriteBatch, style, font ?? FontAssets.MouseText.Value, text, position, new Vector2(scale), 0f, Vector2.Zero,
            color, key ?? text, item));
    }

    /// <summary>Runs the flairs behind, the glyphs, then the flairs in front.</summary>
    internal static void Draw(in RarityNameContext ctx)
    {
        RunPass(ctx, over: false);
        Paint(ctx);
        RunPass(ctx, over: true);
    }

    private static void Paint(in RarityNameContext ctx)
    {
        if (ctx.Style.NamePainter is { } painter)
        {
            painter.PaintName(ctx);
            return;
        }

        TextPainter.DrawPlain(ctx.SpriteBatch, ctx.Font, ctx.Text, ctx.Position, ctx.Color, ctx.Rotation, ctx.Origin, ctx.Scale,
            ctx.Style.Text);
    }

    /// <summary>Draws one side of the text, sharing a batch between consecutive flairs with the same blend.</summary>
    private static void RunPass(in RarityNameContext ctx, bool over)
    {
        FlairHooks hook = over ? FlairHooks.OverName : FlairHooks.UnderName;
        SpriteBatch spriteBatch = ctx.SpriteBatch;

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);
        SpriteBatchState current = home;

        foreach (RarityFlair flair in ctx.Style.Flairs)
        {
            if ((flair.Hooks & hook) == 0)
                continue;

            RarityBatch.Switch(spriteBatch, ref current, flair.ManagesOwnBatch ? home : home.With(flair.Blend));

            if (over)
                flair.OverName(ctx);
            else
                flair.UnderName(ctx);
        }

        RarityBatch.Switch(spriteBatch, ref current, home);
    }
}
