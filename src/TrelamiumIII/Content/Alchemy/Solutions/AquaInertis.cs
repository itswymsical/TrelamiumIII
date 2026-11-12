using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Alchemy.Solutions;

/// <summary>Inert solution: a water bottle whose liquid sloshes, drawn whole by the <c>Splash</c> bottle technique.</summary>
[LegacyName("InertSolutionItem")]
internal sealed class AquaInertis : BottledLiquidItem
{
    /// <remarks>Keeps the internal name it had as a <see cref="SolventID" /> table entry.</remarks>
    public AquaInertis() : base("AquaInertis")
    {
        Rare = ItemRarityID.Gray;

        AlmanacPotency = 1;
        Temperaments = Temperament.Inert;
        AlmanacRarity = ItemRarityID.White;
        Hidden = false;
    }

    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;

    protected override BottleTemplate Template => BottleTemplate.Solution;

    /// <summary>The colours of the old hand-drawn sprite's liquid.</summary>
    protected override LiquidPalette Palette { get; } = new(new Color(63, 96, 137), new Color(122, 160, 181), new Color(231, 247, 248));

    protected override LiquidSurface Surface => Splash;

    protected override bool SurfaceIsLiquid => true;
}
