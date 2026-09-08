using System.Collections.Generic;
using TrelamiumIII.Content.NPCs.Testing;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.NPCs.Combat;

namespace TrelamiumIII.Common.Commands;

/// <summary>
/// Spawns and drives <see cref="TestTerrarian"/>. Development tool for checking the
/// puppet animation and director modules without waiting on natural spawns.
/// </summary>
public class WorldNPCCommand : ModCommand
{
    public override string Command => "wnpc";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/wnpc [spawn|mode <name>|anchor|clear|list|hitbox]";

    public override string Description => "Spawn and drive world NPC test subjects.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string action = args.Length > 0 ? args[0].ToLowerInvariant() : "spawn";

        // Purely a local render toggle, so it runs before the ownership check.
        if (action == "hitbox")
        {
            PuppetDebug.ShowHitboxes = !PuppetDebug.ShowHitboxes;
            caller.Reply(
                PuppetDebug.ShowHitboxes
                    ? "Hitboxes on. Blue is the NPC, red is its swing, orange is a projectile it owns."
                    : "Hitboxes off.",
                Color.LightGreen);
            return;
        }

        // NPC spawning and AI are the server's; a client-side spawn would appear and
        // then evaporate on the next sync.
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            caller.Reply("World NPCs are server-owned; run this in singleplayer.", Color.Red);
            return;
        }

        switch (action)
        {
            case "spawn":
                Spawn(caller);
                break;

            case "mode":
                SetMode(caller, args);
                break;

            case "anchor":
                Anchor(caller);
                break;

            case "clear":
                Clear(caller);
                break;

            case "list":
                caller.Reply("Modes: " + string.Join(", ", Enum.GetNames<TestTerrarian.TestMode>()), Color.LightGreen);
                break;

            default:
                caller.Reply($"Unknown action '{action}'. {Usage}", Color.Red);
                break;
        }
    }

    private static void Spawn(CommandCaller caller)
    {
        Vector2 position = caller.Player.Bottom + new Vector2(caller.Player.direction * 48f, 0f);
        TestTerrarian subject = WorldNPC.Spawn<TestTerrarian>(position);

        if (subject == null)
        {
            caller.Reply("No free NPC slot.", Color.Red);
            return;
        }

        caller.Reply(
            $"Spawned {subject.NPC.GivenName} at tile {subject.AnchorTile.X}, {subject.AnchorTile.Y}. " +
            "Talk to it to cycle modes, or use /wnpc mode <name>.",
            Color.LightGreen);
    }

    private static void SetMode(CommandCaller caller, string[] args)
    {
        if (args.Length < 2)
        {
            caller.Reply("Usage: /wnpc mode <name>. " + string.Join(", ", Enum.GetNames<TestTerrarian.TestMode>()), Color.Red);
            return;
        }

        if (!Enum.TryParse(args[1], ignoreCase: true, out TestTerrarian.TestMode mode))
        {
            caller.Reply($"Unknown mode '{args[1]}'. Try /wnpc list.", Color.Red);
            return;
        }

        int count = 0;
        foreach (TestTerrarian subject in Subjects())
        {
            subject.Mode = mode;
            count++;
        }

        caller.Reply(
            count == 0 ? "No test subjects in the world. Try /wnpc spawn." : $"Set {count} subject(s) to {mode}.",
            count == 0 ? Color.Red : Color.LightGreen);
    }

    private static void Anchor(CommandCaller caller)
    {
        int count = 0;
        foreach (TestTerrarian subject in Subjects())
        {
            subject.SetAnchorTile(subject.NPC.Center);
            count++;
        }

        caller.Reply($"Re-anchored {count} subject(s) where they stand.", Color.LightGreen);
    }

    private static void Clear(CommandCaller caller)
    {
        int count = 0;
        foreach (TestTerrarian subject in Subjects())
        {
            subject.NPC.active = false;
            subject.NPC.netSkip = -1;
            subject.NPC.life = 0;
            count++;
        }

        caller.Reply($"Removed {count} subject(s).", Color.LightGreen);
    }

    /// <summary>
    /// Materialised into a list rather than yielded: Main.ActiveNPCs hands back a ref
    /// struct enumerator, which cannot cross a yield boundary.
    /// </summary>
    private static List<TestTerrarian> Subjects()
    {
        List<TestTerrarian> subjects = [];

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is TestTerrarian subject)
                subjects.Add(subject);
        }

        return subjects;
    }
}
