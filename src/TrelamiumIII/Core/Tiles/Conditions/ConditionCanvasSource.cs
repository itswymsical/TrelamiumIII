using System.Collections.Generic;
using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>A tile condition's batch as a canvas source: every instance is one sample, weighted by its fade-in.</summary>
internal sealed class ConditionCanvasSource(ITileCondition condition, Dictionary<Point, TileConditionInstance> tiles) : ITileCanvasSource
{
    public int CanvasPixelScale => condition.CanvasPixelScale;

    public int FadeOutTicks => condition.FadeOutTicks;

    public BlendState BlendState => condition.BlendState;

    public Asset<Effect>? Shader => condition.Shader;

    public bool SpillsIntoOpen => true;

    public bool IsLive => tiles.Count > 0;

    public bool Contains(Point tile) => tiles.ContainsKey(tile);

    public void Collect(Rectangle area, TileCanvasWriter write)
    {
        foreach (KeyValuePair<Point, TileConditionInstance> pair in tiles)
        {
            if (!area.Contains(pair.Key))
                continue;

            TileConditionInstance instance = pair.Value;
            float weight = FadeInWeight(in instance);

            if (weight > 0f)
                write(pair.Key, condition.EncodeCanvas(in instance), weight);
        }
    }

    public void ApplyShaderParameters(Effect effect) => condition.ApplyShaderParameters(effect);

    /// <summary>Keeps a removed tile's last sample on <paramref name="canvas" /> while it fades out.</summary>
    public void OnRemoved(TileCanvas canvas, in TileConditionInstance instance)
    {
        if (condition.FadeOutTicks <= 0 || !instance.Tile.HasTile)
            return;

        canvas.AddGhost(instance.Coordinates, condition.EncodeCanvas(in instance), FadeInWeight(in instance));
    }

    private float FadeInWeight(in TileConditionInstance instance)
        => condition.FadeInTicks <= 0 ? 1f : MathF.Min((instance.TotalAge + 1f) / condition.FadeInTicks, 1f);
}
