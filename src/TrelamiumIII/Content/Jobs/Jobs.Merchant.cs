using Terraria.DataStructures;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Jobs;

public sealed class GoldMine : Job
{
    public GoldMine() :
        base(JobID.GoldMine, "GoldMine.Name", "GoldMine.Description",
        taskList:
        [
            [("GoldMine.Tasks.Set0.obj0", 1)],
            [("GoldMine.Tasks.Set1.obj0", 25)],
            [("GoldMine.Tasks.Set2.obj0", 1)],
        ],
        rewards: [
            new JobReward(ItemID.SilverCoin, 50),
            new JobReward(ItemID.SilverBar, 14),
            new JobReward("TrelamiumIII.StalactiteCannon", 1)
        ],
        NPCID.Merchant) { }

    public override void OnJobStart()
    {
        base.OnJobStart();

    }

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(true);
    }

    public override void Update()
    {
        base.Update();
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        bool madeFromCopper = evt.Type == GameEventType.ItemPickup && evt.Item.CraftedFromRecursive(ItemID.CopperOre) && evt.ItemType.HasValue;
        bool copperOre = evt.Type == GameEventType.ItemPickup && evt.ItemType == ItemID.CopperOre;
        // talk to merchant
        if (currentSet == 0)
            return evt.Type == GameEventType.NPCChat && evt.NPCType.HasValue 
                && (evt.NPCType == this.ProviderNPC);
        
        // collect copper items
        else if (currentSet == 1)
            return madeFromCopper || copperOre;

        // talk to merchant again
        else if (currentSet == 2)
            return evt.Type == GameEventType.NPCChat && evt.NPCType.HasValue 
                && (evt.NPCType == this.ProviderNPC);
        
        return false;
    }
} // 67