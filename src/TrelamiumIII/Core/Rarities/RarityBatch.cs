namespace TrelamiumIII.Core.Rarities;

/// <summary>
///     The <see cref="SpriteBatch" /> states the rarity renderer swaps between, and the one place the
///     parameters for each are written down.
/// </summary>
/// <remarks>
///     <para>
///         Tooltips and inventory slots are drawn inside a batch the game already opened, so a flair
///         that wants a different blend has to close it, open its own, and put the original back
///         exactly as it was. Getting the restore wrong does not fail loudly -- it leaks a blend
///         state into every interface element drawn after the tooltip, which shows up as unrelated
///         UI quietly going additive.
///     </para>
///     <para>
///         Hence one helper rather than the six hand-copied <c>End/Begin</c> pairs this replaces.
///         Interface layers all open with the same parameters, so restoring them centrally is safe.
///     </para>
/// </remarks>
internal static class RarityBatch
{
    /// <summary>Reopens the interface batch with a different blend, keeping everything else.</summary>
    public static void Restart(SpriteBatch spriteBatch, BlendState blend, SpriteSortMode sort = SpriteSortMode.Deferred)
    {
        spriteBatch.End();

        spriteBatch.Begin(sort, blend, Main.SamplerStateForCursor, DepthStencilState.None,
            Main.Rasterizer, null, Main.UIScaleMatrix);
    }

    /// <summary>Puts the interface batch back the way the game opened it.</summary>
    public static void RestoreInterface(SpriteBatch spriteBatch)
        => Restart(spriteBatch, BlendState.AlphaBlend);

    /// <summary>Reopens the world batch with a different blend and sort.</summary>
    /// <remarks>
    ///     Separate from <see cref="Restart" /> only because of the matrix: world draws are under
    ///     <see cref="Main.GameViewMatrix" />, and handing them the UI matrix puts a dropped item's
    ///     decoration somewhere in the corner of the screen at any zoom but 1.
    /// </remarks>
    public static void RestartWorld(SpriteBatch spriteBatch, BlendState blend, SpriteSortMode sort = SpriteSortMode.Deferred)
    {
        spriteBatch.End();

        spriteBatch.Begin(sort, blend, Main.DefaultSamplerState, DepthStencilState.None,
            Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
    }

    /// <summary>Puts the world batch back the way the game opened it.</summary>
    public static void RestoreWorld(SpriteBatch spriteBatch)
        => RestartWorld(spriteBatch, BlendState.AlphaBlend);

    /// <summary>Reopens whichever batch <paramref name="inWorld" /> names, with the given blend.</summary>
    public static void Restart(SpriteBatch spriteBatch, bool inWorld, BlendState blend, SpriteSortMode sort = SpriteSortMode.Deferred)
    {
        if (inWorld)
            RestartWorld(spriteBatch, blend, sort);
        else
            Restart(spriteBatch, blend, sort);
    }

    /// <summary>Restores whichever batch <paramref name="inWorld" /> names.</summary>
    public static void Restore(SpriteBatch spriteBatch, bool inWorld)
    {
        if (inWorld)
            RestoreWorld(spriteBatch);
        else
            RestoreInterface(spriteBatch);
    }
}
