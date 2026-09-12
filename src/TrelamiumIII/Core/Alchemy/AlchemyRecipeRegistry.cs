using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Alchemy;

public sealed class AlchemyVariant
{
    public ComponentMatcher[] Components { get; }

    /// <summary>Stack size of the produced item before the player's output-count multiplier.</summary>
    public int OutputStack { get; init; } = 1;

    /// <summary>Optional override for output construction.</summary>
    public Func<Item> CustomOutput { get; init; }

    /// <summary>Brew potency (1-5) stamped onto buff-potion outputs. 0 = no brew (plain item).</summary>
    public int Potency { get; init; }

    /// <summary>Override the potency curve's strength (percent of the vanilla effect). 0 = use the curve.</summary>
    public int StrengthPercent { get; init; }

    /// <summary>Override the brewed duration, in seconds. 0 = use the curve.</summary>
    public int DurationSeconds { get; init; }

    /// <summary>Brewed duration override in ticks; wins over <see cref="DurationSeconds"/>. 0 = unset.</summary>
    public int DurationTicks { get; init; }

    /// <summary>Override a gated effect's max ore tier (Spelunker). 0 = use the potency default.</summary>
    public int MaxOreTier { get; init; }

    /// <summary>Optional augmentation pass: boost, re-gate, or combine further effects onto the brew.</summary>
    public Action<BrewBuilder>? Augment { get; init; }

    /// <summary>Required solvent infusion; None means plain solvent only, so one solvent type can key different recipes.</summary>
    public ChemicalQuality SolventInfusion { get; init; }

    /// <summary>Infusion stamped onto the output. Infused outputs are reagents, so they skip brew branding.</summary>
    public ChemicalQuality OutputInfusion { get; init; }

    /// <summary>
    /// One input combination for a shared output (see <see cref="AlchemyRecipeRegistry.RegisterVariants"/>). Each component carries
    /// its own quantity: a plain item type draws one, <c>ComponentMatcher.Of(type, 5)</c> draws five.
    /// </summary>
    public AlchemyVariant(params ComponentMatcher[] components)
    {
        Components = components;
    }
}

/// <summary>
/// A resolved brew: recipe, output, and each slot's cost per batch unit (aligned to the station's slots). When only a stack is short,
/// <see cref="Found"/> is false but <see cref="Shortfall"/> and <see cref="ComponentCosts"/> still describe the requirement.
/// </summary>
public sealed class AlchemyResolution
{
    public static readonly AlchemyResolution None = new();

    public AlchemyRecipe? Recipe { get; init; }

    public Item Output { get; init; } = new();

    /// <summary>Units taken from each solute slot per batch unit; 0 for slots no recipe wants.</summary>
    public int[] ComponentCosts { get; init; } = Array.Empty<int>();

    /// <summary>The right solute in too small a stack. Only meaningful while <see cref="Found"/> is false.</summary>
    public AlchemyShortfall Shortfall { get; init; }

    public bool Found => Recipe != null && !Output.IsAir;

    /// <summary>Units taken from slot <paramref name="index"/> per batch unit.</summary>
    public int CostFor(int index) =>
        index >= 0 && index < ComponentCosts.Length ? ComponentCosts[index] : 0;

    /// <summary>Batch units the slotted ingredients can pay for: one solvent per unit, solutes by recipe cost.</summary>
    public int MaxBatches(Item solvent, Item[] components)
    {
        if (!Found || solvent is null || solvent.IsAir) return 1;

        int max = solvent.stack;
        for (int i = 0; i < components.Length && i < ComponentCosts.Length; i++)
        {
            int cost = ComponentCosts[i];
            if (cost <= 0 || components[i] is null || components[i].IsAir) continue;
            max = Math.Min(max, components[i].stack / cost);
        }
        return Math.Max(1, max);
    }
}

/// <summary>Central registry of alchemy recipes, added via <see cref="Register"/> or <see cref="RegisterVariants"/>.</summary>
public static class AlchemyRecipeRegistry
{
    private static readonly List<AlchemyRecipe> _recipes = new();
    private static readonly HashSet<int> _outputTypes = new();

