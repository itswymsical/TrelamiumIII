using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore.Definitions;

public sealed class PrimordialSandsBook1: ModBook
{
    public override string Key => "PrimordialSands1";

    protected override void Build(BookBuilder book)
    {
        book
            .Space(32f)
            .Heading("Title", rule: false, BlockStyle.Heading with { Scale = 0.9f, Align = TextAlign.Center })
            .Rule(span: 0.45f)
            .Space(12f)
            .Contents("ContentsHeader");

        //Chapter(book, "Ch1");
        //Chapter(book, "Ch2");
        //Chapter(book, "Ch3");
        //Chapter(book, "Ch4");
        //Chapter(book, "Ch5");
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
