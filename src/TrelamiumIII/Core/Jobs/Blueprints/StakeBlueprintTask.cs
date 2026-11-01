using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Jobs.Blueprints;

/// <summary>
///     Pick where <paramref name="blueprint"/> goes. Done once its site is staked in this world, and open again if the
///     site is cleared, so a stage of its own can come before the building ones that share the blueprint.
/// </summary>
/// <remarks>The player stakes it the same way as any other (<see cref="BlueprintPlayer"/>), held to the blueprint's <see cref="Blueprint.Zone"/>.</remarks>
public sealed class StakeBlueprintTask(string key, Blueprint blueprint) : JobTask(key), IBlueprintTask
{
    public Blueprint Blueprint { get; } = blueprint;

    public BlueprintLayer Layers => BlueprintLayer.None;

    public override bool Matches(Events.GameEvent evt) => false;

    public override int Poll(JobContext context) => context.IsLocal ? (Blueprint.Site is null ? 0 : 1) - Count : 0;

    protected override void OnReset() => Blueprint.ClearSite();

    public override void SaveData(TagCompound tag)
    {
        base.SaveData(tag);
        Blueprint.Save(tag);
    }

    public override void LoadData(TagCompound tag)
    {
        base.LoadData(tag);
        Blueprint.Load(tag);
    }
}
