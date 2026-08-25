using ReLogic.Graphics;

namespace TrelamiumIII.Core.Text;

/// <summary>
///     The mod's own typefaces, sitting alongside <see cref="Terraria.GameContent.FontAssets" />
///     rather than replacing it.
/// </summary>
/// <remarks>
///     These are null on a dedicated server, which has no graphics device to upload an atlas to.
///     That matches how vanilla's own font assets behave, and drawing code never runs there.
/// </remarks>
public static class TrelamiumFonts
{
    private const string DIRECTORY = "Common/Fonts";

    /// <summary>Andika Regular. Body copy.</summary>
    public static DynamicSpriteFont Regular { get; private set; } = null!;

    /// <summary>Andika Italic. Emphasis, and the voice of quoted lore.</summary>
    public static DynamicSpriteFont Italic { get; private set; } = null!;

    /// <summary>Andika Bold. Headings, and the weight that pairs with vanilla's Andy Bold.</summary>
    public static DynamicSpriteFont Bold { get; private set; } = null!;

    /// <summary>Andika Bold Italic.</summary>
    public static DynamicSpriteFont BoldItalic { get; private set; } = null!;

    /// <summary>
    ///     Paprika. A light, slightly oblique display face for titles and chapter heads.
    /// </summary>
    /// <remarks>
    ///     Display coverage only -- 353 glyphs against Andika's 606, so it has Latin and the
    ///     typographic punctuation but little beyond. Fine for headings, wrong for arbitrary
    ///     player-facing strings, which can carry characters it would fall back to '?' on.
    /// </remarks>
    public static DynamicSpriteFont Display { get; private set; } = null!;

    internal static void Load(Mod mod)
    {
        if (Main.dedServ) return;

        Regular = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Regular");
        Italic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Italic");
        Bold = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Bold");
        BoldItalic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-BoldItalic");
        Display = BakedFont.Load(mod, $"{DIRECTORY}/Paprika-Regular");
    }

    internal static void Unload()
    {
        Regular = null!;
        Italic = null!;
        Bold = null!;
        BoldItalic = null!;
        Display = null!;
    }
}
