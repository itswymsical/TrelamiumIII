using System.Collections.Generic;
using Terraria.Graphics.Shaders;
using TrelamiumIII.Core.Graphics;

/// <summary>
/// Class for applying overlays on tiles as VFX.
/// <para>
/// For example: King slime splattering slimed overlay textures on top of tiles.
/// </para>
/// </summary>
public abstract class OverlayedTile : GlobalTile
{
    public abstract Texture2D? BlockTexture { get; set; }
    public abstract Texture2D? PlatformTexture { get; set; }

    public virtual float Duration { get; set; } = 3.5f * 60f;

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

        spriteBatch.Draw(texture, drawPos, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16),
            drawColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
    }

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

public class OverlayedTileManager : ModSystem
{
    private struct OverlayData
    {
        public OverlayedTile Overlay;
        public float TimeLeft;
        public float MaxDuration;
    }

    private static Dictionary<Point, OverlayData> overlayedTiles = new Dictionary<Point, OverlayData>();

    public override void PostUpdateWorld()
    {
        UpdateOverlayedTiles();
    }

    private void UpdateOverlayedTiles()
    {
        var tilesToRemove = new List<Point>();
        var tilesToUpdate = new List<Point>(overlayedTiles.Keys);

        foreach (Point tilePos in tilesToUpdate)
        {
            OverlayData data = overlayedTiles[tilePos];
            data.TimeLeft -= 1f;

            if (data.TimeLeft <= 0f)
                tilesToRemove.Add(tilePos);
            else
                overlayedTiles[tilePos] = data;
        }

        foreach (Point tilePos in tilesToRemove)
            overlayedTiles.Remove(tilePos);
    }

    /// <summary>
    /// Applies overlay <typeparamref name="T"/> to the tile at (i, j), using that
    /// overlay's own Duration. Replaces whatever overlay was previously there, if any.
    /// </summary>
    public static void AddOverlayedTile<T>(int i, int j) where T : OverlayedTile
    {
        T overlay = ModContent.GetInstance<T>();
        overlayedTiles[new Point(i, j)] = new OverlayData
        {
            Overlay = overlay,
            TimeLeft = overlay.Duration,
            MaxDuration = overlay.Duration
        };
    }

    /// <summary>True if any overlay is active at (i, j).</summary>
    public static bool OverlayActive(int i, int j)
    {
        return overlayedTiles.ContainsKey(new Point(i, j));
    }

    /// <summary>True if <paramref name="overlay"/> specifically is the active one at (i, j).</summary>
    public static bool OverlayActive(OverlayedTile overlay, int i, int j)
    {
        return overlayedTiles.TryGetValue(new Point(i, j), out OverlayData data) && data.Overlay == overlay;
    }

    public static float GetIntensity(int i, int j)
    {
        if (overlayedTiles.TryGetValue(new Point(i, j), out OverlayData data) && data.MaxDuration > 0f)
            return MathHelper.Clamp(data.TimeLeft / data.MaxDuration, 0.2f, 1f);
        return 0f;
    }

    public override void ClearWorld()
    {
        overlayedTiles.Clear();
    }
}

/// <summary>
///     One VFX overlay applicable to individual tiles -- e.g. the slimed metaball King Slime
///     splatters onto tiles it touches, or the scorch marks a burn effect leaves behind.
/// </summary>
/// <remarks>
///     Painted, not drawn: instead of a texture-atlas sprite per tile, an overlay stamps a white
///     mask into <see cref="TileOverlayManager" />'s accumulation field (see
///     <see cref="StampMask" />) and separately configures the one shader
///     (<see cref="PrepareCompositeShader" />) that turns the whole accumulated field -- every
///     tile this overlay currently owns, summed together -- into the final coloured output. That
///     split is what lets several nearby splats merge into one blobby shape instead of rendering
///     as a grid of separate stamps.
/// </remarks>
internal abstract class TileOverlay
{
    public abstract Texture2D Mask { get; }

    /// <summary>
    ///     Stamps this overlay's mask at <paramref name="screenPos" />, additively -- overlapping
    ///     stamps should sum so nearby tiles merge into one shape once the field is thresholded.
    /// </summary>
    /// <param name="intensity">
    ///     0.2-1, falling as the overlay's remaining time runs out. Letting the stamp itself fade
    ///     means the metaball naturally shrinks below threshold as it expires, rather than needing
    ///     a separate fade-out step.
    /// </param>
    public virtual void StampMask(SpriteBatch spriteBatch, int i, int j, Vector2 screenPos, float intensity)
    {
        Vector2 origin = new Vector2(Mask.Width, Mask.Height) * 0.5f;
        Color stampColor = Color.White * intensity;

        spriteBatch.Draw(Mask, screenPos, null, stampColor, 0f, origin, 1f, SpriteEffects.None, 0f);
    }

    /// <summary>
    ///     Configures the shader that reads the field this overlay's stamps built and produces
    ///     the final on-screen colour. Called once per frame this overlay has at least one active
    ///     tile -- not once per tile.
    /// </summary>
    public abstract ShaderData PrepareCompositeShader(RenderTarget2D field);

    /// <summary>Called once a frame for each player standing on a tile this overlay owns.</summary>
    public virtual void OnContact(Player player)
    {
    }
}