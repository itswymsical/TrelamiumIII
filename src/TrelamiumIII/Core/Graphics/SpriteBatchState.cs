namespace TrelamiumIII.Core.Graphics;

/// <summary>The parameters a <see cref="SpriteBatch" /> was begun with, for ending it and resuming it unchanged.</summary>
/// <remarks>Reads FNA's private fields, which the build publicizes.</remarks>
internal readonly record struct SpriteBatchState(
    SpriteSortMode SortMode,
    BlendState BlendState,
    SamplerState SamplerState,
    DepthStencilState DepthStencilState,
    RasterizerState RasterizerState,
    Effect? Effect,
    Matrix Transform)
{
    public static SpriteBatchState Capture(SpriteBatch batch)
        => new(batch.sortMode, batch.blendState, batch.samplerState, batch.depthStencilState, batch.rasterizerState, batch.customEffect, batch.transformMatrix);

    public void Begin(SpriteBatch batch)
        => batch.Begin(SortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, Transform);
}
