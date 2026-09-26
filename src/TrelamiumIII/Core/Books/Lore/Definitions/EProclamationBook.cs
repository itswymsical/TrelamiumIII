using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore.Definitions;

public sealed class EProclamationBook : ModLoreBook
{
    public override string Key => "Misc.EProclamation";

    protected override BookTypeface? Typeface => BookTypeface.Encyclopedia;

    protected override void Build(BookBuilder book) => book
        .Heading("Title")

        .Prose("Body", BlockStyle.Body with { DropCap = true, Align = TextAlign.Justify });
}