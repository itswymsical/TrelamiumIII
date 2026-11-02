using TrelamiumIII.Common.ChemicalClass;

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

    public float Life;

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

    /// <summary>Ticks since the tile first took the condition. Unlike <see cref="Age" />, survives re-application.</summary>
    public int TotalAge;

    public int Owner;

    /// <summary>
    ///     When this mark first landed, in one order shared by every condition. Survives re-application.
    /// </summary>
    /// <remarks>
    ///     Sorting a tile's marks by this is what makes the oldest of them its parent. See
    ///     <see cref="Reactions.ConditionReaction" />.
    /// </remarks>
    public long Arrival;

    /// <summary>The chemical release that left this mark, or null. Never synced: received applications carry none.</summary>
    public ChemicalPayload? Payload;

    /// <summary>
    ///     Free content-defined state: a burn that has charred through to a second stage, a slime
    ///     that has crusted over. The system never reads it.
    /// </summary>
    public byte Stage;

    /// <summary>
    ///     The continuous metric behind <see cref="Stage" />. The system never reads it either.
    /// </summary>
    /// <remarks>
    ///     <see cref="Stage" /> is what rules branch on, stepped in bands so behaviour doesn't flicker
    ///     across a threshold; this is what it's computed from and what rendering interpolates. Unlike
    ///     <see cref="Intensity" /> (fixed) and <see cref="Fade" /> (only falls), it can climb and then
    ///     drop, like a fire.
    /// </remarks>
    public float Level;

    /// <summary>
    ///     Which flavour of the condition this is: the profile it was applied with.
    /// </summary>
    public byte Variant;

    /// <summary>
    ///     This tile's own colour, or transparent for the condition's palette as authored.
    /// </summary>
    /// <remarks>
    ///     What lets one condition carry every colour of slime: a green slime's splash and a pink
    ///     one's are the same condition with different tints. Replaced, not mixed, by a
    ///     re-application. Only conditions that read it do anything with it; see
    ///     <see cref="FilmCondition" /> and <see cref="TileFilmLook.TintStrength" />.
    /// </remarks>
    public Color Tint;

    public readonly bool HasTint => Tint.A > 0;

    /// <summary>
    ///     Identifies this slot for the eviction queue.
    /// </summary>
    internal int Serial;

    /// <summary>anything not player-made.</summary>
    public const int NoOwner = -1;

    public readonly bool IsOwnedBy(Entity entity) => Owner >= 0 && entity is Player player && player.whoAmI == Owner;

    public readonly Point Coordinates => new(X, Y);

    public readonly Vector2 WorldPosition => new(X * 16f, Y * 16f);

    public readonly Vector2 WorldCenter => new(X * 16f + 8f, Y * 16f + 8f);

    /// <summary>Life remaining as a 0-1</summary>
    public readonly float Fade => MaxLife <= 0f ? 0f : MathHelper.Clamp(Life / MaxLife, 0f, 1f);

    /// <summary>
    ///     <see cref="Intensity" /> scaled by <see cref="Fade" />: what the mark actually looks like
    ///     right now. This is the number nearly all rendering wants.
    /// </summary>
    public readonly float Strength => Intensity * Fade;

    public readonly Tile Tile => Framing.GetTileSafely(X, Y);
}
