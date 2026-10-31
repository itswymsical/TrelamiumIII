using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Blueprints;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
///     The Guide's plans for a townhome (<c>Assets/Structures/townhome0</c>): the frame, then the walls, then the
///     furniture, each a stage over the one blueprint, and the player picks the site. A sample of
///     <see cref="BlueprintTask"/>: nothing unlocks it yet, so it's reached with <c>/job unlock Townhome</c>.
/// </summary>
public sealed class Townhome : Job
{
    public Townhome() : this(new Blueprint("Assets/Structures/townhome0")) { }

    /// <param name="plans">Shared by the three building stages, so the site is staked once.</param>
    private Townhome(Blueprint plans) : base(
        JobID.Townhome, "Townhome.Name", "Townhome.Description",
        taskList:
        [
            [new BlueprintTask("Townhome.Tasks.Set0.obj0", plans, BlueprintLayer.Blocks)],
            [new BlueprintTask("Townhome.Tasks.Set1.obj0", plans, BlueprintLayer.Walls)],
            [new BlueprintTask("Townhome.Tasks.Set2.obj0", plans, BlueprintLayer.Furniture)],
            [new TalkToNPCTask("Townhome.Tasks.Set3.obj0", NPCID.Guide)]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 50)
        ],
        providerNPC: NPCID.Guide)
    { }
}
