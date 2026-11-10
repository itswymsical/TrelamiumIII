using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Alchemy.Solutions;

/// <summary>
///     A principle tonic: a water bottle whose liquid sloshes in a palette around the principle's almanac colour. Loaded per
///     principle by <see cref="SolventID" />; no parameterless constructor keeps the class itself from autoloading.
/// </summary>
public sealed class TonicItem : BottledLiquidItem
{
    private readonly LiquidPalette palette;
    private static LiquidSurface? surface;

    public TonicItem(string name, PrincipleType principle) : base(name)
    {
        Principle = principle;
        palette = LiquidPalette.Around(PrincipleColors.Of(principle));
    }

    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;

    protected override BottleTemplate Template => BottleTemplate.Solvent;

    protected override LiquidPalette Palette => palette;

    protected override LiquidSurface Surface => surface ??= WaterLiquids.Instance.Surface with { Speed = 0.5f, Strength = 0.85f };
}