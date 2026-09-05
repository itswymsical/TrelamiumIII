using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;

namespace TrelamiumIII.Content.Jobs;

public sealed class HomeForager : Job
{
    public HomeForager() :
        base(JobID.HomeForager, "HomeForager.Name", "HomeForager.Description",
        taskList: 
        [
            [
                ("HomeForager.Tasks.Set0.obj0", 10), // trees
                ("HomeForager.Tasks.Set0.obj1", 25) // stone
            ],

            [("HomeForager.Tasks.Set1.obj0", 1)], // create housing(s)
            [("HomeForager.Tasks.Set2.obj0", 1)], // create housing(s)
            [("HomeForager.Tasks.Set3.obj0", 1)], // create housing(s)
            [("HomeForager.Tasks.Set4.obj0", 1)], // check in
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
        DialogueManager.Instance.StartDialogue("Guide.HomeForager_Greeting", zoomIn: true, letterbox: true);
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
        if (evt.Type == GameEventType.HousingChecked && evt.IsOccupied)
        {
            DialogueManager.Instance.StartDialogue("Guide.HomeForager_Occupied", zoomIn: false, letterbox: false);
        }
        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        // Set 0, Task 0: chop trees
        if (currentSet == 0)
        {
            if (taskIndex == 0)
                return evt.Type == GameEventType.TileBreak
                    && evt.TileType.HasValue && evt.TileType == TileID.Trees;
            else if (taskIndex == 1)
                return evt.Type == GameEventType.ItemPickup
                    && evt.ItemType.HasValue && evt.ItemType == ItemID.StoneBlock;
        }

        if ((currentSet >= 1 && currentSet <= 3) && taskIndex == 0)
            return evt.Type == GameEventType.HousingChecked && !evt.IsOccupied && evt.RoomId.HasValue;
        
        if (currentSet == 4 && taskIndex == 0)
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC);        

        return false;
    }
}