namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     One tile's worth of a condition: where it sits, how long it has left, and how hard it took.
/// </summary>
/// <remarks>
///     Instances are owned by <see cref="TileConditionSystem" /> and handed to the condition by
///     <see langword="ref" />, so a hook that wants to end its own tile early sets
///     <see cref="Life" /> to zero rather than reaching back into the system to remove it. That is
///     the only removal channel available from inside a hook, and it is deliberate: the system is
///     mid-iteration over the batch at the time.
/// </remarks>
public struct TileConditionInstance
{
    public int X;

    public int Y;

    /// <summary>Ticks left before the condition lifts. Zero or less ends it on the current tick.</summary>
    public float Life;

    /// <summary>The life this instance started with, so <see cref="Fade" /> has something to divide by.</summary>
    public float MaxLife;

    /// <summary>
    ///     How hard the condition took, 0..1, fixed at application and never decayed.
    /// </summary>
    /// <remarks>
    ///     Kept apart from <see cref="Fade" /> because the two answer different questions. A flask
    ///     that lands square on a tile and one that only clips its corner both fade out over the same
    ///     seconds, and only intensity says that the second one was always the fainter mark.
    /// </remarks>
    public float Intensity;

    /// <summary>Ticks since the condition was applied. Reset by a re-application.</summary>
    public int Age;

    /// <summary>
    ///     The player whose weapon laid this down, or <see cref="NoOwner" />.
    /// </summary>
    /// <remarks>
    ///     Carried so a condition can tell its author apart from everyone else standing in it. The
    ///     case it exists for is the one every alchemical weapon runs into: a flask leaves a patch
    ///     that keeps working for twenty seconds, the thrower has to walk through it to follow up,
    ///     and without an owner the only two options are a residue that hurts the person who made
    ///     it or one that hurts nobody. Refreshed along with the rest of the slot, so a second
    ///     player pouring over the same tile takes it over.
    /// </remarks>
    public int Owner;

    /// <summary>
    ///     Free content-defined state -- a burn that has charred through to a second stage, a slime
    ///     that has crusted over. The system never reads it.
    /// </summary>
    public byte Stage;

    /// <summary>
    ///     Identifies this slot for the eviction queue.
    /// </summary>
    /// <remarks>
    ///     The queue holds positions in insertion order, but a position can expire and be applied
    ///     again while its old queue entry is still sitting there. The serial is what lets the
    ///     evictor tell the tile it queued from the unrelated tile that now occupies the same
    ///     coordinates, so a fresh mark is not dropped to make room for itself. Held stable across a
    ///     re-application of the same live slot, which is what keeps the queue entry valid.
    /// </remarks>
    internal int Serial;

    /// <summary>Terrain nobody marked: world hazards, ambient decay, anything not player-made.</summary>
    public const int NoOwner = -1;

    public readonly bool IsOwnedBy(Entity entity) => Owner >= 0 && entity is Player player && player.whoAmI == Owner;

    public readonly Point Coordinates => new(X, Y);

    /// <summary>Top-left corner of the tile, in world coordinates.</summary>
    public readonly Vector2 WorldPosition => new(X * 16f, Y * 16f);

    public readonly Vector2 WorldCenter => new(X * 16f + 8f, Y * 16f + 8f);

    /// <summary>Life remaining as a 0..1 fraction.</summary>
    public readonly float Fade => MaxLife <= 0f ? 0f : MathHelper.Clamp(Life / MaxLife, 0f, 1f);

    /// <summary>
    ///     <see cref="Intensity" /> scaled by <see cref="Fade" />: what the mark actually looks like
    ///     right now. This is the number nearly all rendering wants.
    /// </summary>
    public readonly float Strength => Intensity * Fade;

    public readonly Tile Tile => Framing.GetTileSafely(X, Y);
}
