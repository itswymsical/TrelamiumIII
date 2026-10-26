using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Alchemy.Brewing;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// One augmentation of <see cref="Output"/>: its components and the ordered edits they make to the carried brew. Built by
/// <see cref="AugmentStep"/> during registration and applied by <see cref="BrewFormula.CreateAugmented"/>.
/// </summary>
public sealed class Augmentation
{
    public int Output { get; }

    public ComponentMatcher[] Components { get; }

    internal List<Action<BrewBuilder>> Edits { get; } = new();

    /// <summary>Net potency the edits add. Above 0 the augmentation is refused once the potion reaches potency 5.</summary>
    public int PotencyDelta { get; internal set; }

    /// <summary>Times one potion may take this augmentation. Null: once, or until potency 5 when it raises potency.</summary>
    public int? MaxApplications { get; internal set; }

    /// <summary>
    /// A potency step made from the output's own ingredients (see <see cref="AlchemyCore.AddPotencySteps"/>): needs the
    /// ingredient one level above the potion's potency, and is not a stage, so it never dilutes.
    /// </summary>
    public bool IsPotencyStep { get; private init; }

    private string? _key;

    /// <summary>Output and components by name, so a saved brew still recognises the augmentation after recipes move around.</summary>
    public string Key => _key ??= $"{ItemKey(Output)}+{string.Join("+", Components.Select(MatcherKey))}";

    private ComponentMatcher[]?[]? _stepMatchers;

    public Augmentation(int output, ComponentMatcher[] components)
    {
        Output = output;
        Components = components;
    }

    internal static Augmentation PotencyStep(int output, ComponentMatcher ingredient)
    {
        var step = new Augmentation(output, [ingredient]) { IsPotencyStep = true, PotencyDelta = 1 };
        step.Edits.Add(b => b.Potency(1));
        return step;
    }

    /// <summary>Whether a potion can take this augmentation: under potency 5 if it raises potency, and under its repeat limit.</summary>
    internal bool Accepts(Item input)
    {
        if (input.GetGlobalItem<BrewGlobalItem>().Brew is not { Effects.Count: > 0 } brew)
            return false;

        if (PotencyDelta > 0 && brew.Primary.Potency >= SolutePotency.MAX)
            return false;

        if (IsPotencyStep)
            return true;

        int limit = MaxApplications ?? (PotencyDelta > 0 ? int.MaxValue : 1);
        return brew.TimesAugmentedBy(Key) < limit;
    }

    /// <summary>A potency step's matcher at the input's potency + 1, as both minimum and maximum, so that level is all it takes.</summary>
    internal ComponentMatcher[] StepComponents(Item input)
    {
        int potency = input.GetGlobalItem<BrewGlobalItem>().Brew is { Effects.Count: > 0 } brew ? brew.Primary.Potency : 1;
        int level = Math.Clamp(potency + 1, 1, SolutePotency.MAX);

        _stepMatchers ??= new ComponentMatcher[]?[SolutePotency.MAX + 1];
        return _stepMatchers[level] ??= [Components[0].AtLevel(level)];
    }

    private static string ItemKey(int type) =>
        type < ItemID.Count ? ItemID.Search.GetName(type) : ItemLoader.GetItem(type)?.FullName ?? type.ToString();

    private static string MatcherKey(ComponentMatcher matcher)
    {
        string key = matcher.Group != null ? "#" + matcher.Group.Name : ItemKey(matcher.ExactType);
        if (matcher.Infusion != Temperament.None) key += ":" + matcher.Infusion;
        return key;
    }
}

/// <summary>
/// The augmentation being authored in an <see cref="AlchemyCore.Result"/> chain: each call adds an edit, and
/// <see cref="Augment"/> or <see cref="From(int, BrewRecipe[])"/> carries the chain on.
/// </summary>
public readonly struct AugmentStep
{
    private readonly BrewResult _result;
    private readonly Augmentation _augmentation;

    internal AugmentStep(BrewResult result, Augmentation augmentation)
    {
        _result = result;
        _augmentation = augmentation;
    }

    /// <inheritdoc cref="BrewBuilder.Amplify(int)"/>
    public AugmentStep Amplify(int strengthPercent) => Edit(b => b.Amplify(strengthPercent));

    /// <inheritdoc cref="BrewBuilder.Amplify(int, int)"/>
    public AugmentStep Amplify(int baseBuff, int strengthPercent) => Edit(b => b.Amplify(baseBuff, strengthPercent));

    /// <inheritdoc cref="BrewBuilder.Potency"/>
    public AugmentStep Potency(int levels)
    {
        _augmentation.PotencyDelta += levels;
        return Edit(b => b.Potency(levels));
    }

    /// <inheritdoc cref="BrewBuilder.Buff"/>
    public AugmentStep Buff(int baseBuff, int potency, int durationTicks = 0) => Edit(b => b.Buff(baseBuff, potency, durationTicks));

    /// <inheritdoc cref="BrewBuilder.Stat"/>
    public AugmentStep Stat(BrewStat stat, float value, int durationTicks) => Edit(b => b.Stat(stat, value, durationTicks));

    /// <inheritdoc cref="BrewBuilder.Effect{T}"/>
    public AugmentStep Effect<T>(int durationTicks, float value = 1f) where T : BrewEffect =>
        Edit(b => b.Effect<T>(durationTicks, value));

    /// <inheritdoc cref="BrewBuilder.Heal"/>
    public AugmentStep Heal(int life) => Edit(b => b.Heal(life));

    /// <inheritdoc cref="BrewBuilder.Mana"/>
    public AugmentStep Mana(int mana) => Edit(b => b.Mana(mana));

    /// <inheritdoc cref="BrewBuilder.Detour(Func{EffectParams, EffectParams})"/>
    public AugmentStep Detour(Func<EffectParams, EffectParams> edit) => Edit(b => b.Detour(edit));

    /// <inheritdoc cref="BrewBuilder.Detour(int, Func{EffectParams, EffectParams})"/>
    public AugmentStep Detour(int baseBuff, Func<EffectParams, EffectParams> edit) => Edit(b => b.Detour(baseBuff, edit));

    /// <summary>Anything the other steps don't cover, run on the brew in order with them.</summary>
    public AugmentStep Custom(Action<BrewBuilder> edit) => Edit(edit);

    /// <summary>Lets one potion take this augmentation up to <paramref name="times"/> times.</summary>
    public AugmentStep Repeat(int times)
    {
        _augmentation.MaxApplications = Math.Max(1, times);
        return this;
    }

    /// <inheritdoc cref="BrewResult.Augment"/>
    public AugmentStep Augment(params ComponentMatcher[] components) => _result.Augment(components);

    /// <inheritdoc cref="BrewResult.From(int, BrewRecipe[])"/>
    public BrewResult From(int input, params BrewRecipe[] variants) => _result.From(input, variants);

    /// <inheritdoc cref="BrewResult.From(int[], BrewRecipe[])"/>
    public BrewResult From(int[] inputs, params BrewRecipe[] variants) => _result.From(inputs, variants);

    public static implicit operator BrewResult(AugmentStep step) => step._result;

    private AugmentStep Edit(Action<BrewBuilder> edit)
    {
        _augmentation.Edits.Add(edit);
        return this;
    }
}
