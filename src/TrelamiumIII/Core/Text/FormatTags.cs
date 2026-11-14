using System.Collections.Generic;
using ReLogic.Graphics;
using Terraria.UI.Chat;
<<<<<<< HEAD
=======
using TrelamiumIII.Core.Books.Authoring;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
using TrelamiumIII.Core.Books.Text;
using TrelamiumIII.Utilities.Authoring;

namespace TrelamiumIII.Core.Text;

/// <summary>Parses the authoring tool's run tag, <c>[fmt/b,i,s1.4,cFF0000:text]</c>.</summary>
public sealed class FormatTagHandler : ITagHandler
{
    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options) =>
        new FormattedSnippet(text, baseColor, FormatCodec.ReadOptions(options));
}

/// <summary>
///     A run of prose carrying character-level formatting from the authoring tool.
/// </summary>
/// <remarks>
///     <para>
///         The face is not known at parse time. <see cref="ChatManager.ParseMessage" /> is a static
///         call with no idea which book, block or typeface it is parsing for, so
///         <see cref="BookText" /> binds the surrounding face onto every one of these after parsing.
///         Without that a bold run would have to guess a typeface, and guessing would set an Andika
///         book's emphasis in whatever face the guess named.
///     </para>
///     <para>
///         Wrappable, like <see cref="HandwritingSnippet" />: this is ordinary prose that happens to
///         be emphasised, so a whole sentence of it must break across lines like any other. That also
///         means <see cref="CopyMorph" /> has to carry both the format and the bound face, or every
///         fragment after the first reverts to plain body text.
///     </para>
/// </remarks>
public sealed class FormattedSnippet(string text, Color color, CharFormat format)
    : TextSnippet(text, color), IWrappableSnippet
{
    public CharFormat Format { get; } = format;

    /// <summary>The role and typeface of the block this run sits in. Bound after parsing.</summary>
    public BookFace Face { get; set; }

    /// <summary>
    ///     Points every formatted run in a parsed message at the face its block is set in.
    /// </summary>
    public static void Bind(IReadOnlyList<TextSnippet> snippets, BookFace face)
    {
        foreach (TextSnippet snippet in snippets)
        {
            if (snippet is FormattedSnippet formatted) formatted.Face = face;
        }
    }

    /// <summary>The emphasised face, or the plain one when the typeface cannot express it.</summary>
    private DynamicSpriteFont Resolve() =>
        (Face.Typeface ?? BookTypeface.Vanilla).Variant(Face.Role, Format.Bold, Format.Italic);

    /// <summary>A character size overrides the block's outright, since two sizes cannot compose.</summary>
    private float Sized(float scale) => Format.HasScale ? Format.Scale : scale;

    public float MeasureText(string run, float scale) => Resolve().MeasureString(run).X * Sized(scale);

    public override TextSnippet CopyMorph(string newText) =>
        new FormattedSnippet(newText, Color, Format) { Face = Face };

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        DynamicSpriteFont font = Resolve();
        float sized = Sized(scale);

        size = font.MeasureString(Text) * sized;

        if (justCheckingString)
            return true;

        // A pure black colour is the drop-shadow pass, which must stay black rather than take the
        // run's own tint -- the same convention the other snippets here follow.
        bool shadowPass = color.R == 0 && color.G == 0 && color.B == 0;
        Color ink = Format.Ink is { } tint
            ? new Color((tint >> 16) & 0xFF, (tint >> 8) & 0xFF, tint & 0xFF)
            : Color;

        spriteBatch.DrawString(font, Text, position, shadowPass ? color : ink,
            0f, Vector2.Zero, sized, SpriteEffects.None, 0f);

        return true;
    }
}
