using System.Collections.Generic;
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

public class OverlayTextureSystem : ModSystem
{
    private static WorldFieldTarget overlayTarget;

    public override void Load()
    {
        if (Main.dedServ) return;

        // Initialize the target with standard alpha blending for textures
        overlayTarget = new WorldFieldTarget(DrawOverlaysToTarget)
        {
            PixelScale = 1,
            Blend = BlendState.AlphaBlend,
            Sampler = SamplerState.PointClamp
        };

        On_Main.CheckMonoliths += BuildTarget;
        On_Main.DoDraw_Tiles_Solid += DrawTargetToScreen;
    }

    private void BuildTarget(On_Main.orig_CheckMonoliths orig)
    {
        orig();
        // Rebuilds the surface from scratch
        overlayTarget.Build();
    }

    private void DrawOverlaysToTarget(SpriteBatch spriteBatch)
    {
        // The ViewMatrix already handles zoom and screen offset[cite: 2].
        // You just draw your textures directly at their world coordinates (i * 16, j * 16).

        /* Example:
        foreach (var tile in activeOverlays) {
            Vector2 worldPos = new Vector2(tile.X * 16, tile.Y * 16);
            spriteBatch.Draw(slimeTexture, worldPos, Color.White);
        }
        */

    }

    private void DrawTargetToScreen(On_Main.orig_DoDraw_Tiles_Solid orig, Main self)
    {
        // 1. Let vanilla draw all solid tiles first
        orig(self);

        // 2. Draw the finished surface on top[cite: 2]
        if (overlayTarget.Field != null)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(overlayTarget.Field, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }
    }

    public override void Unload()
    {
        overlayTarget = null;
    }
}