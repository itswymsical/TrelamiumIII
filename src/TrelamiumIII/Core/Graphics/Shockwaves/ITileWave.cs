using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>
///     One displacement effect applied to the solid tile layer as a single full-target pass.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="TileShockwaveSystem" /> owns the surfaces, the ping-pong and the frame
///         timing; a wave owns its own shape, its own shader and its own lifetime. That split is
///         what lets a smooth ring and a tile-quantised block heave share one pipeline without the
///         system knowing which is which.
///     </para>
///     <para>
///         <b>The coordinate contract.</b> Every method here is handed <c>sceneOrigin</c>, the
///         world position that target texel (0,0) corresponds to, which is <see cref="Main.sceneTilePos" />
///         and <em>not</em> <see cref="Main.screenPosition" />. It is passed in rather than read
///         from the global because the system captures it once, before the frame's tile re-render
///         may move it, and every wave in the pass must use that same captured value.
///     </para>
/// </remarks>
internal interface ITileWave
{
    bool Finished { get; }

    void Update();

    /// <summary>
    ///     Whether the wave touches the target at all this frame.
    /// </summary>
    /// <remarks>
    ///     Returning false skips a full-target pass, which at 1080p is a 2304x1464 blit. Waves
    ///     routinely outgrow the screen well before they expire, so this is not a micro-optimisation.
    /// </remarks>
    bool Intersects(RenderTarget2D target, Vector2 sceneOrigin);

    /// <summary>
    ///     Loads the wave's shader if needed, pushes this frame's uniforms into it, and hands it
    ///     back for the system to <see cref="ShaderData.Apply" /> inside its own batch.
    /// </summary>
    /// <param name="write">The surface being rendered into, for size-dependent uniforms.</param>
    /// <param name="sceneOrigin">World position of target texel (0,0).</param>
    ShaderData PrepareShader(RenderTarget2D write, Vector2 sceneOrigin);
}
