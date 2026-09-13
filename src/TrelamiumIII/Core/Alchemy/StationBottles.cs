namespace TrelamiumIII.Core.Alchemy;

/// <summary>Stack arithmetic for a station's single bottle-return slot.</summary>
public static class StationBottles
{
    /// <summary>Bottles the slot can still accept; 0 while it holds anything other than bottles.</summary>
    public static int Capacity(Item slot) =>
        slot.IsAir ? ContentSamples.ItemsByType[ItemID.Bottle].maxStack
        : slot.type == ItemID.Bottle ? Math.Max(0, slot.maxStack - slot.stack)
        : 0;

    /// <summary>Adds up to <paramref name="count" /> bottles to <c>slot[0]</c>, clamped to <see cref="Capacity" />.</summary>
    public static void Deposit(Item[] slot, int count)
    {
        count = Math.Min(count, Capacity(slot[0]));

        if (count <= 0)
            return;

        if (slot[0].IsAir)
            slot[0] = new Item(ItemID.Bottle, count);
        else
            slot[0].stack += count;
    }
}
