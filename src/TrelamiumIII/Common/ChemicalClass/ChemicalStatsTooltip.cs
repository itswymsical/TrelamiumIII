using System.Collections.Generic;
using System.Globalization;
using ReLogic.Content;
using Terraria.Localization;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     Controller weapon stat rows, pinned to the tooltip's top-right: affliction damage per second, reaction
///     burst and odds (reactive elements only), duration, concentration and catalysis. Values match a release:
///     modifiers and the viewing player's bonuses included. Rows for a stat the item does not have are left out.
/// </summary>
public static class ChemicalStatsTooltip
{
    /// <summary>Vanilla's virtual-keyboard shift key: an up-arrow, for the burst.</summary>
    private const string REACTION_ICON = "Images/UI/VK_Shift";

    private const string DURATION_ICON = Assets.Textures.DurationTooltipIcon.KEY;

    private static readonly Color OddsColor = new(170, 170, 170);

    /// <summary>The almanac's Catalytic badge colour.</summary>
    private static readonly Color CatalysisColor = new(255, 215, 110);

    /// <summary>Vanilla's colours for a prefix line that helps and one that hurts.</summary>
    private static readonly Color GoodColor = new(120, 190, 120);

    private static readonly Color BadColor = new(190, 120, 120);

    private static readonly Color DescriptionColor = new(185, 185, 185);

    /// <summary>The rows for <paramref name="source" /> with <paramref name="modifiers" /> and <paramref name="player" />'s bonuses applied, or null for no chemistry.</summary>
    public static TooltipRowsBlock? Build(IChemicalSource source, IReadOnlyList<ChemicalModifier>? modifiers = null, Player? player = null)
    {
        ChemicalDamageType type = source.ChemicalType;

        if (type == ChemicalDamageType.None)
            return null;

        ChemicalStats stats = ChemicalStats.Of(source);

        if (modifiers is not null)
        {
            foreach (ChemicalModifier modifier in modifiers)
                modifier.ModifyStats(ref stats);
        }

        if (player is not null)
            stats.Apply(player.GetModPlayer<ChemicalPlayerStats>(), type);

        Color color = ElementDisplay.LabelColor(type);
        Asset<Texture2D>? icon = ElementDisplay.Icon(type);
        string element = ElementDisplay.DisplayName(type);
        bool reacts = ChemicalMatchup.IsElemental(type);
        bool afflicts = stats.ChemicalDamage > 0;
        float reactionDamage = stats.ReactionDamage * stats.ReactionDamageMultiplier;

        string perSecond = Number(stats.AfflictionRate > 0f ? stats.ChemicalDamage * stats.DamageMultiplier * 60f / stats.AfflictionRate : 0f);
        string seconds = Number(stats.AfflictionDuration * stats.DurationMultiplier / 60f);
        string burst = Number(reactionDamage);
        string odds = Number(Math.Clamp(stats.ReactionChance, 0f, 100f));
        string concentration = Text("Concentration", Number(stats.Concentration));
        string catalysis = Text("Catalysis", Number(stats.Catalysis * 100f));

        var fallback = new List<string>();

        if (afflicts)
        {
            fallback.Add(reacts
                ? Text("FallbackReactive", element, perSecond, burst, odds, seconds)
                : Text("FallbackPlain", element, perSecond, seconds));
        }

        if (stats.Concentration > 0)
            fallback.Add(concentration);

        if (stats.Catalysis > 0f)
            fallback.Add(catalysis);

        var block = new TooltipRowsBlock(
            TooltipAnchor.End, string.Join(", ", fallback), TooltipAlignment.Right, TooltipPlacement.TopRight);

        if (afflicts)
            block.Row().Icon(icon).Text(Text("DamagePerSecond", perSecond), color);

        if (reacts)
        {
            TooltipRow row = block.Row().Icon(icon);

            if (reactionDamage > 0f)
                row.Text(burst, color).Icon(ReactionIcon);

            row.Text(Text("ReactionChance", odds), OddsColor);
        }

        if (afflicts)
            block.Row().Icon(icon).Icon(DurationIcon).Text(Text("Duration", seconds), color);

        if (stats.Concentration > 0)
            block.Row().Icon(icon).Text(concentration, color);

        if (stats.Catalysis > 0f)
            block.Row().Icon(ChemicalGlyphs.Icon(Temperament.Catalytic)).Text(catalysis, CatalysisColor);

        return block;
    }

    /// <summary>
    ///     A modifier's name, marked with its temperament's glyph and colour, then its description and one line per stat
    ///     it changes for <paramref name="source" />, green where it helps and red where it hurts, like a vanilla prefix.
    /// </summary>
    public static TooltipRowsBlock BuildModifier(ChemicalModifier modifier, IChemicalSource? source = null)
    {
        ChemicalTypeInfo.TryGet(modifier.Temperament, out _, out Color color);

        List<ChemicalStatChange> changes = ChemicalStatChange.Measure(modifier, source);
        string? description = modifier.Description;

        var fallback = new List<string>();

        if (description is not null)
            fallback.Add(description);

        foreach (ChemicalStatChange change in changes)
            fallback.Add(change.Text);

        var block = new TooltipRowsBlock(TooltipAnchor.End, $"{modifier.DisplayName}: {string.Join(", ", fallback)}");

        block.Row().Icon(ChemicalGlyphs.Icon(modifier.Temperament)).Text(modifier.DisplayName, color);

        if (description is not null)
            block.Row().Text(description, DescriptionColor);

        foreach (ChemicalStatChange change in changes)
            block.Row().Text(change.Text, change.Helps ? GoodColor : BadColor);

        return block;
    }

    private static Asset<Texture2D> ReactionIcon
        => Main.Assets.Request<Texture2D>(REACTION_ICON, AssetRequestMode.ImmediateLoad);

    private static Asset<Texture2D>? DurationIcon
        => ModContent.HasAsset(DURATION_ICON) ? ModContent.Request<Texture2D>(DURATION_ICON) : null;

    private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Text(string key, params object[] args)
        => Language.GetTextValue(TrelamiumIII.Instance.GetLocalizationKey("ChemicalStats." + key), args);
}
