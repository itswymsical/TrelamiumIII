using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader.Default;
using Terraria.Utilities;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>How good a chemical prefix is, which sets how often it rolls and what it does to value and rarity.</summary>
public enum ChemicalPrefixTier
{
    /// <summary>Only drawbacks. Lowers value and rarity by a step.</summary>
    Poor,

    /// <summary>Raises value and rarity by a step.</summary>
    Good,

    /// <summary>Rolls half as often as the others; raises value and rarity by two steps.</summary>
    Best,
}

public static class ChemicalPrefixTierExtensions
{
    /// <summary>
    ///     For <see cref="ModPrefix.ModifyValue" />. Vanilla squares it for the price. Rarity moves one step at 1.05 and two
    ///     at 1.2, compared as doubles, so 1.05f itself falls just short of the first step.
    /// </summary>
    public static float ValueMultiplier(this ChemicalPrefixTier tier) => tier switch
    {
        ChemicalPrefixTier.Best => 1.2f,
        ChemicalPrefixTier.Good => 1.1f,
        _ => 0.9f,
    };

    /// <summary>Relative roll weight: Best rolls half as often as the other tiers.</summary>
    public static float RollWeight(this ChemicalPrefixTier tier) => tier == ChemicalPrefixTier.Best ? 0.5f : 1f;
}

/// <summary>
///     A prefix that gives every release from its weapon a <see cref="ChemicalModifier" />. Chemical weapons roll these
///     and nothing else (<see cref="ChemicalPrefixRolls" />). One shared class; the instances are registered from a table,
///     and having no parameterless constructor keeps tModLoader from autoloading it.
/// </summary>
public sealed class ChemicalPrefix : ModPrefix
{
    private static readonly List<ChemicalPrefix> all = [];

    private static readonly Dictionary<ChemicalModifier, ChemicalPrefix> byModifier = [];

    public ChemicalPrefix(ChemicalModifier modifier, ChemicalPrefixTier tier)
    {
        Modifier = modifier;
        Tier = tier;
    }

    public static IReadOnlyList<ChemicalPrefix> All => all;

    public ChemicalModifier Modifier { get; }

    public ChemicalPrefixTier Tier { get; }

    /// <summary>The prefix carrying <paramref name="modifier" />, or null when none does.</summary>
    public static ChemicalPrefix? For(ChemicalModifier modifier) => byModifier.GetValueOrDefault(modifier);

    public override string Name => Modifier.Key;

    /// <summary>The modifier's own name, so it reads the same as a prefix and as a flask's innate temperament.</summary>
    public override LocalizedText DisplayName => Modifier.DisplayNameText;

    /// <summary>Weapons of the controller class, which take chemical prefixes in place of vanilla ones.</summary>
    public static bool IsChemicalWeapon(Item item) => item.CountsAsClass<ChemicalDamageClass>();

    public static ChemicalPrefix? Of(Item item) => PrefixLoader.GetPrefix(item.prefix) as ChemicalPrefix;

    public override void Load()
    {
        all.Add(this);
        byModifier[Modifier] = this;
    }

    public override void Unload()
    {
        all.Clear();
        byModifier.Clear();
    }

    public override float RollChance(Item item) => Tier.RollWeight();

    /// <summary>Only onto a chemical weapon it would change something for, like vanilla's rule for prefixes that round away.</summary>
    public override bool CanRoll(Item item) => IsChemicalWeapon(item) && Modifier.AppliesTo(ChemicalSourceComponent.Of(item));

    public override void ModifyValue(ref float valueMult) => valueMult *= Tier.ValueMultiplier();

    /// <summary>Vanilla prefix lines, measured against the weapon's own chemistry, or against none when its ammo supplies it.</summary>
    public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
    {
        if (Modifier.Description is { } description)
            yield return new TooltipLine(Mod, "PrefixChemicalDescription", description) { IsModifier = true };

        foreach (ChemicalStatChange change in ChemicalStatChange.Measure(Modifier, ChemicalSourceComponent.Of(item)))
            yield return new TooltipLine(Mod, "Prefix" + change.Stat, change.Text) { IsModifier = true, IsModifierBad = !change.Helps };
    }
}

/// <summary>
///     Keeps chemical weapons on chemical prefixes. They have no vanilla prefix categories, so tModLoader's own roll finds
///     nothing for them; <see cref="ChoosePrefix" /> picks instead on every path that rolls: crafting and loot (-1),
///     reforging (-2), and the reforge slot's check that the item can be reforged at all (-3).
/// </summary>
public sealed class ChemicalPrefixRolls : GlobalItem
{
    public override int ChoosePrefix(Item item, UnifiedRandom rand)
    {
        if (!ChemicalPrefix.IsChemicalWeapon(item))
            return -1;

        var pool = new WeightedRandom<int>(rand);

        foreach (ChemicalPrefix prefix in ChemicalPrefix.All)
        {
            if (prefix.CanRoll(item))
                pool.Add(prefix.Type, prefix.RollChance(item));
        }

        return pool.elements.Count > 0 ? pool.Get() : -1;
    }

    /// <summary>
    ///     Refuses any other prefix on a chemical weapon: one picked by another mod, or loaded with an old save. Keeps
    ///     tModLoader's placeholder for a prefix that failed to load, so the saved name survives until it loads again.
    /// </summary>
    public override bool AllowPrefix(Item item, int pre)
        => !ChemicalPrefix.IsChemicalWeapon(item) || PrefixLoader.GetPrefix(pre) is ChemicalPrefix or UnloadedPrefix;
}
