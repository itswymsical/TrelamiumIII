using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Jobs;

public enum Task
{
    TalkToMerchant = 0,
    CollectCopper = 1,
    ReturnToMerchant = 2,
}

public sealed class GoldMine : Job
{
    public int barsCollected = 0;
    public int furnitureCollected = 0;

    public override void SaveCustomData(TagCompound tag) {
        foreach (var match in CopperMatches) 
            tag[match.SaveKey] = match.CurrentContributions;      
    }

    public override void LoadCustomData(TagCompound tag) {
        foreach (var match in CopperMatches)
            match.CurrentContributions = tag.GetInt(match.SaveKey);       
    }

    public GoldMine() :base(
        JobID.GoldMine, "GoldMine.Name", "GoldMine.Description",
        taskList:
        [
            [("GoldMine.Tasks.Set0.obj0", 1)],
            [("GoldMine.Tasks.Set1.obj0", 100)],
            [("GoldMine.Tasks.Set2.obj0", 1)],
        ],
        rewards: [
            new JobReward(ItemID.SilverCoin, 50),
            new JobReward(ItemID.SilverBar, 14),
            new JobReward("TrelamiumIII.VitalTonicItem", 2)
        ],
        NPCID.Merchant){ }

    public override void OnJobStart(
        )
    {
        base.OnJobStart(
            );

    } // lol

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(true);

    }

    private readonly CollectionMatch[] CopperMatches =
    {
        new() { SaveKey = "copperOre", MaxContributions = 5, Matches = evt => evt.ItemType == ItemID.CopperOre }, // 0
        new() { SaveKey = "copperBar", MaxContributions = 10, Matches = evt => evt.ItemType == ItemID.CopperBar },
        new() { SaveKey = "copperFurn", MaxContributions = 35, Matches = evt => ItemUtils.IsCopperFurniture(evt) },
        new() { SaveKey = "copperTool", MaxContributions = 15, Matches = evt => ItemUtils.IsCopperTool(evt) }
    };

    public override void OnEvent(GameEvent evt)
    {
        if (CurrentTaskSet == 1 && evt.Type == GameEventType.ItemPickup)
        {
            if (CopperMatches[0].CurrentContributions >= CopperMatches[0].MaxContributions)
            {
                DialogueManager.Instance.StartDialogue("Merchant.GoldMine.SurplusOre"); // tells you to find other copper items.
            }
            else if (CopperMatches[1].CurrentContributions >= CopperMatches[1].MaxContributions)
            {
                DialogueManager.Instance.StartDialogue("Merchant.GoldMine.SurplusBars"); // tells you to find other copper items.
            }
            else if (CopperMatches[2].CurrentContributions >= CopperMatches[2].MaxContributions)
            {
                DialogueManager.Instance.StartDialogue("Merchant.GoldMine.SurplusFurniture"); // tells you to find other copper items.
            }
            else if (CopperMatches[3].CurrentContributions >= CopperMatches[3].MaxContributions)
            {
                DialogueManager.Instance.StartDialogue("Merchant.GoldMine.SurplusTools"); // tells you to find other copper items.
            }
        }

        base.OnEvent(evt);
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        if (CurrentTaskSet == 1 && evt.Type == GameEventType.ItemPickup)
        {
            foreach (var match in CopperMatches)
            {
                if (match.Matches(evt) && match.CurrentContributions < match.MaxContributions)
                {
                    if (!TryMarkItemContribution(evt.Item.whoAmI))
                        return false;

                    match.CurrentContributions += evt.Amount;
                    return UpdateProgress(taskIndex, evt.Amount);
                }
            }
        }
        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        //bool madeFromCopper = evt.Type == GameEventType.ItemPickup && evt.Item.CraftedFromRecursive(ItemID.CopperBar) && evt.ItemType.HasValue;
        //// talk to merchant
        if (currentSet == 0)
            return evt.Type == GameEventType.NPCChat && evt.NPCType.HasValue
                && (evt.NPCType == this.ProviderNPC);

        // collect copper items
        else if (currentSet == 1)
            return OnMatchedEvent(evt, currentSet);

        // talk to merchant again
        else if (currentSet == 2)
            return evt.Type == GameEventType.NPCChat && evt.NPCType.HasValue
                && (evt.NPCType == this.ProviderNPC);

        return false;
    }
}

public class CollectionMatch
{
    public string SaveKey { get; set; }
    public int MaxContributions { get; set; }
    public int CurrentContributions { get; set; }
    public Func<GameEvent, bool> Matches { get; set; }
}