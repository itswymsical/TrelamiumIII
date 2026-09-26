using ReLogic.Graphics;
using Terraria.GameContent;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     The faces projected panels are set in.
/// </summary>
internal static class ProjectedFonts
{
    /// <summary>Body text: descriptions, rows, labels.</summary>
    public static DynamicSpriteFont Body => TrelamiumFonts.AndikaRegular ?? FontAssets.MouseText.Value;

    /// <summary>Titles, headers and speaker plates.</summary>
    public static DynamicSpriteFont Title => TrelamiumFonts.AndikaBold ?? FontAssets.MouseText.Value;

    public static DynamicSpriteFont Vanilla => FontAssets.MouseText.Value;
}
