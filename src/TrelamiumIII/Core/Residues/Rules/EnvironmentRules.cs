using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Residues.Rules;

// The world's own residues. Each rule only says when it runs and which tiles it wants; see
// ResidueRule for how the sweep, the refresh and the determinism work.

/// <summary>
///     A blizzard, which is what vanilla's rain is in the snow biome, blows ice onto everything on the
///     surface the sky can see. Snow is left bare, being ice already; ice takes it, hardening over. Once
///     the snow stops the frost is left to melt.
/// </summary>
public sealed class BlizzardRule : ResidueRule
{
    public override int SweepTicks => 20;

    public override bool IsActive(Player player)
        => Main.raining && player.ZoneSnow && (player.ZoneOverworldHeight || player.ZoneSkyHeight);

    public override void Visit(int i, int j, Tile tile)
    {
        if (j > Main.worldSurface || IsSnow(tile.TileType) || !IsTopSurface(i, j) || !IsSkyExposed(i, j))
            return;

        float severity = MathHelper.Clamp(Main.maxRaining, 0.35f, 1f);

        Maintain<FrostResidue>(i, j, severity * MathHelper.Lerp(0.75f, 1f, Hash(i, j, salt: 3)));
    }

    private static bool IsSnow(ushort type) => type is TileID.SnowBlock or TileID.SnowBrick;
}

/// <summary>
///     Stains the terrain wherever liquid sits against it -- the bed under a pool, its banks, the
///     ceiling of a flooded cave -- in that liquid's colour.
/// </summary>
public sealed class LiquidContactRule : ResidueRule
{
    public override int SweepTicks => 24;

    public override void Visit(int i, int j, Tile tile)
    {
        if (!tile.HasTile || !WorldGen.SolidTile(i, j))
            return;

        if (!Wettest(i, j, out Point liquid, out int amount))
            return;

        if (CrudeOilResidue.SeepsFrom(liquid.X, liquid.Y))
        {
            CrudeOilResidue.Seep(i, j, MathHelper.Lerp(0.5f, 1f, amount / 255f));

            return;
        }

        if (ResidueSystem.TryGet<LiquidResidue>(i, j, out ResidueInstance existing) && existing.Fade > 0.5f)
            return;

        LiquidResidue.Stain(i, j, liquid.X, liquid.Y, MathHelper.Lerp(0.5f, 1f, amount / 255f));
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
