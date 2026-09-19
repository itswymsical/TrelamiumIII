using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Alchemy;

public sealed class RecipeVariant
{
    public ComponentMatcher[] Components { get; }

    /// <summary>Stack size of the produced item before the player's output-count multiplier.</summary>
    public int OutputStack { get; init; } = 1;

    /// <summary>Optional override for output construction.</summary>
    public Func<Item> CustomOutput { get; init; }

    /// <summary>
    /// Brew potency (1-5) in the recipe's first input at minimum slot levels. 0 uses <see cref="BrewComposition.Potency"/>.
    /// <see cref="BrewFormula"/> raises it for ranged slot levels and for solvents with a larger <see cref="BrewFormula.SolventBonus"/>.
    /// </summary>
    public int Potency { get; init; }

    /// <summary>Strength (percent of the vanilla effect) at <see cref="Potency"/>; shifted with it. 0 = use the curve.</summary>
    public int StrengthPercent { get; init; }

    /// <summary>Brewed duration in seconds at <see cref="Potency"/>; shifted with it. 0 = use the curve.</summary>
    public int DurationSeconds { get; init; }

    /// <summary>Brewed duration in ticks at <see cref="Potency"/>; wins over <see cref="DurationSeconds"/>. 0 = unset.</summary>
    public int DurationTicks { get; init; }

    /// <summary>Override a gated effect's max ore tier (Spelunker). 0 = use the potency default.</summary>
    public int MaxOreTier { get; init; }

    /// <summary>Optional pass after the primary effect: boost, re-gate, or add further effects.</summary>
    public Action<BrewBuilder>? Augment { get; init; }

    /// <summary>Required input infusion; None means plain input only, so one input type can key different recipes.</summary>
    public ChemicalQuality InputInfusion { get; init; }

    /// <summary>Infusion stamped onto the output. Infused outputs are plain solvents, so they skip brew branding.</summary>
    public ChemicalQuality OutputInfusion { get; init; }

    /// <summary>Whether brewing returns the solvent's bottle. Null derives it; see <see cref="AlchemyRecipe.ReturnsBottle" />.</summary>
    public bool? ReturnsBottle { get; init; }

    /// <summary>Non-null excludes this variant from recipe validation; the value is logged as the justification.</summary>
    public string? SuppressValidation { get; init; }

    /// <summary>
    /// One input combination for a shared output (see <see cref="AlchemyRecipes.CreateRecipe"/>). A plain item type
    /// requires potency 1; <c>ComponentMatcher.Of(type, 3)</c> requires potency 3.
    /// </summary>
    public RecipeVariant(params ComponentMatcher[] components)
    {
        Components = components;
    }
}

/// <summary>
/// A resolved brew: recipe, output, and per-slot costs and required potencies aligned to the station's component slots. On a
/// potency-only failure <see cref="Found"/> is false and <see cref="Shortfall"/>, <see cref="ComponentCosts"/> and
/// <see cref="RequiredPotencies"/> describe the nearest recipe.
/// </summary>
public sealed class AlchemyResolution
{
    public static readonly AlchemyResolution None = new();

    public AlchemyRecipe? Recipe { get; init; }

    public Item Output { get; init; } = new();

    /// <summary>Composition of the slotted ingredients; set even when no recipe matches.</summary>
    public BrewComposition Composition { get; init; }

    public bool ReturnsBottle => Found && Recipe!.ReturnsBottle;

    /// <summary>Items taken from each component slot per batch; 0 for unclaimed slots.</summary>
    public int[] ComponentCosts { get; init; } = Array.Empty<int>();

    /// <summary>Minimum potency required of each component slot; 0 for unclaimed slots.</summary>
    public int[] RequiredPotencies { get; init; } = Array.Empty<int>();

    /// <summary>Items taken from the input slot per batch.</summary>
    public int InputCost { get; init; } = 1;

    /// <summary>An item below its required potency or cost. Only meaningful while <see cref="Found"/> is false.</summary>
    public AlchemyShortfall Shortfall { get; init; }

