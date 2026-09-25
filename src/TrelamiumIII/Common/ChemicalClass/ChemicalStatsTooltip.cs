using System.Globalization;
using ReLogic.Content;
using Terraria.Localization;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     Controller weapon stat rows, pinned to the tooltip's top-right: affliction damage per second, reaction
///     burst and odds (reactive elements only), and duration. Values are the source's, without player bonuses.
/// </summary>
public static class ChemicalStatsTooltip
{
    /// <summary>Vanilla's virtual-keyboard shift key: an up-arrow, for the burst.</summary>
    private const string REACTION_ICON = "Images/UI/VK_Shift";

    private const string DURATION_ICON = Assets.Textures.DurationTooltipIcon.KEY;

    private static readonly Color OddsColor = new(170, 170, 170);

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

        string perSecond = Number(source.AfflictionRate > 0f ? source.ChemicalDamage * 60f / source.AfflictionRate : 0f);
        string seconds = Number(source.AfflictionDuration / 60f);
        string burst = Number(source.ReactionDamage);
        string odds = Number(Math.Clamp(source.ReactionChance, 0f, 1f) * 1000f);

        string fallback = reacts
            ? Text("FallbackReactive", element, perSecond, burst, odds, seconds)
            : Text("FallbackPlain", element, perSecond, seconds);

        var block = new TooltipRowsBlock(
            TooltipAnchor.End, fallback, TooltipAlignment.Right, TooltipPlacement.TopRight);

        block.Row().Icon(icon).Text(Text("DamagePerSecond", perSecond), color);

        if (reacts)
        {
            TooltipRow row = block.Row().Icon(icon);

            if (source.ReactionDamage > 0f)
                row.Text(burst, color).Icon(ReactionIcon);

            row.Text(Text("ReactionChance", odds), OddsColor);
        }

        block.Row().Icon(icon).Icon(DurationIcon).Text(Text("Duration", seconds), color);

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
