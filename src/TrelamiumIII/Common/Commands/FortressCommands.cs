using SubworldLibrary;
using System.Linq;
using TrelamiumIII.Common.Generation.Fortress;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Common.Commands;

/// <summary> <c>/fortress</c>: inspects the Ligneous Fortress of the current Ingress generation. Singleplayer only. </summary>
public class FortressCommands : ModCommand
{
    public override string Command => "fortress";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/fortress [info] | goto <gate|arena|altar|1-10> | solve|unsolve <1-10|all> | warden <on|off>";

    public override string Description => "Ligneous Fortress debugging: route, teleports, room seals and the Wooden Warden's defeat flag.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "info";

        switch (verb)
        {
            case "warden":
                bool downed = args.Length > 1 && args[1].Equals("on", StringComparison.OrdinalIgnoreCase);
                DownedBossSystem.DownedWoodenWarden = downed;
                caller.Reply($"Wooden Warden {(downed ? "defeated" : "undefeated")}.", Color.LightGreen);
                return;

            case "solve" or "unsolve" when args.Length > 1:
                SetSolved(caller, args[1], verb == "solve");
                return;
        }

        if (!SubworldSystem.IsActive<IngressRealm>() || ModContent.GetInstance<IngressRealm>().Fortress is not { } fortress)
        {
            caller.Reply("No fortress here; generate Ingress in singleplayer with /realm ingress.", Color.Red);
            return;
        }

        switch (verb)
        {
            case "info":
                string route = string.Join(" ", fortress.Rooms.Select(room => $"{room.Index + 1}:{room.Kind}@{room.Cell.X},{room.Cell.Y}{(FortressProgress.IsSolved(room.Index) ? " solved" : "")}"));
                caller.Reply($"Fortress {fortress.Bounds}; route {route}", Color.LightGreen);
                break;

            case "goto" when args.Length > 1:
                if (Destination(fortress, args[1]) is { } tile)
                    TeleportTo(caller.Player, tile);
                else
                    caller.Reply($"Unknown destination '{args[1]}'. Usage: {Usage}", Color.Red);

                break;

            default:
                caller.Reply($"Usage: {Usage}", Color.Red);
                break;
        }
    }

    /// <summary> Stands in for a room's puzzle: seals or unseals room <paramref name="which"/> (1-based) or every room. </summary>
    private void SetSolved(CommandCaller caller, string which, bool solved)
    {
        if (FortressProgress.RoomCount == 0)
        {
            caller.Reply("No fortress rooms are registered in this world.", Color.Red);
            return;
        }

        if (which.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            for (int room = 0; room < FortressProgress.RoomCount; room++)
                FortressProgress.Solve(room, solved);
        }
        else if (int.TryParse(which, out int number) && number >= 1 && number <= FortressProgress.RoomCount)
        {
            FortressProgress.Solve(number - 1, solved);
        }
        else
        {
            caller.Reply($"Rooms are 1-{FortressProgress.RoomCount} or 'all'.", Color.Red);
            return;
        }

        caller.Reply($"Room {which} {(solved ? "solved" : "sealed")}.", Color.LightGreen);
    }

    /// <summary> The tile the player's feet should occupy, just inside an opening so nothing is fallen far. </summary>
    private static Point? Destination(FortressLayout fortress, string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "gate":
                return fortress.GateApproach;

            case "arena":
                return new Point(fortress.ArenaHatch.Center.X, fortress.Arena.Top + 2);

            case "altar":
                Point altar = FortressBuilder.AltarOrigin(fortress);
                return new Point(altar.X - 4, fortress.Arena.Bottom - 1);
        }

        if (!int.TryParse(name, out int number) || number < 1 || number > fortress.Rooms.Count)
            return null;

        FortressRoom room = fortress.Rooms[number - 1];
        Rectangle interior = room.Interior;

        return room.Entry switch
        {
            RoomSide.Left => new Point(interior.Left + 2, interior.Bottom - 1),
            RoomSide.Right => new Point(interior.Right - 3, interior.Bottom - 1),
            _ => new Point(interior.Center.X, interior.Top + 2)
        };
    }

    private static void TeleportTo(Player player, Point feet)
    {
        var position = new Vector2(feet.X * 16 + 8 - player.width / 2f, (feet.Y + 1) * 16 - player.height);

        player.Teleport(position, TeleportationStyleID.RodOfDiscord);
        player.velocity = Vector2.Zero;
        player.fallStart = feet.Y;
    }
}
