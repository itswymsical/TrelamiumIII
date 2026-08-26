using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Fungore;

public class MyconidSpore : ModItem
{
    public override void SetDefaults()
    {
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 30;
        Item.rare = ItemRarityID.Gray;
    }
    public override bool CanUseItem(Player player) => !NPC.AnyNPCs(ModContent.NPCType<FungoreBoss>());

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
        {
            SoundEngine.PlaySound(SoundID.Roar, player.position);

            int type = ModContent.NPCType<FungoreBoss>();

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.SpawnOnPlayer(player.whoAmI, type);
            
            else
                NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: type);
        }
        return true;
    }
}
public class MyconidSporeTile : ModTile
{
    public override string Texture => AssetReferences.Content.Fungore.MyconidSpore.KEY;
    public override void SetStaticDefaults()
    {
        const int height = 38;

        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;

        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;

        TileID.Sets.SwaysInWindBasic[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

        TileObjectData.newTile.CoordinateWidth = 28;
        TileObjectData.newTile.CoordinateHeights = [height];

        TileObjectData.newTile.DrawYOffset = -(height - 30);

        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);
        TileObjectData.newTile.AnchorValidTiles = [TileID.Dirt, TileID.Mud, TileID.Grass];
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(118, 102, 97), CreateMapEntryName());

        RegisterItemDrop(ModContent.ItemType<MyconidSpore>());
    }

    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        base.KillTile(i, j, ref fail, ref effectOnly, ref noItem);
        if (!fail)
        {
            if (Main.LocalPlayer.whoAmI == Main.myPlayer)
            {

                int type = ModContent.NPCType<FungoreBoss>();

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.SpawnOnPlayer(Main.LocalPlayer.whoAmI, type);

                else
                    NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: Main.LocalPlayer.whoAmI, number2: type);
            }
        }    
    }
}
public class MyconidSporeGlobal : GlobalTile
{
    public override void RandomUpdate(int i, int j, int type)
    {
        base.RandomUpdate(i, j, type);
        if (type == TileID.Grass || type == TileID.Dirt)
        {

        }
    }
}