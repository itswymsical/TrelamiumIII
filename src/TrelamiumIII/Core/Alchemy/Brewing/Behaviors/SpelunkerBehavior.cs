using System;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Items.Lodestone;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// Tier-gated Spelunker: hides ores deeper than <see cref="EffectParams.MaxOreTier"/> from vanilla's reveal via <see cref="SpelunkerReveal"/>.
/// </summary>
public sealed class SpelunkerBehavior : IEffectBehavior
{
    // Potency -> deepest ore tier revealed
    public EffectParams DefaultParams(int potency) =>
        new(MaxOreTier: potency >= 5 ? SpelunkerReveal.Uncapped : Math.Clamp(potency, 1, 4));

    public void Apply(Player player, in AlchemicalEffect fx)
    {
        // Only the local player's reveal is rendered; gating for a remote player's copy would wrongly
        // limit this client's own view (the gate is client-global).
        if (player.whoAmI == Main.myPlayer)
            SpelunkerReveal.RequestGate(fx.Params.MaxOreTier);
    }

    public string Describe(in AlchemicalEffect fx) => SpelunkerReveal.DescribeGate(fx.Params.MaxOreTier);
}

/// <summary>
/// Owns the Spelunker reveal gate: <see cref="BeginFrame"/> lifts it, <see cref="RequestGate"/> lowers it, and
/// <see cref="SpelunkerRevealTile"/> hides ladder ores above it. Chests, pots, hearts and untiered ores stay revealed.
/// <see cref="Main.tileSpelunker"/> is never written, so code that reads it (Lodestone Magnetizer) ignores the gate.
/// </summary>
public static class SpelunkerReveal
{
    /// <summary>Gate value meaning "no cap", leave the vanilla reveal untouched.</summary>
    public const int Uncapped = 99;

    // Curated ore ladder: Tier = how deep the ore sits in progression. A gate of N reveals tiers <= N
    // and masks the rest. Tune freely; any ore not listed here is always revealed.
    private static readonly (int TileType, int Tier)[] Ladder =
    {   
        (TileID.Copper, 0),       (TileID.Tin, 0),
        (TileID.Iron, 1),         (TileID.Lead, 1),
        (TileID.Silver, 2),       (TileID.Tungsten, 2),
        (TileID.Gold, 2),         (TileID.Platinum, 2),
        (TileID.Demonite, 2),     (TileID.Crimtane, 2),
        (TileID.Meteorite, 2),    (TileID.Hellstone, 2),
        (TileID.Cobalt, 4),       (TileID.Palladium, 4),
        (TileID.Mythril, 5),      (TileID.Orichalcum, 5),
        (TileID.Adamantite, 6),   (TileID.Titanium, 6),
        (TileID.Chlorophyte, 7),
    };

    /// <summary><see cref="Ladder"/> tier indexed by tile type; 0 for types off the ladder.</summary>
    private static readonly int[] TierByType = BuildTierLookup();

    /// <summary>Deepest tier revealed this frame. <see cref="Uncapped"/> leaves the vanilla reveal intact.</summary>
    private static int _gate = Uncapped;

    /// <summary>Lift the gate. Call once per frame before applying brews.</summary>
    public static void BeginFrame() => _gate = Uncapped;

    /// <summary>Hide ore types deeper than <paramref name="maxTier"/> for this frame's reveal.</summary>
    public static void RequestGate(int maxTier) => _gate = Math.Min(_gate, maxTier);

    /// <summary>Whether the current gate hides this tile type from the reveal.</summary>
    public static bool IsHidden(int tileType)
        => _gate < Uncapped && (uint)tileType < (uint)TierByType.Length && TierByType[tileType] > _gate;

    private static int[] BuildTierLookup()
    {
        int[] tiers = new int[TileID.Count];

        foreach (var (type, tier) in Ladder)
            tiers[type] = tier;

        return tiers;
    }

    /// <summary>Tooltip phrase for a gate value.</summary>
    public static string DescribeGate(int maxTier) => maxTier switch
    {
        >= Uncapped => LangHelper.LocalizationValue("PotionBrews.Spelunker.Vanilla"),
        <= 1 => LangHelper.LocalizationValue(TrelamiumIII.Instance, "PotionBrews.Spelunker.MineralTier"),
        2 => LangHelper.LocalizationValue(TrelamiumIII.Instance, "PotionBrews.Spelunker.OreTier1"),
        3 => LangHelper.LocalizationValue(TrelamiumIII.Instance, "PotionBrews.Spelunker.OreTier2"),
        4 => LangHelper.LocalizationValue(TrelamiumIII.Instance, "PotionBrews.Spelunker.OreTier3"),
        5 => LangHelper.LocalizationValue(TrelamiumIII.Instance, "PotionBrews.Spelunker.OreTier4"),
        _ => LangHelper.LocalizationValue(TrelamiumIII.Instance, "PotionBrews.Spelunker.OreTier5"),
    };
}

/// <summary>Applies <see cref="SpelunkerReveal"/>'s gate through <c>Main.IsTileSpelunkable</c>: the Spelunker glow and treasure-seeking pets.</summary>
public sealed class SpelunkerRevealTile : GlobalTile
{
    public override bool? IsTileSpelunkable(int i, int j, int type) => SpelunkerReveal.IsHidden(type) ? false : null;
}