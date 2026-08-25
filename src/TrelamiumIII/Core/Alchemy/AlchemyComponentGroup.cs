using System.Collections.Generic;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// RecipeGroups for component item types used in <see cref="AlchemyRecipe"/>.
/// </summary>
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
