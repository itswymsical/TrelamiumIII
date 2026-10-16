using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using Terraria.GameContent.Creative;
using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Liquids;

public class SunflowerOilBucket : ModItem
{
    public override void Load() => On_Player.ItemCheck_UseBuckets += UseBuckets;

    public override void SetStaticDefaults()
    {
        ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
        ItemID.Sets.AlsoABuildingItem[Type] = true;
        //ItemID.Sets.ShimmerTransformToItem[Type] = ModContent.ItemType<SunflowerOilBottomlessBucket>();
        ItemID.Sets.DuplicationMenuToolsFilter[Type] = true;

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
        LiquidUtils.PlaceLiquid(player, Item, LiquidLoader.LiquidType<OilLiquids>(), OilLiquids.Instance.Sunflower.Name);
    }

    private static void UseBuckets(On_Player.orig_ItemCheck_UseBuckets orig, Player self, Item sItem)
    {
        int x = Player.tileTargetX;
        int y = Player.tileTargetY;

        if (!WorldGen.InWorld(x, y))
        {
            orig(self, sItem);
            return;
        }

        Tile tile = Main.tile[x, y];
        byte before = tile.LiquidAmount;
        bool brine = sItem.type == ItemID.EmptyBucket && before > 0 && tile.LiquidType == LiquidLoader.LiquidType<OilLiquids>()
            && tile.LiquidCondition() == OilLiquids.Instance.Sunflower.Index;

        int[] buckets = LiquidID_TLmod.Sets.CreateLiquidBucketItem;
        int waterBucket = buckets[LiquidLoader.LiquidType<OilLiquids>()];

        if (brine)
            buckets[LiquidLoader.LiquidType<OilLiquids>()] = ModContent.ItemType<SunflowerOilBucket>();

        try
        {
            orig(self, sItem);
        }
        finally
        {
            buckets[LiquidLoader.LiquidType<OilLiquids>()] = waterBucket;
        }

        if (tile.LiquidAmount > before && LiquidConditionSet.ByType(tile.LiquidType) is not null)
            LiquidConditionSystem.Set(x, y, 0);
    }
}