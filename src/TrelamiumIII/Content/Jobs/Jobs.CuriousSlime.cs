using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;

namespace TrelamiumIII.Content.Jobs;

public sealed class CuriousSlimeI : Job
{
    public CuriousSlimeI() :
        base(JobID.CuriousSlime1, "CuriousSlime1.Name", "CuriousSlime1.Description",
            taskList: [
            [("CuriousSlime1.Tasks.Set0.obj0", 1)],
            [("CuriousSlime1.Tasks.Set1.obj0", 1)],
                [
                    ("CuriousSlime1.Tasks.Set2.obj0", 1),
                    ("CuriousSlime1.Tasks.Set2.obj1", 1),
                    ("CuriousSlime1.Tasks.Set2.obj2", 1),
                ],
            [("CuriousSlime1.Tasks.Set3.obj0", 1)],
        ],
        rewards: [
            new JobReward(ItemID.SilverCoin, 75)
        ],
        NPCID.BlueSlime)
    {

    }

    public override void OnJobStart()
    {
        base.OnJobStart();
        DialogueManager.Instance.StartDialogue("CuriousSlime.Greeting", true, true);

    }

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        // Set 0, Task 0: Talk to NPC
        if (currentSet == 0 && taskIndex == 0)
        {
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC);
        }

        //if (currentSet == 0 && taskIndex == 0)
        //{
        //    return evt.Type == GameEventType.ItemPickup
        //        && evt.ItemType.HasValue && (evt.ItemType == ModContent.ItemType<PuffballItem>());
        //}

        return false;
    }
}
