using System.Collections.Generic;
using Terraria.Localization;
using TrelamiumIII.Core.Rarities.Text;

namespace TrelamiumIII.Core.Rarities;

/// <summary>
///     Everything a rarity looks like, in one object: its colour, its flavour line, and the list of
///     <see cref="RarityFlair" />s that decorate whatever surface the item is being shown on.
/// </summary>
/// <remarks>
///     <para>
///         A style is deliberately not a <see cref="ModRarity" />. The rarity is an identity -- a
///         number on <see cref="Item.rare" /> that loot tables, prefixes and other mods read -- while
///         the style is a presentation, and the two want different lifetimes. Keeping them apart is
///         what lets a single item be "designated" and wear a bespoke style without inventing a
///         rarity for it (see <see cref="RarityStyles.RegisterItem" />), and what lets several
///         rarities share one look.
///     </para>
///     <para>
///         A style is a singleton, built once in <see cref="FlavorRarity.BuildStyle" /> and shared by
///         every item that resolves to it. Its flairs are therefore singletons too, which is the
///         right shape for the stateful ones: one gas field per rarity rather than one per item,
///         because only one tooltip is ever on screen at a time.
///     </para>
/// </remarks>
public sealed class RarityStyle
{
    /// <summary>The rarity's colour. The tooltip name is drawn in this unless a flair overrides it.</summary>
    public Color Color { get; init; } = Color.White;

    /// <summary>
    ///     The colour a flair reaches for when it wants to read as "lit" rather than "printed".
    /// </summary>
    /// <remarks>
    ///     Defaults to <see cref="Color" />, so a style that does not care never has to say anything.
    ///     Flairs should read <see cref="GlowColor" />, never this.
    /// </remarks>
    public Color? Glow { get; init; }

    public Color GlowColor => Glow ?? Color;

    /// <summary>
    ///     An italic-in-spirit line appended to the tooltip, for rarities that are a piece of
    ///     characterisation rather than a power tier. Null adds no line.
    /// </summary>
    public LocalizedText? Flavor { get; init; }

    /// <summary>Colour of the flavour line. Defaults to a dimmed <see cref="Color" />.</summary>
    public Color? FlavorColor { get; init; } = new Color(240, 75, 75);

    /// <summary>Outline and blends for the name when no flair paints it. Painter flairs without their own fall back to it.</summary>
    public TextStyle Text { get; init; } = TextStyle.Vanilla;

    /// <summary>Outline and blends for the flavour line. Null leaves the line to vanilla.</summary>
    public TextStyle? FlavorText { get; init; }

    /// <summary>
    ///     The decorations, drawn in order. Earlier entries sit further back on every surface.
    /// </summary>
    public IReadOnlyList<RarityFlair> Flairs { get; init; } = [];

    /// <summary>
    ///     The first flair that wants to paint the name text itself, or null to let the renderer draw
    ///     it the way vanilla would.
    /// </summary>
    /// <remarks>
    ///     Only one flair can own the glyphs -- two of them drawing the same string on top of each
    ///     other is a smear, not a layer -- so this is a first-wins search rather than a loop. A
    ///     second name painter is not an error; it is simply outranked, and can still contribute
    ///     through <see cref="RarityFlair.UnderName" /> and <see cref="RarityFlair.OverName" />.
    /// </remarks>
    internal RarityFlair? NamePainter
    {
        get
        {
            foreach (var flair in Flairs)
            {
                if (flair.PaintsName)
                    return flair;
            }

            return null;
        }
    }

    /// <summary>The first flair asking for a shader on the item sprite, or null.</summary>
    /// <remarks>
    ///     One effect can be bound to a <see cref="SpriteBatch" /> at a time, so the same first-wins
    ///     rule applies here and for the same reason.
    /// </remarks>
    internal RarityFlair? SpriteShaderSource
    {
        get
        {
            foreach (var flair in Flairs)
            {
                if (flair.SpriteShader is not null)
                    return flair;
            }

            return null;
        }
    }
}
