using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore.Definitions;

/// <summary>
///     Template showing the shape of a lore book: structure and presentation live here, the words live
///     in <c>Localization/LoreLibrary</c>.
/// </summary>
public sealed class ExampleLoreBook : ModLoreBook
{
    public override string Key => "Example.FirstAccount";

    protected override void Build(LoreBookBuilder book) => book
        .Heading("Title")

        // A drop cap only makes sense on the paragraph that opens a chapter.
        .Prose("Opening", BlockStyle.Body with { DropCap = true, Align = TextAlign.Justify })

        .Prose("Second", BlockStyle.Body with { Align = TextAlign.Justify })

        // Anchored to the paragraph above by default, so it travels with it when the book
        // reflows into another language.
        .StickyNote("Marginal", offset: new Vector2(6f, -14f), rotation: -0.05f, width: 124f)

        .Rule(span: 0.4f)
        .Quote("Excerpt")

        .Panel(panel => panel
            .Heading("WarningTitle", rule: false, BlockStyle.Heading with { Scale = 0.5f })
            .Prose("WarningBody", BlockStyle.Body with { Scale = 0.74f, SpaceAfter = 0f }))

        .PageBreak()
        .Heading("SecondChapter")
        .Prose("Closing", BlockStyle.Body with { Align = TextAlign.Justify });
}