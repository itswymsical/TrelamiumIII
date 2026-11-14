//using ModLiquidLib.ID;
//using Terraria.GameContent.Creative;
//using TrelamiumIII.Core.Liquids;
//using TrelamiumIII.Utilities;

//namespace TrelamiumIII.Content.Liquids;

//public abstract class ModBucket : ModItem
//{
//    public override void Load() => On_Player.ItemCheck_UseBuckets += UseBuckets;
//    public override void SetStaticDefaults()
//    {
//        ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
//        ItemID.Sets.AlsoABuildingItem[Type] = true;
//        ItemID.Sets.DuplicationMenuToolsFilter[Type] = true;

//        CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 5;
//    }

//    public override void SetDefaults()
//    {
//        Item.width = 20;
//        Item.height = 24;
//        Item.maxStack = Item.CommonMaxStack;
//        Item.useTurn = true;
//        Item.autoReuse = true;
//        Item.consumable = true;
//        Item.useAnimation = 15;
//        Item.useTime = 10;
//        Item.useStyle = ItemUseStyleID.Swing;
//    }

//    public abstract int BucketLiquidType { get; }
//    public abstract LiquidCondition BucketLiquidCondition { get; }

//    public override void HoldItem(Player player)
//    {
//        LiquidUtils.PlaceLiquid(player, Item, BucketLiquidType, BucketLiquidCondition.Name);
//    }

//    private void UseBuckets(On_Player.orig_ItemCheck_UseBuckets orig, Player self, Item sItem)
//    {
//        int x = Player.tileTargetX;
//        int y = Player.tileTargetY;

//        if (!WorldGen.InWorld(x, y))
//        {
//            orig(self, sItem);
//            return;
//        }

//        Tile tile = Main.tile[x, y];
//        byte before = tile.LiquidAmount;
//        bool brine = sItem.type == ItemID.EmptyBucket && before > 0 && tile.LiquidType == BucketLiquidType
//            && tile.LiquidCondition() == BucketLiquidCondition.Index;

//        int[] buckets = LiquidID_TLmod.Sets.CreateLiquidBucketItem;
//        int waterBucket = buckets[BucketLiquidType];

//        if (brine)
//            buckets[BucketLiquidType] = Type;

//        try
//        {
//            orig(self, sItem);
//        }
//        finally
//        {
//            buckets[BucketLiquidType] = waterBucket;
//        }

//        if (tile.LiquidAmount > before && LiquidConditionSet.ByType(tile.LiquidType) is not null)
//            LiquidConditionSystem.Set(x, y, 0);
//    }
//}