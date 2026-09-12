using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Where a condition puts its marks.
/// </summary>
[Flags]
public enum TileConditionRenderMode
{
    /// <summary>no draw mode for effect-only conditions.</summary>
    None = 0,

    Overlay = 1,

    /// <summary>
    ///     Every tile is stamped into an off-screen field, and the whole field is composited to the
    ///     screen once through the condition's shader.
    /// </summary>
    Field = 2,
}

/// <summary>
///     A state that can be laid over tiles: how it looks, how it decays, and what happens to whatever
///     touches it.
/// </summary>
/// <remarks>
///     <para>
///         Conditions are flyweights. One instance is registered per type and it holds no per-tile
///         state; <see cref="TileConditionSystem" /> owns the batch of
///         <see cref="TileConditionInstance" />s and hands each one back by <see langword="ref" />,
///         which is why none of these hooks take a coordinate pair.
///     </para>
///     <para>
///         Implement this through <see cref="TileCondition" />, which supplies a default for every
///         member.
///     </para>
///     <para>
///         Hooks may add and remove conditions, but while a batch is being walked both are deferred
///         to the end of the tick, so don't assume the change has landed by the next line. To end the
///         current tile immediately, set <see cref="TileConditionInstance.Life" /> to zero.
///     </para>
/// </remarks>
public interface ITileCondition
{
    /// <summary>Human-readable name, for logging and debug overlays.</summary>
    string Name { get; }

    float Duration { get; }

    int MaxTiles { get; }

    void Load();

    void Unload();

    bool CanApplyTo(int i, int j);

    void OnApplied(ref TileConditionInstance instance);
    void OnExpired(in TileConditionInstance instance);

    bool CanRemain(in TileConditionInstance instance);

    void UpdateTile(ref TileConditionInstance instance);

    void UpdateGlobal();

    /// <summary>
    ///     Liquid found in the tile or one of its four neighbours, wettest first.
    /// </summary>
    /// <remarks>
    ///     Probed one tick in eight per tile, staggered across the batch so the whole thing never
    ///     samples on the same frame. Scale anything done here accordingly.
    /// </remarks>
    /// <param name="liquidType">A <see cref="LiquidID" />.</param>
    /// <param name="amount">0..255.</param>
    void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount);

    void OnPlayerContact(ref TileConditionInstance instance, Player player);

    void OnNPCContact(ref TileConditionInstance instance, NPC npc);

    void OnProjectileContact(ref TileConditionInstance instance, Projectile projectile);

    TileConditionRenderMode RenderMode { get; }

    BlendState BlendState { get; }

    BlendState FieldBlend { get; }

    int FieldPixelScale { get; }

    Asset<Effect>? Shader { get; }

    /// <summary>
    ///     The effect the <see cref="TileConditionRenderMode.Overlay" /> pass is drawn through.
    /// </summary>
    /// <remarks>
    ///     Defaults to <see cref="Shader" />, which is correct for a single-pass condition. A condition
    ///     running both passes needs them separate: <see cref="Shader" /> samples field coverage from
    ///     register s0, and pointed at individual tile sprites it would read a block's own alpha as
    ///     coverage. Return null to draw the overlay plainly.
    /// </remarks>
    Asset<Effect>? OverlayShader { get; }

    /// <summary>
    ///     The effect the stamps are drawn <em>into the field</em> through. Field mode only.
    /// </summary>
    /// <remarks>
    ///     Null for a condition whose field only has to answer "is there coverage here", which the
    ///     alpha channel already does. Set it when the field has to carry something per tile: a
    ///     stamp shader can replace the tile sprite's own colour with the draw's tint, which turns
    ///     the field's three unused colour channels into per-tile storage. See
    ///     <see cref="Graphics.WorldFieldTarget.StampEffect" />.
    /// </remarks>
    Asset<Effect>? StampShader { get; }

    void ApplyShaderParameters(Effect effect);

    /// <summary>Bind anything the shader samples beyond register s0.</summary>
    void BindAdditionalTextures(GraphicsDevice device);

    /// <summary>
    ///     Draws one tile's mark. Called per visible tile, to the screen under
    ///     <see cref="TileConditionRenderMode.Overlay" /> and into the field target under
    ///     <see cref="TileConditionRenderMode.Field" />.
    /// </summary>
    /// <param name="drawPosition">
    ///     The tile's top-left corner, already in the coordinate space of whichever batch is running.
    /// </param>
    void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition);

    /// <summary>
    ///     Draws one tile's mark in the <see cref="TileConditionRenderMode.Overlay" /> pass, for a
    ///     condition that runs both passes.
    /// </summary>
    /// <remarks>
    ///     The render mode is a flag set, so a condition may ask for a field <em>and</em> a per-tile
    ///     draw, and the two want different geometry. Burn is the case: the fire is a field so it can
    ///     be continuous across tile seams, while the char is per tile because it is the block itself
    ///     darkening, not soot laid over it. Defaults to <see cref="DrawTile" />.
    /// </remarks>
    void DrawTileOverlay(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition);
}
