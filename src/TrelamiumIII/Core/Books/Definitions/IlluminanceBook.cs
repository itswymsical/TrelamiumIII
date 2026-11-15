using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII.Core.Books.Definitions;

internal static class IlluminanceSeries
{
    public static BookPalette Palette => BookPalette.Default() with
    {
        Cover = new Color(14, 2, 28),
        Spine = new Color(6, 2, 28),
        Shadow = new Color(30, 28, 68),
        Trim = new Color(47, 43, 79),
        PageColor = new Color(30, 30, 35),
        Ink = Color.White,
        PageEdgeColor = new Color(41, 41, 48),
        CoverArt = Assets.Textures.BookCoverArt.IlluminanceCover.KEY,
    };
}

public sealed class IlluminanceBook : ModBook
{
    public override string Key => "IlluminanceSeries.IlluminanceI";

    protected override BookPalette Palette => IlluminanceSeries.Palette;

    public static BookTypeface Illuminance { get; } = new(
        () => TrelamiumFonts.CascadiaMono_Regular,
        () => TrelamiumFonts.CascadiaMono_Regular,
        () => TrelamiumFonts.CascadiaMono_Medium)
    {
        Bold = () => TrelamiumFonts.CascadiaMono_Bold,
        Italic = () => TrelamiumFonts.CascadiaMono_Italic,
        BoldItalic = () => TrelamiumFonts.CascadiaMono_BoldItalic,
    };

    protected override BookTypeface? Typeface => Illuminance;
    protected override void Build(BookBuilder book)
    {
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