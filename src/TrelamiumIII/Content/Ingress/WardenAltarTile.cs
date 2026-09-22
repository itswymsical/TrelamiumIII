using ReLogic.Content;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Reverie;
using TrelamiumIII.Content.WoodenWarden;

namespace TrelamiumIII.Content.Ingress;

/// <summary>
/// The Altar of Ingress. Right-click summons the Wooden Warden; once it is defeated a slit of white light opens over the
/// altar (<see cref="WardenAltarScar"/>), and right-clicking the altar or the slit enters <see cref="ReverieRealm"/>.
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
            return WardenAltarScar.EnterReverie();

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
        float light = WardenAltarScar.Opacity * 0.3f;

        r = g = b = light;
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (tile.TileFrameX == 0 && tile.TileFrameY == 0)
            WardenAltarScar.Draw(spriteBatch, new Vector2(i * 16 + Width * 8, j * 16));
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

/// <summary>
/// The scar over the Altar of Ingress: a diagonal slit of white light that opens over a few seconds once the Wooden
/// Warden is defeated. Client-side; the slit's position is taken from the altar's last draw.
/// </summary>
public sealed class WardenAltarScar : ModSystem
{
    private const float RevealTicks = 150f;

    /// <summary> Slit tilt from vertical, matching the sprite; the top end leans right. </summary>
    private const float Tilt = 20f * MathHelper.Pi / 180f;

    /// <summary> Half the slit's length along its axis, in world pixels. </summary>
    private const float HalfLength = 92f;

    private const float HoverHalfWidth = 14f;
    private const float InteractRange = 180f;
    private const int DrawTimeoutTicks = 10;

    private static Asset<Texture2D>? slit;

    private static Vector2 center;
    private static uint lastDrawn;

    /// <summary> 0 while the Warden stands, rising to 1 after its defeat; already 1 in a world entered after it. </summary>
    public static float Reveal { get; private set; }

    public static float Opacity => Reveal * Reveal * (3f - 2f * Reveal);

    /// <summary> 0 to 1 while the Reverie transition pulls the slit open; set by <see cref="ReverieEntryCutscene"/>. </summary>
    public static float OpenBoost { get; set; }

    /// <summary> World position of the slit's centre while the altar is on screen. </summary>
    public static Vector2? Center => Main.GameUpdateCount - lastDrawn <= DrawTimeoutTicks ? center : null;

    private static Vector2 Axis => new(MathF.Sin(Tilt), -MathF.Cos(Tilt));

    public override void Load()
    {
        if (!Main.dedServ)
            slit = ModContent.Request<Texture2D>($"{nameof(TrelamiumIII)}/Content/Ingress/WardenAltarTile_Slit");
    }

    public override void Unload() => slit = null;

    public override void OnWorldLoad()
    {
        Reveal = DownedBossSystem.DownedWoodenWarden ? 1f : 0f;
        OpenBoost = 0f;
    }

