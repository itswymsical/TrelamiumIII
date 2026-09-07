using System.Collections.Generic;

namespace TrelamiumIII.Content.FarmingOverhaul.SatchelContent;

/// <summary>
/// Every flower the satchel accepts and every bouquet it can form. The slot whitelist is this
/// registry's key set, so registering a flower is the only step needed to make it placeable, and
/// there is no second hardcoded list to keep in sync.
/// <para>Cross-mod flowers register through the <c>(modName, itemName)</c> overloads. When that mod
/// isn't installed the call returns a detached profile: the chained <c>PerUnit</c> calls still run
/// and go nowhere, so catalog entries need no availability checks around them.</para>
/// </summary>
public static class FlowerRegistry
{
    private static readonly Dictionary<int, FlowerProfile> _flowers = new();
    private static readonly List<BouquetProfile> _bouquets = new();

    public static IReadOnlyCollection<int> FlowerTypes => _flowers.Keys;

    public static IReadOnlyList<BouquetProfile> Bouquets => _bouquets;

    /// <summary>True for any item the satchel's slots accept.</summary>
    public static bool IsFlower(int itemType) => itemType > 0 && _flowers.ContainsKey(itemType);

    public static bool TryGet(int itemType, out FlowerProfile profile)
        => _flowers.TryGetValue(itemType, out profile);

    /// <summary>
    /// Declares a flower. Re-registering a type replaces its profile, so a later catalog pass can
    /// retune an earlier one.
    /// </summary>
    public static FlowerProfile Register(int itemType)
    {
        if (itemType <= 0)
            return new FlowerProfile(0);   // detached: absent cross-mod item, or a bad id

        var profile = new FlowerProfile(itemType);
        _flowers[itemType] = profile;
        return profile;
    }

    /// <summary>
    /// Declares a flower owned by another mod. No-ops into a detached profile when that mod (or that
    /// item) isn't present, which is the normal case rather than an error.
    /// </summary>
    public static FlowerProfile Register(string modName, string itemName)
        => Register(TypeOf(modName, itemName));

    public static BouquetProfile RegisterBouquet(string name)
    {
        var bouquet = new BouquetProfile(name);
        _bouquets.Add(bouquet);
        return bouquet;
    }

    /// <summary>
    /// An item type from another mod, or 0 when it can't be resolved.
    /// <para>The two ways this returns 0 are worlds apart, so they're reported differently: the mod
    /// simply not being installed is the everyday case and stays quiet, while the mod being present
    /// without that item means the name here is wrong, which would otherwise be invisible.</para>
    /// </summary>
    public static int TypeOf(string modName, string itemName)
    {
        if (ModContent.TryFind(modName, itemName, out ModItem item))
            return item.Type;

        if (ModLoader.HasMod(modName))
        {
            TrelamiumIII.Instance.Logger.Warn(
                $"Flower satchel: {modName} is loaded but has no item named \"{itemName}\"");
        }
        return 0;
    }

    /// <summary>
    /// Drops bouquets that can never form. A member of 0 means a cross-mod flower whose mod isn't
    /// loaded (expected, silent); a positive member with no profile is an authoring slip, and is
    /// worth saying out loud because the bouquet would otherwise just never trigger.
    /// </summary>
    public static void PruneUnformable()
    {
        _bouquets.RemoveAll(bouquet =>
        {
            if (bouquet.Members.Length == 0) return true;

            foreach (int member in bouquet.Members)
            {
                if (member <= 0) return true;

                if (!_flowers.ContainsKey(member))
                {
                    TrelamiumIII.Instance.Logger.Warn(
                        $"[ERR] Flower satchel: bouquet \"{bouquet.Name}\" needs item {member}, which is currently not a " +
                        $"registered flower.");
                    return true;
                }
            }
            return false;
        });
    }

    public static void Clear()
    {
        _flowers.Clear();
        _bouquets.Clear();
    }
}
