using ReLogic.Graphics;
using Terraria.GameContent;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     The faces projected panels are set in.
/// </summary>
/// <remarks>
///     <para>
///         The mod's own, not <see cref="FontAssets.MouseText" />. Vanilla's face is drawn from an
///         atlas with heavy antialiasing and fine stems, and thresholding it to hard ink -- which is
///         what a pixelated face has to do -- eats the thin parts and leaves the rest ragged. The
///         baked Andika pages threshold cleanly, which is why the projected dialogue already reads
///         well wherever a speaker names one.
///     </para>
///     <para>
///         Hierarchy comes from weight and colour, never from a fractional font scale: a bitmap face
///         scaled by a non-integer factor loses whole rows out of its strokes, and at body size that
///         is the difference between text and texture.
///     </para>
/// </remarks>
internal static class ProjectedFonts
{
    /// <summary>Body text: descriptions, rows, labels.</summary>
    public static DynamicSpriteFont Body => TrelamiumFonts.AndikaRegular ?? FontAssets.MouseText.Value;

    /// <summary>Titles, headers and speaker plates.</summary>
    public static DynamicSpriteFont Title => TrelamiumFonts.AndikaBold ?? FontAssets.MouseText.Value;

    /// <summary>
    ///     Vanilla's face, for panels showing vanilla content at native resolution.
    /// </summary>
    /// <remarks>
    ///     An item tooltip is the case: its lines are written by the game and by other mods, for
    ///     this face, at this size. Setting them in the mod's own face would be a costume rather
    ///     than a presentation, and the smooth surface has no threshold to survive anyway.
    /// </remarks>
    public static DynamicSpriteFont Vanilla => FontAssets.MouseText.Value;
}
