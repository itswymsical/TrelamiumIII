namespace TrelamiumIII.Core.Alchemy;

/// <summary>Each principle's colour, as the almanac badges it.</summary>
public static class PrincipleColors
{
    /// <summary>The colour for <see cref="PrincipleType.None" />, combined principles and anything unlisted.</summary>
    public static readonly Color Neutral = new(165, 165, 165);

    public static Color Of(PrincipleType principle) => principle switch
    {
        PrincipleType.Vital => new Color(128, 255, 0),
        PrincipleType.Specular => new Color(227, 217, 255),
        PrincipleType.Martial => new Color(230, 95, 80),
        PrincipleType.Brisk => new Color(128, 255, 255),
        PrincipleType.Fortune => new Color(255, 217, 0),
        PrincipleType.Harmony => new Color(255, 55, 155),
        PrincipleType.Bane => new Color(128, 0, 255),
        _ => Neutral,
    };
}
