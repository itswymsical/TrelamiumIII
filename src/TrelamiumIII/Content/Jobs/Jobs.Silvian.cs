using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

public sealed class HerbalForager : Job
{
    public HerbalForager() : base(
        JobID.HerbalForager, "HerbalForager.Name", "HerbalForager.Description",
        taskList:
        [
            [new TalkToNPCTask("HerbalForager.Tasks.Set0.obj0", ModContent.NPCType<Silvian>())],

            [
                new GatherItemTask("HerbalForager.Tasks.Set1.obj0", ItemID.Daybloom, 3),
                new GatherItemTask("HerbalForager.Tasks.Set1.obj1", ItemID.Blinkroot, 2),
                new GatherItemTask("HerbalForager.Tasks.Set1.obj2", ModContent.ItemType<MagnoliaItem>(), 5),
                new HandInTask("HerbalForager.Tasks.Set1.obj3", promptKey: "Silvian.HerbalForager_HandIn")
            ]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 75),
            new JobReward(ItemID.HerbBag, 2),
            new JobReward("TrelamiumIII/SaplingMaker")
        ],
        providerNPC: ModContent.NPCType<Silvian>())
    { }

    public override void OnJobStart() => Context.Play("Silvian.HerbalForager_Greeting", zoomIn: true, style: DialogueStyle.Projected);

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Context.Play("Silvian.HerbalForager_Complete", style: DialogueStyle.Projected);

        // Queued behind the line above. Only fires while this is the last job of his line (SilvianAlmanac.HandoffJob).
        SilvianAlmanac.OfferIfDue(Context);
    }
}