    public static IReadOnlyList<AlchemyRecipe> All => _recipes;

    public static void Register(AlchemyRecipe recipe)
    {
        _recipes.Add(recipe);
        if (recipe.OutputItemType > 0) _outputTypes.Add(recipe.OutputItemType);
    }

    public static void Clear()
    {
        _recipes.Clear();
        _outputTypes.Clear();
    }

    /// <summary>Whether some recipe produces this item, so the recipe system only takes over routes it actually replaces.</summary>
    public static bool Produces(int itemType) => _outputTypes.Contains(itemType);

    /// <summary>Registers several input combinations that share one (mode, vial, output) target.</summary>
    public static void RegisterVariants(
        AlchemyMode mode,
        int inputType,
        int outputType,
        params AlchemyVariant[] variants)
    {
        foreach (var v in variants)
        {
            // Capture-by-value so each closure produces its own variant's stack/potency/overrides.
            var variantStack = v.OutputStack;
            var potency = v.Potency;
            var strengthOverride = v.StrengthPercent;
            // Ticks-native override wins; else seconds are converted below.
            var durationOverride = v.DurationTicks > 0 ? v.DurationTicks : v.DurationSeconds * 60;
            var maxOreTier = v.MaxOreTier;
            var augment = v.Augment;
            var custom = v.CustomOutput;
            var comps = v.Components;
            var outputInfusion = v.OutputInfusion;

            Register(new AlchemyRecipe
            {
                Mode = mode,
                SolventType = inputType,
                SolventInfusion = v.SolventInfusion,
                OutputItemType = outputType,
                Components = v.Components,
                CreateOutput = custom ?? (() =>
                {
                    var r = new Item();
                    r.SetDefaults(outputType);
                    r.stack = variantStack;

                    // Infused outputs are suffixed solvents (reagents), so stamping
                    // a brew here would wrongly brand e.g. Bottled Water via its healLife.
                    if (outputInfusion != ChemicalQuality.None)
                    {
                        if (r.TryGetGlobalItem(out SolutionGlobalItem solution))
                            solution.Infusion = outputInfusion;
                        return r;
                    }

                    // Brand buff and restorative potions (heals get a virtual Restoration effect), then apply overrides. Explicit
                    // potency wins; otherwise it's inferred lazily from almanac ratings, which register after recipes.
                    if (r.buffType > 0 || r.healLife > 0 || r.healMana > 0)
                    {
                        int pot = potency > 0 ? potency : AutoPotency(inputType, comps);
                        if (pot > 0)
                        {
                            var domain = AlmanacEntryRegistry.For(outputType).Principle;
                            var effect = r.buffType > 0
                                ? BrewTuning.Create(r.buffType, r.buffTime, pot)
                                : BrewTuning.CreateRestoration(pot);
                            if (strengthOverride > 0) effect = effect with { StrengthPercent = strengthOverride };
                            if (durationOverride > 0) effect = effect with { DurationTicks = durationOverride };
                            if (maxOreTier > 0) effect = effect with { Params = effect.Params with { MaxOreTier = maxOreTier } };

                            var brew = new PotionBrew { Domain = domain, Effects = { effect } };
                            augment?.Invoke(new BrewBuilder(brew, r.buffTime));
                            r.GetGlobalItem<BrewGlobalItem>().Brew = brew;
                        }
                    }
                    return r;
                })
            });
        }
    }

    /// <summary>Baseline potency inferred from almanac ratings; an explicit <see cref="AlchemyVariant.Potency"/> always wins.</summary>
    public static int AutoPotency(int solventType, ComponentMatcher[] components)
    {
        if (components.Length == 0) return 0;

        int vial = RatedPotency(solventType);
        int sum = 0;
        bool anyRated = vial > 0;
        foreach (var m in components)
        {
            int p = m.Group != null ? GroupPotency(m.Group) : RatedPotency(m.ExactType);
            if (p > 0) anyRated = true;
            sum += p;
        }
        if (!anyRated) return 0;
        return Math.Clamp(sum / components.Length + vial, 1, 5);
    }

    private static int RatedPotency(int itemType) =>
        itemType > 0 ? Math.Max(0, AlmanacEntryRegistry.For(itemType).Potency) : 0;

