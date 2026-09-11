using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.Silvian;

 public sealed class SilvianBotany1 : Job
{
    public SilvianBotany1() : base(
        JobID.SilvianBotany1, "SilvianBotany1.Name", "SilvianBotany1.Description",
        taskList:
        [
            [new TalkToNPCTask("SilvianBotany1.Tasks.Set0.obj0", ModContent.NPCType<Silvian>())],

             [new PredicateTask("SilvianBotany1.Tasks.Set1.obj0",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType is ItemID.Daybloom, 3),
            new PredicateTask("SilvianBotany1.Tasks.Set1.obj1",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType is ItemID.Blinkroot, 2),
            new PredicateTask("SilvianBotany1.Tasks.Set1.obj2",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType is ItemID.Sunflower, 5)],

             [new TalkToNPCTask("SilvianBotany1.Tasks.Set2.obj0", ModContent.NPCType<Silvian>())]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 75),
            new JobReward(ItemID.HerbBag, 2),
            new JobReward("TrelamiumIII/SaplingMaker")
        ],
        providerNPC: ModContent.NPCType<Silvian>())
    { }

    public override void OnJobStart() => Context.Play("Silvian.BotanyJob1_Greeting", zoomIn: true, style: DialogueStyle.Projected);

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Context.Play("Silvian.BotanyJob1_Complete", style: DialogueStyle.Projected);
    }

    //public override ChatEncounter GetChatEncounter(NPC npc, Player player) => CurrentTaskSet switch
    //{
    //    2 => ChatEncounter.Play("Silvian.BotanyJob1_Complete"),
    //    _ => ChatEncounter.None
    //};
}
