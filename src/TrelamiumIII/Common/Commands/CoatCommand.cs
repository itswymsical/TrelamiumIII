using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Residues.Bodies;

namespace TrelamiumIII.Common.Commands;

/// <summary>Shows a Type's body coat on you and the NPCs around you. Development tool; nothing else surfaces it.</summary>
public class CoatCommand : ModCommand
{
    private const float REACH = 40f * 16f;

    private const int TICKS = 10 * 60;

    public override string Command => "coat";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/coat <thermal|cryonic|caustic|necrotic|solute> [fill 0..1]";

    public override string Description => "Show a Type's coat on you and every NPC within 40 tiles for ten seconds. Only draws: it deals nothing.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.dedServ)
        {
            caller.Reply("Body residues are drawn client-side.");

            return;
        }

        if (args.Length == 0 || !Enum.TryParse(args[0], ignoreCase: true, out ChemicalDamageType type) || BodyResidues.Of(type) is null)
        {
            caller.Reply(Usage, Color.Orange);

            return;
        }

        float fill = args.Length > 1 && float.TryParse(args[1], out float parsed) ? MathHelper.Clamp(parsed, 0.05f, 1f) : 0.6f;
        Player player = caller.Player;
        int coated = 0;

        player.GetModPlayer<BodyCoatPlayer>().Coats.Lay(type, fill, TICKS);

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.Distance(player.Center) > REACH)
                continue;

            npc.GetGlobalNPC<BodyCoatNPC>().Coats.Lay(type, fill, TICKS);
            coated++;
        }

        caller.Reply($"{type} on you and {coated} NPCs for ten seconds.");
    }
}
