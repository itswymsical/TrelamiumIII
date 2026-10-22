using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>Report-only consistency checks over <see cref="AlchemyCore" />. Run after load.</summary>
public static class AlchemyRecipeValidator
{
    /// <summary>Longest allowed chain of principled solvents, counting the output itself.</summary>
    private const int MAX_SOLVENT_CHAIN = 3;

    public readonly record struct Finding(string Rule, string Message);

    public static List<Finding> Run()
    {
        var findings = new List<Finding>();
        var seen = new HashSet<string>();
        var signatures = new Dictionary<string, AlchemyRecipe>();

        void Add(string rule, string message)
        {
            if (seen.Add(rule + message))
                findings.Add(new Finding(rule, message));
        }

        foreach (AlchemyRecipe recipe in AlchemyCore.All)
        {
            if (recipe.SuppressValidation != null)
            {
                Add("Suppressed", $"{Describe(recipe)}: {recipe.SuppressValidation}");
                continue;
            }

            string signature = SignatureOf(recipe);

            if (signatures.TryGetValue(signature, out AlchemyRecipe? first))
            {
                Add(first.OutputItemType == recipe.OutputItemType ? "Duplicate" : "Conflict",
                    $"{Describe(recipe)} repeats the inputs of {Describe(first)}");
            }
            else
            {
                signatures[signature] = recipe;
            }

            PrincipleType outputPrinciple = BrewCompositions.PrincipleOf(recipe.OutputItemType);

            foreach (BrewComposition composition in CompositionsOf(recipe))
            {
                if (composition.Category == BrewCategory.Augmentation)
                    continue;

                if (outputPrinciple != PrincipleType.None && (composition.Principle & outputPrinciple) == 0)
                {
                    Add("PrincipleMismatch",
                        $"{Describe(recipe)}: output is {outputPrinciple}, ingredients resolve to {composition.Principle}"
                        + (composition.PrimarySolute > 0 ? $" ({Name(composition.PrimarySolute)})" : string.Empty));
                }

                if (composition.Category == BrewCategory.Refinement && ChemicalRoles.IsPotion(recipe.OutputItemType))
                    Add("UnprincipledPotion", $"{Describe(recipe)}: potion output from ingredients with no principle");
            }
        }

        foreach (AlmanacEntry entry in AlmanacEntryRegistry.All)
        {
            if (entry.Principle == PrincipleType.None || ChemicalRoles.Of(entry.ItemType) != ChemicalRole.Solvent)
                continue;

            if (!AlmanacIndex.RecipesProducing(entry.ItemType).Any(r => BrewCompositions.Of(r).PrimarySolute > 0))
            {
                Add("UnbrewedSolventPrinciple",
                    $"{Name(entry.ItemType)} has principle {entry.Principle} but no recipe produces it from a principled solute");
            }
        }

        var depths = new Dictionary<int, int>();

        foreach (int output in AlmanacIndex.AllOutputTypes)
        {
            int depth = SolventChainDepth(output, depths, new HashSet<int>());

            if (depth > MAX_SOLVENT_CHAIN)
                Add("SolventChainDepth", $"{Name(output)} is {depth} principled solvents deep (max {MAX_SOLVENT_CHAIN})");
        }

        return findings;
    }

    /// <summary>The first-member composition, plus one per other group member substituted into its slot.</summary>
    private static List<BrewComposition> CompositionsOf(AlchemyRecipe recipe)
    {
        int[] types = new int[recipe.Components.Length];
        int[] levels = new int[recipe.Components.Length];

        for (int i = 0; i < types.Length; i++)
        {
            types[i] = BrewCompositions.FirstMember(recipe.Components[i]);
            levels[i] = recipe.Components[i].MinPotency;
        }

        var compositions = new List<BrewComposition> { BrewCompositions.Of(recipe.InputType, 1, types, levels) };

        for (int i = 0; i < types.Length; i++)
        {
            if (recipe.Components[i].Group is not { } group)
                continue;

            int baseline = types[i];

            foreach (int member in group.ItemTypes)
            {
                if (member == baseline)
                    continue;

                types[i] = member;
                compositions.Add(BrewCompositions.Of(recipe.InputType, 1, types, levels));
            }

            types[i] = baseline;
        }

        return compositions;
    }

    /// <summary>Longest chain of principled solvents ending in <paramref name="itemType" />; a cycle contributes 0.</summary>
    private static int SolventChainDepth(int itemType, Dictionary<int, int> memo, HashSet<int> visiting)
    {
        if (memo.TryGetValue(itemType, out int depth))
            return depth;

        if (!visiting.Add(itemType))
            return 0;

        foreach (AlchemyRecipe recipe in AlmanacIndex.RecipesProducing(itemType))
        {
            if (BrewCompositions.Of(recipe).Category != BrewCategory.Synthesis)
                continue;

            bool principledSolvent = ChemicalRoles.Of(recipe.InputType) == ChemicalRole.Solvent
                && BrewCompositions.PrincipleOf(recipe.InputType) != PrincipleType.None;

            depth = Math.Max(depth, 1 + (principledSolvent ? SolventChainDepth(recipe.InputType, memo, visiting) : 0));
        }

        visiting.Remove(itemType);
        memo[itemType] = depth;

        return depth;
    }

    private static string SignatureOf(AlchemyRecipe recipe)
    {
        IEnumerable<string> components = recipe.Components
            .Select(m => $"{(m.Group != null ? "g:" + m.Group.Name : "t:" + m.ExactType)}p{m.MinPotency}-{m.MaxPotency}i{(int)m.Infusion}")
            .OrderBy(part => part, StringComparer.Ordinal);

        return $"{recipe.InputType}i{(int)recipe.InputInfusion}|{string.Join(",", components)}";
    }

    private static string Describe(AlchemyRecipe recipe)
    {
        IEnumerable<string> components = recipe.Components
            .Select(m => (m.Group != null ? m.Group.Name : Name(m.ExactType))
                + (m.IsRanged ? $" (potency {m.MinPotency}-{m.MaxPotency})"
                    : m.MinPotency > 1 ? $" (potency {m.MinPotency})"
                    : string.Empty));

        return $"{Name(recipe.InputType)} + [{string.Join(", ", components)}] -> {Name(recipe.OutputItemType)}";
    }

    private static string Name(int itemType) => itemType > 0 ? Lang.GetItemNameValue(itemType) : "none";
}
