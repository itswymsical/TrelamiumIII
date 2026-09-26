using System.Collections.Generic;
using System.Globalization;
using ReLogic.Content;
using Terraria.Localization;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     Controller weapon stat rows, pinned to the tooltip's top-right: affliction damage per second, reaction
///     burst and odds (reactive elements only), duration, concentration and catalysis. Values are the source's,
///     without player bonuses. Rows for a stat the source does not have are left out.
/// </summary>
public static class ChemicalStatsTooltip
{
    /// <summary>Vanilla's virtual-keyboard shift key: an up-arrow, for the burst.</summary>
    private const string REACTION_ICON = "Images/UI/VK_Shift";

    private const string DURATION_ICON = Assets.Textures.DurationTooltipIcon.KEY;

    private static readonly Color OddsColor = new(170, 170, 170);

    /// <summary>The almanac's Catalytic badge colour.</summary>
    private static readonly Color CatalysisColor = new(255, 215, 110);

    /// <summary>The rows for <paramref name="source" />, or null for a source that emits no chemistry.</summary>
    public static TooltipRowsBlock? Build(IChemicalSource source)
    {
        ChemicalDamageType type = source.ChemicalType;

        if (type == ChemicalDamageType.None)
            return null;

        Color color = ElementDisplay.LabelColor(type);
        Asset<Texture2D>? icon = ElementDisplay.Icon(type);
        string element = ElementDisplay.DisplayName(type);
        bool reacts = ChemicalMatchup.IsElemental(type);
        bool afflicts = source.ChemicalDamage > 0;

        string perSecond = Number(source.AfflictionRate > 0f ? source.ChemicalDamage * 60f / source.AfflictionRate : 0f);
        string seconds = Number(source.AfflictionDuration / 60f);
        string burst = Number(source.ReactionDamage);
        string odds = Number(Math.Clamp(source.ReactionChance, 0, 100));
        string concentration = Text("Concentration", Number(source.Concentration));
        string catalysis = Text("Catalysis", Number(source.Catalysis * 100f));

        var fallback = new List<string>();

        if (afflicts)
        {
            fallback.Add(reacts
                ? Text("FallbackReactive", element, perSecond, burst, odds, seconds)
                : Text("FallbackPlain", element, perSecond, seconds));
        }

        if (source.Concentration > 0)
            fallback.Add(concentration);

        if (source.Catalysis > 0f)
            fallback.Add(catalysis);

        var block = new TooltipRowsBlock(
            TooltipAnchor.End, string.Join(", ", fallback), TooltipAlignment.Right, TooltipPlacement.TopRight);

        if (afflicts)
            block.Row().Icon(icon).Text(Text("DamagePerSecond", perSecond), color);

        if (reacts)
        {
            TooltipRow row = block.Row().Icon(icon);

            if (source.ReactionDamage > 0)
                row.Text(burst, color).Icon(ReactionIcon);

            row.Text(Text("ReactionChance", odds), OddsColor);
        }

        if (afflicts)
            block.Row().Icon(icon).Icon(DurationIcon).Text(Text("Duration", seconds), color);

        if (source.Concentration > 0)
            block.Row().Icon(icon).Text(concentration, color);

        if (source.Catalysis > 0f)
            block.Row().Icon(ChemicalQualityGlyphs.Icon(ChemicalQuality.Catalytic)).Text(catalysis, CatalysisColor);

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
