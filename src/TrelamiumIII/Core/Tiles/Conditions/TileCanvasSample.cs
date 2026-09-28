namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     One tile's contribution to its condition's canvas: coverage and three condition-defined channels, each 0..1.
/// </summary>
/// <remarks>
///     Every channel is interpolated between tile centres on its own, reading zero where no tile contributes. A shader
///     divides a channel by the canvas weight to read it as a local average instead.
/// </remarks>
public readonly record struct TileCanvasSample(float Coverage, Vector3 Data = default);
