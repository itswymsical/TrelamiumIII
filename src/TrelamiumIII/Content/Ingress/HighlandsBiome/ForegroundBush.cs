using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary>
/// A bush the player walks behind. The tile only holds its place in the world and draws nothing itself; <see cref="ForegroundBushSystem"/>
/// draws it after the players, over everything standing on the ground.
/// </summary>
public class ForegroundBushTile : ModTile
{
    public const int Width = 3;
    public const int Height = 2;

    /// <summary> Frames of the sprite <see cref="ForegroundBushSystem"/> draws from. </summary>
    public const int Styles = 12;

    /// <summary> The winterberry bush sheet, scaled up when drawn, until the foreground bushes have their own sprite. </summary>
    public override string Texture => $"{nameof(TrelamiumIII)}/Content/Ingress/HighlandsBiome/WinterberryBush";

    public override void SetStaticDefaults()
    {
        Main.tileCut[Type] = true;
        Main.tileSolid[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;

        TileID.Sets.IgnoredByGrowingSaplings[Type] = true;
        TileID.Sets.BreakableWhenPlacing[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, Width, 0);
        TileObjectData.newTile.AnchorValidTiles =
        [
            ModContent.TileType<HighlandsGrass>(),
            ModContent.TileType<SnowHighlandsGrass>(),
            ModContent.TileType<PodzolGrass>(),
        ];
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleWrapLimit = Styles;
        TileObjectData.addTile(Type);

        HitSound = SoundID.Grass;
        DustType = DustID.Grass;

        AddMapEntry(new Color(46, 112, 66));
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
}

/// <summary>
/// Draws the foreground bushes on screen after the players, lit by the world and swaying a little in the wind, and lets a
/// player show through one they stand behind. Scans the visible tiles rather than queuing from the tile's own draw, since
/// vanilla draws tiles into a cached target that isn't refreshed every frame.
/// </summary>
public sealed class ForegroundBushSystem : ModSystem
{
    // The cell of the winterberry sheet each frame sits in, and how large it's drawn over the three-tile footprint.
    private const int FrameStride = 34;
    private const int FrameWidth = 32;
    private const int FrameHeight = 34;
    private const float Scale = 2.5f;

    /// <summary> Greens the winterberry sheet's frosted leaves, so the stand-in reads as a summer bush. </summary>
    private static readonly Color PlaceholderTint = new(130, 214, 120);

    private const float SwayAngle = 0.03f;
    private const float SwaySpeed = 1.6f;

    /// <summary> Opacity of a bush with a player inside it. </summary>
    private const float BehindOpacity = 0.55f;

    /// <summary> Tiles past the screen edge a bush's origin can sit and still reach onto it. </summary>
    private const int CullPadding = 4;

    private static Asset<Texture2D>? sprite;

    public override void Load()
    {
        if (!Main.dedServ)
            On_Main.DrawPlayers_AfterProjectiles += DrawAfterPlayers;
    }

    public override void Unload() => sprite = null;

    private static void DrawAfterPlayers(On_Main.orig_DrawPlayers_AfterProjectiles orig, Main self)
    {
        orig(self);

        if (!Main.gameMenu)
            DrawBushes();
    }

    private static void DrawBushes()
    {
        int type = ModContent.TileType<ForegroundBushTile>();
        sprite ??= ModContent.Request<Texture2D>(ModContent.GetInstance<ForegroundBushTile>().Texture);

        if (!sprite.IsLoaded)
            return;

        int left = Math.Max((int)(Main.screenPosition.X / 16f) - CullPadding, 10);
        int right = Math.Min((int)((Main.screenPosition.X + Main.screenWidth) / 16f) + CullPadding, Main.maxTilesX - 10);
        int top = Math.Max((int)(Main.screenPosition.Y / 16f) - CullPadding, 10);
        int bottom = Math.Min((int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + CullPadding, Main.maxTilesY - 10);
        bool begun = false;

        for (int x = left; x < right; x++)
        {
            for (int y = top; y < bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                // One draw per bush, from its top-left tile.
                if (!tile.HasTile || tile.TileType != type || tile.TileFrameY != 0 || tile.TileFrameX % (ForegroundBushTile.Width * 18) != 0)
                    continue;

                if (!begun)
                {
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }

                DrawBush(x, y, tile.TileFrameX / (ForegroundBushTile.Width * 18));
            }
        }

        if (begun)
            Main.spriteBatch.End();
    }

    private static void DrawBush(int x, int y, int style)
    {
        Texture2D texture = sprite!.Value;
        var source = new Rectangle(style % ForegroundBushTile.Styles * FrameStride, 0, FrameWidth, FrameHeight);

        // From the middle of the footprint's bottom edge, so it sways from where it's rooted.
        var root = new Vector2((x + ForegroundBushTile.Width * 0.5f) * 16f, (y + ForegroundBushTile.Height) * 16f + 2f);
        float sway = SwayAngle * MathF.Sin((float)Main.timeForVisualEffects / 60f * SwaySpeed + x * 0.7f) + Main.WindForVisuals * SwayAngle;

        float halfWidth = FrameWidth * Scale * 0.5f;
        float height = FrameHeight * Scale;
        var area = new Rectangle((int)(root.X - halfWidth), (int)(root.Y - height), (int)(halfWidth * 2f), (int)height);
        float opacity = 1f;

        foreach (Player player in Main.ActivePlayers)
        {
            if (!player.dead && player.Hitbox.Intersects(area))
            {
                opacity = BehindOpacity;
                break;
            }
        }

        Color light = Lighting.GetColor(x + ForegroundBushTile.Width / 2, y + ForegroundBushTile.Height / 2).MultiplyRGB(PlaceholderTint);

        Main.spriteBatch.Draw(texture, root - Main.screenPosition, source, light * opacity, sway, new Vector2(FrameWidth * 0.5f, FrameHeight), Scale, x % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
    }
}