    public bool Found => Recipe != null && !Output.IsAir;

    public int CostFor(int index) =>
        index >= 0 && index < ComponentCosts.Length ? ComponentCosts[index] : 0;

    public int RequiredPotencyFor(int index) =>
        index >= 0 && index < RequiredPotencies.Length ? RequiredPotencies[index] : 0;

    /// <summary><see cref="Output" />'s <see cref="AlmanacEntry.BrewTicks" /> divided by <paramref name="speed" />; at least 1.</summary>
    public int BrewTicks(float speed)
    {
        int ticks = AlmanacEntryRegistry.TryGet(Output.type, out AlmanacEntry? entry) ? entry.BrewTicks : AlmanacEntry.DefaultBrewTicks;

        return Math.Max(1, (int)MathF.Round(ticks / speed));
    }

    /// <summary>Batches the slotted stacks can pay for at <see cref="InputCost"/> and <see cref="ComponentCosts"/>.</summary>
    public int MaxBatches(Item input, Item[] components)
    {
        if (!Found || input is null || input.IsAir) return 1;

        int max = input.stack / Math.Max(1, InputCost);
        for (int i = 0; i < components.Length && i < ComponentCosts.Length; i++)
        {
            int cost = ComponentCosts[i];
            if (cost <= 0 || components[i] is null || components[i].IsAir) continue;
            max = Math.Min(max, components[i].stack / cost);
        }
        return Math.Max(1, max);
    }
}

/// <summary>Central registry of alchemy recipes, added via <see cref="Register"/> or <see cref="CreateRecipe"/>.</summary>
public static class AlchemyRecipes
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

    /// <summary>Registers each variant for one input type.</summary>
    public static void CreateRecipe(int inputType, int outputType, params RecipeVariant[] variants) =>
        CreateRecipe([inputType], outputType, variants);

    /// <summary>
    /// Registers each variant for every input type. A variant's authored values apply to the first input; the others differ
    /// by <see cref="BrewFormula.SolventBonus"/>.
    /// </summary>
    public static void CreateRecipe(int[] inputTypes, int outputType, params RecipeVariant[] variants)
    {
        int authoredInput = inputTypes[0];

        foreach (int inputType in inputTypes)
        {
            foreach (RecipeVariant variant in variants)
            {
                Register(new AlchemyRecipe
                {
                    InputType = inputType,
                    InputInfusion = variant.InputInfusion,
                    OutputItemType = outputType,
                    Components = variant.Components,
                    ReturnsBottleOverride = variant.ReturnsBottle,
                    SuppressValidation = variant.SuppressValidation,
                    CreateOutput = variant.CustomOutput is { } custom
                        ? _ => custom()
                        : context => BrewFormula.CreateOutput(outputType, variant, context, authoredInput),
                });
            }
        }
    }

    /// <summary>
    /// Resolves the input and component slots against every recipe. The fitting recipe with the highest total minimum potency
    /// wins; ties go to registration order.
    /// </summary>
    public static AlchemyResolution Resolve(Item input, Item[] components)
    {
        if (input == null || input.IsAir) return AlchemyResolution.None;
        if (components == null || components.Length == 0) return AlchemyResolution.None;

        AlchemyRecipe? best = null;
        int bestDemand = -1;   // so even a hypothetical component-free recipe can win
        int[] bestCosts = Array.Empty<int>();
        int[] bestPotencies = Array.Empty<int>();

        // Closest near-miss, kept only in case nothing matches at all.
        AlchemyShortfall shortfall = default;
        int[] shortfallCosts = Array.Empty<int>();
        int[] shortfallPotencies = Array.Empty<int>();

        Span<int> costs = stackalloc int[components.Length];
        Span<int> potencies = stackalloc int[components.Length];

        foreach (var recipe in _recipes)
        {
            // Matching is instance-aware (per-item infusions and stacks), so the slot array
            // goes through as-is rather than being flattened to type ids.
            if (!recipe.TryMatch(input, components, costs, potencies, out var miss))
            {
                if (miss.Exists && !shortfall.Exists)
                {
                    shortfall = miss;
                    shortfallCosts = costs.ToArray();
                    shortfallPotencies = potencies.ToArray();
                }
                continue;
            }

            // Strictly greater, so the first registration wins an equal-demand tie.
            if (recipe.TotalMinPotency <= bestDemand) continue;

            best = recipe;
            bestDemand = recipe.TotalMinPotency;
            bestCosts = costs.ToArray();
            bestPotencies = potencies.ToArray();
        }

        if (best == null)
        {
            return new AlchemyResolution
            {
                Composition = BrewCompositions.Of(input, components),
                Shortfall = shortfall,
                ComponentCosts = shortfallCosts,
                RequiredPotencies = shortfallPotencies,
            };
        }

        // The output is built at the consumed levels, not the slotted stacks.
        var composition = BrewCompositions.Of(input, components, bestPotencies);

        int extraLevels = -best.TotalMinPotency;
        foreach (int level in bestPotencies)
            extraLevels += level;

        var output = best.CreateOutput?.Invoke(new BrewContext(composition, extraLevels, input, components)) ?? new Item();

        if (output.IsAir)
            return new AlchemyResolution { Composition = composition };

        return new AlchemyResolution
        {
            Recipe = best,
            Output = output,
            Composition = composition,
            ComponentCosts = bestCosts,
            RequiredPotencies = bestPotencies,
            InputCost = AlchemyRecipe.InputCostOf(input),
        };
    }

    /// <summary>Just the output item; air when nothing matches.</summary>
    public static Item ResolveOutput(Item input, Item[] components)
        => Resolve(input, components).Output;
}


