using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.ExperientialSystem.Skills;

/// <summary>
/// Discovers every <see cref="Skill"/> in the mod at load and keeps one shared instance of each.
/// Follows the same reflection pass the Job and act registries use, so authoring a skill is
/// "write the class" and nothing else.
/// </summary>
public sealed class SkillRegistry : ModSystem
{
    private static readonly Dictionary<string, Skill> ById = [];
    private static readonly List<Skill> All = [];

    public override void Load()
    {
        ById.Clear();
        All.Clear();

        foreach (Type type in Mod.Code.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Skill))))
        {
            try
            {
                if (Activator.CreateInstance(type) is not Skill skill)
                    continue;

                if (ById.TryGetValue(skill.ID, out Skill? existing))
                {
                    // Ids are save keys. A collision would silently hand one skill another's unlock.
                    TrelamiumIII.Instance.Logger.Error(
                        $"Skill id '{skill.ID}' is claimed by both {existing.GetType().Name} and {type.Name}; {type.Name} was dropped.");
                    continue;
                }

                ById[skill.ID] = skill;
                All.Add(skill);
            }
            catch (Exception ex)
            {
                TrelamiumIII.Instance.Logger.Error($"Failed to register skill {type.Name}: {ex.Message}");
            }
        }

        TrelamiumIII.Instance.Logger.Info($"Registered {All.Count} skills");
    }

    public override void Unload()
    {
        ById.Clear();
        All.Clear();
    }

    public static Skill? Get(string? id) => id is not null && ById.TryGetValue(id, out Skill? skill) ? skill : null;

    public static IReadOnlyList<Skill> Skills => All;

    public static IEnumerable<Skill> InGroup(SkillGroup group) => All.Where(s => s.Group == group);

    /// <summary>Skills whose Threshold gate has opened for this player, whether or not they can afford them.</summary>
    public static IEnumerable<Skill> UnlockableAt(int threshold) => All.Where(s => s.RequiredThreshold <= threshold);
}
