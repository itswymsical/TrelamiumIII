using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrelamiumIII.Core.Graphics.Cinematics;

namespace TrelamiumIII.Common.Commands;

public class CutsceneStartCommand : ModCommand
{
    public override string Command => "startc";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/startc <cutscene name>  |  /startc list";
    public override string Description => "Starts a cutscene by name.";

    // Name (lowercase) -> concrete Cutscene subtype
    private static Dictionary<string, Type> _registry;

    public override void Load()
    {
        _registry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // Scan every assembly the mod loaded into the domain, covers the mod itself
        // and anything it has loaded via tML's dependency resolution.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            IEnumerable<Type> types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null); }

            foreach (var type in types)
            {
                if (type.IsAbstract) continue;
                if (!type.IsSubclassOf(typeof(Cutscene))) continue;
                if (type.GetConstructor(System.Type.EmptyTypes) == null) continue; // needs new()

                _registry[type.Name] = type;
            }
        }
    }

    public override void Unload()
    {
        _registry = null;
    }

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 0)
        {
            caller.Reply($"Usage: {Usage}", Color.Yellow);
            return;
        }

        // /startc list
        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            if (_registry.Count == 0)
            {
                caller.Reply("No cutscenes registered.", Color.Yellow);
                return;
            }

            caller.Reply($"Available cutscenes ({_registry.Count}):", Color.Cyan);
            foreach (var name in _registry.Keys.OrderBy(k => k))
                caller.Reply($"  {name}", Color.White);

            return;
        }

        // /startc <name>
        string query = args[0];

        if (!_registry.TryGetValue(query, out Type cutsceneType))
        {
            // Partial match fallback, helpful when tab-complete isn't available
            var matches = _registry.Keys
                .Where(k => k.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count == 1)
            {
                cutsceneType = _registry[matches[0]];
            }
            else if (matches.Count > 1)
            {
                caller.Reply($"Ambiguous name \"{query}\". Matches:", Color.Yellow);
                foreach (var m in matches)
                    caller.Reply($"  {m}", Color.White);
                return;
            }
            else
            {
                caller.Reply($"No cutscene found matching \"{query}\". Use /startc list to see all.", Color.Red);
                return;
            }
        }

        try
        {
            MethodInfo play = typeof(CutsceneSystem).GetMethod(nameof(CutsceneSystem.PlayCutscene));
            MethodInfo generic = play.MakeGenericMethod(cutsceneType);
            generic.Invoke(null, null);

            caller.Reply($"Started: {cutsceneType.Name}", Color.Green);
        }
        catch (Exception ex)
        {
            caller.Reply($"Failed to start \"{cutsceneType.Name}\": {ex.InnerException?.Message ?? ex.Message}", Color.Red);
            ModContent.GetInstance<TrelamiumIII>().Logger.Error($"CutsceneStartCommand error: {ex}");
        }
    }
}
