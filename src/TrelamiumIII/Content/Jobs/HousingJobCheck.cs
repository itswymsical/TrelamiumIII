using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;

namespace TrelamiumIII.Content.Jobs;

public sealed class HousingJobCheck : Job
{
    public HousingJobCheck() :
        base(999, "NerdySlimeI.Name", "NerdySlimeI.Description",
        taskList: 
        [
            [("NerdySlimeI.Tasks.Set0.obj0", 1)],
            [("NerdySlimeI.Tasks.Set1.obj0", 2)],
            [("NerdySlimeI.Tasks.Set2.obj0", 1)],
        ],
        rewards: [
            new JobReward(ItemID.SilverCoin, 75),
            new JobReward(ItemID.IronBar, 5)
        ],
        NPCID.Guide) { }

    public override void OnJobStart()
    {
        base.OnJobStart();
        DialogueManager.Instance.StartDialogue("NerdySlime.Greeting", true, true);
    }

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        //DialogueManager.Instance.StartDialogue("NerdySlime.Job1End", false, false);
    }
    public override void Update()
    {
        base.Update();
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        if (evt.Type == GameEventType.HousingChecked)
        {
            Main.NewText("A valid housing was found!");
        }
        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        var iron = (evt.ItemType == ItemID.IronOre) || (evt.ItemType == ItemID.LeadOre);

        if (evt.Type == GameEventType.NPCChat && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC))
            DialogueManager.Instance.StartDialogue("NerdySlime.Greeting", false, false);

        // Set 0, Task 0: Talk to NPC
        if (currentSet == 0 && taskIndex == 0)
        {
            return evt.Type == GameEventType.HousingChecked;
        }

        if (currentSet == 1 && taskIndex == 0)
        {
            return evt.Type == GameEventType.ItemPickup
                && evt.ItemType.HasValue && iron;
        }

        if (currentSet == 2 && taskIndex == 0)
        {
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC);
        }

        return false;
    }
}