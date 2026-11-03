using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Residues.Bodies;

namespace TrelamiumIII.Common.Commands;

/// <summary>Shows a Type's body coat on you and the NPCs around you. Development tool; nothing else surfaces it.</summary>
public class CoatCommand : ModCommand
{
    private const float REACH = 40f * 16f;

    private const int TICKS = 10 * 60;

    /// <summary>The share of a body's height a splash lands on.</summary>
    private const float SPLASH_RADIUS = 0.3f;

    public override string Command => "coat";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/coat <thermal|cryonic|caustic|necrotic|solute> [fill 0..1 | mouse]";

    public override string Description => "Show a Type's coat on you and every NPC within 40 tiles for ten seconds, or with \"mouse\", splash it on whoever's under the cursor. Only draws: it deals nothing.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.dedServ)
        {
            caller.Reply("Body residues are drawn client-side.");

            return;
        }

        if (args.Length == 0 || !Enum.TryParse(args[0], ignoreCase: true, out ChemicalDamageType type) || BodyResidues.Of(type) is not { } residue)
        {
            caller.Reply(Usage, Color.Orange);

            return;
        }

        if (args is [_, "mouse", ..])
        {
            Entity body = NearestToMouse(caller.Player);

            BodyCoatNet.Splash(body, residue, Main.MouseWorld, SPLASH_RADIUS, 1f);
            caller.Reply($"{type} splashed on {(body is NPC npc ? npc.FullName : caller.Player.name)}.");

            return;
        }

        float fill = args.Length > 1 && float.TryParse(args[1], out float parsed) ? MathHelper.Clamp(parsed, 0.05f, 1f) : 0.6f;
        Player player = caller.Player;
        int coated = 0;

        BodyCoatNet.Lay(player, type, fill, TICKS);

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.Distance(player.Center) > REACH)
                continue;

            BodyCoatNet.Lay(npc, type, fill, TICKS);
            coated++;
        }

        caller.Reply($"{type} on you and {coated} NPCs for ten seconds.");
    }

    private static Entity NearestToMouse(Player player)
    {
        Entity nearest = player;
        float distance = player.Distance(Main.MouseWorld);

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.Distance(Main.MouseWorld) < distance)
            {
                nearest = npc;
                distance = npc.Distance(Main.MouseWorld);
            }
        }

        return nearest;
    }
}
