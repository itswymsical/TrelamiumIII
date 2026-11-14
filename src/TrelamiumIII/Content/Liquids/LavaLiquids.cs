using Terraria.Localization;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Liquids on vanilla lava: molten by default. Like <see cref="WaterLiquids" />, every liquid stays
///     <see cref="LiquidID.Lava" /> to vanilla and other mods, so new kinds of lava cost no liquid IDs.
/// </summary>
public sealed class LavaLiquids : CustomLiquidSet, ILoadable
{
    public static LavaLiquids Instance { get; private set; } = null!;

    public InternalLiquid Molten { get; private set; } = null!;

    public override int LiquidType => LiquidID.Lava;

    public override string FullName => "Terraria/Lava";

    public override string Name => "Lava";

    public void Load(Mod mod)
    {
        Molten = AddLiquid(new InternalLiquid("Molten"));

        Register();
        Instance = this;
    }

    public void Unload() => Instance = null!;

    public override string LiquidDisplayName(InternalLiquid liquid)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Lava.Liquids.{liquid.Name}"), () => liquid.Name).Value;
}