/// <summary>Item types referenced by <see cref="AlchemyRecipes"/>: every recipe input and component.</summary>
public static class AlchemyIngredients
{
    private static HashSet<int> _substances;
    private static HashSet<int> _inputs;
    private static List<int> _allIngredients;

    /// <summary>Discard derived caches. Called on mod unload via <see cref="ChemistryRecipeSystem"/>.</summary>
    public static void Clear()
    {
        _substances = null;
        _inputs = null;
        _allIngredients = null;
    }

    public static bool IsInputType(int itemType)
    {
        if (itemType <= 0) return false;
        EnsureBuilt();
        return _inputs.Contains(itemType);
    }

    public static bool IsSubstanceType(int itemType)
    {
        if (itemType <= 0) return false;
        EnsureBuilt();
        return _substances.Contains(itemType);
    }

    /// <summary>
    /// Every item type used as an input or component, sorted by name. Undiscovered hidden entries stay listed and render as "?".
    /// </summary>
    public static IReadOnlyList<int> AllIngredientTypes
    {
        get { EnsureBuilt(); return _allIngredients; }
    }

    private static void EnsureBuilt()
    {
        if (_substances != null) return;

        var components = new HashSet<int>();
        var inputs = new HashSet<int>();

        foreach (var recipe in AlchemyRecipes.All)
        {
            inputs.Add(recipe.InputType);

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
        _inputs = inputs;

        var all = new HashSet<int>(components);
        all.UnionWith(inputs);
        _allIngredients = all
            .Where(t => t > 0)
            .OrderBy(t => Lang.GetItemNameValue(t))
            .ToList();
    }
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

    /// <summary>Minimum slot potency, 1 to <see cref="SolutePotency.MAX"/>.</summary>
    public readonly int MinPotency;

    /// <summary>
    /// Highest slot potency a batch consumes, at least <see cref="MinPotency"/>. A batch consumes the slot's level clamped to
    /// this range; each level above the minimum raises the brew's potency by one (see <see cref="BrewFormula"/>).
    /// </summary>
    public readonly int MaxPotency;

    public ComponentMatcher(int exactType, ChemicalQuality infusion = ChemicalQuality.None, int minPotency = 1, int maxPotency = 0)
    {
        ExactType = exactType;
        Group = null;
        Infusion = infusion;
        MinPotency = Math.Clamp(minPotency, 1, SolutePotency.MAX);
        MaxPotency = Math.Clamp(maxPotency, MinPotency, SolutePotency.MAX);
    }

    public ComponentMatcher(AlchemyComponentGroup group, int minPotency = 1, int maxPotency = 0)
    {
        ExactType = 0;
        Group = group;
        Infusion = ChemicalQuality.None;
        MinPotency = Math.Clamp(minPotency, 1, SolutePotency.MAX);
        MaxPotency = Math.Clamp(maxPotency, MinPotency, SolutePotency.MAX);
    }

    /// <summary>An exact-type slot that additionally requires the given infusion.</summary>
    public static ComponentMatcher Infused(int itemType, ChemicalQuality infusion) => new(itemType, infusion);

    /// <summary>An exact-type slot requiring <paramref name="minPotency"/>; the more demanding variant wins when both fit.</summary>
    public static ComponentMatcher Of(int itemType, int minPotency, ChemicalQuality infusion = ChemicalQuality.None, int maxPotency = 0)
        => new(itemType, infusion, minPotency, maxPotency);

    /// <summary>A group slot requiring <paramref name="minPotency"/>, whichever member fills it.</summary>
    public static ComponentMatcher Of(AlchemyComponentGroup group, int minPotency, int maxPotency = 0) => new(group, minPotency, maxPotency);

    /// <summary>Whether the slot's level above <see cref="MinPotency"/> is consumed and raises the brew's potency.</summary>
    public bool IsRanged => MaxPotency > MinPotency;

    /// <summary>Type-level acceptance (almanac indexing: "which recipes use this item type").</summary>
    public bool Matches(int itemType) =>
        Group != null ? Group.Contains(itemType) : itemType == ExactType;

    /// <summary>Type/group plus infusion match. Potency is checked separately by <see cref="AlchemyRecipe.TryMatch"/>.</summary>
    public bool Matches(Item item) =>
        Matches(item.type) && SolutionGlobalItem.GetInfusion(item) == Infusion;

    /// <summary>Lower is more specific: 1 for exact types, the group size for groups.</summary>
    public int Specificity => Group != null ? Group.Count : 1;

    public static implicit operator ComponentMatcher(int itemType) => new(itemType);
    public static implicit operator ComponentMatcher(AlchemyComponentGroup group) => new(group);
}

/// <summary>A slot holding the right item in too small a stack, so the station can name what's missing.</summary>
public readonly record struct AlchemyShortfall(int ItemType, int Have, int Required)
{
    public bool Exists => ItemType > 0;
}

/// <summary>One recipe: (input, components) to output, with components matched as an unordered multiset.</summary>
public sealed class AlchemyRecipe
{
    public int InputType { get; init; }

