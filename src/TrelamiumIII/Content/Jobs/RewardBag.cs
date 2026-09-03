using Terraria.GameContent.ItemDropRules;

namespace TrelamiumIII.Content.Jobs;

public class RewardBag : ModItem
{
	public override void SetStaticDefaults()
	{
		ItemID.Sets.BossBag[Type] = true;
		ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;
		Item.ResearchUnlockCount = 3;
	}
    public Item[]? RewardPool { get; set; }

    public override void SetDefaults()
	{
		Item.maxStack = Item.CommonMaxStack;
		Item.consumable = true;
		Item.width = 24;
		Item.height = 24;
		Item.rare = ItemRarityID.Quest;
		Item.expert = true;
	}

	public override bool CanRightClick() => true;

    public override void ModifyItemLoot(ItemLoot itemLoot)
    {
		if (RewardPool?.Length == 0)
			return;
        
        foreach (var itemType in RewardPool ?? [])
		{
            itemLoot.Add(ItemDropRule.Common(itemType.type, minimumDropped: itemType.stack, maximumDropped: itemType.stack));
        }
    }
}
