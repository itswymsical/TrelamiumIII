using ReLogic.Graphics;
using Terraria.GameContent;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII.Core.Books.Text;

/// <summary>
///     The real faces a book's three <see cref="BookFont" /> roles resolve to.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="BookFont" /> says what a run of text <em>is</em> -- body, display, hand. A
///         typeface says what those look like. Keeping them apart is what lets one book read as a
///         printed encyclopedia and another as a soft glossary without either one's blocks knowing
///         anything about it: the roles, scales and leading are authored once and only the set
///         underneath changes.
///     </para>
///     <para>
///         Faces are held as delegates rather than resolved fonts. A <see cref="DynamicSpriteFont" />
///         captured once would survive a mod reload as a stale reference, and vanilla's own fonts can
///         be swapped underneath by a resource pack; resolving per use costs nothing measurable and is
///         always current.
///     </para>
/// </remarks>
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

    /// <summary>
    ///     Terraria's own fonts. The default, so a book that says nothing about type reads exactly as
    ///     it did before typefaces existed.
    /// </summary>
    public static BookTypeface Vanilla { get; } = new(
        () => FontAssets.MouseText.Value,
        () => FontAssets.DeathText.Value,
        () => FontAssets.DeathText.Value);

    /// <summary>
    ///     Andika, across all three roles. A reference book: wide glyph coverage, real weights, and a
    ///     body face meant to be read in quantity.
    /// </summary>
    public static BookTypeface Encyclopedia { get; } = new(
        () => TrelamiumFonts.Regular,
        () => TrelamiumFonts.Bold,
        () => TrelamiumFonts.Italic);

    /// <summary>
    ///     Paprika throughout. The softer, warmer setting -- the roles are told apart by scale and
    ///     colour rather than by face.
    /// </summary>
    /// <remarks>
    ///     Paprika carries 353 glyphs against Andika's 606, so it is safe for authored lore but not
    ///     for arbitrary player-facing strings, which fall back to '?' on anything it does not map.
    ///     Point <c>hand</c> at <see cref="TrelamiumFonts.Italic" /> if marginalia needs to read as a
    ///     different pen from the body.
    /// </remarks>
    public static BookTypeface Glossary { get; } = new(
        () => TrelamiumFonts.Display,
        () => TrelamiumFonts.Display,
        () => TrelamiumFonts.Display);
}

/// <summary>
///     A <see cref="BookFont" /> role together with the typeface it resolves against.
/// </summary>
/// <remarks>
///     Converts implicitly from a bare <see cref="BookFont" />, which resolves against
///     <see cref="BookTypeface.Vanilla" />. That keeps a role usable on its own where there is no
///     style to inherit from, but inside a block always pass <see cref="Layout.BlockStyle.Face" />
///     rather than <c>Style.Font</c> -- the bare role silently drops the book's typeface.
/// </remarks>
public readonly record struct BookFace(BookFont Role, BookTypeface? Typeface = null)
{
    public static implicit operator BookFace(BookFont role) => new(role);

    public DynamicSpriteFont Font => (Typeface ?? BookTypeface.Vanilla)[Role];
}
