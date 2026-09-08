using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Common.Commands;

public class DialogueCommand : ModCommand
{
    public override string Command => "dialogue";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/dialogue <play|stop|list|help> [args]";
    public override string Description => "Play, stop, or list dialogue sequences for dev testing.";

    private const string KeyPrefix = "Mods.TrelamiumIII.DialogueLibrary.";
    private const string KeySuffix = ".Line1.Text";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp(caller);
            return;
        }

        switch (args[0].ToLower())
        {
            case "play":
            case "start":
                PlayDialogue(caller, args);
                break;

            case "stop":
            case "end":
                StopDialogue(caller);
                break;

            case "list":
                ListDialogue(caller, args.Length > 1 ? args[1] : null);
                break;

            case "help":
            default:
                ShowHelp(caller);
                break;
        }
    }

    private static void PlayDialogue(CommandCaller caller, string[] args)
    {
        if (args.Length < 2)
        {
            caller.Reply("Usage: /dialogue play <Category.Conversation> [projected]", Color.Yellow);
            return;
        }

        var query = args[1];
        var keys = DiscoverDialogueKeys();

        if (!keys.Contains(query))
        {
            var matches = keys
                .Where(k => k.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count == 1)
            {
                query = matches[0];
            }
            else if (matches.Count > 1)
            {
                caller.Reply($"Ambiguous \"{query}\". Matches:", Color.Yellow);
                foreach (var m in matches)
                    caller.Reply($"  {m}", Color.White);
                return;
            }
            else
            {
                caller.Reply($"No dialogue found for \"{query}\". Try /dialogue list.", Color.Red);
                return;
            }
        }

        bool projected = args.Length > 2 && args[2].StartsWith("proj", StringComparison.OrdinalIgnoreCase);

        bool started = DialogueManager.Instance.StartDialogue(
            query,
            style: projected ? DialogueStyle.Projected : DialogueStyle.Panel,
            anchor: projected ? DialogueAnchor.OnSpeaker(NearestNPC(caller.Player)) : null);

        if (started)
            caller.Reply($"Started: {query}{(projected ? " (projected)" : string.Empty)}", Color.Green);
        else
            caller.Reply($"Failed to start \"{query}\" (missing Line1.Text?).", Color.Red);
    }

    /// <summary>
    ///     The town NPC nearest the caller, so a projected box has something to hang off while it is
    ///     being looked at. Null falls the box back to hovering over the player.
    /// </summary>
    private static NPC NearestNPC(Player player)
    {
        NPC nearest = null;
        float best = 900f * 900f;

        foreach (NPC npc in Main.npc)
        {
            if (!npc.active || !npc.friendly && !npc.townNPC)
                continue;

            float distance = Vector2.DistanceSquared(npc.Center, player.Center);

            if (distance >= best)
                continue;

            best = distance;
            nearest = npc;
        }

        return nearest;
    }

    private static void StopDialogue(CommandCaller caller)
    {
        if (!DialogueManager.Instance.IsAnyActive() && DialogueManager.Instance.GetQueuedDialogueCount() == 0)
        {
            caller.Reply("No dialogue is active.", Color.Yellow);
            return;
        }

        DialogueManager.Instance.ForceEndAll();
        caller.Reply("Dialogue ended.", Color.Green);
    }

    private static void ListDialogue(CommandCaller caller, string filter)
    {
        var keys = DiscoverDialogueKeys();

        if (!string.IsNullOrEmpty(filter))
        {
            keys = keys
                .Where(k => k.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        if (keys.Count == 0)
        {
            caller.Reply(string.IsNullOrEmpty(filter)
                ? "No dialogue keys found."
                : $"No dialogue keys match \"{filter}\".", Color.Yellow);
            return;
        }

        caller.Reply($"Available dialogue ({keys.Count}):", Color.Cyan);

        string lastCategory = null;
        foreach (var key in keys)
        {
            var dot = key.IndexOf('.');
            var category = dot >= 0 ? key.Substring(0, dot) : key;
            if (category != lastCategory)
            {
                caller.Reply($"[{category}]", new Color(255, 255, 100));
                lastCategory = category;
            }
            caller.Reply($"  {key}", Color.White);
        }
    }

    private static void ShowHelp(CommandCaller caller)
    {
        caller.Reply($" [i:{ItemID.Sign}] Dialogue Commands [i:{ItemID.Sign}]", new Color(255, 215, 0));
        caller.Reply("/dialogue play <key> [projected] - Start a dialogue (partial match ok)", Color.White);
        caller.Reply("/dialogue stop         - Force-end the active dialogue", Color.White);
        caller.Reply("/dialogue list [filter]- List dialogue keys, optional substring filter", Color.White);
        caller.Reply("/dialogue help         - Show this help", Color.White);
        caller.Reply("Example: /dialogue play Example.Greeting", new Color(255, 215, 0));
    }

    private static List<string> DiscoverDialogueKeys()
    {
        var found = Language.FindAll((key, text) =>
            key.StartsWith(KeyPrefix) && key.EndsWith(KeySuffix));

        var keys = new List<string>(found.Length);
        foreach (var lt in found)
        {
            var k = lt.Key;
            var inner = k.Substring(KeyPrefix.Length, k.Length - KeyPrefix.Length - KeySuffix.Length);

            // Only top-level Category.Conversation entries (skip any deeper nesting).
            if (inner.Count(c => c == '.') == 1)
                keys.Add(inner);
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return keys;
    }
}
