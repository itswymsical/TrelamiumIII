using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Content.FarmingOverhaul;

/// <summary>One stat a flower feeds, per unit of it held in the satchel.</summary>
public readonly record struct FlowerContribution(FlowerStat Stat, float PerUnit);

/// <summary>
/// Everything the satchel knows about one flower: which stats it feeds and how much per unit. 
/// The wording of its panel lines and the vanilla fields it touches come from
/// <see cref="FlowerStats"/>, so a flower is declared, not coded.
/// </summary>
public sealed class FlowerProfile
{
    /// <summary>The item this profile describes. 0 marks a detached profile. See <see cref="FlowerRegistry"/>.</summary>
    public int ItemType { get; }

    public List<FlowerContribution> Contributions { get; } = new();

    public FlowerProfile(int itemType) => ItemType = itemType;

    /// <summary>Adds a per-unit stat contribution. Chainable.</summary>
    public FlowerProfile PerUnit(FlowerStat stat, float amount)
    {
        Contributions.Add(new FlowerContribution(stat, amount));
        return this;
    }
}

/// <summary>
/// One stat a bouquet feeds, scaled by how many of <paramref name="Flower"/> are held. Bouquets pay
/// per member flower rather than per bouquet, so an uneven arrangement still rewards the flower you
/// stocked heavily.
/// </summary>
public readonly record struct BouquetContribution(int Flower, FlowerStat Stat, float PerUnit);

/// <summary>
/// A named combination. Its member flowers are inferred from its contributions: a bouquet requires
/// exactly the flowers it pays out on, so the two can't drift apart. While active it claims those
/// flowers, which stops them also paying their standalone <see cref="FlowerProfile"/>.
/// </summary>
public sealed class BouquetProfile
{
    public string Name { get; }

    public List<BouquetContribution> Contributions { get; } = new();

    /// <summary>Perks unlocked once <em>every</em> member flower reaches the keyed count.</summary>
    public List<(int AtCount, FlowerPerk Perk)> Perks { get; } = new();

    private int[] _members;

    /// <summary>The distinct flowers this bouquet needs, derived from its contributions once.</summary>
    public int[] Members => _members ??= Contributions.Select(c => c.Flower).Distinct().ToArray();

    public BouquetProfile(string name) => Name = name;

    /// <summary>Adds a stat this bouquet pays out per unit of <paramref name="flower"/> held. Chainable.</summary>
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

/// <summary>
/// A behaviour a bouquet unlocks at a stocking threshold. The behaviour itself lives in
/// <c>SatchelPlayer</c>'s update hooks.
/// </summary>
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
