using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore.Definitions;

/// <summary>
///     Laine L.'s <em>Illuminance</em>: a scholar's memoir, in chapters.
/// </summary>
/// <remarks>
///     <para>
///         The shape a long book wants. A chapter is one <see cref="LoreBookBuilder.Chapter" /> call
///         and a handful of prose blocks; the chapter breaks its own page, earns its contents row, and
///         reports the page it landed on once the book is flowed.
///     </para>
///     <para>
///         Note how little grows with length. A fifty-page book is more chapters, not more machinery,
///         and a chapter's prose can be one localization entry however many pages it runs to --
///         paragraphs are parted with <c>[p]</c> inside it and the block breaks across pages by itself.
///     </para>
/// </remarks>
public static class IlluminanceBook
{
    private const string Book = "Illuminance";

    public static void Register()
    {
        LoreBookBuilder builder = LoreBook.Define(Book);

        // Front matter: the title, then the table of contents. Contents may sit anywhere; it measures
        // from the chapter count, so putting it before the chapters it lists is not circular.
        builder
            .Space(32f)
            .Heading("Title", rule: false, BlockStyle.Heading with { Scale = 0.9f, Align = TextAlign.Center })
            .Rule(span: 0.45f)
            .Space(12f)
            .Contents("ContentsHeader");

        Chapter(builder, "Ch1");
        Chapter(builder, "Ch2");
        ChapterThree(builder);

        builder.Register();
    }

    /// <summary>
    ///     One chapter's furniture. Every chapter in this book is set the same way, so the arrangement
    ///     lives in one place rather than being repeated per chapter.
    /// </summary>
    private static void Chapter(LoreBookBuilder builder, string chapter)
    {
        builder
            .Chapter(chapter)

            .Prose($"{chapter}.Opening", BlockStyle.Body with { DropCap = true, Align = TextAlign.Justify })
            .Prose($"{chapter}.Second", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Third", BlockStyle.Body with { Align = TextAlign.Justify })


            .Rule()
            .Quote($"{chapter}.Excerpt")

            .Panel(panel => panel
                .Heading($"{chapter}.WarningTitle", rule: false, BlockStyle.Heading with { Scale = 0.45f })
                .Prose($"{chapter}.WarningBody", BlockStyle.Body with { Scale = 0.74f, SpaceAfter = 0f }))

            .Prose($"{chapter}.Closing", BlockStyle.Body with { Align = TextAlign.Justify });
    }

    private static void ChapterThree(LoreBookBuilder builder)
    {
        builder
            .Chapter("Ch3")

            .Prose("Ch3.Opening", BlockStyle.Body with { DropCap = true, Align = TextAlign.Justify })
            .Prose("Ch3.Second", BlockStyle.Body with { Align = TextAlign.Justify })

            .Prose("Ch3.Third", BlockStyle.Body with { Align = TextAlign.Justify })
            .StickyNote("Ch3.Marginal", offset: new Vector2(9f, -10f), rotation: -0.04f, width: 108f)

            .Rule(span: 0.8f)
            .Quote("Ch3.Excerpt")

            .Panel(panel => panel
                .Heading("Ch3.AsideTitle", rule: false, BlockStyle.Heading with { Scale = 0.45f })
                .Prose("Ch3.AsideBody", BlockStyle.Body with { Scale = 0.74f, SpaceAfter = 0f }))

            .Prose("Ch3.Fourth", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose("Ch3.Closing", BlockStyle.Body with { Align = TextAlign.Justify });
    }
}