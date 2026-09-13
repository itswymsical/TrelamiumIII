using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Common.Commands;

/// <summary>Runs <see cref="AlchemyRecipeValidator" />; findings are written to the log.</summary>
public class AlchemyCommand : ModCommand
{
    public override string Command => "alchemy";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/alchemy validate";
    public override string Description => "Validates alchemy recipes against item roles and principles";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1 || !args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        List<AlchemyRecipeValidator.Finding> findings = AlchemyRecipeValidator.Run();

        foreach (AlchemyRecipeValidator.Finding finding in findings)
            Mod.Logger.Warn($"[{finding.Rule}] {finding.Message}");

        if (findings.Count == 0)
        {
            caller.Reply("No recipe findings.", Color.GreenYellow);
            return;
        }

        string counts = string.Join(", ", findings.GroupBy(f => f.Rule).Select(g => $"{g.Key}: {g.Count()}"));
        caller.Reply($"{findings.Count} findings ({counts}). Details are in the log.", Color.Orange);
    }
}
