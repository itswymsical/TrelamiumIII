using ReLogic.Content;

<<<<<<< HEAD:src/TrelamiumIII/Core/Residues/ITileCanvasSource.cs
namespace TrelamiumIII.Core.Residues;
=======
namespace TrelamiumIII.Core.Tiles.Conditions;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb:src/TrelamiumIII/Core/Tiles/Conditions/ITileCanvasSource.cs

/// <summary>Hands one tile's sample to the canvas being built.</summary>
internal delegate void TileCanvasWriter(Point tile, TileCanvasSample sample, float weight);

/// <summary>What a <see cref="TileCanvas" /> draws: the tiles it stamps, and the shader that composites them.</summary>
internal interface ITileCanvasSource
{
<<<<<<< HEAD:src/TrelamiumIII/Core/Residues/ITileCanvasSource.cs
    /// <inheritdoc cref="IResidue.CanvasPixelScale" />
    int CanvasPixelScale { get; }

    /// <inheritdoc cref="IResidue.FadeOutTicks" />
    int FadeOutTicks { get; }

    /// <inheritdoc cref="IResidue.BlendState" />
    BlendState BlendState { get; }

    /// <inheritdoc cref="IResidue.Shader" />
=======
    /// <inheritdoc cref="ITileCondition.CanvasPixelScale" />
    int CanvasPixelScale { get; }

    /// <inheritdoc cref="ITileCondition.FadeOutTicks" />
    int FadeOutTicks { get; }

    /// <inheritdoc cref="ITileCondition.BlendState" />
    BlendState BlendState { get; }

    /// <inheritdoc cref="ITileCondition.Shader" />
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb:src/TrelamiumIII/Core/Tiles/Conditions/ITileCanvasSource.cs
    Asset<Effect>? Shader { get; }

    /// <summary>
    ///     Whether samples are copied into open neighbours, so coverage holds up to the terrain edge and the mask cuts it.
    ///     Without it coverage falls to about half at the edge, which lets the shader find solid tiles the mask lacks.
    /// </summary>
    bool SpillsIntoOpen { get; }

    /// <summary>Whether any tile is live. A canvas with none and no ghosts releases its targets after a while.</summary>
    bool IsLive { get; }

    /// <summary>Whether <paramref name="tile" /> is stamped by this source, so the canvas neither spills nor ghosts into it.</summary>
    bool Contains(Point tile);

    /// <summary>Writes every live tile inside <paramref name="area" />.</summary>
    void Collect(Rectangle area, TileCanvasWriter write);

<<<<<<< HEAD:src/TrelamiumIII/Core/Residues/ITileCanvasSource.cs
    /// <inheritdoc cref="IResidue.ApplyShaderParameters" />
=======
    /// <inheritdoc cref="ITileCondition.ApplyShaderParameters" />
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb:src/TrelamiumIII/Core/Tiles/Conditions/ITileCanvasSource.cs
    void ApplyShaderParameters(Effect effect);
}
