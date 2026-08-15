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
        itemLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<Myclobutanil>()));

        itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<FungoreMask>(), 7));

		itemLoot.Add(ItemDropRule.AlwaysAtleastOneSuccess(
			ItemDropRule.Common(ModContent.ItemType<Girthquake>(), 2), 
			ItemDropRule.Common(ModContent.ItemType<SporeclotSac>(), 2),
            ItemDropRule.Common(ModContent.ItemType<OrphanSporeStaff>(), 2)));
    }
}
