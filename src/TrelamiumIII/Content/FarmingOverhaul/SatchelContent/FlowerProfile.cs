using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Content.FarmingOverhaul.SatchelContent;

/// <summary>One stat a flower feeds, per unit of it held in the satchel.</summary>
public readonly record struct FlowerContribution(FlowerStat Stat, float PerUnit);

/// <summary>
/// Which stats one flower feeds and how much per unit; wording and application come from <see cref="FlowerStats"/>.
/// </summary>
public sealed class FlowerProfile
{
    /// <summary>The item this profile describes. 0 marks a detached profile. See <see cref="FlowerRegistry"/>.</summary>
    public int ItemType { get; }

    public List<FlowerContribution> Contributions { get; } = new();

    public FlowerProfile(int itemType) => ItemType = itemType;

    public FlowerProfile PerUnit(FlowerStat stat, float amount)
    {
        Contributions.Add(new FlowerContribution(stat, amount));
        return this;
    }
}

/// <summary>
/// One stat a bouquet feeds per unit of <paramref name="Flower"/> held, so uneven arrangements still reward heavily
/// stocked flowers.
/// </summary>
public readonly record struct BouquetContribution(int Flower, FlowerStat Stat, float PerUnit);

/// <summary>
/// A named flower combination. Members are inferred from its contributions; while active it claims them, so they
/// don't also pay their standalone <see cref="FlowerProfile"/>.
/// </summary>
public sealed class BouquetProfile
{
    public string Name { get; }

    public List<BouquetContribution> Contributions { get; } = new();

    /// <summary>Perks unlocked once <em>every</em> member flower reaches the keyed count.</summary>
    public List<(int AtCount, FlowerPerk Perk)> Perks { get; } = new();

    private int[] _members;

    public int[] Members => _members ??= Contributions.Select(c => c.Flower).Distinct().ToArray();

    public BouquetProfile(string name) => Name = name;

    public BouquetProfile From(int flower, FlowerStat stat, float perUnit)
    {
        Contributions.Add(new BouquetContribution(flower, stat, perUnit));
        _members = null;
        return this;
    }

    /// <summary>Grants <paramref name="perk"/> once every member is stocked to <paramref name="atCount"/>. Chainable.</summary>
    public BouquetProfile Perk(int atCount, FlowerPerk perk)
    {
        Perks.Add((atCount, perk));
        return this;
    }
}

/// <summary>A behaviour a bouquet unlocks at a stocking threshold, implemented in <c>SatchelPlayer</c>'s update hooks.</summary>
public enum FlowerPerk
{
    /// <summary>Permanent Night Owl vision.</summary>
    NightVision,

    /// <summary>Dangersense for a moment after taking a hit.</summary>
    DangersenseOnHit,

    /// <summary>Extra movement speed while it is daytime.</summary>
    DaytimeSpeed,

    /// <summary>Hunter and Dangersense while above 90% life.</summary>
    HealthySight,

    /// <summary>Enemies may leave a sapling behind. Not implemented yet.</summary>
    SaplingOnKill,
}

public static class FlowerPerks
{
    private static readonly Dictionary<FlowerPerk, string> _descriptions = new()
    {
        [FlowerPerk.NightVision] = "Gain Night Owl Vision",
        [FlowerPerk.DangersenseOnHit] = "Gain Dangersense when hit",
        [FlowerPerk.DaytimeSpeed] = "+4% speed during day",
        [FlowerPerk.HealthySight] = "Hunter & Trapsight effects when above 90% HP",
        [FlowerPerk.SaplingOnKill] = "Enemies have a chance to plant a sapling on death (not done)",
    };

    public static string Describe(FlowerPerk perk)
        => _descriptions.TryGetValue(perk, out string text) ? text : perk.ToString();
}
