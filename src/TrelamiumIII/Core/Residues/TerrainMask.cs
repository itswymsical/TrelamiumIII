using System.Diagnostics.CodeAnalysis;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Core.Residues;

/// <summary>
///     The solid tile layer as a texture, which canvas composites are clipped to.
/// </summary>
/// <remarks>
///     Vanilla's <c>tileTarget</c> while render targets are in use. Under <see cref="Main.drawToScreen" /> (retro lighting at
///     zoom 1) tiles go straight to the back buffer, so <see cref="BuildFallback" /> redraws the ones a canvas covers.
/// </remarks>
internal static class TerrainMask
{
    private static RenderTarget2D? fallback;

    private static Vector2 fallbackOrigin;

    private static bool fallbackReady;

    /// <summary>The mask for this frame, and the world position of its top-left texel.</summary>
    public static bool TryGet([NotNullWhen(true)] out Texture2D? texture, out Vector2 origin)
    {
        if (!Main.drawToScreen && Main.instance.tileTarget is { IsDisposed: false } target)
        {
            texture = target;
            origin = Main.sceneTilePos;

            return true;
        }

        if (Main.drawToScreen && fallbackReady && fallback is { IsDisposed: false } own)
        {
            texture = own;
            origin = fallbackOrigin;

            return true;
        }

        texture = null;
        origin = default;

        return false;
    }

    /// <summary>Drops last frame's fallback. Call before any canvas is built.</summary>
    public static void BeginFrame() => fallbackReady = false;

    /// <summary>Draws the solid tiles in <paramref name="area" /> into the fallback target. Call from <c>Main.CheckMonoliths</c>.</summary>
    public static void BuildFallback(Rectangle area)
    {
        GraphicsDevice device = Main.graphics.GraphicsDevice;

        if (fallback is not { IsDisposed: false } || fallback.Width != Main.screenWidth || fallback.Height != Main.screenHeight)
        {
            fallback?.Dispose();
            fallback = new RenderTarget2D(device, Main.screenWidth, Main.screenHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        int left = Math.Max(Math.Max(area.Left, (int)(Main.screenPosition.X / 16f) - 1), 0);
        int top = Math.Max(Math.Max(area.Top, (int)(Main.screenPosition.Y / 16f) - 1), 0);
        int right = Math.Min(Math.Min(area.Right, (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + 2), Main.maxTilesX);
        int bottom = Math.Min(Math.Min(area.Bottom, (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + 2), Main.maxTilesY);

        // Vanilla draws tiles against the truncated screen position here, and DrawSloped against the float one.
        var snap = new Vector2(Main.screenPosition.X - (int)Main.screenPosition.X, Main.screenPosition.Y - (int)Main.screenPosition.Y);

        device.SetRenderTarget(fallback);
        device.Clear(Color.Transparent);

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

        for (int x = left; x < right; x++)
        {
            for (int y = top; y < bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if (!tile.HasTile || tile.IsActuated || !Main.tileSolid[tile.TileType] || !TileDrawing.IsVisible(tile))
                    continue;

                Main.instance.LoadTiles(tile.TileType);

                if (TextureAssets.Tile[tile.TileType] is { Value: { } texture })
                    TileExtensions.DrawSloped(x, y, texture, Color.White, snap);
            }
        }

        Main.spriteBatch.End();
        device.SetRenderTarget(null);

        fallbackOrigin = Main.screenPosition;
        fallbackReady = true;
    }

    public static void Unload()
    {
        RenderTarget2D? target = fallback;

        fallback = null;
        fallbackReady = false;

        if (target is not null)
            Main.QueueMainThreadAction(target.Dispose);
    }
}