    // A group slot can be filled by any member and the brew is stamped without slot context,
    // so rate the group as the floor-average of its members.
    private static int GroupPotency(AlchemyComponentGroup group)
    {
        int count = 0, sum = 0;
        foreach (int t in group.ItemTypes) { sum += RatedPotency(t); count++; }
        return count == 0 ? 0 : sum / count;
    }

    /// <summary>Resolves the vial and solute slots into a brew, with the mode taken from the vial.</summary>
    public static AlchemyResolution Resolve(Item vial, Item[] components)
        => Resolve(vial, components, AlchemyIngredients.GetModeForSolvent(vial));

    /// <summary>
    /// As <see cref="Resolve(Item, Item[])"/> with an explicit mode. The fitting recipe drawing the most solute wins; ties go to registration order.
    /// </summary>
    public static AlchemyResolution Resolve(Item vial, Item[] components, AlchemyMode mode)
    {
        if (mode == AlchemyMode.None) return AlchemyResolution.None;
        if (vial == null || vial.IsAir) return AlchemyResolution.None;
        if (components == null || components.Length == 0) return AlchemyResolution.None;

        AlchemyRecipe? best = null;
        int bestDemand = -1;   // so even a hypothetical component-free recipe can win
        int[] bestCosts = Array.Empty<int>();

        // Closest near-miss, kept only in case nothing matches at all.
        AlchemyShortfall shortfall = default;
        int[] shortfallCosts = Array.Empty<int>();

        Span<int> costs = stackalloc int[components.Length];

        foreach (var recipe in _recipes)
        {
            if (recipe.Mode != mode) continue;

            // Matching is instance-aware (per-item infusions and stacks), so the slot array
            // goes through as-is rather than being flattened to type ids.
            if (!recipe.TryMatch(vial, components, costs, out var miss))
            {
                if (miss.Exists && !shortfall.Exists)
                {
                    shortfall = miss;
                    shortfallCosts = costs.ToArray();
                }
                continue;
            }

            // Strictly greater, so the first registration wins an equal-demand tie.
            if (recipe.TotalQuantity <= bestDemand) continue;

            best = recipe;
            bestDemand = recipe.TotalQuantity;
            bestCosts = costs.ToArray();
        }

        // No match, no output: products like Inert Solution need explicit recipes, not a catch-all. (The steeping fallback is
        // parked below with the rest of the infusion front-end.)
        var output = best?.CreateOutput?.Invoke() ?? new Item();
        if (output.IsAir)
        {
            return new AlchemyResolution
            {
                Shortfall = shortfall,
                ComponentCosts = shortfallCosts,
            };
        }

        return new AlchemyResolution
        {
            Recipe = best,
            Output = output,
            ComponentCosts = bestCosts,
        };
    }

    /// <summary>Just the output item; air when nothing matches.</summary>
    public static Item ResolveOutput(Item vial, Item[] components)
        => Resolve(vial, components).Output;

    /// <inheritdoc cref="ResolveOutput(Item, Item[])"/>
    public static Item ResolveOutput(Item vial, Item[] components, AlchemyMode mode)
        => Resolve(vial, components, mode).Output;

    // Tie-break order when solutes share several flags, following the three axes.
    private static readonly ChemicalQuality[] _infusionPriority =
    [
        ChemicalQuality.Inert, ChemicalQuality.Catalytic,
        ChemicalQuality.Alkaline, ChemicalQuality.Acidic,
        ChemicalQuality.Volatile, ChemicalQuality.Kinetic,
    ];

    private static Item TryInfusionFallback(Item solvent, List<Item> solutes, AlchemyMode mode)
    {
        if (mode != AlchemyMode.Synthesis) return new Item();
        if (solvent.type is not (ItemID.BottledWater or ItemID.BottledHoney)) return new Item();
        if (SolutionGlobalItem.GetInfusion(solvent) != ChemicalQuality.None) return new Item();
        if (solutes.Count == 0) return new Item();

        var common = (ChemicalQuality)~0;
        foreach (var s in solutes)
            common &= AlmanacEntryRegistry.For(s.type).Chemical;

        foreach (var flag in _infusionPriority)
        {
            if ((common & flag) == 0) continue;

            var r = new Item();
            r.SetDefaults(solvent.type);
            if (r.TryGetGlobalItem(out SolutionGlobalItem solution))
                solution.Infusion = flag;
            return r;
        }
        return new Item();
    }
}


