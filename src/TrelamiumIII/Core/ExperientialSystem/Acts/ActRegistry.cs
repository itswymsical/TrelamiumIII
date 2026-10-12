using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>
/// Supplies acts built from data (usually <see cref="DataAct"/>s). Instantiated by reflection; <see cref="Act"/>
/// subclasses with a parameterless constructor are found without one.
/// </summary>
public interface IActProvider
{
    IEnumerable<Act> Provide();
}

/// <summary>Discovers and validates every act at load, and buckets them by <see cref="Act.ListensFor"/> for event matching.</summary>
public sealed class ActRegistry : ModSystem
{
    private static readonly Dictionary<string, Act> ById = [];
    private static readonly Dictionary<ActCategory, List<Act>> ByCategory = [];
    private static readonly Dictionary<GameEventType, List<Act>> ByEvent = [];

    /// <summary>Acts that match on their own logic rather than a single event type, so must be tested against everything.</summary>
    private static readonly List<Act> Unbucketed = [];

    private static readonly List<Act> All = [];

    public static IReadOnlyList<Act> Acts => All;

    public override void Load()
    {
        Clear();

        foreach (Act act in Discover())
            Register(act);

        TrelamiumIII.Instance.Logger.Info($"Registered {All.Count} experiential acts");
    }

    public override void Unload()
    {
        Clear();
        ActUnits.ClearCache();
    }

    private static void Clear()
    {
        ById.Clear();
        ByCategory.Clear();
        ByEvent.Clear();
        Unbucketed.Clear();
        All.Clear();
    }

    #region Discovery

    private IEnumerable<Act> Discover()
    {
        foreach (Type type in Mod.Code.GetTypes().Where(t => !t.IsAbstract))
        {
            if (type.IsSubclassOf(typeof(Act)))
            {
                // Acts with constructor arguments come from providers.
                if (type.GetConstructor(Type.EmptyTypes) is null)
                    continue;

                foreach (Act act in TryCreate(type))
                    yield return act;

                continue;
            }

            if (typeof(IActProvider).IsAssignableFrom(type))
            {
                foreach (Act act in TryProvide(type))
                    yield return act;
            }
        }
    }

    private static IEnumerable<Act> TryCreate(Type type)
    {
        Act? act = null;

        try
        {
            act = Activator.CreateInstance(type) as Act;
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance.Logger.Error($"Failed to create act {type.Name}: {ex.Message}");
        }

        if (act is not null)
            yield return act;
    }

    private static IEnumerable<Act> TryProvide(Type type)
    {
        List<Act> provided = [];

        try
        {
            if (Activator.CreateInstance(type) is IActProvider provider)
                provided.AddRange(provider.Provide());
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance.Logger.Error($"Act provider {type.Name} failed: {ex.Message}");
        }

        return provided;
    }

    #endregion

    #region Registration

    public static void Register(Act act)
    {
        if (!Validate(act))
            return;

        ById[act.ID] = act;
        All.Add(act);

        if (!ByCategory.TryGetValue(act.Category, out List<Act>? category))
            ByCategory[act.Category] = category = [];

        category.Add(act);

        if (act.ListensFor is GameEventType listens)
        {
            if (!ByEvent.TryGetValue(listens, out List<Act>? bucket))
                ByEvent[listens] = bucket = [];

            bucket.Add(act);
        }
        else
        {
            Unbucketed.Add(act);
        }
    }

    /// <summary>Drops acts with no id, a duplicate id or no thresholds; sorts an unsorted ladder; warns on duplicate members.</summary>
    private static bool Validate(Act act)
    {
        if (string.IsNullOrWhiteSpace(act.ID))
        {
            TrelamiumIII.Instance.Logger.Error($"Act {act.GetType().Name} has no ID; dropped.");
            return false;
        }

        if (ById.TryGetValue(act.ID, out Act? existing))
        {
            // Ids are save keys and Exp source keys. A collision merges two acts' progress into one.
            TrelamiumIII.Instance.Logger.Error(
                $"Act id '{act.ID}' is claimed by both {existing.GetType().Name} and {act.GetType().Name}; the latter was dropped.");
            return false;
        }

        if (act.Ladder is not { Length: > 0 })
        {
            TrelamiumIII.Instance.Logger.Error($"Act '{act.ID}' has no thresholds; dropped.");
            return false;
        }

        // Sorts act.Ladder, the snapshot: Thresholds may return a fresh array per read.
        for (int i = 1; i < act.Ladder.Length; i++)
        {
            if (act.Ladder[i] > act.Ladder[i - 1])
                continue;

            TrelamiumIII.Instance.Logger.Warn(
                $"Act {act.ID} has thresholds that are not strictly ascending ({string.Join(", ", act.Ladder)}); sorting them in place.");

            Array.Sort(act.Ladder);
            break;
        }

        // EventsPerAction is not read here: TileObjectData is not populated during Mod.Load.

        if (act.Members.Length > 1 && act.Members.Distinct().Count() != act.Members.Length)
        {
            TrelamiumIII.Instance.Logger.Warn($"Act '{act.ID}' lists a duplicate member; group coverage can never reach 100%.");
        }

        return true;
    }

    #endregion

    #region Lookup

    public static Act? Get(string? id) => id is not null && ById.TryGetValue(id, out Act? act) ? act : null;

    public static IReadOnlyList<Act> InCategory(ActCategory category)
        => ByCategory.TryGetValue(category, out List<Act>? acts) ? acts : [];

    /// <summary>Clears <paramref name="into"/> and fills it with every act this event advances.</summary>
    public static void CollectMatches(in GameEvent evt, List<Act> into)
    {
        into.Clear();

        if (ByEvent.TryGetValue(evt.Type, out List<Act>? bucket))
        {
            foreach (Act act in bucket)
            {
                if (act.Matches(evt))
                    into.Add(act);
            }
        }

        foreach (Act act in Unbucketed)
        {
            if (act.Matches(evt))
                into.Add(act);
        }
    }

    #endregion
}
