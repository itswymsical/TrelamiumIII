using ReLogic.Content;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues;

public enum ResidueRenderMode
{
    None,

    /// <summary>
    ///     Each tile is a sample on the residue's <see cref="TileCanvas" />, reconstructed as a smooth surface and
    ///     composited over the terrain through <see cref="IResidue.Shader" />.
    /// </summary>
    Canvas,
}

/// <summary>
///     A state that can be laid over tiles: how it looks, how it decays, and what happens to whatever
///     touches it.
/// </summary>
/// <remarks>
///     <para>
///         Residues are flyweights. One instance is registered per type and it holds no per-tile
///         state; <see cref="ResidueSystem" /> owns the batch of
///         <see cref="ResidueInstance" />s and hands each one back by <see langword="ref" />,
///         which is why none of these hooks take a coordinate pair.
///     </para>
///     <para>
///         Implement this through <see cref="Residue" />, which supplies a default for every
///         member.
///     </para>
///     <para>
///         Hooks may add and remove residues, but while a batch is being walked both are deferred
///         to the end of the tick, so don't assume the change has landed by the next line. To end the
///         current tile immediately, set <see cref="ResidueInstance.Life" /> to zero.
///     </para>
/// </remarks>
public interface IResidue
{
    /// <summary>Human-readable name, for logging and debug overlays.</summary>
    string Name { get; }

    float Duration { get; }

    int MaxTiles { get; }

    void Load();

    void Unload();

    bool CanApplyTo(int i, int j);

    void OnApplied(ref ResidueInstance instance);
    void OnExpired(in ResidueInstance instance);

    bool CanRemain(in ResidueInstance instance);

    void UpdateTile(ref ResidueInstance instance);

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
    void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount);

    void OnPlayerContact(ref ResidueInstance instance, Player player);

    void OnNPCContact(ref ResidueInstance instance, NPC npc);

    void OnProjectileContact(ref ResidueInstance instance, Projectile projectile);

    /// <summary>
    ///     What this mark counts as to <see cref="ResidueReaction" />s right now, or
    ///     <see cref="ResidueElement.None" /> to stay out of them.
    /// </summary>
    ResidueElement ElementsOf(in ResidueInstance instance);

    /// <summary>How much of its element this mark holds, 0..1: what thresholds and contests read. A mark at zero sits reactions out.</summary>
    float PotencyOf(in ResidueInstance instance);

    /// <summary>A reaction took <paramref name="share" /> of what this mark has left, 0..1.</summary>
    void Diminish(ref ResidueInstance instance, float share);

    ResidueRenderMode RenderMode { get; }

    /// <summary>Blend for the composite. Canvas shaders output premultiplied colour.</summary>
    BlendState BlendState { get; }

    /// <summary>Divisor on screen resolution for the canvas targets. Shading stays on the tile art grid regardless.</summary>
    int CanvasPixelScale { get; }

    /// <summary>Ticks a newly marked tile takes to reach full weight on the canvas, so marks grow in rather than pop.</summary>
    int FadeInTicks { get; }

    /// <summary>Ticks a removed tile's last sample stays on the canvas, fading, so marks wash away rather than vanish.</summary>
    int FadeOutTicks { get; }

    /// <summary>
    ///     The composite shader. It reads coverage and data from s0, kernel weight and light from <c>uCanvasLight</c>, and
    ///     the solid tile layer from <c>uMaskTex</c>; see <c>ResidueFilmMask.fx</c> for the frame parameters.
    /// </summary>
    Asset<Effect>? Shader { get; }

    /// <summary>
    ///     This tile's sample on the canvas. Scale amount channels by coverage: a sample with no coverage is not kept
    ///     fading after its tile is removed.
    /// </summary>
    TileCanvasSample EncodeCanvas(in ResidueInstance instance);

    /// <summary>Sets the look parameters, after the canvas has set the frame ones.</summary>
    void ApplyShaderParameters(Effect effect);

    /// <summary>Where a body's silhouette is copied for this residue's mask, in pixels with up negative: how far it stands proud of the outline.</summary>
    Point[] BodyReach { get; }

    /// <summary>How this residue moves over a body. See <see cref="Bodies.BodyGrid" />.</summary>
    Bodies.BodyFlow BodyFlow { get; }

    /// <summary>What comes off it while it's on a body, or null for nothing.</summary>
    Bodies.BodyShed? BodyShed { get; }

    /// <summary>
    ///     What rubs off this mark onto a body touching it, or null for nothing. It need not be this residue, as magma leaves
    ///     a body burning rather than coated in magma.
    /// </summary>
    /// <param name="amount">What each tick of touching it adds to the cells it touches, 0..1.</param>
    IResidue? BodyPickup(in ResidueInstance instance, out float amount);

    /// <summary>One cell's sample on a body's canvas. See <see cref="Bodies.BodyCanvas" />.</summary>
    /// <param name="amount">How much of it this cell has, 0..1.</param>
    /// <param name="age">Ticks the coat has been on.</param>
    /// <param name="tint">The coat's own colour, or transparent for the palette as authored.</param>
    TileCanvasSample EncodeBody(float amount, int age, Color tint);

    /// <summary>Sets the look parameters for a body, which is far smaller than what the ground's are scaled for.</summary>
    /// <param name="gravDir">-1 for a body with its feet on the ceiling, whose up is the other way.</param>
    /// <param name="height">Its sprite's height in pixels, for look details that shouldn't shrink to static on something large.</param>
    void ApplyBodyParameters(Effect effect, float gravDir, float height);

    /// <summary>Runs every tick this residue is on a body, for its light and particles.</summary>
    /// <param name="fill">How far up the body it reaches, 0..1.</param>
    void UpdateBody(Entity body, float fill);
}
