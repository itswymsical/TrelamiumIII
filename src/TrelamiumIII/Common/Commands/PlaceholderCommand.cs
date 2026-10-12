using System.Linq;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Common.Commands;

public class PlaceholderCommand : ModCommand
{
    public override string Command => "placeholders";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/placeholders";

    public override string Description => "Lists content drawn with a placeholder because its texture file is missing.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        var entries = PlaceholderTextures.Missing.OrderBy(entry => entry.Kind).ThenBy(entry => entry.Name).ToList();

        if (entries.Count == 0)
        {
            caller.Reply("No placeholders: every texture is present.", Color.Green);
            return;
        }

        caller.Reply($"{entries.Count} placeholder texture(s), expected at:", Color.Yellow);

        foreach (var entry in entries)
            caller.Reply($"{entry.Kind} {entry.Name}: {entry.ExpectedPath}.png", Color.LightGray);
    }
}
