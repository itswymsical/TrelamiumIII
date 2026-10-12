using SubworldLibrary;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;

namespace TrelamiumIII.Common.Commands;

public class RealmCommands : ModCommand
{
    public override string Command => "realm";

    public override CommandType Type => CommandType.Chat | CommandType.World;

    public override string Usage => "/realm [ingress|reverie|treelab] [instant]";

    public override string Description => "teleports you the realm, folk.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string realm = args.Length > 0 ? args[0].ToLowerInvariant() : "ingress";
        bool instant = args.Length > 1 && args[1].Equals("instant", StringComparison.OrdinalIgnoreCase);

        switch (realm)
        {
            case "ingress" when instant:
                SubworldSystem.Enter<IngressRealm>();
                break;
            case "ingress":
                RealmTransition.Enter<IngressRealm>();
                break;
            case "reverie":
                RealmTransition.Enter<ReverieRealm>();
                break;
            case "treelab":
                SubworldSystem.Enter<CanopyTreeLabRealm>();
                break;
            default:
                caller.Reply($"Unknown realm '{args[0]}'. Usage: {Usage}", Color.Red);
                break;
        }
    }
}