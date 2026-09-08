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

    /// <summary>
    ///     Aaargh. A hand-drawn face for speech -- the one that suits a bubble.
    /// </summary>
    /// <remarks>
    ///     Coverage is 122 glyphs against Andika's 606, so this is for authored lines and nothing
    ///     else. Anything that can carry a player's name, a mod's item name, or a translation into a
    ///     script it has never heard of falls back to '?' for most of the string. Line spacing lands
    ///     at 30px, the same as vanilla's tooltip face, so it drops into the dialogue boxes without
    ///     changing their metrics.
    /// </remarks>
    public static DynamicSpriteFont Speech { get; private set; } = null!;

    internal static void Load(Mod mod)
    {
        if (Main.dedServ) return;

        Regular = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Regular");
        Italic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Italic");
        Bold = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Bold");
        BoldItalic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-BoldItalic");
        Display = BakedFont.Load(mod, $"{DIRECTORY}/Paprika-Regular");
        Speech = BakedFont.Load(mod, $"{DIRECTORY}/Aaargh");
    }

    internal static void Unload()
    {
        Regular = null!;
        Italic = null!;
        Bold = null!;
        BoldItalic = null!;
        Display = null!;
        Speech = null!;
    }
}
