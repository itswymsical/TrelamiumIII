using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.ExperientialSystem.Skills;

/// <summary>
/// Yields several skills that differ only by data, for families where one class each would be the same
/// file copied. Reflection finds every parameterless <see cref="Skill"/> on its own.
/// </summary>
public interface ISkillProvider
{
    IEnumerable<Skill> Provide();
}

/// <summary>
/// Finds every skill at load, validates it, and answers which skills a Threshold has opened.
/// Skills are grouped by <see cref="Skill.UnlockThreshold"/>, so the ladder the player climbs and the
/// order the UI lists are the same thing.
/// </summary>
public sealed class SkillRegistry : ModSystem
{
    private static readonly Dictionary<string, Skill> ById = [];
    private static readonly Dictionary<Type, Skill> ByType = [];
    private static readonly Dictionary<SkillGroup, List<Skill>> ByGroup = [];
    private static readonly Dictionary<int, List<Skill>> ByThreshold = [];

    private static readonly List<Skill> All = [];

    /// <summary>Thresholds that open at least one skill, ascending.</summary>
    private static int[] _bands = [];

    public override void Load()
    {
        Clear();

        foreach (Skill skill in Discover())
            Register(skill);

        Index();

        TrelamiumIII.Instance.Logger.Info(
            $"Registered {All.Count} skills across {_bands.Length} Threshold bands " +
            $"(first at {ExperientialConstants.FirstSkillThreshold})");
    }

    public override void Unload() => Clear();

    private static void Clear()
    {
        ById.Clear();
        ByType.Clear();
        ByGroup.Clear();
        ByThreshold.Clear();
        All.Clear();

        _bands = [];
    }

    #region Discovery

    private IEnumerable<Skill> Discover()
    {
        foreach (Type type in Mod.Code.GetTypes().Where(t => !t.IsAbstract))
        {
            if (type.IsSubclassOf(typeof(Skill)))
            {
                // A family class built by a provider takes constructor arguments; reflection cannot
                // build it, and the provider hands it over anyway.
                if (type.GetConstructor(Type.EmptyTypes) is null)
                    continue;

                foreach (Skill skill in TryCreate(type))
                    yield return skill;

                continue;
            }

            if (typeof(ISkillProvider).IsAssignableFrom(type))
            {
                foreach (Skill skill in TryProvide(type))
                    yield return skill;
            }
        }
    }

    private static IEnumerable<Skill> TryCreate(Type type)
    {
        Skill? skill = null;

        try
        {
            skill = Activator.CreateInstance(type) as Skill;
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance.Logger.Error($"Failed to create skill {type.Name}: {ex.Message}");
        }

        if (skill is not null)
            yield return skill;
    }

    private static IEnumerable<Skill> TryProvide(Type type)
    {
        List<Skill> provided = [];

        try
        {
            if (Activator.CreateInstance(type) is ISkillProvider provider)
                provided.AddRange(provider.Provide());
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance.Logger.Error($"Skill provider {type.Name} failed: {ex.Message}");
        }

        return provided;
    }

    #endregion

    #region Registration

    public static void Register(Skill skill)
    {
        if (!Validate(skill))
            return;

        ById[skill.ID] = skill;
        All.Add(skill);

        // Only the first of a provided family is reachable by type; Get<T> is for one-off skills whose
        // class is the identity, and a family's members are told apart by id.
        ByType.TryAdd(skill.GetType(), skill);
    }

