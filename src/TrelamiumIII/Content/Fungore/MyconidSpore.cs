using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.Localization;
using Terraria.ObjectData;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Rarities;
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
        Item.rare = ModContent.RarityType<MyconidRarity>();
    }
    public override bool CanUseItem(Player player) => !NPC.AnyNPCs(ModContent.NPCType<FungoreBoss>());

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
        {
            SoundEngine.PlaySound(SoundID.Roar, player.position);
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            int type = ModContent.NPCType<FungoreBoss>();

            int spawnX = (int)player.Center.X;
            int spawnY = (int)player.Bottom.Y;

            int npcIndex = NPC.NewNPC(player.GetSource_ItemUse(Item), spawnX, spawnY, type);

            if (npcIndex != Main.maxNPCs)
            {
                if (Main.netMode == NetmodeID.SinglePlayer)
                    Main.NewText(Language.GetTextValue("Announcement.HasAwoken", Main.npc[npcIndex].TypeName), 175, 75, 255);
                else
                    ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasAwoken", Main.npc[npcIndex].GetTypeNetName()), new Color(175, 75, 255));
            }
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
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int type = ModContent.NPCType<FungoreBoss>();

                int spawnX = i * 16 + 8;
                int spawnY = j * 16 + 16;

                var source = new EntitySource_TileBreak(i, j);

                int npcIndex = NPC.NewNPC(source, spawnX, spawnY, type);

                if (npcIndex != Main.maxNPCs)
                {
                    if (Main.netMode == NetmodeID.SinglePlayer)
                        Main.NewText(Language.GetTextValue("Announcement.HasAwoken", Main.npc[npcIndex].TypeName), 175, 75, 255);
                    else
                        ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasAwoken", Main.npc[npcIndex].GetTypeNetName()), new Color(175, 75, 255));
                }
            }
        }
    }
}
public class MyconidSporeGlobal : GlobalTile
{
    public override void RandomUpdate(int i, int j, int type)
    {
        base.RandomUpdate(i, j, type);
        var chance = DownedBossSystem.DownedFungore ? WorldGen.genRand.NextBool(10000) : WorldGen.genRand.NextBool(4800);

        if (type == TileID.Grass)
        {
            if (chance)
            {
                if (!Main.tile[i, j - 1].HasTile)
                {
                    WorldGen.PlaceTile(i, j - 1, ModContent.TileType<MyconidSporeTile>(), mute: true);

                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendTileSquare(-1, i, j - 1, 1);
                    }
                }
            }
        }
    }
}