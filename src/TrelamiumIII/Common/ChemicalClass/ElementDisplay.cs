using ReLogic.Content;
using Terraria.Localization;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     How an element presents itself in text and UI: its medallion, the tint its name is written
///     in, and that name in the player's language.
/// </summary>
/// <remarks>
///     <see cref="ElementProfile" /> is what an element does; this is what it looks like on a
///     tooltip. Kept apart because the colours there are for gas and residue, and a label tint that
///     reads against tooltip grey is a different colour from a pool of the stuff. Every surface that
///     names an element -- the <c>[chem]</c> tag, the controller stat rows -- reads from here, so a
///     new element gets a colour and a sprite once.
/// </remarks>
public static class ElementDisplay
{
    private const string ICON_DIRECTORY = $"{nameof(TrelamiumIII)}/Assets/Textures/UI/Elements/";

    /// <summary>
    ///     Tints sampled from the medallions themselves, so the word and the icon beside it read
    ///     as one element rather than two.
    /// </summary>
    public static Color LabelColor(ChemicalDamageType type)
    {
        return type switch
        {
            ChemicalDamageType.Solute => new Color(126, 186, 240),
            ChemicalDamageType.Reagent => new Color(222, 184, 120),
            ChemicalDamageType.Thermal => new Color(255, 135, 31),
            ChemicalDamageType.Cryonic => new Color(157, 202, 238),
            ChemicalDamageType.Caustic => new Color(93, 173, 63),
            ChemicalDamageType.Necrotic => new Color(200, 84, 255),
            ChemicalDamageType.Kinetic => new Color(245, 229, 88),
            ChemicalDamageType.Electric => new Color(255, 106, 106),
            _ => Color.White,
        };
    }

    /// <summary>
    ///     The element's medallion, or null for one with no sprite yet. Sprites are named after
    ///     their enum member under <c>Assets/Textures/UI/Elements</c>, so a new element needs
    ///     nothing here beyond a file of the same name.
    /// </summary>
    public static Asset<Texture2D>? Icon(ChemicalDamageType type)
    {
        if (type == ChemicalDamageType.None)
            return null;

        string path = ICON_DIRECTORY + type;

        return ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;
    }

    /// <summary>
    ///     Localised under <c>ChemicalDamageTypes.{member}</c>, so a translation can name an element
    ///     in its own words without every tooltip that mentions it having to change. The member
    ///     name when no translation exists.
    /// </summary>
    public static string DisplayName(ChemicalDamageType type)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"ChemicalDamageTypes.{type}");
        string value = Language.GetTextValue(key);

        return value == key ? type.ToString() : value;
    }
}
