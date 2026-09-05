using TrelamiumIII.Core.ExperientialSystem;

namespace TrelamiumIII.Common.Commands;

/// <summary>Grants Exp directly, for exercising the popup, the meter and the Threshold crossing.</summary>
public sealed class GrantExpCommand : ModCommand
{
    public override string Command => "exp";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/exp <amount>";

    public override string Description => "Grants Exp to yourself, bypassing the source allowance.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        var experiential = caller.Player.GetModPlayer<ExperientialPlayer>();

        if (args.Length == 0)
        {
            caller.Reply($"Threshold {experiential.Threshold} - {experiential.Exp} / {experiential.Capacity} Exp",
                Color.LightGray);
            return;
        }

        if (!int.TryParse(args[0], out int amount) || amount == 0)
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        // Unmetered on purpose: routing test Exp through a real source would eat that source's
        // per-Threshold allowance and quietly change what the next honest kill is worth.
        int granted = experiential.GrantUnmetered(amount);

        caller.Reply($"+{granted} Exp -> Threshold {experiential.Threshold}, {experiential.Exp} / {experiential.Capacity}",
            Color.LightGreen);
    }
}
