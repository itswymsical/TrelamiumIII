namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     What a dropped stack of matching items does while submerged in a liquid. Each step consumes <see cref="Amount" />
///     items and <see cref="Spent" /> liquid, then converts the liquid around the stack and makes <see cref="Product" />.
/// </summary>
public sealed class ItemReaction(Predicate<int> items)
{
    public ItemReaction(params int[] items) : this(type => Array.IndexOf(items, type) >= 0) { }

<<<<<<< HEAD
    /// <summary>The internal liquid the stack must be in; any when null.</summary>
    public InternalLiquid? From { get; init; }
=======
    /// <summary>The condition the stack must be in; any when null.</summary>
    public LiquidCondition? From { get; init; }
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    /// <summary>Items consumed per step. A smaller stack does not react.</summary>
    public int Amount { get; init; } = 1;

    /// <summary>Ticks submerged per step.</summary>
    public int Ticks { get; init; } = 60;

    /// <summary>Liquid units used per step, taken from the surface of the body. No step runs if the body holds less.</summary>
    public int Spent { get; init; }

<<<<<<< HEAD
    /// <summary>Internal liquid the liquid around the stack turns into, from the stack outward.</summary>
    public InternalLiquid? Becomes { get; init; }
=======
    /// <summary>Condition the liquid around the stack turns into, from the stack outward.</summary>
    public LiquidCondition? Becomes { get; init; }
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    /// <summary>Item made per step. Products float at the surface of the liquid that made them.</summary>
    public int Product { get; init; }

    public int ProductStack { get; init; } = 1;

    public bool Matches(int itemType) => items(itemType);
}
