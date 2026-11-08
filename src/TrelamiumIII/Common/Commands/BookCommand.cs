using System.Linq;
using Terraria.ModLoader;
using TrelamiumIII.Core.Books.Lore;

namespace TrelamiumIII.Common.Commands;

public class BookCommand : ModCommand
{
    public override string Command => "book";

    public override CommandType Type => CommandType.Chat | CommandType.World;

    public override string Usage => "/book <key|list>";

    public override string Description => "Open a book by key, or list the registered ones.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            string[] keys = BookRegistry.All.Select(book => book.Key).OrderBy(key => key).ToArray();

            caller.Reply(keys.Length == 0
                ? "No books are registered."
                : $"Registered books: {string.Join(", ", keys)}");

            return;
        }

        string requested = args[0];

        if (!BookRegistry.Has(requested))
        {
            caller.Reply($"No book registered under '{requested}'. Try /book list.");
            return;
        }

        BasicBookUI.Open(requested);
    }
}
