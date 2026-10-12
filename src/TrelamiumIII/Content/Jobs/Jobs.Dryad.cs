using SubworldLibrary;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Dryad;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
///     The Dryad's first job, and the spine of the Ingress arc: into Ingress, past the Wooden Warden, and on to the
///     Library of Reverie. Offered a day after she moves in (<see cref="DryadArrivalSystem.Settled"/>). Taking it
///     opens <see cref="IngressRift"/> by the world spawn, which stays open for good.
/// </summary>
/// <remarks>
///     Every stage is a place reached or a boss downed rather than an event, so the stages are polled from
///     <see cref="Update"/>. That also catches up a player who got there before taking the job. The job never takes
///     over the Dryad's chat, since it stays active for a long stretch and her shop has to stay reachable.
/// </remarks>
public sealed class CrossingOver : Job
{
    public CrossingOver() : base(
        JobID.CrossingOver, "CrossingOver.Name", "CrossingOver.Description",
        taskList:
        [
            [new MilestoneTask("CrossingOver.Tasks.Set0.obj0", () => SubworldSystem.IsActive<IngressRealm>())],
            [new MilestoneTask("CrossingOver.Tasks.Set1.obj0", () => DownedBossSystem.DownedWoodenWarden)],
            [new MilestoneTask("CrossingOver.Tasks.Set2.obj0", () => SubworldSystem.IsActive<ReverieRealm>())]
        ],
        rewards:
        [
            new JobReward(ItemID.GoldCoin, 2)
        ],
        providerNPC: NPCID.Dryad)
    { }

    public override JobCondition UnlockCondition => JobCondition.World(() => DryadArrivalSystem.Settled);

    public override void OnJobStart() => Context.Play("Dryad.CrossingOver_Greeting", zoomIn: true, style: DialogueStyle.Projected);

    public override void Update()
    {
        base.Update();

        if (State == JobState.Active && Context.IsLocal && CurrentSet?.Task[0] is MilestoneTask { IsCompleted: false, IsReached: true })
            UpdateProgress(0, 1);
    }
}

/// <summary>
///     Done once the world or the player reaches a state no event announces: a realm entered, a boss downed. The job
///     that owns it polls <see cref="IsReached"/> and credits it.
/// </summary>
public sealed class MilestoneTask(string key, Func<bool> reached) : JobTask(key)
{
    public bool IsReached => reached();

    public override bool Matches(GameEvent evt) => false;
}