/// <summary>
/// Alchemy Station slot whitelists, derived from <see cref="AlchemyRecipeRegistry"/>: every vial and component any recipe references.
/// </summary>
public static class AlchemyIngredients
{
    private static HashSet<int> _substances;
    private static Dictionary<int, AlchemyMode> _solventModes;
    private static List<int> _allIngredients;

    /// <summary>Discard derived caches. Called on mod unload via <see cref="ChemistryRecipeSystem"/>.</summary>
    public static void Clear()
    {
        _substances = null;
        _solventModes = null;
        _allIngredients = null;
    }

    /// <summary>The mode a vial activates, or <see cref="AlchemyMode.None"/> for empty items or items no recipe uses as a vial.</summary>
    public static AlchemyMode GetModeForSolvent(Item vial)
    {
        if (vial == null || vial.IsAir) return AlchemyMode.None;

        EnsureBuilt();
        return _solventModes.TryGetValue(vial.type, out var mode) ? mode : AlchemyMode.None;
    }

    public static bool IsValidSolvent(Item item)
    {
        if (item == null || item.IsAir) return false;

        EnsureBuilt();
        return _solventModes.ContainsKey(item.type);
    }

    public static bool IsValidSubstance(Item item)
    {
        if (item == null || item.IsAir) return false;

        EnsureBuilt();
        return _substances.Contains(item.type);
    }

    public static bool IsSolventType(int itemType)
    {
        if (itemType <= 0) return false;
        EnsureBuilt();
        return _solventModes.ContainsKey(itemType);
    }

    public static bool IsSubstanceType(int itemType)
    {
        if (itemType <= 0) return false;
        EnsureBuilt();
        return _substances.Contains(itemType);
    }

    /// <summary>
    /// Every item type used as a vial or component, sorted by name. Undiscovered hidden entries stay listed and render as "?".
    /// </summary>
    public static IReadOnlyList<int> AllIngredientTypes
    {
        get { EnsureBuilt(); return _allIngredients; }
    }

    private static void EnsureBuilt()
    {
        if (_substances != null) return;

        var components = new HashSet<int>();
        var vialModes = new Dictionary<int, AlchemyMode>();

        foreach (var recipe in AlchemyRecipeRegistry.All)
        {
            // First mode wins; conflicting registrations are a data bug, not something to merge.
            vialModes.TryAdd(recipe.SolventType, recipe.Mode);

            foreach (var matcher in recipe.Components)
            {
                if (matcher.Group != null)
                {
                    foreach (int t in matcher.Group.ItemTypes)
                        components.Add(t);
                }
                else if (matcher.ExactType > 0)
                {
                    components.Add(matcher.ExactType);
                }
            }
        }

        _substances = components;
        _solventModes = vialModes;

        var all = new HashSet<int>(components);
        foreach (int vial in vialModes.Keys) all.Add(vial);
        _allIngredients = all
            .Where(t => t > 0)
            .OrderBy(t => Lang.GetItemNameValue(t))
            .ToList();
    }
}

public enum AlchemyMode
{
    None,

    /// <summary>Modifying existing items, such as reforging or enchanting.</summary>
    Augmentation,

    /// <summary>Making potions and elixirs.</summary>
    Synthesis,

    /// <summary>Making utilities and weapons.</summary>
    Alchemy
}

