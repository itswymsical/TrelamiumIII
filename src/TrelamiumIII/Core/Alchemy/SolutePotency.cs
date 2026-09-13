namespace TrelamiumIII.Core.Alchemy;

/// <summary>Station slot potency: <c>stack / StackPerPotency</c>, clamped to 0..<see cref="MAX" />.</summary>
public static class SolutePotency
{
    public const int MAX = 5;

    public static int StackPerPotency(int itemType) =>
        AlmanacEntryRegistry.TryGet(itemType, out AlmanacEntry? entry) ? Math.Max(1, entry.StackPerPotency) : 1;

    public static int LevelOf(Item? item) => item is { IsAir: false } ? LevelOf(item.type, item.stack) : 0;

    public static int LevelOf(int itemType, int stack) => Math.Clamp(stack / StackPerPotency(itemType), 0, MAX);

    /// <summary>Items consumed per batch by a slot at <paramref name="level" />.</summary>
    public static int CostOf(int itemType, int level) => level * StackPerPotency(itemType);
}
