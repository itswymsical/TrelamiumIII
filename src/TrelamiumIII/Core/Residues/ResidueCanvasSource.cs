using System.Collections.Generic;
using ReLogic.Content;

namespace TrelamiumIII.Core.Residues;

/// <summary>A residue's batch as a canvas source: every instance is one sample, weighted by its fade-in.</summary>
internal sealed class ResidueCanvasSource(IResidue residue, Dictionary<Point, ResidueInstance> tiles) : ITileCanvasSource
{
    public IResidue Residue => residue;

    public int CanvasPixelScale => residue.CanvasPixelScale;

    public int FadeOutTicks => residue.FadeOutTicks;

    public BlendState BlendState => residue.BlendState;

    public Asset<Effect>? Shader => residue.Shader;

    public bool SpillsIntoOpen => true;

    public bool IsLive => tiles.Count > 0;

    public bool Contains(Point tile) => tiles.ContainsKey(tile);

    public void Collect(Rectangle area, TileCanvasWriter write)
    {
        foreach (KeyValuePair<Point, ResidueInstance> pair in tiles)
        {
            if (!area.Contains(pair.Key))
                continue;

            ResidueInstance instance = pair.Value;
            float weight = FadeInWeight(in instance);

            if (weight > 0f)
                write(pair.Key, residue.EncodeCanvas(in instance), weight);
        }
    }

    public void ApplyShaderParameters(Effect effect) => residue.ApplyShaderParameters(effect);

    /// <summary>Keeps a removed tile's last sample on <paramref name="canvas" /> while it fades out.</summary>
    public void OnRemoved(TileCanvas canvas, in ResidueInstance instance)
    {
        if (residue.FadeOutTicks <= 0 || !instance.Tile.HasTile)
            return;

        canvas.AddGhost(instance.Coordinates, residue.EncodeCanvas(in instance), FadeInWeight(in instance));
    }

    private float FadeInWeight(in ResidueInstance instance)
        => residue.FadeInTicks <= 0 ? 1f : MathF.Min((instance.TotalAge + 1f) / residue.FadeInTicks, 1f);
}
