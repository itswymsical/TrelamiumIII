using ReLogic.Content;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.WoodenWarden;

namespace TrelamiumIII.Content.Ingress;

/// <summary>
/// The Altar of Ingress. Right-click summons the Wooden Warden; once it is defeated the crack in the stone turns into a
/// white scar and right-click enters <see cref="ReverieRealm"/> instead. Cannot be broken.
/// </summary>
public sealed class WardenAltarTile : ModTile
{
    public const int Width = 6;
    public const int Height = 6;

    private const int FrameStride = 18;

    /// <summary> Unpadded image of the whole altar, white where the crack is; drawn once from the top-left tile. </summary>
    private static Asset<Texture2D>? scar;

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

        if (!Main.dedServ)
            scar = ModContent.Request<Texture2D>($"{Texture}_Scar");
    }

    public override void Unload() => scar = null;

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

    public override void MouseOver(int i, int j)
    {
        Player player = Main.LocalPlayer;

        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = -1;
        player.cursorItemIconText = this.GetLocalization(DownedBossSystem.DownedWoodenWarden ? "Enter" : "Summon").Value;
    }

    public override bool RightClick(int i, int j)
    {
        if (DownedBossSystem.DownedWoodenWarden)
            return RealmTransition.Enter<ReverieRealm>();

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
        float light = WardenAltarScar.Opacity * 0.45f;

        r = g = b = light;
    }

    public override void EmitParticles(int i, int j, Tile tile, short tileFrameX, short tileFrameY, Color tileLight, bool visible)
    {
        if (!visible || tileFrameX != 0 || tileFrameY != 0 || WardenAltarScar.Reveal is <= 0f or >= 1f || !Main.rand.NextBool(2))
            return;

        // Across the crack's span of the sprite.
        Vector2 position = new Vector2(i, j) * 16f + new Vector2(Main.rand.NextFloat(24f, 72f), Main.rand.NextFloat(16f, 72f));
        Dust dust = Dust.NewDustPerfect(position, DustID.TintableDustLighted, new Vector2(0f, -Main.rand.NextFloat(0.5f, 2f)), 0, Color.White, Main.rand.NextFloat(0.8f, 1.4f));
        dust.noGravity = true;
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        float opacity = WardenAltarScar.Opacity;

        if (opacity <= 0f || scar is not { IsLoaded: true } || tile.TileFrameX != 0 || tile.TileFrameY != 0)
            return;

        Vector2 offscreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offscreen;
        Texture2D texture = scar.Value;

        // Zero alpha adds under premultiplied AlphaBlend, so the halo needs no batch restart.
        Color halo = new Color(255, 255, 255, 0) * WardenAltarScar.Glow;

        for (int ring = 2; ring >= 1; ring--)
        {
            Color ringColor = halo * (ring == 1 ? 0.5f : 0.22f);

            for (int k = 0; k < 8; k++)
                spriteBatch.Draw(texture, position + (k * MathHelper.PiOver4).ToRotationVector2() * ring * 2f, ringColor);
        }

        spriteBatch.Draw(texture, position, Color.White * opacity);
    }
}

/// <summary> Client-side timing of the altar's scar: it opens over a few seconds once the Wooden Warden is defeated. </summary>
public sealed class WardenAltarScar : ModSystem
{
    private const float RevealTicks = 150f;

    /// <summary> 0 while the Warden stands, rising to 1 after its defeat; already 1 in a world entered after it. </summary>
    public static float Reveal { get; private set; }

    public static float Opacity => Reveal * Reveal * (3f - 2f * Reveal);

    /// <summary> Halo strength: a surge while the scar opens, then a slow pulse. </summary>
    public static float Glow => Opacity * (0.55f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2f)) + MathF.Sin(MathHelper.Pi * Reveal) * 1.2f;

    public override void OnWorldLoad() => Reveal = DownedBossSystem.DownedWoodenWarden ? 1f : 0f;

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        Reveal = DownedBossSystem.DownedWoodenWarden ? MathF.Min(Reveal + 1f / RevealTicks, 1f) : 0f;
    }
}
