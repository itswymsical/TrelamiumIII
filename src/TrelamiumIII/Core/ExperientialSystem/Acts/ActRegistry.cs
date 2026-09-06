using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>
/// Yields several acts that differ only by data. Reflection finds every <see cref="Act"/> with a
/// parameterless constructor on its own; a provider is for families - fourteen ore acts that are the
/// same class with a different tile id - where one class each would be fourteen copies of the same file.
/// </summary>
public interface IActProvider
{
    IEnumerable<Act> Provide();
}

/// <summary>
/// Finds every act at load, validates it, and answers "which acts care about this event".
///
/// <para>
/// Acts are bucketed by the event type they listen for. The previous implementation ran
/// <c>SelectMany().Where().ToList()</c> across every registered act on every broadcast, allocating a list
/// per tile break - and tile breaks are the single most frequent event in the game. Matching now touches
/// only the acts that could possibly match and fills a caller-owned buffer.
/// </para>
/// </summary>
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
                // A family class instantiated by a provider takes constructor arguments; reflection
                // cannot build it, and the provider will hand it over anyway.
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

    /// <summary>
    /// Rejects acts that would misbehave silently rather than crash. Progress is compared against
    /// thresholds by index, so an unsorted ladder makes a later tier unreachable and its Exp unpayable -
    /// which is a bug you would only ever notice as "that reward never came".
    /// </summary>
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

        // Sorts act.Ladder, not act.Thresholds: an act that builds its array inline hands back a fresh
        // one on every read, so sorting that would fix nothing.
        for (int i = 1; i < act.Ladder.Length; i++)
        {
            if (act.Ladder[i] > act.Ladder[i - 1])
                continue;

            TrelamiumIII.Instance.Logger.Warn(
                $"Act {act.ID} has thresholds that are not strictly ascending ({string.Join(", ", act.Ladder)}); sorting them in place.");

            Array.Sort(act.Ladder);
            break;
        }

        // Deliberately does not touch act.EventsPerAction. Reading it here runs during Mod.Load, before
        // tile object data is populated, so an act that resolves its divisor from TileObjectData would
        // cache a wrong answer for the session - which is precisely what made one pot count as four.
        // The tracker clamps the divisor at its point of use instead.

        if (act.Members.Length > 1 && act.Members.Distinct().Count() != act.Members.Length)
        {
            // Coverage divides by Members.Length, so a duplicate makes full coverage unreachable.
            TrelamiumIII.Instance.Logger.Warn($"Act '{act.ID}' lists a duplicate member; group coverage can never reach 100%.");
        }

        return true;
    }

    #endregion

    #region Lookup

    public static Act? Get(string? id) => id is not null && ById.TryGetValue(id, out Act? act) ? act : null;

    public static IReadOnlyList<Act> InCategory(ActCategory category)
        => ByCategory.TryGetValue(category, out List<Act>? acts) ? acts : [];

    /// <summary>
    /// Fills <paramref name="into"/> with every act this event advances. The buffer is cleared first and
    /// is expected to be reused by the caller, so a broadcast allocates nothing.
    /// </summary>
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
