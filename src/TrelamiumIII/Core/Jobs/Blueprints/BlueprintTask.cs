using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.Jobs.Blueprints;

/// <summary>
///     Build the <paramref name="layers"/> of <paramref name="blueprint"/> at its site. Progress is read from the world, so
///     breaking a piece takes it back off the count, and anyone's building counts.
/// </summary>
/// <remarks>
///     Tasks of one job that build the same structure should share one <see cref="Blueprint"/>, so its site is staked
///     once: the frame, then the walls, then the furniture, as stages over the same house. Until the job stakes the site
///     itself, the player does, with <see cref="KeybindsSystem.StakeBlueprintKeybind"/> (<see cref="BlueprintPlayer"/>).
/// </remarks>
public sealed class BlueprintTask(string key, Blueprint blueprint, BlueprintLayer layers = BlueprintLayer.All) : JobTask(key)
{
    /// <summary>Ticks between recounts. A count is cheap, but there's no need for one every tick.</summary>
    private const int CountInterval = 10;

    public Blueprint Blueprint { get; } = blueprint;

    public BlueprintLayer Layers { get; } = layers;

    /// <summary>Says how to stake the site while there isn't one here.</summary>
    public override string Description
    {
        get
        {
            SyncRequirement();

            var text = base.Description;

            return Blueprint.Site is null && !Blueprint.IsFixed ? $"{text} {StakeHint()}" : text;
        }
    }

    public override bool Matches(GameEvent evt) => false;

    public override int Poll(JobContext context)
    {
        if (!context.IsLocal || Main.GameUpdateCount % CountInterval != 0)
            return 0;

        SyncRequirement();

        return Blueprint.Built(Layers) - Count;
    }

    /// <summary>The blueprint tasks of every stage underway for <paramref name="jobs"/>.</summary>
    public static IEnumerable<BlueprintTask> Underway(JobPlayer jobs)
        => jobs.ActiveJobs().SelectMany(job => job.CurrentSet?.Task ?? []).OfType<BlueprintTask>();

    /// <summary>The requirement is the blueprint's piece count, which can't be read until content has loaded.</summary>
    private void SyncRequirement() => RequiredCount = Math.Max(1, Blueprint.Required(Layers));

    private static string StakeHint()
    {
        var key = KeybindsSystem.StakeBlueprintKeybind?.GetAssignedKeys().FirstOrDefault();

        return key is null
            ? TrelamiumIII.Instance.GetLocalization("Jobs.Blueprint.Unbound").Value
            : TrelamiumIII.Instance.GetLocalization("Jobs.Blueprint.Unstaked").Format(key);
    }

    protected override void OnReset() => Blueprint.ClearSite();

    public override void SaveData(TagCompound tag)
    {
        base.SaveData(tag);
        Blueprint.Save(tag);
    }

    public override void LoadData(TagCompound tag)
    {
        // First, since the base clamps the saved count to the requirement.
        SyncRequirement();

        base.LoadData(tag);
        Blueprint.Load(tag);
    }
}
