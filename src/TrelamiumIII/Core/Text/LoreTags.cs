using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.UI.Chat;
using TrelamiumIII.Core.Books;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Text;

/// <summary>
///     A glossary term: <c>[gloss:Tidemark]</c>.
/// </summary>
/// <remarks>
///     The tag carries the glossary key, not the word, so both the label and its explanation come out
///     of <c>LoreGlossary.{key}</c> and a translation is free to render the term however that language
///     wants without the key drifting.
/// </remarks>
public sealed class GlossTagHandler : ITagHandler
{
    private static readonly Color DefaultColor = new(126, 96, 54);

    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        string key = text.Trim();
        string label = Lookup($"{key}.Term") ?? key;
        string? definition = Lookup($"{key}.Definition");

        return new GlossSnippet(label, definition, InlineTags.ParseColor(options) ?? DefaultColor);
    }

    private static string? Lookup(string suffix)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"LoreGlossary.{suffix}");
        string value = Language.GetTextValue(key);

        return value == key ? null : value;
    }
}

/// <summary>A term set slightly apart and underscored, explained when the cursor rests on it.</summary>
public sealed class GlossSnippet(string label, string? definition, Color color)
    : TextSnippet(label, color), IBookInteractiveSnippet
{
    public string? TooltipText => definition;

    public string? LinkTarget => null;

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;

        // The base already holds the label in its Text field; reading it back avoids storing the
        // same string twice.
        size = font.MeasureString(Text) * scale;

        if (justCheckingString)
            return true;

        spriteBatch.DrawString(font, Text, position, color.R == 0 && color.G == 0 && color.B == 0 ? color : Color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        // Only on coloured passes: the shadow pass would otherwise stamp four black rules.
        if (color.R != 0 || color.G != 0 || color.B != 0)
        {
            spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)position.X, (int)(position.Y + size.Y - 2f), (int)size.X, 1),
                Color * 0.55f);
        }

        return true;
    }
}

/// <summary>
///     A cross-reference to another lore book: <c>[lore:DrownedArchive]</c>. Shows that book's title,
///     and opens it when clicked from inside a book.
/// </summary>
public sealed class LoreLinkTagHandler : ITagHandler
{
    private static readonly Color DefaultColor = new(92, 108, 168);

    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        string key = text.Trim();
        BookDefinition? book = BookRegistry.Get(key);

        // An unresolved reference reads as ordinary text rather than as a broken tag, so a book that
        // has not been written yet does not litter finished pages.
        if (book is null)
            return new TextSnippet(key, baseColor);

        return new LoreLinkSnippet(book.Title, key, InlineTags.ParseColor(options) ?? DefaultColor);
    }
}

/// <summary>The clickable form of a lore cross-reference.</summary>
public sealed class LoreLinkSnippet(string label, string bookKey, Color color)
    : TextSnippet(label, color), IBookInteractiveSnippet
{
    public string? TooltipText => Language.GetTextValue(TrelamiumIII.Instance.GetLocalizationKey("LoreUI.OpenBook"), Text);

    public string? LinkTarget => bookKey;

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        size = font.MeasureString(Text) * scale;

        if (justCheckingString)
            return true;

        bool shadowPass = color.R == 0 && color.G == 0 && color.B == 0;

        spriteBatch.DrawString(font, Text, position, shadowPass ? color : Color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        if (!shadowPass)
        {
            spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)position.X, (int)(position.Y + size.Y - 2f), (int)size.X, 1),
                Color);
        }

        return true;
    }
}

/// <summary>
///     Text struck out under a bar: <c>[redacted:the third seal]</c>.
/// </summary>
/// <remarks>
///     The bar is measured from the hidden text, so the gap is the right shape and the reader can see
///     how much was taken. What counts as earned is a decision for content rather than for text
///     layout, so it is left to <see cref="RevealCheck" />; unset, nothing is ever revealed.
/// </remarks>
public sealed class RedactedTagHandler : ITagHandler
{
    /// <summary>Decides whether a given redaction is legible yet. The argument is the tag's option.</summary>
    public static Func<string, bool>? RevealCheck { get; set; }

    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        bool revealed = RevealCheck?.Invoke(options ?? string.Empty) ?? false;

        return new RedactedSnippet(text, revealed, baseColor);
    }
}

/// <summary>Hidden text and the bar that hides it.</summary>
public sealed class RedactedSnippet(string hidden, bool revealed, Color baseColor)
    : TextSnippet(hidden, baseColor)
{
    private static readonly Color Ink = new(28, 22, 18);

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;

        // Measured from the hidden text even while it is covered, so the bar is the shape of what
        // was taken and the line around it does not shift when it is revealed.
        size = font.MeasureString(Text) * scale;

        if (justCheckingString)
            return true;

        bool shadowPass = color.R == 0 && color.G == 0 && color.B == 0;

        if (revealed)
        {
            spriteBatch.DrawString(font, Text, position, shadowPass ? color : Color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return true;
        }

        if (!shadowPass)
        {
            spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)position.X, (int)(position.Y + 2f), (int)size.X, (int)Math.Max(2f, size.Y - 5f)),
                Ink * (color.A / 255f));
        }

        return true;
    }
}

/// <summary>Switches to the handwriting face for a run of text: <c>[hand:added later]</c>.</summary>
public sealed class HandwritingTagHandler : ITagHandler
{
    private static readonly Color DefaultColor = new(46, 58, 104);

    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options) =>
        new HandwritingSnippet(text, InlineTags.ParseColor(options) ?? DefaultColor);
}

/// <summary>
///     A run set in the handwriting face.
/// </summary>
/// <remarks>
///     <para>
///         The face is larger than the body text at the same scale, so it is knocked back to sit on
///         the same line rather than pushing the leading around it.
///     </para>
///     <para>
///         Wrappable, unlike the other snippets here. A glossary term or a redaction is one
///         indivisible thing, but this is ordinary prose that merely happens to be in another hand,
///         and a whole sentence of it has to be free to break across lines like any other.
///     </para>
/// </remarks>
public sealed class HandwritingSnippet(string text, Color color) : TextSnippet(text, color), IWrappableSnippet
{
    /// <summary>Correction that brings the display face down to body size.</summary>
    private const float FIT = 0.62f;

    public float MeasureText(string run, float scale) =>
        BookText.FontFor(BookFont.Hand).MeasureString(run).X * scale * FIT;

    /// <summary>
    ///     Keeps the face when the line breaker splits this run; the base returns a plain snippet,
    ///     which would drop every fragment after the first back into body text.
    /// </summary>
    public override TextSnippet CopyMorph(string newText) => new HandwritingSnippet(newText, Color);

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        DynamicSpriteFont font = BookText.FontFor(BookFont.Hand);
        float fitted = scale * FIT;

        size = font.MeasureString(Text) * fitted;

        if (justCheckingString)
            return true;

        bool shadowPass = color.R == 0 && color.G == 0 && color.B == 0;

        spriteBatch.DrawString(font, Text, position, shadowPass ? color : Color, 0f, Vector2.Zero, fitted, SpriteEffects.None, 0f);

        return true;
    }
}
