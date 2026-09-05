using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Base for every <see cref="ITileCondition" />: a default for each member of the contract, plus
///     the handful of drawing and sampling helpers a condition normally wants.
/// </summary>
/// <remarks>
///     Derived types are discovered and registered automatically by
///     <see cref="TileConditionSystem" />, so a new condition is one file and nothing else. It needs
///     a parameterless constructor and it must not keep per-tile state -- see
///     <see cref="ITileCondition" /> for why.
/// </remarks>
public abstract class TileCondition : ITileCondition
{
    public virtual string Name => GetType().Name;

    public virtual float Duration => 8f * 60f;

    public virtual int MaxTiles => 1024;

    public virtual void Load() { }

    public virtual void Unload() { }

    /// <summary>
    ///     Defaults to any solid tile. Conditions coat terrain, and a mark laid on empty space has
    ///     nothing to sit on.
    /// </summary>
    public virtual bool CanApplyTo(int i, int j)
    {
        Tile tile = Framing.GetTileSafely(i, j);

        return tile.HasTile && Main.tileSolid[tile.TileType];
    }

    public virtual void OnApplied(ref TileConditionInstance instance) { }

    public virtual void OnExpired(in TileConditionInstance instance) { }

    public virtual bool CanRemain(in TileConditionInstance instance) => instance.Tile.HasTile;

    public virtual void UpdateTile(ref TileConditionInstance instance) { }

    public virtual void UpdateGlobal() { }

    public virtual void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount) { }

    public virtual void OnPlayerContact(ref TileConditionInstance instance, Player player) { }

    public virtual void OnNPCContact(ref TileConditionInstance instance, NPC npc) { }

    public virtual void OnProjectileContact(ref TileConditionInstance instance, Projectile projectile) { }

    public virtual TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    public virtual BlendState BlendState => BlendState.AlphaBlend;

    public virtual BlendState FieldBlend => BlendState.AlphaBlend;

    public virtual int FieldPixelScale => 2;

    public virtual Asset<Effect>? Shader => null;

    public virtual void ApplyShaderParameters(Effect effect) { }

    public virtual void BindAdditionalTextures(GraphicsDevice device) { }

    public virtual void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition) { }

    /// <summary>
    ///     Draws the tile's own sprite in <paramref name="color" />, so the mark takes the exact
    ///     shape of the terrain under it -- slopes, half blocks and merged edges included.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The alternative, a flat 16x16 quad, is wrong on anything that is not a full block:
    ///         it paints char into the empty triangle above a slope and squares off a half block.
    ///         Stamping the real silhouette costs the same draw and is right everywhere.
    ///     </para>
    ///     <para>
    ///         Batches into <see cref="Main.spriteBatch" /> rather than an argument, because the
    ///         slope walk it delegates to does. That is the batch the system has begun in both
    ///         render modes, so it is the same one either way.
    ///     </para>
    /// </remarks>
    protected static void DrawSilhouette(in TileConditionInstance instance, Color color)
    {
        Tile tile = instance.Tile;

        if (!tile.HasTile)
            return;

        Main.instance.LoadTiles(tile.TileType);

        if (TextureAssets.Tile[tile.TileType] is not { Value: { } texture })
            return;

        // DrawSloped adds TileOffset, the offscreen-range shift that belongs to the tile render
        // target's own pass. Neither of our passes runs inside it, so cancel it back out rather
        // than reimplementing the slope walk to avoid it.
        TileExtensions.DrawSloped(instance.X, instance.Y, texture, color, -TileExtensions.TileOffset);
    }

    /// <summary>Whether the tile is close enough to the camera to be worth spending anything on.</summary>
    /// <remarks>
    ///     Per-tile update runs for the whole batch, on and off screen alike, because the simulation
    ///     has to stay consistent. Dust, light and sound do not, and a thousand tiles all rolling for
    ///     an ember two screens away is pure cost.
    /// </remarks>
    protected static bool OnScreen(in TileConditionInstance instance, float padding = 64f)
    {
        Vector2 relative = instance.WorldCenter - Main.screenPosition;

        return relative.X >= -padding
               && relative.Y >= -padding
               && relative.X <= Main.screenWidth + padding
               && relative.Y <= Main.screenHeight + padding;
    }

    /// <summary>
    ///     World light averaged over a coarse grid across the viewport.
    /// </summary>
    /// <remarks>
    ///     A field composite is one full-screen draw, so its shader gets one light value for the
    ///     whole thing no matter how the marked tiles are spread. Nine fixed samples keep that value
    ///     roughly honest as the player moves between a lit surface and a dark cave, at a cost that
    ///     does not grow with the batch.
    /// </remarks>
    protected static Vector3 AverageScreenLight()
    {
        Vector3 light = Vector3.Zero;

        for (int x = 1; x <= 3; x++)
        {
            for (int y = 1; y <= 3; y++)
                light += Lighting.GetSubLight(Main.screenPosition + new Vector2(Main.screenWidth * x / 4f, Main.screenHeight * y / 4f));
        }

        return light / 9f;
    }

    /// <summary>
    ///     Sets the frame-varying parameters every field shader in this system shares: the time base,
    ///     the field's texel size, and the world offset that locks its noise to the world instead of
    ///     to the screen.
    /// </summary>
    /// <remarks>
    ///     Without the world offset the noise swims with the camera, which reads as the effect being
    ///     painted on the monitor rather than on the terrain -- the single most obvious tell that a
    ///     screen-space pass is faking a world-space one.
    /// </remarks>
    protected void ApplyFieldFrameParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(FieldPixelScale / (float)Main.screenWidth, FieldPixelScale / (float)Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // The camera offset above is only half of the world lock. The field bakes the zoom into its
        // own transform, so the shader is handed a screen uv and has to undo that zoom before the
        // offset means anything -- otherwise the error is (1 - Zoom) * cameraCentre, which is zero
        // at Zoom 1 and moves with the camera at every other zoom.
        effect.Parameters["Zoom"]?.SetValue(Main.GameViewMatrix.Zoom);
    }
}
