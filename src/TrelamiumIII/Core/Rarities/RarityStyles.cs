using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TrelamiumIII.Core.Rarities;

/// <summary>
///     The lookup from an item to the <see cref="RarityStyle" /> it draws with.
/// </summary>
/// <remarks>
///     <para>
///         Two tables, checked in order. A <b>designation</b> is keyed by item type and wins: it is
///         how one specific item gets a look of its own without a rarity being invented to carry it,
///         which is the whole point of a bespoke treatment being bespoke. A <b>rarity</b> style is
///         keyed by <see cref="Item.rare" /> and applies to everything at that tier.
///     </para>
///     <para>
///         Keyed by the rarity's integer rather than by <see cref="ModRarity" /> so that vanilla
///         tiers can be restyled too -- <c>Register(ItemRarityID.Quest, ...)</c> is a legal and
///         occasionally useful thing to write.
///     </para>
/// </remarks>
public static class RarityStyles
{
    private static readonly Dictionary<int, RarityStyle> byRarity = [];
    private static readonly Dictionary<int, RarityStyle> byItem = [];

    /// <summary>Binds a style to a rarity tier, vanilla or modded.</summary>
    public static void Register(int rarity, RarityStyle style) => byRarity[rarity] = style;

    /// <summary>
    ///     Binds a style to one item type, overriding whatever its rarity would otherwise give it.
    /// </summary>
    /// <remarks>
    ///     For the designated item that earns a one-off: a boss drop that smoulders, a quest piece
    ///     whose name is written in something other than ink. The item keeps its rarity, and with it
    ///     its place in every value and prefix calculation, and only the look changes.
    /// </remarks>
    public static void RegisterItem(int itemType, RarityStyle style) => byItem[itemType] = style;

    /// <summary>The style <paramref name="item" /> should draw with, or null for none.</summary>
    public static RarityStyle? Get(Item item)
    {
        if (item is null || item.IsAir)
            return null;

        if (byItem.TryGetValue(item.type, out var designated))
            return designated;

        return byRarity.GetValueOrDefault(item.rare);
    }

    public static bool TryGet(Item item, [NotNullWhen(true)] out RarityStyle? style)
        => (style = Get(item)) is not null;

    /// <summary>True when anything at all is registered, so the hot hooks can bail in one test.</summary>
    internal static bool Any => byRarity.Count > 0 || byItem.Count > 0;

    internal static void Clear()
    {
        // Flairs own render targets and node pools, so they have to be told to let go. A style can
        // legally appear in both tables, and a flair in more than one style, so the walk is
        // de-duplicated rather than trusting each Unload to be idempotent.
        HashSet<RarityFlair> seen = [];

        foreach (var style in byRarity.Values)
            UnloadFlairs(style, seen);

        foreach (var style in byItem.Values)
            UnloadFlairs(style, seen);

        byRarity.Clear();
        byItem.Clear();
    }

    private static void UnloadFlairs(RarityStyle style, HashSet<RarityFlair> seen)
    {
        foreach (var flair in style.Flairs)
        {
            if (seen.Add(flair))
                flair.Unload();
        }
    }
}
