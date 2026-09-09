using System.Globalization;
using ReLogic.Content;
using Terraria.Localization;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     The stat rows a controller weapon shows at the foot of its tooltip.
/// </summary>
/// <remarks>
///     <para>
///         One row per number, each led by the element's medallion so the list reads as "this
///         element, this much": affliction damage per second; the reaction burst, with the shift
///         key's up-arrow after it (a spike, not a rate) and its odds beside; then how long the
///         affliction runs.
///     </para>
///     <para>
///         The rows float in the tooltip's top-right corner rather than taking lines of their own,
///         so they read as a panel pinned beside the weapon's name and cost the tooltip no height.
///         Wedged under the damage line they split the vanilla weapon stats in half, and left at the
///         foot of the tooltip they trailed off into space away from everything they describe.
///     </para>
///     <para>
///         The per-second figure is the real rate -- tick damage times ticks per second -- where the
///         old line printed the tick damage and called it a second. Reaction odds are the
///         percentage that is actually rolled. The numbers are the source's own; the thrower's
///         bonuses are not folded in, the same as before.
///     </para>
///     <para>
///         Reaction rows only for something that can react. A solute weapon reporting a zero
///         reaction chance is stating an absence the player cannot act on, and reads as the weapon
///         being broken rather than as it being a different kind of weapon.
///     </para>
/// </remarks>
public static class ChemicalStatsTooltip
{
    /// <summary>Vanilla's virtual-keyboard shift key: an up-arrow, for the burst.</summary>
    private const string REACTION_ICON = "Images/UI/VK_Shift";

    /// <summary>The clock that leads the duration row.</summary>
    private const string DURATION_ICON = $"{nameof(TrelamiumIII)}/Assets/Textures/DurationTooltipIcon";

    /// <summary>Dimmed, for the odds beside the burst: a qualifier, not a stat of its own.</summary>
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

            // The arrow marks the burst as a spike rather than a rate, so it only belongs where
            // there is a burst figure for it to point at.
            if (source.ReactionDamage > 0f)
                row.Text(burst, color).Icon(ReactionIcon);

            row.Text(Text("ReactionChance", odds), OddsColor);
        }

        // Medallion then clock: every row opens with the element, and the clock says which of its
        // numbers this one is, so the duration is not mistaken for another rate.
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