    /// <summary>Required input infusion; None means plain input only.</summary>
    public ChemicalQuality InputInfusion { get; init; }

    /// <summary>Output type for indexing without invoking <see cref="CreateOutput"/>; 0 only for fully dynamic recipes.</summary>
    public int OutputItemType { get; init; }

    /// <summary>Required component slots (1-3): unordered, duplicates allowed, each with its own minimum potency.</summary>
    public ComponentMatcher[] Components { get; init; } = Array.Empty<ComponentMatcher>();

    /// <summary>Builds the output from the slotted ingredients.</summary>
    public Func<BrewContext, Item> CreateOutput { get; init; }

    /// <inheritdoc cref="RecipeVariant.ReturnsBottle" />
    public bool? ReturnsBottleOverride { get; init; }

    /// <inheritdoc cref="RecipeVariant.SuppressValidation" />
    public string? SuppressValidation { get; init; }

    /// <summary>
    /// <see cref="ReturnsBottleOverride" />, else true when a solvent other than an empty Bottle produces a solute.
    /// Only evaluate after load: <see cref="ChemicalRoles.Of(int)" /> caches its inference.
    /// </summary>
    public bool ReturnsBottle => ReturnsBottleOverride ?? (InputType != ItemID.Bottle
        && ChemicalRoles.Of(InputType) == ChemicalRole.Solvent
        && ChemicalRoles.Of(OutputItemType) == ChemicalRole.Solute);

