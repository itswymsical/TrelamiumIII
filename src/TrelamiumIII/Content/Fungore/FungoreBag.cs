using Terraria.GameContent.ItemDropRules;

namespace TrelamiumIII.Content.Fungore;

public class FungoreBag : ModItem
{
	public override void SetStaticDefaults()
	{
		ItemID.Sets.BossBag[Type] = true;
		ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;
		Item.ResearchUnlockCount = 3;
	}

	public override void SetDefaults()
	{
		Item.maxStack = Item.CommonMaxStack;
		Item.consumable = true;
		Item.width = 24;
		Item.height = 24;
		Item.rare = ItemRarityID.Expert;
		Item.expert = true;
	}

	public override bool CanRightClick() => true;

	public override void ModifyItemLoot(ItemLoot itemLoot)
    {
        // Common, not BossBag: BossBag zeroes the dropping NPC's money and throws inside a bag. The bag is already
        // expert-gated, so nothing is lost.
        itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Myclobutanil>()));

        itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<FungoreMask>(), 7));
		itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<FungoreBoss>()));
        itemLoot.Add(ItemDropRule.OneFromOptions(
            1,
            ModContent.ItemType<Girthquake>(),
            ModContent.ItemType<SporeclotSac>(),
            ModContent.ItemType<OrphanSporeStaff>()
        ));
    }
}
