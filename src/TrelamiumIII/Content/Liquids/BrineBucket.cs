using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using TrelamiumIII.Utilities;
using Terraria.Audio;
using Terraria.GameContent.Creative;

namespace TrelamiumIII.Content.Liquids;

public class BrineBucketItem : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
        ItemID.Sets.AlsoABuildingItem[Type] = true;
        //ItemID.Sets.ShimmerTransformToItem[Type] = ModContent.ItemType<BrineBottomlessBucket>();
        ItemID.Sets.DuplicationMenuToolsFilter[Type] = true;

        LiquidID_TLmod.Sets.CreateLiquidBucketItem[LiquidLoader.LiquidType<BrineLiquid>()] = Type;

        CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 5;
    }

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 24;
        Item.maxStack = Item.CommonMaxStack;
        Item.useTurn = true;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.useAnimation = 15;
        Item.useTime = 10;
        Item.useStyle = ItemUseStyleID.Swing;
    }

    public override void HoldItem(Player player)
    {
        LiquidUtils.PlaceLiquid(player, Item, LiquidLoader.LiquidType<BrineLiquid>());
    }
}
