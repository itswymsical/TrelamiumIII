using ModLiquidLib.ID;
using System.Collections.Generic;
using Terraria.GameContent.Creative;
using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Liquids;

public class BrineBucketItem : ModItem
{
    public override void Load() => On_Player.ItemCheck_UseBuckets += UseBuckets;

    public override void SetStaticDefaults()
    {
        ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
        ItemID.Sets.AlsoABuildingItem[Type] = true;
        //ItemID.Sets.ShimmerTransformToItem[Type] = ModContent.ItemType<BrineBottomlessBucket>();
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
        LiquidUtils.PlaceLiquid(player, Item, LiquidID.Water, WaterLiquids.Instance.Brine.Name);
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        var consumable = tooltips.FindIndex(t => t.Name == "Consumable");
        tooltips.RemoveAt(consumable);
    }

    /// <summary>
    ///     An empty bucket on brine gives this bucket: ModLiquidLib hands out <c>CreateLiquidBucketItem[type]</c>, so the
    ///     water entry is swapped for the call. A poured bucket resets the tile's condition, which pouring leaves stale.
    /// </summary>
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
        bool brine = sItem.type == ItemID.EmptyBucket && before > 0 && tile.LiquidType == LiquidID.Water
            && tile.InternalLiquid() == WaterLiquids.Instance.Brine.Index;

        int[] buckets = LiquidID_TLmod.Sets.CreateLiquidBucketItem;
        int waterBucket = buckets[LiquidID.Water];

        if (brine)
            buckets[LiquidID.Water] = ModContent.ItemType<BrineBucketItem>();

        try
        {
            orig(self, sItem);
        }
        finally
        {
            buckets[LiquidID.Water] = waterBucket;
        }

        if (tile.LiquidAmount > before && CustomLiquidSet.ByType(tile.LiquidType) is not null)
            LiquidInternalSystem.Set(x, y, 0);
    }
}