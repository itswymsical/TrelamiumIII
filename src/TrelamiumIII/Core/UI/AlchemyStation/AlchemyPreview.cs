using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// Resolved recipe and output preview for a station's slots. Resolves again only when a slot's item instance, type, stack or
/// prefix changes, and replaces the preview item only when the recipe or composition changes, so hover and slot effects
/// keyed on the item instance don't restart every tick.
/// </summary>
internal sealed class AlchemyPreview
{
    private readonly SlotState[] _ingredients = new SlotState[AlchemyStationUI.COMPONENT_SLOT_COUNT + 1];
    private SlotState _bottles;
    private Item _output = new();

    public AlchemyResolution Resolution { get; private set; } = AlchemyResolution.None;

    /// <summary>Largest batch the slotted stacks can pay for, capped by the output's max stack and the bottle slot's capacity.</summary>
    public int MaxBatches { get; private set; } = 1;

    /// <summary><paramref name="components" /> holds at most <see cref="AlchemyStationUI.COMPONENT_SLOT_COUNT" /> slots.</summary>
    public void Refresh(Item input, Item[] components, Item bottleReturn)
    {
        bool ingredientsChanged = Track(ref _ingredients[0], input);

        for (int i = 0; i < components.Length; i++)
            ingredientsChanged |= Track(ref _ingredients[i + 1], components[i]);

        bool bottlesChanged = Track(ref _bottles, bottleReturn);

        if (ingredientsChanged)
        {
            AlchemyResolution previous = Resolution;
            Resolution = AlchemyRecipes.Resolve(input, components);

            if (Resolution.Recipe != previous.Recipe || Resolution.Composition != previous.Composition)
                _output = Resolution.Output.Clone();
        }

        if (ingredientsChanged || bottlesChanged)
            MaxBatches = ComputeMaxBatches(input, components, bottleReturn);
    }

    /// <summary>The preview item with its stack set for <paramref name="batches" />; air when nothing resolves.</summary>
    public Item ItemFor(int batches)
    {
        if (!_output.IsAir)
            _output.stack = Math.Max(1, Resolution.Output.stack) * batches;

        return _output;
    }

    public void Reset()
    {
        Array.Clear(_ingredients);
        _bottles = default;
        _output = new Item();
        Resolution = AlchemyResolution.None;
        MaxBatches = 1;
    }

    private int ComputeMaxBatches(Item input, Item[] components, Item bottleReturn)
    {
        if (!Resolution.Found)
            return 1;

        int max = Resolution.MaxBatches(input, components);
        int baseStack = Math.Max(1, Resolution.Output.stack);

        if (Resolution.Output.maxStack > 0)
            max = Math.Min(max, Math.Max(1, Resolution.Output.maxStack / baseStack));

        if (Resolution.ReturnsBottle)
            max = Math.Min(max, StationBottles.Capacity(bottleReturn));

        return Math.Max(1, max);
    }

    private static bool Track(ref SlotState state, Item item)
    {
        if (state.Matches(item))
            return false;

        state = new SlotState(item, item.type, item.stack, item.prefix);
        return true;
    }

    private readonly record struct SlotState(Item? Instance, int Type, int Stack, int Prefix)
    {
        public bool Matches(Item item) =>
            ReferenceEquals(Instance, item) && Type == item.type && Stack == item.stack && Prefix == item.prefix;
    }
}
