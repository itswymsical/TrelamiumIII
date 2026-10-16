using ModLiquidLib.ModLoader;
using Terraria.GameContent.Creative;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Liquids;

public class CrudeOilBucketItem : ModItem
{
	public override void SetStaticDefaults() {
		ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
		ItemID.Sets.AlsoABuildingItem[Type] = true;
		ItemID.Sets.DuplicationMenuToolsFilter[Type] = true;

		ModLiquidLib.ID.LiquidID_TLmod.Sets.CreateLiquidBucketItem[LiquidLoader.LiquidType<DarkOilLiquids>()] = Type;

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
        Item.consumable = true;
        Item.useStyle = ItemUseStyleID.Swing;
	}

	public override void HoldItem(Player player) {
		LiquidUtils.PlaceLiquid(player, Item, LiquidLoader.LiquidType<DarkOilLiquids>());
	}
}