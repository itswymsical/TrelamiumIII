using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.Rarities;

/// <summary>Switches a <see cref="SpriteBatch" /> between <see cref="SpriteBatchState" />s and puts back exactly the state the caller had.</summary>
internal static class RarityBatch
{
    /// <summary>This state with another blend, sort and effect. The sampler is kept unless one is given.</summary>
    public static SpriteBatchState With(this in SpriteBatchState state, BlendState blend, SpriteSortMode sort = SpriteSortMode.Deferred,
        Effect? effect = null, SamplerState? sampler = null)
        => state with { SortMode = sort, BlendState = blend, Effect = effect, SamplerState = sampler ?? state.SamplerState };

    /// <summary>Ends the active batch and begins <paramref name="state" />.</summary>
    public static void Restart(SpriteBatch spriteBatch, in SpriteBatchState state)
    {
        spriteBatch.End();
        state.Begin(spriteBatch);
    }

    /// <summary>Restarts into <paramref name="next" /> unless it equals <paramref name="current" />, then records it as current.</summary>
    public static void Switch(SpriteBatch spriteBatch, ref SpriteBatchState current, in SpriteBatchState next)
    {
        if (current == next)
            return;

        Restart(spriteBatch, next);
        current = next;
    }
}
