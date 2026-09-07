using System.Collections.Generic;
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
