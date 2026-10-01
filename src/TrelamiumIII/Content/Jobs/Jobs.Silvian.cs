using TrelamiumIII.Content.Items.Alchemy.MagnoliaContent;
using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

public sealed class HerbalForager : Job
{
    public HerbalForager() : base(
        JobID.HerbalForager, "HerbalForager.Name", "HerbalForager.Description",
        taskList:
        [
            [new TalkToNPCTask("HerbalForager.Tasks.Set0.obj0", ModContent.NPCType<Silvian>())],

             [new PredicateTask("HerbalForager.Tasks.Set1.obj0",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType is ItemID.Daybloom, 3),
            new PredicateTask("HerbalForager.Tasks.Set1.obj1",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType is ItemID.Blinkroot, 2),
            new PredicateTask("HerbalForager.Tasks.Set1.obj2",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType == ModContent.ItemType<MagnoliaItem>(), 5)],

             [new TalkToNPCTask("HerbalForager.Tasks.Set2.obj0", ModContent.NPCType<Silvian>())]
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
