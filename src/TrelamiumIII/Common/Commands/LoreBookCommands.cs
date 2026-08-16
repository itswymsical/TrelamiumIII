using System.Linq;
using Terraria.ModLoader;
using TrelamiumIII.Core.Books.Lore;

namespace TrelamiumIII.Common.Commands;

/// <summary>Opens a registered lore book, for checking layout without hunting down the item.</summary>
public class LoreBookCommand : ModCommand
{
    public override string Command => "lorebook";

    public override CommandType Type => CommandType.Chat | CommandType.World;

    public override string Usage => "/lorebook <key|list>";

    public override string Description => "Open a lore book by key, or list the registered ones.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            string[] keys = LoreBookRegistry.All.Select(book => book.Key).OrderBy(key => key).ToArray();

            caller.Reply(keys.Length == 0
                ? "No lore books are registered."
                : $"Registered lore books: {string.Join(", ", keys)}");

            return;
        }

        string requested = args[0];

        if (!LoreBookRegistry.Has(requested))
        {
            caller.Reply($"No lore book registered under '{requested}'. Try /lorebook list.");
            return;
        }

        LoreBookUI.Open(requested);
    }
}
