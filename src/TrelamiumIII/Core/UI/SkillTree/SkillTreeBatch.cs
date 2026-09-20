using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>
/// Restarts the interface batch point-sampled and optionally clipped, then puts back exactly the state
/// the caller had. Applied per draw rather than per element, because <see cref="Terraria.UI.UIElement.Draw"/>
/// hardcodes AnisotropicClamp around overflow and immediate-mode children.
/// </summary>
internal static class SkillTreeBatch
{
    private static readonly RasterizerState Clipping = new() { CullMode = CullMode.None, ScissorTestEnable = true };

    /// <summary>Batch and device state captured by <see cref="Point"/>, to be handed back to <see cref="Restore"/>.</summary>
    internal readonly record struct Scope(SpriteBatchState State, Rectangle Scissor, RasterizerState Rasterizer);

    /// <summary>Restarts point-sampled, keeping the caller's blend and transform. <paramref name="clip"/> is in screen pixels.</summary>
    public static Scope Point(SpriteBatch spriteBatch, Rectangle? clip = null)
    {
        GraphicsDevice device = spriteBatch.GraphicsDevice;
        var scope = new Scope(SpriteBatchState.Capture(spriteBatch), device.ScissorRectangle, device.RasterizerState);

        spriteBatch.End();

        if (clip is { } rectangle)
            device.ScissorRectangle = Rectangle.Intersect(rectangle, scope.Scissor);

        device.RasterizerState = Clipping;

        spriteBatch.Begin(scope.State.SortMode, scope.State.BlendState, SamplerState.PointClamp,
            scope.State.DepthStencilState, Clipping, scope.State.Effect, scope.State.Transform);

        return scope;
    }

    public static void Restore(SpriteBatch spriteBatch, in Scope scope)
    {
        spriteBatch.End();

        spriteBatch.GraphicsDevice.ScissorRectangle = scope.Scissor;
        spriteBatch.GraphicsDevice.RasterizerState = scope.Rasterizer;

        scope.State.Begin(spriteBatch);
    }
}
