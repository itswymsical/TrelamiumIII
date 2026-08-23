using System;

namespace TrelamiumIII.Core.Alchemy;

public enum AlchemyMode
{
    None,

    /// <summary>
    /// for modifying existing items, such as reforging or enchanting.
    /// </summary>
    Augmentation,

    /// <summary>
    /// for making potions and elixirs.
    /// </summary>
    Synthesis,

    /// <summary>
    /// for making utilities and weapons.
    /// </summary>
    Alchemy
}

/// <summary>
/// A single component slot's match rule: either an exact item type, or a
/// <see cref="AlchemyComponentGroup"/> that accepts any of its members. Optionally,
/// narrowed to a specific per-instance <see cref="ChemicalType"/> infusion (suffixed
/// solutions like "Bottled Water (Inert)"). Implicit conversions let recipes mix
/// specific ints and groups in the same array.
/// </summary>
public readonly struct ComponentMatcher
{
    public readonly int ExactType;
    public readonly AlchemyComponentGroup Group;

    /// <summary>
    /// Required instance infusion. None = the slot only accepts a plain (un-infused) item,
    /// so infused and plain variants of the same item type never cross-match.
    /// </summary>
    public readonly ChemicalType Infusion;

    /// <summary>
    /// How many of the solute this slot draws per batch unit. This is the potency dial:
    /// the station reads it as a stack requirement, and the front end shows it as
    /// "potency N" on the solute. Always at least 1.
    /// </summary>
    public readonly int Quantity;

    public ComponentMatcher(int exactType, ChemicalType infusion = ChemicalType.None, int quantity = 1)
    { ExactType = exactType; Group = null; Infusion = infusion; Quantity = Math.Max(1, quantity); }

    public ComponentMatcher(AlchemyComponentGroup group, int quantity = 1)
    { ExactType = 0; Group = group; Infusion = ChemicalType.None; Quantity = Math.Max(1, quantity); }

    /// <summary>An exact-type slot that additionally requires the given infusion.</summary>
    public static ComponentMatcher Infused(int itemType, ChemicalType infusion) => new(itemType, infusion);

    /// <summary>
    /// An exact-type slot drawn at <paramref name="quantity"/> potency, i.e. consuming that
    /// many of the item per batch unit. Two variants over the same solute at different
    /// quantities are different recipes, and the heavier one wins when both are affordable.
    /// </summary>
    public static ComponentMatcher Of(int itemType, int quantity, ChemicalType infusion = ChemicalType.None)
        => new(itemType, infusion, quantity);

    /// <summary>A group slot drawn at <paramref name="quantity"/> potency, whichever member fills it.</summary>
    public static ComponentMatcher Of(AlchemyComponentGroup group, int quantity) => new(group, quantity);

    /// <summary>Type-level acceptance (almanac indexing: "which recipes use this item type").</summary>
    public bool Matches(int itemType) =>
        Group != null ? Group.Contains(itemType) : itemType == ExactType;

    /// <summary>
    /// Full instance match: type/group plus the slot's required infusion. Deliberately
    /// stack-blind. Quantity is checked by <see cref="AlchemyRecipe.TryMatch"/>, which needs
    /// to tell "wrong solute" apart from "not enough of the right one".
    /// </summary>
    public bool Matches(Item item) =>
        Matches(item.type) && SolutionGlobalItem.GetInfusion(item) == Infusion;

    /// <summary>
    /// Lower = more specific. Exact-type matchers are 1; group matchers equal the group size.
    /// Used by the matcher to claim specific items before falling back to broad groups.
    /// </summary>
    public int Specificity => Group != null ? Group.Count : 1;

    public static implicit operator ComponentMatcher(int itemType) => new(itemType);
    public static implicit operator ComponentMatcher(AlchemyComponentGroup group) => new(group);
}

/// <summary>
/// A slot holding the right solute but not enough of it. Reported by
/// <see cref="AlchemyRecipe.TryMatch"/> so the station can name what's missing instead of
/// silently previewing nothing.
/// </summary>
public readonly record struct AlchemyShortfall(int ItemType, int Have, int Required)
{
    public bool Exists => ItemType > 0;
}

/// <summary>
/// One alchemy recipe: a (mode, vial, components) -> output mapping.
/// Components are unordered and matched by multiset equality.
/// </summary>
public sealed class AlchemyRecipe
{
    public AlchemyMode Mode { get; init; }
    public int SolventType { get; init; }

    /// <summary>
    /// Required infusion on the solvent instance. None = plain solvent only, so a recipe
    /// keyed to "Bottled Water (Inert)" never fires from plain water and vice versa.
    /// </summary>
    public ChemicalType SolventInfusion { get; init; }

