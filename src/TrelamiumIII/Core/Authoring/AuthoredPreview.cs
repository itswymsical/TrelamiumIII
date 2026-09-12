using System.Collections.Generic;
using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Layout.Blocks;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Authoring;

/// <summary>
///     Builds real blocks from the authored model for the viewport, with text inline since it isn't in localization yet.
/// </summary>
public static class AuthoredPreview
{
    public static List<BookBlock> ToBlocks(AuthoredBook book)
    {
        List<BookBlock> blocks = [];

        foreach (AuthoredBlock authored in book.Blocks)
        {
            BlockStyle style = StyleFor(authored, book);

            blocks.Add(authored.Kind switch
            {
                AuthoredBlockKind.Heading   => new HeadingBlock(authored.Text, authored.Rule) { Style = style },
                AuthoredBlockKind.Chapter   => new HeadingBlock(authored.Text, authored.Rule) { Style = style },
                AuthoredBlockKind.Rule      => new RuleBlock { Style = style },
                AuthoredBlockKind.Space     => new SpacerBlock(32f) { Style = style },
                AuthoredBlockKind.PageBreak => new PageBreakBlock(),
                _                           => new ProseBlock(authored.Text) { Style = style },
            });
        }

        return blocks;
    }

    private static BlockStyle StyleFor(AuthoredBlock authored, AuthoredBook book)
    {
        BlockStyle style = authored.Kind switch
        {
            AuthoredBlockKind.Heading or AuthoredBlockKind.Chapter => BlockStyle.Heading,
            AuthoredBlockKind.Quote                                => BlockStyle.Quote,
            AuthoredBlockKind.Caption                              => BlockStyle.Caption,
            _                                                      => BlockStyle.Body,
        };

        // Element choice first, then the book's, matching the cascade the exported definition gets.
        BookTypeface? face = Resolve(authored.Typeface) ?? Resolve(book.Typeface);

        style = style with
        {
            Align = (TextAlign)authored.Align,
            DropCap = authored.DropCap,
            Typeface = face,
        };

        if (authored.Scale > 0f) style = style with { Scale = authored.Scale };

        if (authored.Ink is { } packed)
            style = style with { Color = new Color((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF) };

        return style;
    }

    public static BookTypeface? Resolve(AuthoredTypeface choice) => choice switch
    {
        AuthoredTypeface.Vanilla      => BookTypeface.Vanilla,
        AuthoredTypeface.Encyclopedia => BookTypeface.Encyclopedia,
        AuthoredTypeface.Glossary     => BookTypeface.Glossary,
        _                             => null,
    };
}
