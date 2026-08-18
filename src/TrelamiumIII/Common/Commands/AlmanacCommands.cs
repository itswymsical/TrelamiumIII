using TrelamiumIII.Common.Systems.Alchemy;
using System.Collections.Generic;

namespace TrelamiumIII.Common.Commands;

/// <summary>
/// Dev toggle for Almanac discovery state, so entries can be reviewed and balanced without brewing
/// every product first. Drives the same two sets the Almanac's indexes read from
/// (<see cref="AlmanacDiscoverySystem"/>), over exactly the types those indexes list.
/// </summary>
public class AlmanacCommand : ModCommand
{
    public override string Command => "almanac";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/almanac <unlock|lock> [recipes|ingredients]";
    public override string Description => "Unlocks or locks all Almanac entries (singleplayer)";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        // Discovery is world data the server owns and only pushes to clients on join, so a
        // client-side edit would show in the UI and then evaporate. Refusing beats looking like
        // it worked.
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            caller.Reply("Almanac discovery is world data; run this in singleplayer.", Color.Red);
            return;
        }

        if (args.Length < 1)
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        bool unlock;
        switch (args[0].ToLowerInvariant())
        {
            case "unlock": unlock = true; break;
            case "lock": unlock = false; break;
            default:
                caller.Reply($"Unknown mode '{args[0]}'. Usage: {Usage}", Color.Red);
                return;
        }

        string scope = args.Length > 1 ? args[1].ToLowerInvariant() : "all";
        bool doRecipes = scope is "all" or "recipes";
        bool doIngredients = scope is "all" or "ingredients";

        if (!doRecipes && !doIngredients)
        {
            caller.Reply($"Unknown scope '{args[1]}'. Usage: {Usage}", Color.Red);
            return;
        }

        var parts = new List<string>();

        if (doRecipes)
        {
            int n = unlock ? AlmanacDiscoverySystem.UnlockAllRecipes()
                           : AlmanacDiscoverySystem.LockAllRecipes();
            parts.Add($"{n} recipe{(n == 1 ? "" : "s")}");
        }

        if (doIngredients)
        {
            int n = unlock ? AlmanacDiscoverySystem.UnlockAllIngredients()
                           : AlmanacDiscoverySystem.LockAllIngredients();
            parts.Add($"{n} ingredient{(n == 1 ? "" : "s")}");
        }

        caller.Reply($"{(unlock ? "Unlocked" : "Locked")} {string.Join(" and ", parts)}.",
            unlock ? Color.GreenYellow : Color.Orange);
    }
}