    /// <summary> From the altar or the slit; plays the transition from the slit when it is on screen. </summary>
    public static bool EnterReverie()
    {
        ReverieEntryCutscene.Origin = Center;

        return RealmTransition.Enter<ReverieRealm>();
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        float previous = Reveal;
        Reveal = DownedBossSystem.DownedWoodenWarden ? MathF.Min(Reveal + 1f / RevealTicks, 1f) : 0f;

        if (Center is not { } at)
            return;

        if (previous == 0f && Reveal > 0f)
            SoundEngine.PlaySound(Assets.SFX.RiftOpenAlt.Asset with { Volume = 0.6f, Pitch = 0.5f }, at);

        if (Opacity <= 0f)
            return;

        float brightness = Opacity * (1f + OpenBoost * 2f);

        for (int k = -2; k <= 2; k++)
            Lighting.AddLight(at + Axis * (HalfLength * 0.4f * k), new Vector3(0.9f, 0.95f, 1f) * brightness);

        if (!Main.gamePaused && Main.rand.NextFloat() < 0.25f + (1f - Reveal) * 0.6f)
        {
            Vector2 along = at + Axis * Main.rand.NextFloat(-HalfLength, HalfLength) * Opacity;
            Vector2 drift = new Vector2(Axis.Y, -Axis.X) * Main.rand.NextFloat(-0.6f, 0.6f) - Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.9f);
            Dust dust = Dust.NewDustPerfect(along, DustID.TintableDustLighted, drift, 0, Color.White, Main.rand.NextFloat(0.6f, 1.1f));
            dust.noGravity = true;
        }
    }

    public override void PostUpdatePlayers()
    {
        if (Main.dedServ || Reveal < 1f || Center is not { } at || Main.LocalPlayer.mouseInterface || Main.mapFullscreen || !Hovered(at, Main.MouseWorld))
            return;

        if (Vector2.Distance(Main.LocalPlayer.Center, at) > InteractRange + HalfLength)
            return;

        WardenAltarTile.ShowCursorText(ModContent.GetInstance<WardenAltarTile>().GetLocalization("Enter").Value);

        if (Main.mouseRight && Main.mouseRightRelease)
        {
            Main.mouseRightRelease = false;
            EnterReverie();
        }
    }

    private static bool Hovered(Vector2 at, Vector2 point)
    {
        Vector2 offset = point - at;
        float along = Vector2.Dot(offset, Axis);
        float across = Vector2.Dot(offset, new Vector2(-Axis.Y, Axis.X));

        return MathF.Abs(along) <= HalfLength && MathF.Abs(across) <= HoverHalfWidth;
    }

    /// <summary> From the altar's top-left tile; <paramref name="anchor"/> is the altar's top centre in world pixels. </summary>
    internal static void Draw(SpriteBatch spriteBatch, Vector2 anchor)
    {
        center = anchor;
        lastDrawn = Main.GameUpdateCount;

        float opacity = Opacity;

        if (opacity <= 0f || slit is not { IsLoaded: true })
            return;

        Vector2 offscreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 position = anchor - Main.screenPosition + offscreen;
        Texture2D texture = slit.Value;
        Vector2 origin = texture.Size() * 0.5f;

        // Grows along its axis first, then widens; the transition pulls it wider still.
        float length = MathHelper.Lerp(0.05f, 1f, opacity);
        float width = MathF.Pow(opacity, 3f) * (1f + OpenBoost * 2.5f);
        float surge = MathF.Sin(MathHelper.Pi * Reveal);
        float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f);

        // Zero alpha adds under premultiplied AlphaBlend, so the glow needs no batch restart.
        var additive = new Color(255, 255, 255, 0);
        Texture2D glow = Assets.Textures.VFX.RadialGradient.Asset.Value;
        Vector2 glowOrigin = glow.Size() * 0.5f;
        float glowStrength = opacity * pulse + surge * 1.2f + OpenBoost * 2f;

        spriteBatch.Draw(glow, position, null, new Color(200, 225, 255, 0) * (glowStrength * 0.35f), Tilt, glowOrigin,
            new Vector2(70f, HalfLength * 3.2f) / glow.Width * new Vector2(0.4f + width, length), SpriteEffects.None, 0f);

        spriteBatch.Draw(glow, position, null, additive * (glowStrength * 0.45f), Tilt, glowOrigin,
            new Vector2(26f, HalfLength * 2.3f) / glow.Width * new Vector2(0.3f + width, length), SpriteEffects.None, 0f);

        Vector2 scale = new(MathF.Max(width, 0.08f), length);

        for (int k = 0; k < 8; k++)
        {
            Vector2 ring = (k * MathHelper.PiOver4).ToRotationVector2() * 2f;
            spriteBatch.Draw(texture, position + ring, null, additive * (0.3f * glowStrength), 0f, origin, scale, SpriteEffects.None, 0f);
        }

        spriteBatch.Draw(texture, position, null, Color.White * opacity, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}
