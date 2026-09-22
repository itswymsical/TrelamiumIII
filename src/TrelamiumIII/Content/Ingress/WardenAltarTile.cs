using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.WoodenWarden;

namespace TrelamiumIII.Content.Ingress;

/// <summary>
/// The Altar of Ingress. Right-click summons the Wooden Warden; once it is defeated a slit of white light opens over the
/// altar (<see cref="IngressScar"/>), and right-clicking the altar or the slit enters <see cref="ReverieRealm"/>.
/// Cannot be broken.
/// </summary>
public sealed class WardenAltarTile : ModTile
{
    public const int Width = 6;
    public const int Height = 6;

    private const int FrameStride = 18;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = false;
        Main.tileLighted[Type] = true;

        TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.Origin = new Point16(Width / 2, Height - 1);
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16];
        TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, Width, 0);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);

        DustType = DustID.t_LivingWood;
        AddMapEntry(new Color(96, 118, 71), CreateMapEntryName());

        _ = this.GetLocalization("Summon");
        _ = this.GetLocalization("Enter");
    }

    public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

    public override bool CanExplode(int i, int j) => false;

    /// <summary> Writes the altar's frames with its top-left at <paramref name="origin"/>; the row below must be solid. </summary>
    public static void Place(Point origin)
    {
        ushort type = (ushort)ModContent.TileType<WardenAltarTile>();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Tile tile = Main.tile[origin.X + x, origin.Y + y];
                tile.ClearTile();
                tile.HasTile = true;
                tile.TileType = type;
                tile.TileFrameX = (short)(x * FrameStride);
                tile.TileFrameY = (short)(y * FrameStride);
            }
        }
    }

    public override void MouseOver(int i, int j) => ShowCursorText(this.GetLocalization(DownedBossSystem.DownedWoodenWarden ? "Enter" : "Summon").Value);

    public override bool RightClick(int i, int j)
    {
        if (DownedBossSystem.DownedWoodenWarden)
            return IngressScar.Enter();

        int warden = ModContent.NPCType<WoodenWardenBoss>();

        if (NPC.AnyNPCs(warden))
            return false;

        Player player = Main.LocalPlayer;
        SoundEngine.PlaySound(SoundID.Roar, player.Center);

        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: warden);
        else
            NPC.SpawnOnPlayer(player.whoAmI, warden);

        return true;
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        float light = IngressScar.Opacity * 0.3f;

        r = g = b = light;
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (tile.TileFrameX == 0 && tile.TileFrameY == 0)
            IngressScar.MarkAltar(new Vector2(i * 16 + Width * 8, j * 16));
    }

    internal static void ShowCursorText(string text)
    {
        Player player = Main.LocalPlayer;

        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = -1;
        player.cursorItemIconText = text;
    }
}

