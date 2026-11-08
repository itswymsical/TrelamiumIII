using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore.Definitions;

/// <summary>The series binding: deep teal boards around the Illuminance cover art.</summary>
internal static class IlluminanceSeries
{
    public static BookPalette Palette => BookPalette.Default() with
    {
        Cover = new Color(2, 19, 27),
        Spine = new Color(2, 19, 27),
        Shadow = new Color(3, 42, 59),
        Trim = new Color(30, 86, 113),
        CoverArt = Assets.Textures.BookCoverArt.IlluminanceCover.KEY,
    };
}

/// <summary>
///     Laine L.'s <em>Illuminance</em>, a scholar's memoir: the shape a long book takes, one Chapter call plus prose per chapter.
/// </summary>
public sealed class IlluminanceBook : ModBook
{
    public override string Key => "IlluminanceSeries.IlluminanceI";

    protected override BookPalette Palette => IlluminanceSeries.Palette;

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

    /// <summary>One chapter's layout, shared by every chapter in the book.</summary>
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

public sealed class IlluminanceBook2 : ModBook
{
    public override string Key => "IlluminanceSeries.IlluminanceII";

    protected override BookPalette Palette => IlluminanceSeries.Palette;

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

    /// <summary>One chapter's layout, shared by every chapter in the book.</summary>
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
