using ReLogic.Content;
using ReLogic.Peripherals.RGB;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Core.Graphics;

/// <summary>
/// Class for applying overlays on tiles as VFX.
/// <para>
/// For example: King slime splattering slimed overlay textures on top of tiles.
/// </para>
/// </summary>
public abstract class OverlayedTile : GlobalTile
{
    public abstract Texture2D? BlockTexture { get; }
    public abstract Texture2D? PlatformTexture { get; }

    public virtual float Duration { get; set; } = 3.5f * 60f;

    public virtual BlendState BlendState { get; set; } = BlendState.AlphaBlend;

    protected virtual Asset<Effect>? Shader { get; }

    public override void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
        // Only draw if this overlay is the one active at (i, j).
        // Without this check, every loaded OverlayedTile subclass would draw
        // on every overlaid tile, regardless of which overlay put it there.
        if (!OverlayedTileManager.OverlayActive(this, i, j))
            return;

        Tile tile = Framing.GetTileSafely(i, j);
        bool isPlatform = TileID.Sets.Platforms[tile.TileType] && Main.tileSolidTop[tile.TileType];
        Texture2D? texture = isPlatform ? PlatformTexture : BlockTexture;

        if (texture == null)
            return;

        float intensity = OverlayedTileManager.GetIntensity(i, j);
        Color drawColor = Color.White * (intensity + 0.15f);

        Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 drawPos = new Vector2(i * 16, j * 16) + zero - Main.screenPosition;

        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState);

        spriteBatch.Draw(texture, drawPos, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16),
            drawColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }
    public virtual void EffectParams(Effect effect) { }

    public override void FloorVisuals(int type, Player player)
    {
        int tileX = (int)(player.Center.X / 16f);
        int tileY = (int)((player.position.Y + player.height) / 16f);

        if (!OverlayedTileManager.OverlayActive(this, tileX, tileY))
            return;

        if (Main.rand.NextBool(8))
        {
            Dust dust = Dust.NewDustDirect(player.position, player.width, player.height,
                DustID.t_Slime, 0f, 0f, 150, new Color(86, 162, 255, 100), 0.8f);
            dust.velocity *= 0.3f;
            dust.noGravity = true;
        }
    }
}
