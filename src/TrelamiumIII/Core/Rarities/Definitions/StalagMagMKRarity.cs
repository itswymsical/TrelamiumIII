using Terraria.Localization;
using TrelamiumIII.Core.Rarities.Flairs;

namespace TrelamiumIII.Core.Rarities.Definitions;

/// <summary>
///     The smallest custom rarity that still does something: a colour and one text treatment.
/// </summary>
public class StalagMagMKRarity : FlavorRarity
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

        FlavorColor = new Color(240, 75, 75),
        Flavor = Language.GetOrRegister("Mods.TrelamiumIII.Items.StalagMagMKI.FlavorText", () => "\"We Rock. Stone. BOULDER.\""),
    };
}
