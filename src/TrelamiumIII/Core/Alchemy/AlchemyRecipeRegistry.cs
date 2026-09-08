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

    /// <summary>
    /// Optional override for output construction.
    /// </summary>
    public Func<Item> CustomOutput { get; init; }

    /// <summary>Brew potency (1-5) stamped onto buff-potion outputs. 0 = no brew (plain item).</summary>
    public int Potency { get; init; }

    /// <summary>Override the potency curve's strength (percent of the vanilla effect). 0 = use the curve.</summary>
    public int StrengthPercent { get; init; }

    /// <summary>Override the brewed duration, in seconds. 0 = use the curve.</summary>
    public int DurationSeconds { get; init; }

    /// <summary>
    /// Override the brewed duration, in ticks. pairs with <see cref="TimeConversion.SetTime(int, int, int)"/>.
    /// Takes precedence over <see cref="DurationSeconds"/>. 0 = unset.
    /// </summary>
    public int DurationTicks { get; init; }

    /// <summary>Override a gated effect's max ore tier (Spelunker). 0 = use the potency default.</summary>
    public int MaxOreTier { get; init; }

    /// <summary>Optional augmentation pass: boost, re-gate, or combine further effects onto the brew.</summary>
    public Action<BrewBuilder>? Augment { get; init; }

    /// <summary>
    /// Required infusion on the solvent instance (suffixed solutions). None = plain solvent
    /// only. Lets the same solvent item type key different recipes per infusion.
    /// </summary>
    public ChemicalQuality SolventInfusion { get; init; }

    /// <summary>
    /// Stamp this infusion onto the output (producing e.g. "Bottled Water (Inert)").
    /// Infused outputs are reagents, not potions: brew branding is skipped for them.
    /// </summary>
    public ChemicalQuality OutputInfusion { get; init; }

    /// <summary>
    /// One input combination for a shared output. Used with
    /// <see cref="AlchemyRecipeRegistry.RegisterVariants"/>.
    /// <para>Each component is a <see cref="ComponentMatcher"/> and carries its own quantity/
    /// a plain item type draws one, <c>ComponentMatcher.Of(type, 5)</c> draws five.</para>
    /// </summary>
    public AlchemyVariant(params ComponentMatcher[] components)
    {
        Components = components;
    }
}

/// <summary>
/// A resolved brew: the recipe that fired, the item it produces, and what each solute slot
/// gives up per batch unit. Costs are aligned to the station's own slot array, so the UI can
/// gate the batch counter, draw each slot's potency, and consume exactly what was asked for.
/// <para>When nothing matched but a solute was merely under-stacked, <see cref="Found"/> is
/// false while <see cref="Shortfall"/> and <see cref="ComponentCosts"/> still describe the
/// requirement.</para>
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

    /// <summary>
    /// How many batch units the slotted ingredients can pay for. The solvent is one per unit
    /// (a bottle in, a bottle out); solutes go by their recipe cost.
    /// </summary>
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

