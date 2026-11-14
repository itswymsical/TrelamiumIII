using Terraria.Localization;
using TrelamiumIII.Content.Ingress;

namespace TrelamiumIII.Utilities;
/// <summary>
/// Adds recipe groups for items that should really already have them.
/// </summary>
public class CustomRecipeGroups : ModSystem
{
    public const string CopperBar = nameof(ItemID.CopperBar);
    public const string SilverBar = nameof(ItemID.SilverBar);
    public const string GoldBar = nameof(ItemID.GoldBar);
    public const string Fish = nameof(ItemID.Bass);
    public const string Soil = "Soil";
    public const string Dirts = nameof(ItemID.DirtBlock);
    public const string RottenChunk = nameof(ItemID.RottenChunk);
    public override void AddRecipeGroups()
    {
        RegisterRecipeGroup(CopperBar, displayItem: ItemID.CopperBar, items: [ItemID.CopperBar, ItemID.TinBar]);
        RegisterRecipeGroup(SilverBar, displayItem: ItemID.SilverBar, items: [ItemID.SilverBar, ItemID.TungstenBar]);
        RegisterRecipeGroup(GoldBar, displayItem: ItemID.GoldBar, items: [ItemID.GoldBar, ItemID.PlatinumBar]);

        RegisterRecipeGroup(Fish, displayItem: ItemID.Bass, items: [
            2290, 2297, 2298, 2299, 2300, 2301, 2302, 2303, 2304, 2305, 2306, 2307,
            2308, 2309, 2310, 2311, 2312, 2313, 2314, 2315, 2316, 2317, 2318, 2319, 2321, 4401, 4402]);

        RegisterRecipeGroup(Soil, displayItem: ItemID.DirtBlock, items: [ItemID.DirtBlock, ItemID.MudBlock, ItemID.AshBlock]);
        RegisterRecipeGroup(RottenChunk, displayItem: ItemID.RottenChunk, items: [ItemID.RottenChunk, ItemID.Vertebrae]);

        RegisterRecipeGroup(Dirts, displayItem: ItemID.DirtBlock, items: 
            [ItemID.DirtBlock, ItemID.MudBlock, ModContent.ItemType<PeatBlock>()]);
    }

    private static void RegisterRecipeGroup(string name, int displayItem, params int[] items)
    {
        var group = new RecipeGroup(
            () => $"{Language.GetTextValue("LegacyMisc.37")} {Lang.GetItemNameValue(displayItem)}",
            items);

        RecipeGroup.RegisterGroup(name, group);
    }
}