/// <summary>
/// One component slot's rule: an exact type or an <see cref="AlchemyComponentGroup"/>, optionally requiring an infusion. Ints and groups convert implicitly.
/// </summary>
public readonly struct ComponentMatcher
{
    public readonly int ExactType;
    public readonly AlchemyComponentGroup Group;

    /// <summary>Required instance infusion; None accepts only plain items, so infused and plain variants never cross-match.</summary>
    public readonly ChemicalQuality Infusion;

    /// <summary>Units drawn per batch unit (the potency dial, shown as "potency N"). Always at least 1.</summary>
    public readonly int Quantity;

    public ComponentMatcher(int exactType, ChemicalQuality infusion = ChemicalQuality.None, int quantity = 1)
    { ExactType = exactType; Group = null; Infusion = infusion; Quantity = Math.Max(1, quantity); }

    public ComponentMatcher(AlchemyComponentGroup group, int quantity = 1)
    { ExactType = 0; Group = group; Infusion = ChemicalQuality.None; Quantity = Math.Max(1, quantity); }

    /// <summary>An exact-type slot that additionally requires the given infusion.</summary>
    public static ComponentMatcher Infused(int itemType, ChemicalQuality infusion) => new(itemType, infusion);

    /// <summary>
    /// An exact-type slot drawing <paramref name="quantity"/> per batch unit; the heavier variant wins when both are affordable.
    /// </summary>
    public static ComponentMatcher Of(int itemType, int quantity, ChemicalQuality infusion = ChemicalQuality.None)
        => new(itemType, infusion, quantity);

    /// <summary>A group slot drawn at <paramref name="quantity"/> potency, whichever member fills it.</summary>
    public static ComponentMatcher Of(AlchemyComponentGroup group, int quantity) => new(group, quantity);

    /// <summary>Type-level acceptance (almanac indexing: "which recipes use this item type").</summary>
    public bool Matches(int itemType) =>
        Group != null ? Group.Contains(itemType) : itemType == ExactType;

    /// <summary>
    /// Type/group plus infusion match. Stack-blind on purpose: <see cref="AlchemyRecipe.TryMatch"/> checks quantity separately.
    /// </summary>
    public bool Matches(Item item) =>
        Matches(item.type) && SolutionGlobalItem.GetInfusion(item) == Infusion;

    /// <summary>Lower is more specific: 1 for exact types, the group size for groups.</summary>
    public int Specificity => Group != null ? Group.Count : 1;

    public static implicit operator ComponentMatcher(int itemType) => new(itemType);
    public static implicit operator ComponentMatcher(AlchemyComponentGroup group) => new(group);
}

/// <summary>A slot with the right solute but too few of it, so the station can name what's missing.</summary>
public readonly record struct AlchemyShortfall(int ItemType, int Have, int Required)
{
    public bool Exists => ItemType > 0;
}

/// <summary>One recipe: (mode, vial, components) to output, with components matched as an unordered multiset.</summary>
public sealed class AlchemyRecipe
{
    public AlchemyMode Mode { get; init; }
    public int SolventType { get; init; }

    /// <summary>Required solvent infusion; None means plain solvent only.</summary>
    public ChemicalQuality SolventInfusion { get; init; }

    /// <summary>Output type for indexing without invoking <see cref="CreateOutput"/>; 0 only for fully dynamic recipes.</summary>
    public int OutputItemType { get; init; }

    /// <summary>Required component slots (1-3): unordered, duplicates allowed, each with its own quantity.</summary>
    public ComponentMatcher[] Components { get; init; } = Array.Empty<ComponentMatcher>();

    /// <summary>Output factory, for dynamic results like custom stacks, prefixes or augmented potion stats.</summary>
    public Func<Item> CreateOutput { get; init; }

    /// <summary>Solute units consumed per batch unit; ranks matches so a heavier stack reaches the heavier recipe.</summary>
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
    /// Whether the solvent and slots satisfy this recipe, stacks included. <paramref name="slotCosts"/> gets each slot's per-unit cost;
    /// on a stacks-only failure it holds the unmet requirement, otherwise it's cleared.
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

    /// <summary>Claims a slot per matcher, most specific first; each claimed slot's cost (never 0) doubles as its claimed marker.</summary>
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

    /// <summary>Narrower matchers pick first; among equals the hungrier one does, so a light matcher can't starve a heavy one.</summary>
    private static bool ClaimsFirst(ComponentMatcher a, ComponentMatcher b) =>
        a.Specificity != b.Specificity
            ? a.Specificity < b.Specificity
            : a.Quantity > b.Quantity;
}