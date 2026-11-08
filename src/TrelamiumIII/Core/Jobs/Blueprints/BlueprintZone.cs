namespace TrelamiumIII.Core.Jobs.Blueprints;

/// <summary>
///     Where a <see cref="Blueprint"/> may be staked: the whole structure has to land inside <see cref="Area"/>. Read
///     each time it's asked, since the world's spawn can move.
/// </summary>
/// <param name="Area">The allowed area, in tiles.</param>
/// <param name="ProblemKey">Localization key for the hint shown over a blueprint held outside it.</param>
public sealed record BlueprintZone(Func<Rectangle> Area, string ProblemKey)
{
    /// <summary>
    ///     Around the world's spawn point: <paramref name="halfWidth"/> tiles to either side, from
    ///     <paramref name="above"/> tiles over it to <paramref name="below"/> under it.
    /// </summary>
    public static BlueprintZone NearSpawn(int halfWidth = 120, int above = 40, int below = 20) => new(
        () => new Rectangle(Main.spawnTileX - halfWidth, Main.spawnTileY - above, halfWidth * 2 + 1, above + below + 1),
        "Jobs.Blueprint.FarFromSpawn");

    public bool Allows(Rectangle structure) => Area().Contains(structure);
}