/// <summary>
/// Central registry of all alchemy recipes. Recipes are added via <see cref="Register"/> /
/// <see cref="RegisterVariants"/>.
/// </summary>
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

    /// <summary>
    /// True when some registered alchemy recipe produces this item type. Lets the recipe system take
    /// over a crafting route only where it actually offers a replacement.
    /// </summary>
    public static bool Produces(int itemType) => _outputTypes.Contains(itemType);

    /// <summary>
    /// Bulk-register many input combinations that share the same (mode, vial, output) target.
    /// Cuts down on boilerplate when one product has several valid component sets.
    /// </summary>
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

                    // Brand buff AND restorative potions with a brewed instance, then apply any
                    // explicit per-recipe overrides (strength / duration). Heal potions have no
                    // buffType; they get a virtual Restoration effect instead. An explicit variant
                    // potency wins; unset, it's inferred from the almanac ratings of the parts
                    // (resolved lazily here because entries register after recipes).
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

    /// <summary>
    /// Potency inferred from almanac ratings when a variant doesn't set one.
    /// <para>This is only the automatic baseline. an explicit <see cref="AlchemyVariant.Potency"/>
    /// always takes precedence, since each effect should be balanced by hand.</para>
    /// </summary>
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

    /// <summary>
    /// Resolve the vial & solute slots into a brew, with the mode taken from the vial.
    /// </summary>
    public static AlchemyResolution Resolve(Item vial, Item[] components)
        => Resolve(vial, components, AlchemyIngredients.GetModeForSolvent(vial));

    /// <summary>
    /// Resolve as <see cref="Resolve(Item, Item[])"/>, but with an explicit mode override
    /// (used by the alchemy mode cycle button).
    /// <para>Among the recipes that fit, the one drawing the most solute wins, so stacking an
    /// ingredient higher reaches the stronger product rather than being change left on the
    /// counter. Registration order breaks ties, which keeps quantity-free recipes behaving
    /// exactly as they did before.</para>
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

        // No matching recipe: no output. Products like Inert Solution must be authored as
        // explicit recipes rather than being a catch-all for any junk combination.
        // (Steeping fallback parked below with the rest of the infusion front-end.)
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

    /// <summary>
    /// The output item alone, for callers that don't care what it costs. Air when nothing matches.
    /// </summary>
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
/// Slot whitelists for the Alchemy Station. Derived entirely from
/// <see cref="AlchemyRecipeRegistry"/>: any vial type referenced by a registered recipe
/// becomes a valid vial in its recipe's mode, and any item type referenced by a
/// component matcher (or member of a referenced <see cref="AlchemyComponentGroup"/>)
/// becomes a valid component.
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

    /// <summary>
    /// Resolves which mode a vial activates. Returns <see cref="AlchemyMode.None"/>
    /// for empty items or items with no recipes consuming them as a vial.
    /// </summary>
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
    /// All distinct item types referenced as a vial or component by any registered recipe,
    /// sorted by display name. Backing list for the Almanac's Ingredients index, undiscovered
    /// <see cref="AlmanacEntry.Hidden"/> entries stay in the list and render there as "?"
    /// (see <see cref="AlmanacDiscoverySystem.IsIngredientVisible"/>).
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
            // First mode wins.
            // Conflicting registrations would be a recipe-data bug, not something to silently merge.
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
/// narrowed to a specific per-instance <see cref="ChemicalQuality"/> infusion (suffixed
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
    public readonly ChemicalQuality Infusion;

    /// <summary>
    /// How many of the solute this slot draws per batch unit. This is the potency dial:
    /// the station reads it as a stack requirement, and the front end shows it as
    /// "potency N" on the solute. Always at least 1.
    /// </summary>
    public readonly int Quantity;

    public ComponentMatcher(int exactType, ChemicalQuality infusion = ChemicalQuality.None, int quantity = 1)
    { ExactType = exactType; Group = null; Infusion = infusion; Quantity = Math.Max(1, quantity); }

    public ComponentMatcher(AlchemyComponentGroup group, int quantity = 1)
    { ExactType = 0; Group = group; Infusion = ChemicalQuality.None; Quantity = Math.Max(1, quantity); }

    /// <summary>An exact-type slot that additionally requires the given infusion.</summary>
    public static ComponentMatcher Infused(int itemType, ChemicalQuality infusion) => new(itemType, infusion);

    /// <summary>
    /// An exact-type slot drawn at <paramref name="quantity"/> potency, i.e. consuming that
    /// many of the item per batch unit. Two variants over the same solute at different
    /// quantities are different recipes, and the heavier one wins when both are affordable.
    /// </summary>
    public static ComponentMatcher Of(int itemType, int quantity, ChemicalQuality infusion = ChemicalQuality.None)
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
    public ChemicalQuality SolventInfusion { get; init; }

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