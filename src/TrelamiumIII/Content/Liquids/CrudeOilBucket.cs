using ModLiquidLib.ModLoader;
using TrelamiumIII.Utilities;
using Terraria.GameContent.Creative;

namespace TrelamiumIII.Content.Liquids;

public class CrudeOilBucketItem : ModItem
{
	public override void SetStaticDefaults() {
		ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
		ItemID.Sets.AlsoABuildingItem[Type] = true;
		ItemID.Sets.DuplicationMenuToolsFilter[Type] = true;

		ModLiquidLib.ID.LiquidID_TLmod.Sets.CreateLiquidBucketItem[LiquidLoader.LiquidType<CrudeOilLiquid>()] = Type;

		CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 5;
	}

	public override void SetDefaults() {
		Item.width = 20;
		Item.height = 24;
		Item.maxStack = Item.CommonMaxStack;
		Item.useTurn = true;
		Item.autoReuse = true;
		Item.useAnimation = 15;
		Item.useTime = 10;
		Item.useStyle = ItemUseStyleID.Swing;
	}

	//It's recommended that you look at ExampleLiquidBucket to see how buckets work
	public override void HoldItem(Player player) {
		LiquidUtils.PlaceLiquid(player, Item, LiquidLoader.LiquidType<CrudeOilLiquid>());
	}
}
