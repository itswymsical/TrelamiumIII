using System.Collections.Generic;
using System.Globalization;
using Terraria.Localization;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>A stat a <see cref="ChemicalAccessoryPrefix" /> gives its wearer. Each name is its line's key under <c>ChemicalAccessoryStats</c>.</summary>
public enum ChemicalAccessoryStat
{
    /// <summary>Affliction damage of the prefix's element.</summary>
    ElementDamage,

    ReactionDamage,

    /// <summary>Damage of the controller class, the number on its weapons' damage line.</summary>
    ControlledDamage,
}

/// <summary>
///     An accessory prefix giving its wearer one chemical stat, rolled among vanilla's accessory prefixes. It shows the
///     name of the weapon prefix it borrows, and its internal name adds "_2" to that prefix's, since names must be unique.
///     Registered from a table; having no parameterless constructor keeps tModLoader from autoloading it.
/// </summary>
public sealed class ChemicalAccessoryPrefix : ModPrefix
{
    /// <summary>Roll weight of a Good prefix against a vanilla accessory prefix's 1.</summary>
    private const float ROLL_WEIGHT = 0.5f;

    private readonly ChemicalModifier nameSource;

    public ChemicalAccessoryPrefix(ChemicalModifier nameSource, ChemicalAccessoryStat stat, ChemicalDamageType element, float amount, ChemicalPrefixTier tier)
    {
        this.nameSource = nameSource;
        Stat = stat;
        Element = element;
        Amount = amount;
        Tier = tier;
    }

    public ChemicalAccessoryStat Stat { get; }

    /// <summary>The element whose damage <see cref="ChemicalAccessoryStat.ElementDamage" /> raises; None for the other stats.</summary>
    public ChemicalDamageType Element { get; }

    /// <summary>The bonus as a fraction (0.04 = +4%).</summary>
    public float Amount { get; }

    public ChemicalPrefixTier Tier { get; }

    public override string Name => nameSource.Key + "_2";

    public override LocalizedText DisplayName => nameSource.DisplayNameText;

    public override PrefixCategory Category => PrefixCategory.Accessory;

    public override float RollChance(Item item) => ROLL_WEIGHT * Tier.RollWeight();

    public override void ModifyValue(ref float valueMult) => valueMult *= Tier.ValueMultiplier();

    public override void ApplyAccessoryEffects(Player player)
    {
        switch (Stat)
        {
            case ChemicalAccessoryStat.ElementDamage:
                player.GetModPlayer<ChemicalPlayerStats>()[Element].Damage += Amount;
                break;

            case ChemicalAccessoryStat.ReactionDamage:
                player.GetModPlayer<ChemicalPlayerStats>().Global.ReactionDamage += Amount;
                break;

            case ChemicalAccessoryStat.ControlledDamage:
                player.GetDamage<ChemicalDamageClass>() += Amount;
                break;
        }
    }

    /// <summary>A vanilla-style prefix line. An element's name is drawn with its medallion, which weapons show without it.</summary>
    public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
    {
        string percent = "+" + (Amount * 100f).ToString("0.#", CultureInfo.InvariantCulture);
        string key = Mod.GetLocalizationKey("ChemicalAccessoryStats." + Stat);

        string text = Stat == ChemicalAccessoryStat.ElementDamage
            ? Language.GetTextValue(key, percent, ChemicalTagHandler.Tag(Element))
            : Language.GetTextValue(key, percent);

        yield return new TooltipLine(Mod, "PrefixAcc" + Stat, text) { IsModifier = true };
    }
}