    /// <summary>Sum of component minimum potencies; ranks matches so the more demanding recipe wins.</summary>
    public int TotalMinPotency
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < Components.Length; i++) sum += Components[i].MinPotency;
            return sum;
        }
    }

    /// <summary>The output at minimum slot levels with no input item, for almanac display and indexing.</summary>
    public Item PreviewOutput() => CreateOutput?.Invoke(new BrewContext(BrewCompositions.Of(this), 0, null)) ?? new Item();

    /// <summary>Items consumed from the input per batch: one potency level's stack for a solute, otherwise 1.</summary>
    public static int InputCostOf(Item input) =>
        ChemicalRoles.Of(input) == ChemicalRole.Solute ? SolutePotency.StackPerPotency(input.type) : 1;

    /// <summary>
    /// Whether the input and slots satisfy this recipe. <paramref name="slotCosts"/> and <paramref name="slotPotencies"/> receive
    /// each claimed slot's per-batch cost and consumed potency. On a stack-only failure they still describe the requirement and
    /// <paramref name="shortfall"/> names the first short item; on any other failure both are cleared.
    /// </summary>
    public bool TryMatch(Item input, Item[] slots, Span<int> slotCosts, Span<int> slotPotencies, out AlchemyShortfall shortfall)
    {
        shortfall = default;
        slotCosts.Clear();
        slotPotencies.Clear();

        if (input is null || input.IsAir) return false;
        if (input.type != InputType) return false;
        if (SolutionGlobalItem.GetInfusion(input) != InputInfusion) return false;

        int filled = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { IsAir: false }) filled++;
        if (filled != Components.Length) return false;

        int inputCost = InputCostOf(input);
        bool inputCovered = input.stack >= inputCost;

        if (inputCovered && Assign(slots, slotCosts, slotPotencies, requirePotency: true))
            return true;

        // The shape may fit with something under its requirement. Re-assign ignoring potency so the
        // requirement survives for the station to display, and name the first short item.
        slotCosts.Clear();
        slotPotencies.Clear();
        if (!Assign(slots, slotCosts, slotPotencies, requirePotency: false))
        {
            slotCosts.Clear();
            slotPotencies.Clear();
            return false;
        }

        if (!inputCovered)
        {
            shortfall = new AlchemyShortfall(input.type, input.stack, inputCost);
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
    /// Claims a slot per matcher, most specific first. A claimed slot receives its level clamped to the matcher's range (never
    /// 0, which marks it claimed) and that level's cost. Without <paramref name="requirePotency"/> it receives the minimum.
    /// </summary>
    private bool Assign(Item[] slots, Span<int> slotCosts, Span<int> slotPotencies, bool requirePotency)
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
            bool found = false;

            for (int j = 0; j < slots.Length; j++)
            {
                if (slotPotencies[j] != 0) continue;
                var slot = slots[j];
                if (slot is null || slot.IsAir) continue;
                if (!matcher.Matches(slot)) continue;

                int level = SolutePotency.LevelOf(slot);
                if (requirePotency && level < matcher.MinPotency) continue;

                int claimed = requirePotency ? Math.Min(level, matcher.MaxPotency) : matcher.MinPotency;
                slotPotencies[j] = claimed;
                slotCosts[j] = SolutePotency.CostOf(slot.type, claimed);
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>Narrower matchers pick first; among equals the more demanding one does, so a light matcher can't starve a heavy one.</summary>
    private static bool ClaimsFirst(ComponentMatcher a, ComponentMatcher b) =>
        a.Specificity != b.Specificity
            ? a.Specificity < b.Specificity
            : a.MinPotency > b.MinPotency;
}
