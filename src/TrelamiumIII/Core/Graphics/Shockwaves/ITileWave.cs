using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>A displacement applied to the solid tile layer as one full-target pass of <see cref="TileShockwaveSystem" />.</summary>
/// <remarks>
///     <c>sceneOrigin</c> is the world position of target texel (0,0): the <see cref="Main.sceneTilePos" /> captured
///     once per pass, not <see cref="Main.screenPosition" />.
/// </remarks>
internal interface ITileWave
{
    bool Finished { get; }

    void Update();

    /// <summary>Whether the wave touches the target this frame; false skips its pass.</summary>
    bool Intersects(RenderTarget2D target, Vector2 sceneOrigin);

    /// <summary>Pushes this frame's uniforms and returns the shader for the system to apply.</summary>
    /// <param name="write">The surface being rendered into, for size-dependent uniforms.</param>
    /// <param name="sceneOrigin">World position of target texel (0,0).</param>
    ShaderData PrepareShader(RenderTarget2D write, Vector2 sceneOrigin);
}
