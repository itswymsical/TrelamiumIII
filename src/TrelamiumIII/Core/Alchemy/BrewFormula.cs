using TrelamiumIII.Core.Alchemy.Brewing;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>What <see cref="AlchemyRecipe.CreateOutput" /> reads. <see cref="Input" /> and <see cref="Components" /> are null in previews.</summary>
/// <param name="ExtraLevels">Consumed slot levels above the recipe's minimum potencies.</param>
public readonly record struct BrewContext(BrewComposition Composition, int ExtraLevels, Item? Input, Item[]? Components = null);

/// <summary>
///     Builds recipe outputs. A variant's authored values describe the brew at its authored potency in its first input; ranged
///     slot levels and a larger <see cref="SolventBonus" /> raise that potency, and <see cref="BrewTuning.Shift" /> moves
///     strength and duration with it. Augmenting a potion into itself keeps the input's brew (see <see cref="CreateAugmented" />).
/// </summary>
public static class BrewFormula
{
    /// <summary>Potency a solvent with a principle adds over one without.</summary>
    public const int PRINCIPLED_SOLVENT_BONUS = 1;

    /// <summary><see cref="PRINCIPLED_SOLVENT_BONUS" /> for a solvent with a principle, else 0. Only call after load.</summary>
    public static int SolventBonus(int itemType) =>
        ChemicalRoles.Of(itemType) == ChemicalRole.Solvent && BrewCompositions.PrincipleOf(itemType) != PrincipleType.None
            ? PRINCIPLED_SOLVENT_BONUS
            : 0;

    /// <param name="authoredInput">The input type the variant's values were authored for.</param>
    public static Item CreateOutput(int outputType, BrewRecipe variant, in BrewContext context, int authoredInput)
    {
        var output = new Item();
        output.SetDefaults(outputType);
        output.stack = variant.OutputStack;

        // Infused outputs are suffixed solvents; a brew would brand e.g. Bottled Water through its healLife.
        if (variant.OutputInfusion != Temperament.None)
        {
            if (output.TryGetGlobalItem(out SolutionGlobalItem solution))
                solution.Infusion = variant.OutputInfusion;

            return output;
        }

        if ((output.buffType > 0 || output.healLife > 0 || output.healMana > 0)
            && CreateBrew(output, variant, context, authoredInput) is { } brew)
        {
            output.GetGlobalItem<BrewGlobalItem>().Brew = brew;
        }

        return output;
    }

    private static PotionBrew? CreateBrew(Item output, BrewRecipe variant, in BrewContext context, int authoredInput)
    {
        BrewComposition composition = context.Composition;
        int duration = variant.DurationTicks > 0 ? variant.DurationTicks : variant.DurationSeconds * 60;
        PotionBrew brew;

        if (CarriedBrew(output, context) is { } carried)
        {
            // Authored values on an augmentation replace the carried primary's, unshifted.
            brew = carried.Clone();
            AlchemicalEffect primary = variant.Potency > 0 ? BaseEffect(output, variant.Potency) : brew.Primary;
            brew.Effects[0] = WithOreTier(WithAuthored(primary, variant, duration), variant);
        }
        else
        {
            if (variant.Potency <= 0 && composition.Potency <= 0)
                return null;

            int authored = variant.Potency > 0 ? variant.Potency : Math.Max(1, composition.Potency - context.ExtraLevels);
            int potency = authored + context.ExtraLevels + SolventBonus(composition.Input) - SolventBonus(authoredInput);

            // A dissolved potion keeps its own potency, so transmuting a strong brew never weakens it.
            potency = Math.Max(potency, DissolvedPotency(context));

            AlchemicalEffect primary = WithAuthored(BaseEffect(output, authored), variant, duration);
            primary = WithOreTier(BrewTuning.Shift(primary, potency), variant);

            brew = new PotionBrew { Principle = composition.Principle, Effects = { primary } };
        }

        return brew;
    }

    /// <summary>
    ///     The input potion's brew with <paramref name="augmentation" />'s edits, recorded as one more stage. Every stage past
    ///     <see cref="BrewDilution.FREE_STAGES" /> then costs the primary effect potency; potency steps are not stages. Previews
    ///     augment the output's default brew.
    /// </summary>
    public static Item CreateAugmented(Augmentation augmentation, in BrewContext context)
    {
        var output = new Item();
        output.SetDefaults(augmentation.Output);

        Item source = context.Input is { IsAir: false } input ? input : output;

        if (source.GetGlobalItem<BrewGlobalItem>().Brew is not { Effects.Count: > 0 } carried)
            return output;

        PotionBrew brew = carried.Clone();
        var builder = new BrewBuilder(brew, output.buffTime, context.Composition.Potency);

        foreach (Action<BrewBuilder> edit in augmentation.Edits)
            edit(builder);

        if (!augmentation.IsPotencyStep)
        {
            brew.Augments.Add(augmentation.Key);

            if (brew.Stage > BrewDilution.FREE_STAGES)
                builder.Potency(-BrewDilution.POTENCY_PER_STAGE);
        }

        output.GetGlobalItem<BrewGlobalItem>().Brew = brew;
        return output;
    }

    /// <summary>
    ///     When a potion is augmented into its own type: the input's brew, or the output's default brew in previews.
    /// </summary>
    private static PotionBrew? CarriedBrew(Item output, in BrewContext context)
    {
        if (context.Composition.Category != BrewCategory.Augmentation || context.Composition.Input != output.type)
            return null;

        Item source = context.Input is { IsAir: false } input ? input : output;

        // An effectless brew has no primary to carry, so the output is built from the variant instead.
        return source.GetGlobalItem<BrewGlobalItem>().Brew is { Effects.Count: > 0 } brew ? brew : null;
    }

    /// <summary>The highest brewed potency among solutions in the component slots, or 0 when none is dissolved.</summary>
    private static int DissolvedPotency(in BrewContext context)
    {
        if (context.Composition.Category != BrewCategory.Transmutation || context.Components is not { } components)
            return 0;

        int potency = 0;

        foreach (Item component in components)
        {
            if (component is { IsAir: false } && component.GetGlobalItem<BrewGlobalItem>().Brew is { Effects.Count: > 0 } brew)
                potency = Math.Max(potency, brew.Primary.Potency);
        }

        return potency;
    }

    private static AlchemicalEffect BaseEffect(Item output, int potency) =>
        output.buffType > 0
            ? BrewTuning.Create(output.buffType, output.buffTime, potency)
            : BrewTuning.CreateRestoration(potency);

    private static AlchemicalEffect WithAuthored(AlchemicalEffect effect, BrewRecipe variant, int duration)
    {
        if (variant.StrengthPercent > 0)
            effect = effect with { StrengthPercent = variant.StrengthPercent };

        if (duration > 0)
            effect = effect with { DurationTicks = duration };

        return effect;
    }

    private static AlchemicalEffect WithOreTier(AlchemicalEffect effect, BrewRecipe variant) =>
        variant.MaxOreTier > 0 ? effect with { Params = effect.Params with { MaxOreTier = variant.MaxOreTier } } : effect;
}
