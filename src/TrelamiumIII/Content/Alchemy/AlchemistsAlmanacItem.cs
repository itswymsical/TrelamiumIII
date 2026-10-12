using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core;
using TrelamiumIII.Core.UI.AlchemyAlmanac;

namespace TrelamiumIII.Content.Alchemy;

/// <summary>
///     John Substrate's almanac. It has no recipe: Silvian, his nephew, hands it over at the end of his jobs
///     (<see cref="TrelamiumIII.Content.NPCs.WorldNPCs.Silvian.SilvianAlmanac" />).
/// </summary>
public class AlchemistsAlmanacItem : ModItem
{
    public override string Texture => AssetReferences.Content.Alchemy.AlchemyBookItem.KEY;

    public override void SetDefaults()
    {
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.buyPrice(gold: 1);
        Item.width = Item.height = 28;
        Item.useTime = Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.HoldUp;
    }

    public override bool? UseItem(Player player)
    {
        if (Main.myPlayer != player.whoAmI) return true;
        AlchemyAlmanacUI.ShowAlmanac();
        player.GetModPlayer<SilvianPlayer>().AlmanacOpened = true;
        return true;
    }
}