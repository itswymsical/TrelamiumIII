using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Where a condition puts its marks.
/// </summary>
public enum TileConditionRenderMode
{
    /// <summary>Nothing is drawn. For conditions that are purely mechanical.</summary>
    None,

    /// <summary>
    ///     One sprite draw per tile, straight to the screen, in a batch that can carry a shader.
    /// </summary>
    /// <remarks>
    ///     Exact and cheap, and the right choice when the mark belongs to its tile and nothing else
    ///     -- a wet sheen, a glowmask, a decal. Its limit is that a sprite draw only ever sees the
    ///     fragment it is on, so nothing here can know that the tile next door is marked too.
    /// </remarks>
    Overlay,

    /// <summary>
    ///     Every tile is stamped into an off-screen field, and the whole field is composited to the
    ///     screen once through the condition's shader.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the mode for anything that has to be continuous across tile boundaries. The
    ///         shader reads a neighbourhood rather than a fragment, so it can fuse adjacent stamps
    ///         into one silhouette, erode the whole region with world-locked noise, or find the
    ///         outline of the marked area -- none of which a per-tile draw can do.
    ///     </para>
    ///     <para>
    ///         The two useful shapes it takes are the same mechanism with a different stamp. Soft
    ///         radial blobs summed additively and cut at a threshold give metaballs: neighbouring
    ///         tiles merge into one gooey sheet. Tile silhouettes alpha-blended in give a coverage
    ///         mask: the shader gets the real shape of the marked terrain, slopes and half blocks
    ///         included, and can char or frost it with a noise pattern that runs across tile seams
    ///         instead of repeating every 16 pixels. The second is why this beats a 16x16 overlay
    ///         texture for burn and freeze.
    ///     </para>
    /// </remarks>
    Field,
}

/// <summary>
///     A state that can be laid over tiles: how it looks, how it decays, and what happens to whatever
///     touches it.
/// </summary>
/// <remarks>
///     <para>
///         Conditions are flyweights. One instance is registered per type and it holds no per-tile
///         state; <see cref="TileConditionSystem" /> owns the batch of
///         <see cref="TileConditionInstance" />s and hands each one back by <see langword="ref" />.
///         That split is what lets the same condition object answer for ten tiles or ten thousand,
///         and it is why none of these hooks take a coordinate pair -- the instance carries it.
///     </para>
///     <para>
///         Implement this through <see cref="TileCondition" /> unless there is a reason not to; the
///         base supplies a default for every member here. The interface exists because it is the
///         whole contract in one place, and because the system talks to nothing else.
///     </para>
///     <para>
///         Hooks may add and remove conditions -- the system makes it safe by deferring both to the
///         end of the tick while a batch is being walked -- but they must not assume the change has
///         landed by the next line. A hook that wants to end its own tile sets
///         <see cref="TileConditionInstance.Life" /> to zero instead, which takes effect immediately.
///     </para>
/// </remarks>
public interface ITileCondition
{
    /// <summary>Human-readable name, for logging and debug overlays.</summary>
    string Name { get; }

    /// <summary>Ticks a fresh application lasts, when the caller does not override it.</summary>
    float Duration { get; }

    /// <summary>
    ///     Ceiling on tiles held at once. Past it the oldest are dropped, so a runaway emitter costs
    ///     a patchier effect rather than the frame rate.
    /// </summary>
    int MaxTiles { get; }

    /// <summary>Called once on registration. Load assets here; the field target is not built yet.</summary>
    void Load();

    void Unload();

    /// <summary>
    ///     Whether the condition may be laid on this tile at all.
    /// </summary>
    /// <remarks>
    ///     Also the place to resolve a clash with another condition, because it is the one hook that
    ///     runs before anything has been written. A burn landing on a soaked tile boils the water off
    ///     and refuses, which is both halves of "water wins" in a single answer.
    /// </remarks>
    bool CanApplyTo(int i, int j);

    /// <summary>
    ///     Called on a fully built instance just before it enters the batch, so it can be adjusted.
    /// </summary>
    void OnApplied(ref TileConditionInstance instance);

    /// <summary>Called once the instance has already left the batch, expired or evicted.</summary>
    void OnExpired(in TileConditionInstance instance);

    /// <summary>
    ///     Whether the instance is still valid where it sits. Returning false ends it this tick.
    /// </summary>
    /// <remarks>The default answer is "the tile is still there", which covers being mined out.</remarks>
    bool CanRemain(in TileConditionInstance instance);

    /// <summary>Per-tile tick. Life has already been decremented and Age incremented.</summary>
    void UpdateTile(ref TileConditionInstance instance);

    /// <summary>
    ///     Per-condition tick, once per frame regardless of how many tiles are held -- including
    ///     none. Where a condition that seeds itself does the seeding.
    /// </summary>
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

    /// <summary>
    ///     A player's hitbox overlaps this tile. Fires every tick of contact, per tile touched, so
    ///     anything expensive or repeated wants rate-limiting here.
    /// </summary>
    void OnPlayerContact(ref TileConditionInstance instance, Player player);

    /// <inheritdoc cref="OnPlayerContact" />
    void OnNPCContact(ref TileConditionInstance instance, NPC npc);

    /// <inheritdoc cref="OnPlayerContact" />
    void OnProjectileContact(ref TileConditionInstance instance, Projectile projectile);

    TileConditionRenderMode RenderMode { get; }

    /// <summary>
    ///     How the condition's output blends with the screen: the batch blend under
    ///     <see cref="TileConditionRenderMode.Overlay" />, the composite blend under
    ///     <see cref="TileConditionRenderMode.Field" />.
    /// </summary>
    BlendState BlendState { get; }

    /// <summary>
    ///     How stamps accumulate inside the field. Field mode only.
    /// </summary>
    /// <remarks>
    ///     Additive for metaballs, where neighbouring blobs have to sum past the shader's threshold
    ///     to fuse. AlphaBlend for a coverage mask, where the useful reading is the alpha channel and
    ///     premultiplied alpha is the one blend that leaves it linear in the stamp's own alpha.
    /// </remarks>
    BlendState FieldBlend { get; }

    /// <summary>Divisor on screen resolution for the field target. Field mode only.</summary>
    int FieldPixelScale { get; }

    /// <summary>The effect the marks are drawn through, or null to draw them plainly.</summary>
    Asset<Effect>? Shader { get; }

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
}
