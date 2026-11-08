using TrelamiumIII.Core.ExperientialSystem;

namespace TrelamiumIII.Common.Commands;

public sealed class GrantExpCommand : ModCommand
{
    public override string Command => "exp";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/exp <amount>";

    public override string Description => "Grants Exp to yourself.";

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

        // Unmetered: a real source would spend its per-Threshold allowance and change what later kills are worth.
        int granted = experiential.GrantUnmetered(amount);

        caller.Reply($"+{granted} Exp -> Threshold {experiential.Threshold}, {experiential.Exp} / {experiential.Capacity}",
            Color.LightGreen);
    }
}