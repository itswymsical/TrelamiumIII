using Terraria.DataStructures;

namespace TrelamiumIII.Content.Ingress;

public class Bramblewood : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 14;
        Item.useTime = 10;
        Item.useAnimation = 15;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTurn = true;
        Item.autoReuse = true;
        Item.maxStack = Item.CommonMaxStack;
    }
}