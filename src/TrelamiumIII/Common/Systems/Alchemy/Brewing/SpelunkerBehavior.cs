using System;

namespace TrelamiumIII.Common.Systems.Alchemy.Brewing;

/// <summary>
/// Tier-gated Spelunker. Vanilla lights every ore whose <c>Main.tileSpelunker[type]</c> flag is set
/// while the Spelunker buff is up; this rides that renderer, masking ore types deeper than the brew's
/// <see cref="EffectParams.MaxOreTier"/> so a weak brew only reveals shallow ores. The mask is a
/// local-client, render-only concern owned by <see cref="SpelunkerReveal"/>.
/// </summary>
public sealed class SpelunkerBehavior : IEffectBehavior
{
    // Potency -> deepest ore tier revealed
    public EffectParams DefaultParams(int potency) =>
        new(MaxOreTier: potency >= 5 ? SpelunkerReveal.Uncapped : Math.Clamp(potency, 1, 4));

    public void Apply(Player player, in AlchemicalEffect fx)
    {
        // Only the local player's reveal is rendered; masking for a remote player's copy would wrongly
        // gate this client's own view (the flag array is client-global).
        if (player.whoAmI == Main.myPlayer)
            SpelunkerReveal.RequestGate(fx.Params.MaxOreTier);
    }

    public string Describe(in AlchemicalEffect fx) => SpelunkerReveal.DescribeGate(fx.Params.MaxOreTier);
}

/// <summary>
/// Owns the tile-reveal gate for tier-limited Spelunker brews. Each frame the effect player calls
/// <see cref="BeginFrame"/> (restoring the vanilla reveal for our ore ladder), then the active brew (if
/// any) calls <see cref="RequestGate"/> to re-mask ores above its tier. Only ladder ore types are ever
/// touched. Chests, pots, and hearts stay fully revealed, and un-tiered ores are never hidden.
/// </summary>
public static class SpelunkerReveal
{
    /// <summary>Gate value meaning "no cap", leave the vanilla reveal untouched.</summary>
    public const int Uncapped = 99;

    // Curated ore ladder: Tier = how deep the ore sits in progression. A gate of N reveals tiers <= N
    // and masks the rest. Tune freely; any ore not listed here is always revealed.
    private static readonly (int TileType, int Tier)[] Ladder =
    {
        (TileID.Copper, 1),       (TileID.Tin, 1),
        (TileID.Iron, 2),         (TileID.Lead, 2),
        (TileID.Silver, 3),       (TileID.Tungsten, 3),
        (TileID.Gold, 4),         (TileID.Platinum, 4),
        (TileID.Demonite, 5),     (TileID.Crimtane, 5),
        (TileID.Meteorite, 5),    (TileID.Hellstone, 5),
        (TileID.Cobalt, 6),       (TileID.Palladium, 6),
        (TileID.Mythril, 6),      (TileID.Orichalcum, 6),
        (TileID.Adamantite, 7),   (TileID.Titanium, 7),
        (TileID.Chlorophyte, 7),
    };

    private static bool _masked;

    /// <summary>Restore the vanilla reveal for our ladder. Call once per frame before applying brews.</summary>
    public static void BeginFrame()
    {
        if (!_masked) return;
        foreach (var (type, _) in Ladder) Main.tileSpelunker[type] = true;
        _masked = false;
    }

    /// <summary>Mask ore types deeper than <paramref name="maxTier"/> for this frame's reveal.</summary>
    public static void RequestGate(int maxTier)
    {
        if (maxTier >= Uncapped) return;   // uncapped: leave the vanilla reveal intact
        foreach (var (type, tier) in Ladder)
            if (tier > maxTier)
                Main.tileSpelunker[type] = false;
        _masked = true;
    }

    /// <summary>Tooltip phrase for a gate value.</summary>
    public static string DescribeGate(int maxTier) => maxTier switch
    {
        >= Uncapped => "Reveals all ores",
        <= 1 => "Reveals shallow ores (copper/tin)",
        2 => "Reveals ores down to iron/lead",
        3 => "Reveals ores down to silver/tungsten",
        4 => "Reveals ores down to gold/platinum",
        5 => "Reveals ores down to hellstone",
        _ => "Reveals ores down to mythril-grade",
    };
}
