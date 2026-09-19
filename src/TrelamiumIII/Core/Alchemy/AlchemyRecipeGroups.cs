using System.Collections.Generic;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>A named set of item types that one <see cref="AlchemyRecipe"/> component slot accepts.</summary>
public sealed class AlchemyComponentGroup
{
    public string Name { get; }

    private readonly HashSet<int> _itemTypes;

    public IReadOnlyCollection<int> ItemTypes => _itemTypes;

    /// <summary>Number of item types in the group. Used by the matcher to prefer specifics over groups.</summary>
    public int Count => _itemTypes.Count;

    public AlchemyComponentGroup(string name, params int[] itemTypes)
    {
        Name = name;
        _itemTypes = new HashSet<int>(itemTypes);
    }

    public AlchemyComponentGroup Add(params int[] itemTypes)
    {
        foreach (var t in itemTypes) _itemTypes.Add(t);
        return this;
    }

    public bool Contains(int itemType) => _itemTypes.Contains(itemType);

    public void Clear() => _itemTypes.Clear();
}

/// <summary>Component groups shared by the alchemy recipes. Filled in <c>ChemistryRecipeSystem.PopulateRecipeGroups</c>.</summary>
public static class AlchemyRecipeGroups
{
    public static readonly AlchemyComponentGroup AnyOre = new("AnyOre");

    public static readonly AlchemyComponentGroup Critters = new("Critters");

    public static readonly AlchemyComponentGroup Iron = new("Iron");
    public static readonly AlchemyComponentGroup Silver = new("Silver");
    public static readonly AlchemyComponentGroup Gold = new("Gold");
    public static readonly AlchemyComponentGroup SpecularFishOrMoonglow = new("SpecularFishOrMoonglow");

    public static readonly AlchemyComponentGroup AnyEvilOre = new("AnyEvilOre");
    public static readonly AlchemyComponentGroup Mushroom = new("AnyStandardMushroom");
    public static readonly AlchemyComponentGroup AnyEvilMushroom = new("AnyEvilMushroom");
    public static readonly AlchemyComponentGroup CorruptTiles = new("CorruptTiles");
    public static readonly AlchemyComponentGroup BloodTiles = new("BloodTiles");
    public static readonly AlchemyComponentGroup AnyHerb = new("AnyHerb");
    public static readonly AlchemyComponentGroup RottenChunkVertabrae = new("AnyEvilDrop");
    public static readonly AlchemyComponentGroup AnyStandardFish = new("AnyStandardFish");
    public static readonly AlchemyComponentGroup AnySoil = new("AnySoil");
    public static readonly AlchemyComponentGroup DirtOrMud = new("AnyDirt");
    public static readonly AlchemyComponentGroup AnyButterfly = new("AnyButterfly");
    public static readonly AlchemyComponentGroup AnyBaseBottle = new("AnyBaseBottle");

    public static readonly AlchemyComponentGroup ShiverthornOrWaterleaf = new("ShiverthornOrWaterleaf");
    public static readonly AlchemyComponentGroup ShiverthornOrSeeds = new("ShiverthornOrSeeds");

    // Conventional chemistry, so recipes can ask for "any acid" rather than naming each one.
    public static readonly AlchemyComponentGroup AnyAcid = new("AnyAcid");
    public static readonly AlchemyComponentGroup AnyAlkali = new("AnyAlkali");
    public static readonly AlchemyComponentGroup AnyMineralSalt = new("AnyMineralSalt");

    // Declared after the groups: static initializers run in textual order.
    private static readonly AlchemyComponentGroup[] All =
    [
        AnyOre, Critters, Iron, Silver, Gold, SpecularFishOrMoonglow, AnyEvilOre, Mushroom, AnyEvilMushroom,
        CorruptTiles, BloodTiles, AnyHerb, RottenChunkVertabrae, AnyStandardFish, AnySoil, DirtOrMud, AnyButterfly,
        AnyBaseBottle, ShiverthornOrWaterleaf, ShiverthornOrSeeds, AnyAcid, AnyAlkali, AnyMineralSalt,
    ];

    public static void Clear()
    {
        foreach (AlchemyComponentGroup group in All)
            group.Clear();
    }
}
