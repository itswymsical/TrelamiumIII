using System.Collections.Generic;
using System.Linq;
using Terraria.Utilities;

namespace TrelamiumIII.Core.Structures;

public enum LootMode : byte
{
    /// <summary>Every entry in order, each with <see cref="LootRule.Chance"/>.</summary>
    All,

    /// <summary>With <see cref="LootRule.Chance"/>, <see cref="LootRule.Count"/> distinct entries picked by weight.</summary>
    Pick
}

/// <summary>Randomised chest contents, rolled when a structure is generated.</summary>
public sealed class LootRule
{
    public LootMode Mode;

    /// <summary>0 to 1. Per entry for <see cref="LootMode.All"/>, for the whole rule for <see cref="LootMode.Pick"/>.</summary>
    public float Chance = 1f;

    /// <summary>Entries drawn by <see cref="LootMode.Pick"/>.</summary>
    public int Count = 1;

    public List<LootEntry> Entries = [];

    public LootRule Clone() => new()
    {
        Mode = Mode,
        Chance = Chance,
        Count = Count,
        Entries = [.. Entries.Select(entry => entry.Clone())]
    };

    /// <summary>Puts items into the empty slots of <paramref name="chest"/> from <paramref name="slot"/> onward. False once the chest is full.</summary>
    public bool Roll(Chest chest, ref int slot, UnifiedRandom random)
    {
        if (Mode == LootMode.All)
        {
            foreach (LootEntry entry in Entries)
            {
                if (random.NextFloat() < Chance && !Put(chest, ref slot, entry.Create(random)))
                    return false;
            }

            return true;
        }

        if (random.NextFloat() >= Chance)
            return true;

        List<LootEntry> pool = [.. Entries];

        for (int i = 0; i < Count && pool.Count > 0; i++)
        {
            int roll = random.Next(pool.Sum(entry => Math.Max(1, entry.Weight)));
            int index = 0;

            while ((roll -= Math.Max(1, pool[index].Weight)) >= 0)
                index++;

            if (!Put(chest, ref slot, pool[index].Create(random)))
                return false;

            pool.RemoveAt(index);
        }

        return true;
    }

    private static bool Put(Chest chest, ref int slot, Item item)
    {
        while (slot < chest.maxItems && !chest.item[slot].IsAir)
            slot++;

        if (slot >= chest.maxItems)
            return false;

        chest.item[slot++] = item;
        return true;
    }
}

public sealed class LootEntry(Item item, int minStack = 1, int maxStack = 1, int weight = 1)
{
    /// <summary>The item given, with its prefix and data; its stack is ignored.</summary>
    public Item Item = item;

    public int MinStack = Math.Max(1, minStack);

    /// <summary>Inclusive.</summary>
    public int MaxStack = Math.Max(Math.Max(1, minStack), maxStack);

    public int Weight = Math.Max(1, weight);

    public LootEntry Clone() => new(Item.Clone(), MinStack, MaxStack, Weight);

    public Item Create(UnifiedRandom random)
    {
        Item created = Item.Clone();
        created.stack = random.Next(MinStack, MaxStack + 1);
        return created;
    }
}
