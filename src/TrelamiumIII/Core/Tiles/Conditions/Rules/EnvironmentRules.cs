using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Core.Tiles.Conditions.Rules;

// The world's own conditions. Each rule only says when it runs and which tiles it wants; see
// TileConditionRule for how the sweep, the refresh and the determinism work.

/// <summary>
///     Rime across the snow biome, surface and underground: on most exposed snow and ice, and on the
///     odd block of anything else near them. Hashed per tile, so the same blocks are frosted every
///     visit and it reads as part of the world rather than as weather.
/// </summary>
public sealed class SnowFrostRule : TileConditionRule
{
    public override int SweepTicks => 40;

    public override bool IsActive(Player player) => player.ZoneSnow;

    public override void Visit(int i, int j, Tile tile)
    {
        if (!tile.HasTile || !IsExposed(i, j))
            return;

        bool icy = IsIcy(tile.TileType);
        float chance = icy ? 0.8f : NearIce(i, j) ? 0.25f : 0f;

        if (Hash(i, j, salt: 1) >= chance)
            return;

        Maintain<FrostCondition>(i, j, MathHelper.Lerp(0.55f, 1f, Hash(i, j, salt: 2)));
    }

    private static bool IsIcy(ushort type)
        => type is TileID.SnowBlock or TileID.IceBlock or TileID.CorruptIce or TileID.HallowedIce or TileID.FleshIce
            or TileID.SnowBrick or TileID.IceBrick or TileID.Slush;

    /// <summary>Snow or ice within two tiles along either axis, so the frost thins out at the biome's edge.</summary>
    private static bool NearIce(int i, int j)
    {
        for (int d = 1; d <= 2; d++)
        {
            if (IcyAt(i - d, j) || IcyAt(i + d, j) || IcyAt(i, j - d) || IcyAt(i, j + d))
                return true;
        }

        return false;
    }

    private static bool IcyAt(int i, int j)
        => WorldGen.InWorld(i, j) && Main.tile[i, j] is { HasTile: true } tile && IsIcy(tile.TileType);
}

/// <summary>A blizzard blows ice onto everything on the surface the sky can see.</summary>
public sealed class BlizzardRule : TileConditionRule
{
    public override int SweepTicks => 20;

    public override bool IsActive(Player player)
        => Main.raining && player.ZoneSnow && (player.ZoneOverworldHeight || player.ZoneSkyHeight);

    public override void Visit(int i, int j, Tile tile)
    {
        if (j > Main.worldSurface || !IsTopSurface(i, j) || !IsSkyExposed(i, j))
            return;

        float severity = MathHelper.Clamp(Main.maxRaining, 0.35f, 1f);

        Maintain<FrostCondition>(i, j, severity * MathHelper.Lerp(0.75f, 1f, Hash(i, j, salt: 3)));
    }
}

/// <summary>Rain soaks the surface it falls on. The stain outlasts the rain and dries off after.</summary>
public sealed class RainSoakRule : TileConditionRule
{
    /// <summary>Darker and greyer than a pool's stain: soaked ground, not standing water.</summary>
    private static readonly Color RainTint = new(70, 95, 130);

    private const float DRYING_TICKS = 45f * 60f;

    public override int SweepTicks => 30;

    public override bool IsActive(Player player)
        => Main.raining && !player.ZoneSnow && !player.ZoneDesert && (player.ZoneOverworldHeight || player.ZoneSkyHeight);

    public override void Visit(int i, int j, Tile tile)
    {
        if (j > Main.worldSurface || !IsTopSurface(i, j) || !IsSkyExposed(i, j))
            return;

        float wetness = MathHelper.Lerp(0.45f, 0.8f, Main.maxRaining) * MathHelper.Lerp(0.8f, 1f, Hash(i, j, salt: 4));

        Maintain<LiquidResidueCondition>(i, j, wetness, DRYING_TICKS, tint: RainTint);
    }
}

/// <summary>
///     Stains the terrain wherever liquid sits against it -- the bed under a pool, its banks, the
///     ceiling of a flooded cave -- in that liquid's colour.
/// </summary>
public sealed class LiquidContactRule : TileConditionRule
{
    public override int SweepTicks => 24;

    public override void Visit(int i, int j, Tile tile)
    {
        if (!tile.HasTile || !WorldGen.SolidTile(i, j))
            return;

        if (!Wettest(i, j, out Point liquid, out int amount))
            return;

        if (TileConditionSystem.TryGet<LiquidResidueCondition>(i, j, out TileConditionInstance existing) && existing.Fade > 0.5f)
            return;

        LiquidResidueCondition.Stain(i, j, liquid.X, liquid.Y, MathHelper.Lerp(0.5f, 1f, amount / 255f));
    }

    /// <summary>The fullest of the four neighbours holding liquid, if any does.</summary>
    private static bool Wettest(int i, int j, out Point liquid, out int amount)
    {
        liquid = default;
        amount = 0;

        foreach (Point next in (ReadOnlySpan<Point>)[new(i, j - 1), new(i - 1, j), new(i + 1, j), new(i, j + 1)])
        {
            int held = Main.tile[next.X, next.Y].LiquidAmount;

            if (held <= amount)
                continue;

            amount = held;
            liquid = next;
        }

        return amount > 0;
    }
}
