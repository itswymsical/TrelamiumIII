using TrelamiumIII.Core.Rarities;
using TrelamiumIII.Core.Rarities.Flairs;

namespace TrelamiumIII.Core.Tooltips;

/// <summary>
///     The smallest custom rarity that still does something: a colour and one text treatment.
/// </summary>
/// <remarks>
///     <para>
///         Kept as the scratch example. Use it by setting <c>Item.rare =
///         ModContent.RarityType&lt;CustomRarityTest&gt;()</c> and nothing else -- no draw hooks on
///         the item, no tooltip override. Everything past this point is more entries in
///         <see cref="RarityStyle.Flairs" />; see <c>Content/Rarities/FlavorRarities.cs</c> for
///         gas, shaders and drifting sprites.
///     </para>
///     <para>
///         For a look that belongs to one specific item rather than to a whole tier, skip the rarity
///         entirely and call <see cref="RarityStyles.RegisterItem" /> instead.
///     </para>
/// </remarks>
public class CustomRarityTest : FlavorRarity
{
    public override Color RarityColor => new(190, 130, 255);

    protected override RarityStyle BuildStyle() => new()
    {
        Color = RarityColor,
        Glow = new Color(225, 195, 255),

        Flairs =
        [
            new GhostNameFlair(),

            new GradientNameFlair
            {
                From = new Color(150, 100, 230),
                To = new Color(240, 220, 255),
            },
        ],
    };
}
