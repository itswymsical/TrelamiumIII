using ReLogic.Content;
using Terraria.Localization;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     How an element appears in text and UI: medallion, label tint and localised name. Separate from
///     <see cref="ElementProfile" />, whose colours are for gas and residue rather than tooltip text.
/// </summary>
public static class ElementDisplay
{
    private const string ICON_DIRECTORY = $"{nameof(TrelamiumIII)}/Assets/Textures/UI/Elements/";

    /// <summary>Label tint, sampled from the element's medallion.</summary>
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
    ///     The element's medallion, or null if it has no sprite. Loaded by enum member name from
    ///     <c>Assets/Textures/UI/Elements</c>.
    /// </summary>
    public static Asset<Texture2D>? Icon(ChemicalDamageType type)
    {
        if (type == ChemicalDamageType.None)
            return null;

        string path = ICON_DIRECTORY + type;

        return ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;
    }

    /// <summary>Localised from <c>ChemicalDamageTypes.{member}</c>; falls back to the member name.</summary>
    public static string DisplayName(ChemicalDamageType type)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"ChemicalDamageTypes.{type}");
        string value = Language.GetTextValue(key);

        return value == key ? type.ToString() : value;
    }
}
