using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore.Definitions;

/// <summary>
///     Laine L.'s <em>Illuminance</em>: a scholar's memoir.
/// </summary>
/// <remarks>
///     <para>
///         The shape a long book wants. A chapter is one <see cref="BookBuilder.Chapter" /> call
///         and a handful of prose blocks; the chapter breaks its own page, earns its contents row, and
///         reports the page it landed on once the book is flowed.
///     </para>
///     <para>
///         Note how little grows with length. A fifty-page book is more chapters, not more machinery,
///         and a chapter's prose can be one localization entry however many pages it runs to --
///         paragraphs are parted with <c>[p]</c> inside it and the block breaks across pages by itself.
///     </para>
/// </remarks>
public sealed class IlluminanceBook : ModLoreBook
{
    public override string Key => "IlluminanceSeries.IlluminanceI";
    protected override void Build(BookBuilder book)
    {
        // Front matter: the title, then the table of contents. Contents may sit anywhere; it measures
        // from the chapter count, so putting it before the chapters it lists is not circular.
        book
            .Space(32f)
            .Heading("Title", rule: false, BlockStyle.Heading with { Scale = 0.9f, Align = TextAlign.Center })
            .Rule(span: 0.45f)
            .Space(12f)
            .Contents("ContentsHeader");

        Chapter(book, "Ch1");
        Chapter(book, "Ch2");
        Chapter(book, "Ch3");
        Chapter(book, "Ch4");
        Chapter(book, "Ch5");
    }

    /// <summary>
    ///     One chapter's furniture. Every chapter in this book is set the same way, so the arrangement
    ///     lives in one place rather than being repeated per chapter.
    /// </summary>
    private static void Chapter(BookBuilder builder, string chapter)
    {
        builder
            .Chapter(chapter)

            .Prose($"{chapter}.Opening", BlockStyle.Body with { DropCap = true, Align = TextAlign.Justify })
            .Prose($"{chapter}.Second", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Third", BlockStyle.Body with { Align = TextAlign.Justify })


            .Rule(span: 0.8f)
            .Quote($"{chapter}.Excerpt")

            .Panel(panel => panel
                .Heading($"{chapter}.AsideTitle", rule: false, BlockStyle.Heading with { Scale = 0.45f })
                .Prose($"{chapter}.AsideBody", BlockStyle.Body with { Scale = 0.74f, SpaceAfter = 0f }))

            .Prose($"{chapter}.Fourth", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Fifth", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Sixth", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Closing", BlockStyle.Body with { Align = TextAlign.Justify });
    }
}

public sealed class IlluminanceBook2 : ModLoreBook
{
    public override string Key => "IlluminanceSeries.IlluminanceII";

    protected override void Build(BookBuilder book)
    {
        // Front matter: the title, then the table of contents. Contents may sit anywhere; it measures
        // from the chapter count, so putting it before the chapters it lists is not circular.
        book
            .Space(32f)
            .Heading("Title", rule: false, BlockStyle.Heading with { Scale = 0.9f, Align = TextAlign.Center })
            .Rule(span: 0.45f)
            .Space(12f)
            .Contents("ContentsHeader");

        Chapter(book, "Ch1");
        //Chapter(book, "Ch2");
        //Chapter(book, "Ch3");
        //Chapter(book, "Ch4");
        //Chapter(book, "Ch5");
    }

    /// <summary>
    ///     One chapter's furniture. Every chapter in this book is set the same way, so the arrangement
    ///     lives in one place rather than being repeated per chapter.
    /// </summary>
    private static void Chapter(BookBuilder builder, string chapter)
    {
        builder
            .Chapter(chapter)

            .Prose($"{chapter}.Opening", BlockStyle.Body with { DropCap = true, Align = TextAlign.Justify })
            .Prose($"{chapter}.Second", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Third", BlockStyle.Body with { Align = TextAlign.Justify })


            .Rule(span: 0.8f)
            .Quote($"{chapter}.Excerpt")

            .Panel(panel => panel
                .Heading($"{chapter}.AsideTitle", rule: false, BlockStyle.Heading with { Scale = 0.45f })
                .Prose($"{chapter}.AsideBody", BlockStyle.Body with { Scale = 0.74f, SpaceAfter = 0f }))

            .Prose($"{chapter}.Fourth", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Fifth", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Sixth", BlockStyle.Body with { Align = TextAlign.Justify })
            .Prose($"{chapter}.Closing", BlockStyle.Body with { Align = TextAlign.Justify });
    }
}
