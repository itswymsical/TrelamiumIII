using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Utilities.Extensions;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Base for every <see cref="ITileCondition" />: a default for each member of the contract, plus
///     the handful of drawing and sampling helpers a condition normally wants.
/// </summary>
/// <remarks>
///     Derived types are discovered and registered automatically by
///     <see cref="TileConditionSystem" />, so a new condition is one file and nothing else. It needs
///     a parameterless constructor and it must not keep per-tile state: see
///     <see cref="ITileCondition" /> for why.
/// </remarks>
public abstract class TileCondition : ITileCondition
{
    public virtual string Name => GetType().Name;

    public virtual float Duration => 8f * 60f;

    public virtual int MaxTiles => 1024;

    public virtual void Load() { }

    public virtual void Unload() { }

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

    public virtual Asset<Effect>? StampShader => null;

    public virtual Asset<Effect>? OverlayShader => Shader;

    public virtual void ApplyShaderParameters(Effect effect) { }

    public virtual void BindAdditionalTextures(GraphicsDevice device) { }

    public virtual void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition) { }

    public virtual void DrawTileOverlay(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
        => DrawTile(spriteBatch, in instance, drawPosition);

    /// <summary>
    ///     Draws the tile's own sprite in <paramref name="color" />, so the mark takes the exact
    ///     shape of the terrain (slopes, half blocks and merged edges included).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Batches into <see cref="Main.spriteBatch" /> because of
    ///         slopes.
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

        DrawAsTile(in instance, texture, color);
    }

    /// <summary>
    ///     Draws <paramref name="texture" /> in the shape of the tile this instance sits on.
    /// </summary>
    protected static void DrawAsTile(in TileConditionInstance instance, Texture2D texture, Color color)
    {
        // TileOffset is the offscreen-range shift of the tile render target's own pass, which
        // neither of our passes runs inside.
        //
        // The pixel snap: DrawSloped positions against the float Main.screenPosition, while vanilla
        // draws tiles against its truncated integer, so without it the mark crawls up to a pixel
        // against the terrain as the camera moves.
        var pixelSnap = new Vector2(
            Main.screenPosition.X - (int)Main.screenPosition.X,
            Main.screenPosition.Y - (int)Main.screenPosition.Y);

        TileExtensions.DrawSloped(instance.X, instance.Y, texture, color, pixelSnap - TileExtensions.TileOffset);
    }

    /// <summary>Whether the tile is close enough to the camera to be worth spending anything on.</summary>
    /// <remarks>
    ///     Per-tile update runs for the whole batch, on and off screen alike, because the simulation
    ///     has to stay consistent. Dust, light and sound dont, and a thousand tiles all rolling for
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
    ///     whole thing no matter how the marked tiles are spread.
    /// </remarks>
    protected static Vector3 AverageScreenLight()
    {
        Vector3 light = Vector3.Zero;

        for (int x = 1; x <= 3; x++)
        {
            for (int y = 1; y <= 3; y++)
                light += Lighting.GetSubLight(Main.screenPosition + new Vector2(Main.screenWidth * x / 4f, Main.screenHeight * y / 4f));
        }

        return light / 9f; // Nine fixed samples keep the value roughly honest as the player moves between a lit surface and a dark cave
    }

    /// <summary>
    ///     Hands the shader the frame, on the same terms a <see cref="FieldLayer" /> does: the time
    ///     base, the field's texel size, and the camera offset and zoom that lock its noise to the
    ///     world rather than to the screen.
    /// </summary>
    /// <remarks>
    ///     Delegated rather than duplicated, and it matters more than tidiness. A condition drawing
    ///     through <c>GasFbm</c> shares one <see cref="Effect" /> instance with every field layer,
    ///     so a parameter it never sets holds whatever the last layer left there -- or zero, if it
    ///     draws first. Several of them are ruinous at zero: no <c>FieldGain</c> discards every
    ///     pixel. Setting them in one place is what stops that being a question of draw order.
    /// </remarks>
    protected void ApplyFieldFrameParameters(Effect effect)
        => FieldLayer.ApplyScreenParameters(effect, FieldPixelScale);
}
