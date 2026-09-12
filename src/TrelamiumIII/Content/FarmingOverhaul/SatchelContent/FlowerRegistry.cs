using System.Collections.Generic;

namespace TrelamiumIII.Content.FarmingOverhaul.SatchelContent;

/// <summary>
/// Every flower the satchel accepts and every bouquet it can form; the slot whitelist is this registry's keys.
/// Cross-mod registrations return a detached profile when the mod is missing, so catalog entries need no checks.
/// </summary>
public static class FlowerRegistry
{
    private static readonly Dictionary<int, FlowerProfile> _flowers = new();
    private static readonly List<BouquetProfile> _bouquets = new();

    public static IReadOnlyCollection<int> FlowerTypes => _flowers.Keys;

    public static IReadOnlyList<BouquetProfile> Bouquets => _bouquets;

    public static bool IsFlower(int itemType) => itemType > 0 && _flowers.ContainsKey(itemType);

    public static bool TryGet(int itemType, out FlowerProfile profile)
        => _flowers.TryGetValue(itemType, out profile);

    /// <summary>Declares a flower. Re-registering replaces the profile, so later catalog passes can retune it.</summary>
    public static FlowerProfile Register(int itemType)
    {
        if (itemType <= 0)
            return new FlowerProfile(0);   // detached: absent cross-mod item, or a bad id

        var profile = new FlowerProfile(itemType);
        _flowers[itemType] = profile;
        return profile;
    }

    /// <summary>Declares another mod's flower; returns a detached profile if that mod or item is missing.</summary>
    public static FlowerProfile Register(string modName, string itemName)
        => Register(TypeOf(modName, itemName));

    public static BouquetProfile RegisterBouquet(string name)
    {
        var bouquet = new BouquetProfile(name);
        _bouquets.Add(bouquet);
        return bouquet;
    }

    /// <summary>
    /// Another mod's item type, or 0 if unresolved. A missing mod is silent; a present mod without the item is reported,
    /// since that means the name here is wrong.
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
    /// Drops bouquets that can never form. A member of 0 (cross-mod flower, mod not loaded) is silent; a positive member
    /// with no profile is an authoring slip and is reported.
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
