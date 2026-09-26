using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore.Definitions;

/// <summary>Template lore book: structure and presentation here, words in <c>Localization/LoreLibrary</c>.</summary>
public sealed class ExampleLoreBook : ModLoreBook
{
    public override string Key => "Example.FirstAccount";

    /// <summary>The book's typeface and the bottom of the cascade: everything is Andika unless a section or element says otherwise.</summary>
    protected override BookTypeface? Typeface => BookTypeface.Encyclopedia;

    protected override void Build(BookBuilder book) => book
        .Heading("Title")

        .Prose("Opening", BlockStyle.Body with { DropCap = true, Align = TextAlign.Justify })

        .Prose("Second", BlockStyle.Body with { Align = TextAlign.Justify })

        .StickyNote("Marginal", offset: new Vector2(6f, -14f), rotation: -0.05f, width: 124f)

        .Rule(span: 0.4f)
        .Quote("Excerpt")

        .Section(BookTypeface.Glossary, glossary => glossary
            .Panel(panel => panel
                .Heading("WarningTitle", rule: false, BlockStyle.Heading with { Scale = 0.5f })
                .Prose("WarningBody", BlockStyle.Body with { Scale = 0.74f, SpaceAfter = 0f })))

        .PageBreak()
        .Heading("SecondChapter")

        .Prose("Closing", BlockStyle.Body with
        {
            Align = TextAlign.Justify,
            Typeface = BookTypeface.Glossary,
        });
}
