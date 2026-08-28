using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;

namespace TrelamiumIII.Content.Jobs;

public sealed class HomeForager : Job
{
    public HomeForager() :
        base(JobID.HomeForager, "TownlessTown.Name", "TownlessTown.Description",
        taskList: 
        [
            [
                ("TownlessTown.Tasks.Set0.obj0", 50), // wood
                ("TownlessTown.Tasks.Set0.obj0", 25) // stone
            ],

            [("TownlessTown.Tasks.Set1.obj0", 3)], // create housing(s)
            [("TownlessTown.Tasks.Set2.obj0", 1)], // check in
        ],
        rewards: 
        [
            new JobReward(ItemID.SilverCoin, 75),
            new JobReward(ItemID.IronBar, 5)
        ],
        providerNPC: NPCID.Guide) { }

    public override void OnJobStart()
    {
        base.OnJobStart();
        DialogueManager.Instance.StartDialogue("Guide.HomeForagerGreeting", true, true);
    }

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
    }

    public override void Update()
    {
        base.Update();
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        if (CurrentSet == 1 && evt.Type == GameEventType.HousingChecked)
        {

        }
        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        var wood = (evt.ItemType == ItemID.Wood) 
            || (evt.ItemType == ItemID.BorealWood)
            || (evt.ItemType == ItemID.PalmWood)
            || (evt.ItemType == ItemID.Shadewood)
            || (evt.ItemType == ItemID.Ebonwood)
            || (evt.ItemType == ItemID.AshWood)
            || (evt.ItemType == ItemID.RichMahogany);

        // Set 0, Task 0: gather resources
        if (currentSet == 0 && taskIndex == 0)
        {
            return evt.Type == GameEventType.ItemPickup
                && evt.ItemType.HasValue && wood;
        }

        if (currentSet == 0 && taskIndex == 1)
        {
            return evt.Type == GameEventType.ItemPickup
                && evt.ItemType.HasValue && evt.ItemType == ItemID.StoneBlock;
        }

        if (currentSet == 1 && taskIndex == 0)
        {
            return evt.Type == GameEventType.HousingChecked;
        }

        if (currentSet == 2 && taskIndex == 0)
        {
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC);
        }

        return false;
    }
}