    /// <summary>Rejects skills that would misbehave silently rather than crash.</summary>
    private static bool Validate(Skill skill)
    {
        if (string.IsNullOrWhiteSpace(skill.ID))
        {
            TrelamiumIII.Instance.Logger.Error($"Skill {skill.GetType().Name} has no ID; dropped.");
            return false;
        }

        if (ById.TryGetValue(skill.ID, out Skill? existing))
        {
            // Ids are save keys. A collision would silently hand one skill another's unlock.
            TrelamiumIII.Instance.Logger.Error(
                $"Skill id '{skill.ID}' is claimed by both {existing.GetType().Name} and {skill.GetType().Name}; the latter was dropped.");
            return false;
        }

        if (skill.RequiredThreshold > ExperientialConstants.MaxThreshold)
        {
            // SkillGate clamps to the top of the curve, so this would be buyable at max Threshold
            // rather than never - which is not what a gate past the end of the curve was meant to say.
            TrelamiumIII.Instance.Logger.Error(
                $"Skill '{skill.ID}' is gated at Threshold {skill.RequiredThreshold}, past the last one " +
                $"({ExperientialConstants.MaxThreshold}); dropped.");
            return false;
        }

        if (skill.Cost < 0)
        {
            TrelamiumIII.Instance.Logger.Error($"Skill '{skill.ID}' costs {skill.Cost} Exp; dropped.");
            return false;
        }

        if (skill.Cost > ExperientialConstants.Capacity(skill.UnlockThreshold))
        {
            // Affordable only after ascending further, which makes the stated gate a lie.
            TrelamiumIII.Instance.Logger.Warn(
                $"Skill '{skill.ID}' costs {skill.Cost} Exp but the meter at Threshold {skill.UnlockThreshold} " +
                $"only holds {ExperientialConstants.Capacity(skill.UnlockThreshold)}; it cannot be bought on the Threshold that opens it.");
        }

        return true;
    }

    /// <summary>
    /// Sorts the registry into the order the UI reads it in and fills the group and Threshold buckets.
    /// Runs once after discovery, since sort order depends on the whole set.
    /// </summary>
    private static void Index()
    {
        All.Sort(static (a, b) =>
        {
            int band = a.UnlockThreshold.CompareTo(b.UnlockThreshold);
            if (band != 0)
                return band;

            int group = a.Group.CompareTo(b.Group);
            if (group != 0)
                return group;

            int cost = a.Cost.CompareTo(b.Cost);

            // Falls through to the id so the order is stable: reflection does not promise a type order,
            // and two skills that tie on every authored field must still list the same way each load.
            return cost != 0 ? cost : string.CompareOrdinal(a.ID, b.ID);
        });

        foreach (Skill skill in All)
        {
            if (!ByGroup.TryGetValue(skill.Group, out List<Skill>? group))
                ByGroup[skill.Group] = group = [];

            group.Add(skill);

            if (!ByThreshold.TryGetValue(skill.UnlockThreshold, out List<Skill>? band))
                ByThreshold[skill.UnlockThreshold] = band = [];

            band.Add(skill);
        }

        _bands = [.. ByThreshold.Keys.Order()];
    }

    #endregion

    #region Lookup

    public static IReadOnlyList<Skill> Skills => All;

    /// <summary>Thresholds that open at least one skill, ascending.</summary>
    public static ReadOnlySpan<int> Bands => _bands;

    public static Skill? Get(string? id) => id is not null && ById.TryGetValue(id, out Skill? skill) ? skill : null;

    /// <summary>The shared instance of a one-off skill. Null for a skill class a provider yields several of.</summary>
    public static T? Get<T>() where T : Skill => ByType.TryGetValue(typeof(T), out Skill? skill) ? skill as T : null;

    public static IReadOnlyList<Skill> InGroup(SkillGroup group)
        => ByGroup.TryGetValue(group, out List<Skill>? skills) ? skills : [];

    /// <summary>Skills whose gate opens exactly at <paramref name="threshold"/>, in list order.</summary>
    public static IReadOnlyList<Skill> AtThreshold(int threshold)
        => ByThreshold.TryGetValue(threshold, out List<Skill>? skills) ? skills : [];

    /// <summary>Skills whose gate has opened for this player, whether or not they can afford them.</summary>
    public static IEnumerable<Skill> UnlockableAt(int threshold) => All.Where(s => s.UnlockThreshold <= threshold);

    /// <summary>Lowest Threshold past <paramref name="threshold"/> that opens a skill, or -1 if none is left.</summary>
    public static int NextBandAfter(int threshold)
    {
        foreach (int band in _bands)
        {
            if (band > threshold)
                return band;
        }

        return -1;
    }

    #endregion
}
