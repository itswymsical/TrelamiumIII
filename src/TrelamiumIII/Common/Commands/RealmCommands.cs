using SubworldLibrary;
using System.Linq;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Commands;

public class RealmCommandHelper
{
    public static int FindRealm(string identifier)
    {
        var field = typeof(SubworldRealm).GetFields()
            .FirstOrDefault(f => string.Equals(
                f.Name,
                identifier,
                StringComparison.Ordinal));

        return field != null ? (int)field.GetValue(null) : -1;
    }

}
public class RealmCommands : ModCommand
{
    public override string Command => "realm";

    public override CommandType Type => CommandType.Chat | CommandType.World;

    public override string Usage => "/realm [ingress|treelab]";

    public override string Description => "teleports you the realm, folk.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string realm = args.Length > 0 ? args[0].ToLowerInvariant() : "ingress";

        switch (realm)
        {
            case "ingress":
                SubworldSystem.Enter<IngressRealm>();
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