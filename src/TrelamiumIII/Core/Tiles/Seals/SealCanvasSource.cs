using ReLogic.Content;
<<<<<<< HEAD
using TrelamiumIII.Core.Residues;
=======
using TrelamiumIII.Core.Tiles.Conditions;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

namespace TrelamiumIII.Core.Tiles.Seals;

/// <summary>One seal type's awake tiles as a canvas source, composited additively through the type's shader.</summary>
internal sealed class SealCanvasSource(SealType type) : ITileCanvasSource
{
    /// <summary>Premultiplied colour added to the terrain.</summary>
    private static readonly BlendState Glow = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    public int CanvasPixelScale => 2;

    public int FadeOutTicks => 0;

    public BlendState BlendState => Glow;

    public Asset<Effect>? Shader => type.Shader;

    /// <summary>Off, so SealField.fx can tell unlit solid tiles, which vanilla leaves out of the terrain mask, from open air.</summary>
    public bool SpillsIntoOpen => false;

    public bool IsLive => SealField.AnyAwake;

    public bool Contains(Point tile) => SealField.IsAwake(tile) && SealField.IsSolidSealed(tile, type.Index);

    public void Collect(Rectangle area, TileCanvasWriter write) => SealField.Collect(type.Index, area, write);

    public void ApplyShaderParameters(Effect effect)
    {
        SealField.ApplyImpulses(effect);
        type.ApplyShaderParameters(effect);
    }
}
