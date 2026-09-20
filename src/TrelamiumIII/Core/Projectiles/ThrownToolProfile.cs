using Terraria.Audio;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
/// How a tool thrown by <see cref="ThrownTool"/> is launched and what it does to the tiles it passes
/// through.
/// </summary>
/// <remarks>
///     Flight is vanilla's boomerang AI, so out-and-back travel, spin and the catch are not stated
///     here. A tool that needs its own curve should be its own projectile - see
///     <c>BorealHewerProj</c>.
/// </remarks>
public readonly record struct ThrownToolProfile
{
    /// <summary>Launch speed in pixels per tick. Boomerang AI decides how far that carries.</summary>
    public float Speed { get; init; }

    /// <summary>
    /// Full-power hits applied to each tile the head enters. A tile breaks at 100 damage and an ordinary
    /// one takes the tool's pick power per hit, so 3 is what a 35-power pickaxe needs to clear stone in
    /// one pass. Each hit carries the real pick power, so the tool's tier still gates what it can break.
    /// </summary>
    public int MineHits { get; init; }

    /// <summary>Tiles either side of the head that are mined with it. 0 cuts a one-tile tunnel.</summary>
    public int MineRadius { get; init; }

    /// <summary>Sound on the throw, over vanilla's boomerang loop. Null leaves the loop on its own.</summary>
    public SoundStyle? ThrowSound { get; init; }

    /// <summary>Most tools in flight at once. The rework refuses a use past this.</summary>
    public int MaxInFlight { get; init; }

    /// <summary>A pickaxe thrown flat, cutting a one-tile tunnel on the way out and back.</summary>
    public static ThrownToolProfile Pickaxe => new()
    {
        Speed = 11f,
        MineHits = 3,
        MineRadius = 0,
        ThrowSound = null,
        MaxInFlight = 1,
    };
}
