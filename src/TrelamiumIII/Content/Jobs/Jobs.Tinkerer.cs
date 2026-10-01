using TrelamiumIII.Content.Items.Lodestone;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

public sealed class ExplosiveArchitecture : Job
{
    public ExplosiveArchitecture() : base(
        JobID.ExplosiveArchitecture, "ExplosiveArchitecture.Name", "ExplosiveArchitecture.Description",
        taskList:
        [
             [new PredicateTask("ExplosiveArchitecture.Tasks.Set0.obj0",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType is ItemID.IronBar or ItemID.LeadBar, 10),
            new PredicateTask("ExplosiveArchitecture.Tasks.Set0.obj1",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType == ModContent.ItemType<DudBomb>(), 10),
            new PredicateTask("ExplosiveArchitecture.Tasks.Set0.obj2",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType == ModContent.ItemType<LodestoneItem>(), 8),
            new PredicateTask("ExplosiveArchitecture.Tasks.Set0.obj3",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType == ModContent.ItemType<MagnetizedCoil>(), 5)],

            [new PredicateTask("ExplosiveArchitecture.Tasks.Set1.obj0",
                 evt => evt.Type == GameEventType.ItemPickup
                     && evt.ItemType is ItemID.WoodWall, 50)]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 75),
            new JobReward(ItemID.GoldCoin, 2),
            new JobReward("TrelamiumIII/WallBomb", 1)
        ],
        providerNPC: NPCID.GoblinTinkerer)
    { }

    public override JobCondition UnlockCondition => JobCondition.World(() => NPC.savedGoblin && NPC.AnyNPCs(ProviderNPC));

    public override void OnSetAdvanced(int previousSet, int newSet)
    {
        base.OnSetAdvanced(previousSet, newSet);
        if (previousSet == 0)
        {
            Context.Play("Tinkerer.ExplosiveArchitecture_TaskSet1Complete");
        }
    }

    public override void OnJobStart()
        => Context.Play("Tinkerer.ExplosiveArchitecture_Greeting");
}

public class DudBomb : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.Bomb;
    public override void SetDefaults()
    {
        Item.rare = ItemRarityID.Quest;
    }
}