using ReLogic.Graphics;
using Terraria.GameContent;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII.Core.Books.Text;

/// <summary>
///     The real faces behind the three <see cref="BookFont" /> roles, held as delegates so reloads and resource packs never leave stale fonts.
/// </summary>
public sealed class BookTypeface(
    Func<DynamicSpriteFont> body,
    Func<DynamicSpriteFont> display,
    Func<DynamicSpriteFont> hand)
{
    public DynamicSpriteFont this[BookFont role] => role switch
    {
        BookFont.Display => display(),
        BookFont.Hand    => hand(),
        _                => body(),
    };

    /// <summary>Emphasis faces; null means the set can't express it, and the caller keeps the upright rather than synthesising.</summary>
    public Func<DynamicSpriteFont>? Bold { get; init; }

    public Func<DynamicSpriteFont>? Italic { get; init; }

    public Func<DynamicSpriteFont>? BoldItalic { get; init; }

    /// <summary>Resolves a role with emphasis applied, falling back to the plain face.</summary>
    public DynamicSpriteFont Variant(BookFont role, bool bold, bool italic)
    {
        Func<DynamicSpriteFont>? emphasis = bold && italic ? BoldItalic ?? Bold ?? Italic
            : bold ? Bold
            : italic ? Italic
            : null;

        return emphasis?.Invoke() ?? this[role];
    }

    /// <summary>Whether this set can express the emphasis at all, for greying the toolbar toggle.</summary>
    public bool Supports(bool bold, bool italic) =>
        (!bold || Bold is not null) && (!italic || Italic is not null);

    /// <summary>Terraria's own fonts; the default, so a book without a typeface looks as it always did.</summary>
    public static BookTypeface Vanilla { get; } = new(
        () => FontAssets.MouseText.Value,
        () => FontAssets.DeathText.Value,
        () => FontAssets.DeathText.Value);

    /// <summary>Andika across all roles: wide glyph coverage, real weights, readable in quantity.</summary>
    public static BookTypeface Encyclopedia { get; } = new(
        () => TrelamiumFonts.AndikaRegular,
        () => TrelamiumFonts.AndikaBold,
        () => TrelamiumFonts.AndikaItalic)
    {
        Bold = () => TrelamiumFonts.AndikaBold,
        Italic = () => TrelamiumFonts.AndikaItalic,
        BoldItalic = () => TrelamiumFonts.AndikaBoldItalic,
    };

    public static BookTypeface Rajdhani { get; } = new(
    () => TrelamiumFonts.RajdhaniMedium,
    () => TrelamiumFonts.RajdhaniBold,
    () => TrelamiumFonts.RajdhaniMedium);

    public static BookTypeface AaarghFont { get; } = new(
    () => TrelamiumFonts.AaarghFont,
    () => TrelamiumFonts.AaarghFont,
    () => TrelamiumFonts.AaarghFont);

    /// <summary>
    ///     Paprika throughout, roles told apart by scale and colour. Only 353 glyphs, so fine for authored lore but not arbitrary strings.
    /// </summary>
    public static BookTypeface Glossary { get; } = new(
        () => TrelamiumFonts.PaprikaFont,
        () => TrelamiumFonts.PaprikaFont,
        () => TrelamiumFonts.PaprikaFont);
}

/// <summary>
///     A role plus its typeface. A bare <see cref="BookFont" /> converts implicitly to vanilla, so inside blocks pass <see cref="Layout.BlockStyle.Face" />.
/// </summary>
public readonly record struct BookFace(BookFont Role, BookTypeface? Typeface = null)
{
    public static implicit operator BookFace(BookFont role) => new(role);

    public DynamicSpriteFont Font => (Typeface ?? BookTypeface.Vanilla)[Role];
}
