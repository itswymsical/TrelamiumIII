using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader.Default;
using Terraria.Utilities;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     A prefix that gives every release from its weapon a <see cref="ChemicalModifier" />. Chemical weapons roll these
///     and nothing else (<see cref="ChemicalPrefixRolls" />). One shared class; the instances are registered from a table,
///     and having no parameterless constructor keeps tModLoader from autoloading it.
/// </summary>
public sealed class ChemicalPrefix : ModPrefix
{
    private static readonly List<ChemicalPrefix> all = [];

    private readonly float rollChance;

    private readonly float value;

    /// <param name="rollChance">Weight against the other chemical prefixes; 1 is ordinary.</param>
    /// <param name="value">Sell value multiplier, which also moves rarity the way vanilla prefixes do.</param>
    public ChemicalPrefix(ChemicalModifier modifier, float rollChance = 1f, float value = 1f)
    {
        Modifier = modifier;
        this.rollChance = rollChance;
        this.value = value;
    }

    public static IReadOnlyList<ChemicalPrefix> All => all;

    public ChemicalModifier Modifier { get; }

    public override string Name => Modifier.Key;

    /// <summary>The modifier's own name, so it reads the same as a prefix and as a flask's innate temperament.</summary>
    public override LocalizedText DisplayName => Modifier.DisplayNameText;

    /// <summary>Weapons of the controller class, which take chemical prefixes in place of vanilla ones.</summary>
    public static bool IsChemicalWeapon(Item item) => item.CountsAsClass<ChemicalDamageClass>();

    public static ChemicalPrefix? Of(Item item) => PrefixLoader.GetPrefix(item.prefix) as ChemicalPrefix;

    public override void Load() => all.Add(this);

    public override void Unload() => all.Clear();

    public override float RollChance(Item item) => rollChance;

    /// <summary>Only onto a chemical weapon it would change something for, like vanilla's rule for prefixes that round away.</summary>
    public override bool CanRoll(Item item)
        => rollChance > 0f
           && IsChemicalWeapon(item)
           && ChemicalStatChange.Measure(Modifier, ChemicalSourceComponent.Of(item)).Count > 0;

    public override void ModifyValue(ref float valueMult) => valueMult *= value;

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