    /// <summary>
    /// Item type produced by this recipe, used by the Almanac index to group recipes by
    /// output without invoking <see cref="CreateOutput"/>. <c>0</c> means the type is only
    /// known after invoking the factory (rare and only for fully dynamic recipes).
    /// </summary>
    public int OutputItemType { get; init; }

    /// <summary>
    /// Required component slots (1–3). Each slot is a <see cref="ComponentMatcher"/>: an
    /// exact item type or a <see cref="AlchemyComponentGroup"/>, plus the quantity it draws.
    /// Order does not matter; duplicates are allowed (e.g. {Mushroom, Mushroom, Daybloom}).
    /// </summary>
    public ComponentMatcher[] Components { get; init; } = Array.Empty<ComponentMatcher>();

    /// <summary>
    /// Factory invoked when this recipe is selected. Lets the recipe build dynamic outputs
    /// (custom stack size, modded prefix, augmented potion stats, etc.) instead of being
    /// limited to a single item type.
    /// </summary>
    public Func<Item> CreateOutput { get; init; }

    /// <summary>
    /// Total solute units consumed per batch unit. Ranks competing matches so a heavier stack
    /// of the same solutes reaches the heavier recipe (10 blood orbs make what 5 cannot).
    /// </summary>
    public int TotalQuantity
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < Components.Length; i++) sum += Components[i].Quantity;
            return sum;
        }
    }

    /// <summary>
    /// True if the solvent instance and the station's solute slots satisfy this recipe,
    /// stacks included.
    /// <para><paramref name="slotCosts"/> is filled with how many units each slot gives up per
    /// batch unit (0 for slots this recipe leaves alone). On a stacks-only failure it holds the
    /// requirement that went unmet, so the station can show the gap; on any other failure it is
    /// cleared.</para>
    /// </summary>
    public bool TryMatch(Item solvent, Item[] slots, Span<int> slotCosts, out AlchemyShortfall shortfall)
    {
        shortfall = default;
        slotCosts.Clear();

        if (solvent is null || solvent.IsAir) return false;
        if (solvent.type != SolventType) return false;
        if (SolutionGlobalItem.GetInfusion(solvent) != SolventInfusion) return false;

        int filled = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { IsAir: false }) filled++;
        if (filled != Components.Length) return false;

        if (Assign(slots, slotCosts, requireStack: true))
            return true;

        // The shape fits but something is under-stacked. Re-assign ignoring stacks so the
        // requirement survives for the station to display, and name the first short slot.
        slotCosts.Clear();
        if (!Assign(slots, slotCosts, requireStack: false))
        {
            slotCosts.Clear();
            return false;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slotCosts[i] <= 0 || slots[i].stack >= slotCosts[i]) continue;
            shortfall = new AlchemyShortfall(slots[i].type, slots[i].stack, slotCosts[i]);
            break;
        }
        return false;
    }

    /// <summary>
    /// Claims one slot per matcher, most-specific matcher first so an exact-type matcher takes
    /// its target before a group that would also accept it sweeps it up. Writes each claimed
    /// slot's per-unit cost, which doubles as the "already claimed" marker (costs are never 0).
    /// </summary>
    private bool Assign(Item[] slots, Span<int> slotCosts, bool requireStack)
    {
        // Insertion sort: these arrays are 1–3 long, so this beats allocating a comparison.
        Span<int> order = stackalloc int[Components.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = 1; i < order.Length; i++)
        {
            int cur = order[i];
            int j = i - 1;
            while (j >= 0 && ClaimsFirst(Components[cur], Components[order[j]]))
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = cur;
        }

        foreach (int matcherIdx in order)
        {
            var matcher = Components[matcherIdx];
            int need = matcher.Quantity;
            bool found = false;

            for (int j = 0; j < slots.Length; j++)
            {
                if (slotCosts[j] != 0) continue;
                var slot = slots[j];
                if (slot is null || slot.IsAir) continue;
                if (!matcher.Matches(slot)) continue;
                if (requireStack && slot.stack < need) continue;

                slotCosts[j] = need;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>
    /// Whether <paramref name="a"/> gets to pick a slot before <paramref name="b"/>.
    /// Narrower matchers go first, so an exact type isn't swallowed by a group that also accepts
    /// it. Among equals the hungrier one goes first: two slots of one solute at different
    /// quantities would otherwise let a light matcher take the big stack and starve a heavy one.
    /// </summary>
    private static bool ClaimsFirst(ComponentMatcher a, ComponentMatcher b) =>
        a.Specificity != b.Specificity
            ? a.Specificity < b.Specificity
            : a.Quantity > b.Quantity;
}
