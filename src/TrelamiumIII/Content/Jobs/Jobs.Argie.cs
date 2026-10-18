using System.Linq;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Items.Quest;
using TrelamiumIII.Content.NPCs.WorldNPCs.Argie;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
///     Argie's first job, offered at the end of meeting her: fungi from outside the grotto to compare hers against. Fly
///     agaric from surface grass, puffballs off Scrunglepuffs, dewdrop bonnets from the clouds of floating islands. Ends
///     on the spore survey offer.
/// </summary>
public sealed class MycologyHunt : Job
{
    private const int PuffballTask = 1;

    public MycologyHunt() : base(
        JobID.MycologyHunt, "MycologyHunt.Name", "MycologyHunt.Description",
        taskList:
        [
            [
                new GatherItemTask("MycologyHunt.Tasks.Set0.obj0", ModContent.ItemType<FlyAgaricItem>(), 5),
                new GatherItemTask("MycologyHunt.Tasks.Set0.obj1", ModContent.ItemType<PuffballItem>(), 3),
                new GatherItemTask("MycologyHunt.Tasks.Set0.obj2", ModContent.ItemType<DewdropBonnetItem>(), 3),
                new HandInTask("MycologyHunt.Tasks.Set0.obj3", promptKey: "Argie.MycologyHunt_HandIn")
            ]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 60),
            new JobReward(ItemID.MushroomGrassSeeds, 3),
            new JobReward(ItemID.SpelunkerPotion, 2)
        ],
        providerNPC: ModContent.NPCType<ArgieNPC>())
    { }

    /// <summary>The job is underway and its puffballs aren't held yet, so Scrunglepuffs turn up more often.</summary>
    public static bool WantsPuffballs(Player player)
    {
        Job? job = player.GetModPlayer<JobPlayer>().GetAllJobs().FirstOrDefault(j => j.ID == JobID.MycologyHunt);

        return job is { State: JobState.Active, CurrentSet: { } set } && !set.Task[PuffballTask].IsCompleted;
    }

    public override void OnJobStart() => Context.Play("Argie.MycologyHunt_Greeting", zoomIn: true, style: DialogueStyle.Projected);

    /// <summary>The closing lines offer <see cref="SporeSurvey" />.</summary>
    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Context.Play("Argie.MycologyHunt_Complete", style: DialogueStyle.Projected);
    }
}

/// <summary>
///     Argie's second job: spore samples from the grotto's creatures, to find what is moving under it. She makes the
///     <see cref="Fungore.MyconidSpore" /> from them, which is how Fungore is found.
/// </summary>
public sealed class SporeSurvey : Job
{
    public SporeSurvey() : base(
        JobID.SporeSurvey, "SporeSurvey.Name", "SporeSurvey.Description",
        taskList:
        [
            [
                new GatherItemTask("SporeSurvey.Tasks.Set0.obj0", ModContent.ItemType<SporeSample>(), 10),
                new HandInTask("SporeSurvey.Tasks.Set0.obj1", promptKey: "Argie.SporeSurvey_HandIn")
            ]
        ],
        rewards:
        [
            new JobReward("TrelamiumIII/MyconidSpore"),
            new JobReward(ItemID.SilverCoin, 50)
        ],
        providerNPC: ModContent.NPCType<ArgieNPC>())
    { }

    public override void OnJobStart() => Context.Play("Argie.SporeSurvey_Greeting", zoomIn: true, style: DialogueStyle.Projected);

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Context.Play("Argie.SporeSurvey_Complete", style: DialogueStyle.Projected);
    }
}
