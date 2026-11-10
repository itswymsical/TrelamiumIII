using ModLiquidLib.ID;
using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Utilities;
using Terraria.GameContent.Creative;
using System.Collections.Generic;

namespace TrelamiumIII.Content.Liquids;
/// <summary>
/// Functionality for all buckets that can hold liquids from <see cref="InternalLiquidSet"/>.
/// </summary>
public abstract class BucketItem : ModItem
{
    public override void Load() => On_Player.ItemCheck_UseBuckets += UseBuckets;
    public abstract InternalLiquidType LiquidSet { get; }
    public abstract InternalLiquid Liquid { get; }
    public override void SetStaticDefaults()
    {
        ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
        ItemID.Sets.AlsoABuildingItem[Type] = true;
        //ItemID.Sets.ShimmerTransformToItem[Type] = ModContent.ItemType<BottomlessBucket>();
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
        LiquidUtils.PlaceLiquid(player, Item, Liquid.Type, LiquidSet.Name);
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        var consumable = tooltips.FindIndex(t => t.Name == "Consumable");
        tooltips.RemoveAt(consumable);

        tooltips.Add(new TooltipLine(Mod, "LiquidTooltip0", 
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "LiquidTooltips.LiquidTooltip0") + $" {LiquidSet.DisplayName}"));
        tooltips.Add(new TooltipLine(Mod, "LiquidTooltip1", 
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "LiquidTooltips.LiquidTooltip1")));
    }

    private void UseBuckets(On_Player.orig_ItemCheck_UseBuckets orig, Player self, Item sItem)
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
        bool liquid = sItem.type == ItemID.EmptyBucket && before > 0 && tile.LiquidType == Liquid.Type
            && tile.InternalLiquid() == LiquidSet.Index;

        int[] buckets = LiquidID_TLmod.Sets.CreateLiquidBucketItem;
        int waterBucket = buckets[Liquid.Type];

        if (liquid)
            buckets[Liquid.Type] = Type;

        try
        {
            orig(self, sItem);
        }
        finally
        {
            buckets[Liquid.Type] = waterBucket;
        }

        if (tile.LiquidAmount > before && InternalLiquidSet.ByType(tile.LiquidType) is not null)
            LiquidInternalSystem.Set(x, y, 0);
    }
}
